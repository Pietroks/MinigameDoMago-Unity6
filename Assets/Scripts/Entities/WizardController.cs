using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WizardGame.Core;
using WizardGame.Data;

namespace WizardGame.Entities
{
    /// <summary>
    /// Representa um projétil disparado por chefes que viaja pela tela e pode ser interceptado com tiros do jogador.
    /// Gerencia seu próprio voo em Update para nunca congelar no ar caso o chefe seja derrotado durante o trajeto.
    /// </summary>
    public class BossProjectileInstance : MonoBehaviour
    {
        public WizardController Owner { get; set; }
        public int Damage { get; set; } = 1;
        public string AttackName { get; set; } = "Magia do Chefe";
        public Color Color { get; set; } = Color.red;
        public bool IsIntercepted { get; private set; } = false;

        private Vector3 startPos;
        private Vector3 targetPos;
        private float flightDuration = 1.6f;
        private float elapsed = 0f;
        private bool isInitialized = false;
        private Action<WizardController, int, string> onReachedPlayerCallback;

        public void Launch(
            WizardController owner,
            Vector3 start,
            Vector3 target,
            Color color,
            float duration,
            int damage,
            string attackName,
            Action<WizardController, int, string> onReachedPlayer)
        {
            Owner = owner;
            startPos = start;
            targetPos = target;
            Color = color;
            flightDuration = Mathf.Max(0.25f, duration);
            Damage = damage;
            AttackName = attackName;
            onReachedPlayerCallback = onReachedPlayer;
            elapsed = 0f;
            isInitialized = true;
            transform.position = startPos;
            transform.localScale = Vector3.one * 0.35f;
        }

        private void Update()
        {
            if (!isInitialized || IsIntercepted) return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightDuration);
            float ease = t * t * 0.45f + t * 0.55f;
            transform.position = Vector3.Lerp(startPos, targetPos, ease);
            transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.15f, ease);

            if (elapsed >= flightDuration)
            {
                IsIntercepted = true;
                SpellEffectsManager.Instance?.SpawnAreaExplosion(transform.position, 0.9f);
                if (Owner != null && !Owner.IsDeadOrEscaping && Owner.gameObject.activeInHierarchy)
                {
                    onReachedPlayerCallback?.Invoke(Owner, Damage, AttackName);
                }
                WizardController.UnregisterBossProjectile(gameObject);
                Destroy(gameObject);
            }
        }

        public void Intercept()
        {
            if (IsIntercepted) return;
            IsIntercepted = true;
            SpellEffectsManager.Instance?.SpawnIceShatter(transform.position);
            WizardController.UnregisterBossProjectile(gameObject);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Controlador completo de Goblin. Gerencia ciclo de vida, maquina de estados de animacao
    /// por spritesheet (FrameAnimator), comportamentos especializados por tipo (Comum, Fugitivo, Dourado, Fantasma),
    /// barra de vida e eventos de combate sem travamentos.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
    public class WizardController : MonoBehaviour
    {
        [Header("Componentes Visuais")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Collider2D hitCollider;
        [SerializeField] private FrameAnimator frameAnimator;

        [Header("Barra de Vida (Health Bar)")]
        [SerializeField] private GameObject healthBarRoot;
        [SerializeField] private SpriteRenderer healthBarBg;
        [SerializeField] private SpriteRenderer healthBarFill;

        [Header("Configuracao de Escala")]
        [SerializeField] private float targetHeight = 1.85f;

        private WizardDataSO currentData;
        private int currentHealth;
        private bool isDeadOrEscaping;
        private bool isEnraged; // Para o Goblin Fugitivo ao tomar dano
        private bool isBossEnraged; // Fase 2 de fúria dos Chefes (< 50% HP)
        private bool isFrozen;
        private bool isInvulnerable;
        private bool isScaleInFinished;
        private bool isChargingBossAttack;
        private bool wasBossChargeInterrupted;
        private int bossChargeHitsReceived;
        private float remainingEscapeTime;
        private Bounds screenWorldBounds;
        private Vector3 normalizedScale = Vector3.one;
        private float currentSpeedMultiplier = 1.0f;
        private int currentStageNumber = 1;
        private int hitStaggerExtensionsUsed = 0;
        private Coroutine movementCoroutine;
        private Coroutine pulseCoroutine;
        private Coroutine escapeCoroutine;
        private Coroutine deathCoroutine;
        private Coroutine hitReactionCoroutine;
        private Coroutine attackRoutine;
        private Coroutine freezeCoroutine;
        private Coroutine scaleInCoroutine;
        private Coroutine flashCoroutine;
        private GameObject activeTelegraphRing;
        private TextMesh escapeWarningText;
        private SpriteRenderer groundShadowRenderer;
        private Vector3 baseShadowLocalPos;
        private Vector3 baseShadowLocalScale = Vector3.one;
        private float baseShadowAlpha = 0.50f;
        private float currentDepthNormalized = 0.5f;
        private float baseEffectiveTargetHeight = 1.85f;
        private float currentEffectiveHeight = 1.85f;

        private static readonly List<WizardController> activeGoblins = new List<WizardController>();
        public static IReadOnlyList<WizardController> ActiveGoblins => activeGoblins;

        private static readonly List<GameObject> activeBossProjectiles = new List<GameObject>();
        public static IReadOnlyList<GameObject> ActiveBossProjectiles => activeBossProjectiles;

        public static void RegisterBossProjectile(GameObject proj)
        {
            if (proj != null && !activeBossProjectiles.Contains(proj))
            {
                activeBossProjectiles.Add(proj);
            }
        }

        public static void UnregisterBossProjectile(GameObject proj)
        {
            if (proj != null)
            {
                activeBossProjectiles.Remove(proj);
            }
        }

        public static void ClearAllBossProjectiles()
        {
            for (int i = activeBossProjectiles.Count - 1; i >= 0; i--)
            {
                if (activeBossProjectiles[i] != null)
                {
                    Destroy(activeBossProjectiles[i]);
                }
            }
            activeBossProjectiles.Clear();
        }

        public bool IsDeadOrEscaping => isDeadOrEscaping;
        public bool IsFrozen => isFrozen;
        public bool IsBossEnraged => isBossEnraged;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => currentData != null ? currentData.maxHealth : 1;
        public bool IsTeleporting { get; private set; }
        public bool DiedWhileTeleporting { get; private set; }
        public bool WasLastDamageNormalShot { get; private set; }

        public event Action<WizardController, int> OnDefeated;
        public event Action<WizardController, int> OnEscaped;
        public event Action<WizardController, int, string> OnAttackPlayer;
        public event Action<WizardController, WizardType> OnBossSummonRequested;
        public event Action<WizardController, string> OnBossAttackInterrupted;
        public event Action<WizardController, WizardDataSO> OnBossEnraged;
        public event Action<WizardController, int, int, bool> OnBossHealthChanged;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (hitCollider == null) hitCollider = GetComponent<Collider2D>();
            if (frameAnimator == null) frameAnimator = GetComponent<FrameAnimator>();
            if (frameAnimator == null) frameAnimator = gameObject.AddComponent<FrameAnimator>();

            CreateHealthBarIfMissing();
            CreateEscapeWarningIfMissing();
            CreateShadowIfMissing();
        }

        private static Sprite cachedGroundShadowSprite;
        private static Sprite CreateGroundShadowSprite()
        {
            if (cachedGroundShadowSprite != null) return cachedGroundShadowSprite;
            int w = 64, h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / (cx - 1f);
                    float dy = (y - cy) / (cy - 1f);
                    float norm = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float alpha = Mathf.Pow(1f - norm, 2.0f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            cachedGroundShadowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return cachedGroundShadowSprite;
        }

        private void CreateShadowIfMissing()
        {
            if (groundShadowRenderer != null) return;
            GameObject shadowGo = new GameObject("GroundShadow");
            shadowGo.transform.SetParent(transform, false);
            groundShadowRenderer = shadowGo.AddComponent<SpriteRenderer>();
            groundShadowRenderer.sprite = CreateGroundShadowSprite();
            groundShadowRenderer.color = new Color(0.02f, 0.02f, 0.06f, 0.50f);
            groundShadowRenderer.sortingOrder = 4;
        }

        private void CreateEscapeWarningIfMissing()
        {
            if (escapeWarningText != null) return;

            GameObject warnGo = new GameObject("EscapeWarning");
            warnGo.transform.SetParent(transform, false);
            warnGo.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            escapeWarningText = warnGo.AddComponent<TextMesh>();
            escapeWarningText.text = "!";
            escapeWarningText.fontSize = 36;
            escapeWarningText.characterSize = 0.08f;
            escapeWarningText.fontStyle = FontStyle.Bold;
            escapeWarningText.alignment = TextAlignment.Center;
            escapeWarningText.anchor = TextAnchor.MiddleCenter;
            escapeWarningText.color = new Color(1f, 0.3f, 0.15f, 1f);

            var mr = warnGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 22;
            warnGo.SetActive(false);
        }

        private void CreateHealthBarIfMissing()
        {
            if (healthBarRoot != null) return;

            healthBarRoot = new GameObject("HealthBar");
            healthBarRoot.transform.SetParent(transform, false);
            healthBarRoot.transform.localPosition = new Vector3(0f, 0.85f, 0f);

            GameObject bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(healthBarRoot.transform, false);
            healthBarBg = bgGo.AddComponent<SpriteRenderer>();
            healthBarBg.sortingOrder = 15;
            healthBarBg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            healthBarBg.sprite = CreateSolidWhiteSprite();
            bgGo.transform.localScale = new Vector3(0.9f, 0.15f, 1f);

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(healthBarRoot.transform, false);
            healthBarFill = fillGo.AddComponent<SpriteRenderer>();
            healthBarFill.sortingOrder = 16;
            healthBarFill.color = Color.green;
            healthBarFill.sprite = CreateSolidWhiteSprite();
            fillGo.transform.localScale = new Vector3(0.86f, 0.11f, 1f);
        }

        private static Sprite cachedWhiteSprite;
        private static Sprite CreateSolidWhiteSprite()
        {
            if (cachedWhiteSprite == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            return cachedWhiteSprite;
        }

        public static float GetProgressiveFugitiveSpeedMultiplier(int stageNumber)
        {
            return stageNumber switch
            {
                <= 1 => 0.58f,
                2 => 0.68f,
                3 => 0.80f,
                4 => 0.94f,
                _ => 1.10f + (stageNumber - 5) * 0.10f
            };
        }

        public void Initialize(
            WizardDataSO data,
            Vector3 spawnPosition,
            Bounds bounds,
            float speedMultiplier = 1.0f,
            float escapeTimeMultiplier = 1.0f,
            int stageNumber = 1,
            float fugitiveSpeedMult = -1f)
        {
            currentData = data;
            currentHealth = data.maxHealth;
            screenWorldBounds = bounds;
            currentStageNumber = Mathf.Max(1, stageNumber);
            hitStaggerExtensionsUsed = 0;

            if (data.wizardType == WizardType.Fugitivo)
            {
                float stageFugitiveMult = fugitiveSpeedMult > 0f
                    ? fugitiveSpeedMult
                    : GetProgressiveFugitiveSpeedMultiplier(currentStageNumber);
                currentSpeedMultiplier = Mathf.Clamp(stageFugitiveMult, 0.5f, 2.2f);
            }
            else
            {
                currentSpeedMultiplier = Mathf.Max(0.5f, speedMultiplier);
            }
            isDeadOrEscaping = false;
            isEnraged = false;
            isBossEnraged = false;
            isInvulnerable = false;
            isScaleInFinished = false;
            isChargingBossAttack = false;
            wasBossChargeInterrupted = false;
            bossChargeHitsReceived = 0;
            IsTeleporting = false;
            DiedWhileTeleporting = false;
            WasLastDamageNormalShot = false;

            transform.position = spawnPosition;
            transform.rotation = Quaternion.identity;

            // Inicializar sprite inicial
            Sprite defaultSprite = (data.walkFrames != null && data.walkFrames.Length > 0) ? data.walkFrames[0] : data.sprite;
            spriteRenderer.sprite = defaultSprite;
            spriteRenderer.color = data.baseTint;
            spriteRenderer.flipX = false;
            hitCollider.enabled = true;

            // Registrar animacoes no FrameAnimator
            SetupAnimations(data);

            // Normalizar escala proporcionalmente ao sprite
            float spriteHeight = 1f;
            if (defaultSprite != null && defaultSprite.bounds.size.y > 0.05f)
            {
                spriteHeight = defaultSprite.bounds.size.y;
            }
            float effectiveTargetHeight = targetHeight;
            if (data.wizardType == WizardType.Chefe || data.wizardType == WizardType.Chefe2) effectiveTargetHeight = targetHeight * 1.35f;
            else if (data.wizardType == WizardType.Chefe3) effectiveTargetHeight = targetHeight * 1.40f;
            else if (data.wizardType == WizardType.Chefe4) effectiveTargetHeight = targetHeight * 1.45f;
            else if (data.wizardType == WizardType.ChefeFinal) effectiveTargetHeight = targetHeight * 1.55f;

            baseEffectiveTargetHeight = effectiveTargetHeight;
            currentEffectiveHeight = effectiveTargetHeight;

            float scaleFactor = effectiveTargetHeight / Mathf.Max(0.1f, spriteHeight);
            normalizedScale = new Vector3(scaleFactor, scaleFactor, 1f);
            transform.localScale = Vector3.zero;

            if (hitCollider is CircleCollider2D circleCol)
            {
                circleCol.radius = spriteHeight * 0.48f;
            }

            if (healthBarRoot != null)
            {
                bool isBoss = WaveConfig.IsBossType(data.wizardType);
                float barYOffset = isBoss ? (spriteHeight * 0.70f) : (spriteHeight * 0.62f);
                healthBarRoot.transform.localPosition = new Vector3(0f, barYOffset, 0f);
                if (escapeWarningText != null)
                {
                    escapeWarningText.transform.localPosition = new Vector3(0f, barYOffset + (isBoss ? 0.35f : 0.28f), 0f);
                    escapeWarningText.gameObject.SetActive(false);
                }
            }

            if (groundShadowRenderer != null)
            {
                bool isGhost = data.wizardType == WizardType.Fantasma || data.movementPattern == AIMovementPattern.SpectralHover;
                bool isBoss = WaveConfig.IsBossType(data.wizardType);
                float spW = (defaultSprite != null && defaultSprite.bounds.size.x > 0.05f) ? defaultSprite.bounds.size.x : spriteHeight * 0.75f;
                Vector2 shSize = groundShadowRenderer.sprite != null ? (Vector2)groundShadowRenderer.sprite.bounds.size : Vector2.one;
                baseShadowAlpha = isGhost ? 0.26f : (isBoss ? 0.56f : 0.48f);
                baseShadowLocalPos = new Vector3(0f, -spriteHeight * (isGhost ? 0.48f : 0.41f), 0f);
                baseShadowLocalScale = new Vector3(
                    (spW * 0.78f) / Mathf.Max(0.01f, shSize.x),
                    (spriteHeight * 0.20f) / Mathf.Max(0.01f, shSize.y),
                    1f
                );
                groundShadowRenderer.transform.localPosition = baseShadowLocalPos;
                groundShadowRenderer.transform.localScale = baseShadowLocalScale;
                groundShadowRenderer.color = new Color(0.02f, 0.02f, 0.06f, baseShadowAlpha);
                groundShadowRenderer.gameObject.SetActive(true);
            }

            ApplyDepthSortingAndScale();
            UpdateHealthBar();
            StopAllActiveCoroutines();

            if (!activeGoblins.Contains(this)) activeGoblins.Add(this);
            isFrozen = false;

            scaleInCoroutine = StartCoroutine(SpawnScaleInRoutine());
            StartBehavior();

            bool isBossUnit = WaveConfig.IsBossType(data.wizardType);
            float baseEscape = data.escapeTimeSeconds * (isBossUnit ? 1.0f : Mathf.Max(0.65f, escapeTimeMultiplier));
            float escapeTime = isBossUnit
                ? Mathf.Max(20f, baseEscape)
                : Mathf.Max(2.5f, baseEscape / Mathf.Sqrt(currentSpeedMultiplier));
            escapeCoroutine = StartCoroutine(EscapeTimerRoutine(escapeTime));
        }

        private void OnDisable()
        {
            activeGoblins.Remove(this);
            StopAllActiveCoroutines();
        }

        private void SetupAnimations(WizardDataSO data)
        {
            frameAnimator.UnlockAnimation();

            // Idle
            if (data.idleFrames != null && data.idleFrames.Length > 0)
            {
                frameAnimator.RegisterState(AnimationState.Idle, data.idleFrames, 8f, true);
            }

            // Walk / Levitation
            if (data.walkFrames != null && data.walkFrames.Length > 0)
            {
                float stageT = Mathf.Clamp01((currentStageNumber - 1) / 4f);
                float fps = data.wizardType == WizardType.Dourado
                    ? 12f
                    : (data.wizardType == WizardType.Fugitivo ? Mathf.Lerp(7.5f, 12.5f, stageT) : 8f);
                frameAnimator.RegisterState(AnimationState.Walk, data.walkFrames, fps, true);
            }

            // Run / Dash
            if (data.runFrames != null && data.runFrames.Length > 0)
            {
                float stageT = Mathf.Clamp01((currentStageNumber - 1) / 4f);
                float runFps = (data.wizardType == WizardType.Fugitivo)
                    ? Mathf.Lerp(9.5f, 15.5f, stageT)
                    : 12f;
                frameAnimator.RegisterState(AnimationState.Run, data.runFrames, runFps, true);
            }

            // Attack / Golpe Forte
            if (data.attackFrames != null && data.attackFrames.Length > 0)
            {
                float fps = WaveConfig.IsBossType(data.wizardType) ? 10f : 8f;
                frameAnimator.RegisterState(AnimationState.Attack, data.attackFrames, fps, false);
            }

            // Attack2 / Investida Brutal ou Habilidade 2
            if (data.attack2Frames != null && data.attack2Frames.Length > 0)
            {
                frameAnimator.RegisterState(AnimationState.Attack2, data.attack2Frames, 12f, false);
            }

            // Attack3 / Ataque Mágico Supremo (Chefe Final)
            if (data.attack3Frames != null && data.attack3Frames.Length > 0)
            {
                frameAnimator.RegisterState(AnimationState.Attack3, data.attack3Frames, 10f, false);
            }

            // Summon / Invocação Mística
            if (data.summonFrames != null && data.summonFrames.Length > 0)
            {
                frameAnimator.RegisterState(AnimationState.Summon, data.summonFrames, 10f, false);
            }

            // Hit / Dano Sofrido
            if (data.hitFrames != null && data.hitFrames.Length > 0)
            {
                frameAnimator.RegisterState(AnimationState.Hit, data.hitFrames, 12f, false);
            }

            // Teleport / Deslocamento Dimensional
            if (data.teleportFrames != null && data.teleportFrames.Length > 0)
            {
                frameAnimator.RegisterState(AnimationState.Teleport, data.teleportFrames, 12f, false);
            }

            // Death
            if (data.deathFrames != null && data.deathFrames.Length > 0)
            {
                float fps = WaveConfig.IsBossType(data.wizardType) ? 7.5f : 8.5f;
                frameAnimator.RegisterState(AnimationState.Death, data.deathFrames, fps, false);
            }

            // Comeca andando
            frameAnimator.Play(AnimationState.Walk);
        }

        private void UpdateHealthBar()
        {
            if (healthBarRoot == null) return;

            bool showBar = currentData != null && currentData.maxHealth > 1 && !isDeadOrEscaping;
            healthBarRoot.SetActive(showBar);

            if (showBar && healthBarFill != null)
            {
                bool isBoss = WaveConfig.IsBossType(currentData.wizardType);
                float barWidth = (currentData.wizardType == WizardType.ChefeFinal) ? 1.6f : (isBoss ? 1.35f : 0.86f);
                float barHeight = isBoss ? 0.16f : 0.11f;

                if (healthBarBg != null)
                {
                    healthBarBg.transform.localScale = new Vector3(barWidth + 0.08f, barHeight + 0.04f, 1f);
                    Color bgColor = currentData.wizardType switch
                    {
                        WizardType.Chefe2 => new Color(0.2f, 0.08f, 0.02f, 0.92f), // Laranja escuro / Caos
                        WizardType.Chefe3 => new Color(0.12f, 0.02f, 0.2f, 0.92f), // Púrpura escuro
                        WizardType.Chefe4 => new Color(0.22f, 0.02f, 0.05f, 0.92f), // Carmesim profundo
                        WizardType.ChefeFinal => new Color(0.25f, 0.02f, 0.02f, 0.95f), // Magma profundo
                        _ => isBoss ? new Color(0.18f, 0.02f, 0.02f, 0.92f) : new Color(0.1f, 0.1f, 0.1f, 0.85f)
                    };
                    healthBarBg.color = bgColor;
                }

                float pct = Mathf.Clamp01((float)currentHealth / currentData.maxHealth);
                healthBarFill.transform.localScale = new Vector3(barWidth * pct, barHeight, 1f);

                Color fillColor = currentData.wizardType switch
                {
                    WizardType.Chefe2 => Color.Lerp(new Color(1f, 0.3f, 0.05f), new Color(1f, 0.85f, 0.2f), pct),
                    WizardType.Chefe3 => Color.Lerp(new Color(0.6f, 0.1f, 0.9f), new Color(0.2f, 0.85f, 1f), pct),
                    WizardType.Chefe4 => Color.Lerp(new Color(0.85f, 0.05f, 0.15f), new Color(1f, 0.35f, 0.45f), pct),
                    WizardType.ChefeFinal => Color.Lerp(new Color(0.95f, 0.1f, 0.05f), new Color(1f, 0.9f, 0.25f), pct),
                    WizardType.Chefe => Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(1f, 0.85f, 0.2f), pct),
                    _ => Color.Lerp(Color.red, Color.green, pct)
                };
                healthBarFill.color = fillColor;
            }
        }

        public float GetCurrentHeight() => currentEffectiveHeight;

        public void SetSortingOrder(int order)
        {
            if (spriteRenderer != null) spriteRenderer.sortingOrder = order;
            if (groundShadowRenderer != null) groundShadowRenderer.sortingOrder = order - 1;
            if (healthBarBg != null) healthBarBg.sortingOrder = order + 2;
            if (healthBarFill != null) healthBarFill.sortingOrder = order + 3;
            if (escapeWarningText != null)
            {
                var mr = escapeWarningText.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = order + 4;
            }
        }

        private Color GetDepthAdjustedTint(Color baseColor)
        {
            if (StageEnvironmentManager.Instance == null) return baseColor;
            float fogAmount = Mathf.Clamp01(currentDepthNormalized) * StageEnvironmentManager.Instance.CurrentDepthFogStrength;
            Color fogCol = StageEnvironmentManager.Instance.CurrentDepthFogColor;
            Color blended = Color.Lerp(baseColor, new Color(fogCol.r, fogCol.g, fogCol.b, baseColor.a), fogAmount);
            blended.a = baseColor.a;
            return blended;
        }

        private void ApplyDepthSortingAndScale()
        {
            float feetY = transform.position.y - (currentEffectiveHeight * 0.34f);
            SetSortingOrder(Mathf.RoundToInt((10f - feetY) * 10f));

            float minY = screenWorldBounds.size.y > 0.1f ? screenWorldBounds.min.y : -4.5f;
            float maxY = screenWorldBounds.size.y > 0.1f ? screenWorldBounds.max.y : 4.5f;
            currentDepthNormalized = Mathf.InverseLerp(minY, maxY, transform.position.y);

            if (isScaleInFinished && !IsTeleporting && !isDeadOrEscaping)
            {
                // Perspectiva 2.5D marcante: maior próximo à câmera (baixo), menor ao fundo (alto)
                float depthScale = Mathf.Lerp(1.14f, 0.78f, currentDepthNormalized);
                currentEffectiveHeight = baseEffectiveTargetHeight * depthScale;
                transform.localScale = normalizedScale * depthScale;

                if (flashCoroutine == null && !isFrozen && !isChargingBossAttack && !isInvulnerable && currentData != null &&
                    currentData.wizardType != WizardType.Dourado && currentData.wizardType != WizardType.Fantasma && spriteRenderer != null)
                {
                    spriteRenderer.color = GetDepthAdjustedTint(currentData.baseTint);
                }
            }
        }

        private void CleanupTelegraphRing()
        {
            if (activeTelegraphRing != null)
            {
                Destroy(activeTelegraphRing);
                activeTelegraphRing = null;
            }
            isChargingBossAttack = false;
        }

        private void StopMovementAndCombatCoroutines()
        {
            if (movementCoroutine != null) { StopCoroutine(movementCoroutine); movementCoroutine = null; }
            if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
            if (hitReactionCoroutine != null) { StopCoroutine(hitReactionCoroutine); hitReactionCoroutine = null; }
            if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
            if (freezeCoroutine != null) { StopCoroutine(freezeCoroutine); freezeCoroutine = null; }
            isInvulnerable = false;
            CleanupTelegraphRing();
        }

        private void StopAllActiveCoroutines()
        {
            StopMovementAndCombatCoroutines();
            if (scaleInCoroutine != null) { StopCoroutine(scaleInCoroutine); scaleInCoroutine = null; }
            if (flashCoroutine != null) { StopCoroutine(flashCoroutine); flashCoroutine = null; }
            if (escapeCoroutine != null) { StopCoroutine(escapeCoroutine); escapeCoroutine = null; }
            if (deathCoroutine != null) { StopCoroutine(deathCoroutine); deathCoroutine = null; }
            if (escapeWarningText != null) escapeWarningText.gameObject.SetActive(false);
            isFrozen = false;
            IsTeleporting = false;
            isInvulnerable = false;
            if (currentData != null && spriteRenderer != null)
            {
                spriteRenderer.color = currentData.baseTint;
            }
            if (groundShadowRenderer != null)
            {
                groundShadowRenderer.color = new Color(0.02f, 0.02f, 0.06f, baseShadowAlpha);
            }
        }

        public void CancelAndDespawnImmediately()
        {
            StopAllActiveCoroutines();
            isDeadOrEscaping = true;
            if (hitCollider != null) hitCollider.enabled = false;
            if (healthBarRoot != null) healthBarRoot.SetActive(false);
            if (escapeWarningText != null) escapeWarningText.gameObject.SetActive(false);
            if (groundShadowRenderer != null) groundShadowRenderer.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        private IEnumerator SpawnScaleInRoutine()
        {
            float elapsed = 0f;
            float duration = 0.2f;

            float minY = screenWorldBounds.size.y > 0.1f ? screenWorldBounds.min.y : -4.5f;
            float maxY = screenWorldBounds.size.y > 0.1f ? screenWorldBounds.max.y : 4.5f;
            float yNorm = Mathf.InverseLerp(minY, maxY, transform.position.y);
            Vector3 targetScale = normalizedScale * Mathf.Lerp(1.14f, 0.78f, yNorm);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float ease = Mathf.Sin(t * Mathf.PI * 0.5f);
                transform.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, ease);
                yield return null;
            }
            transform.localScale = targetScale;
            isScaleInFinished = true;
        }

        private void StartBehavior()
        {
            if (movementCoroutine != null) StopCoroutine(movementCoroutine);
            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
            if (attackRoutine != null) StopCoroutine(attackRoutine);

            if (currentData == null) return;

            // Se for um dos Chefes, direciona para a rotina tática correspondente
            if (WaveConfig.IsBossType(currentData.wizardType) || currentData.movementPattern == AIMovementPattern.BossTactical)
            {
                switch (currentData.wizardType)
                {
                    case WizardType.Chefe:
                        movementCoroutine = StartCoroutine(BossBehaviorRoutine());
                        break;
                    case WizardType.Chefe2:
                        movementCoroutine = StartCoroutine(Boss2BehaviorRoutine());
                        break;
                    case WizardType.Chefe3:
                        movementCoroutine = StartCoroutine(Boss3BehaviorRoutine());
                        break;
                    case WizardType.Chefe4:
                        movementCoroutine = StartCoroutine(Boss4BehaviorRoutine());
                        break;
                    case WizardType.ChefeFinal:
                        movementCoroutine = StartCoroutine(BossFinalBehaviorRoutine());
                        break;
                    default:
                        movementCoroutine = StartCoroutine(BossBehaviorRoutine());
                        break;
                }
                return;
            }

            // Ativa auras passivas visuais
            if (currentData.wizardType == WizardType.Dourado || currentData.personalityArchetype == AIPersonalityArchetype.GoldenStraight)
            {
                pulseCoroutine = StartCoroutine(GoldenGlowRoutine());
            }
            else if (currentData.wizardType == WizardType.Fantasma || currentData.movementPattern == AIMovementPattern.SpectralHover)
            {
                pulseCoroutine = StartCoroutine(GhostSpectralAlphaRoutine());
            }

            // Inicia padrão primário de locomoção baseado no WizardDataSO
            switch (currentData.movementPattern)
            {
                case AIMovementPattern.Wander:
                    movementCoroutine = StartCoroutine(WanderRoutine());
                    if (currentData.wizardType == WizardType.Comum)
                    {
                        attackRoutine = StartCoroutine(PeriodicAttackRoutine(3f, 5f));
                    }
                    break;

                case AIMovementPattern.StraightMovement:
                    movementCoroutine = StartCoroutine(StraightMovementRoutine());
                    break;

                case AIMovementPattern.SpectralHover:
                    movementCoroutine = StartCoroutine(SpectralHoverRoutine());
                    break;

                case AIMovementPattern.ProtectAlly:
                    movementCoroutine = StartCoroutine(ProtectAllyRoutine());
                    break;

                case AIMovementPattern.HealerSupport:
                    movementCoroutine = StartCoroutine(HealerSupportRoutine());
                    break;

                default:
                    movementCoroutine = StartCoroutine(WanderRoutine());
                    break;
            }
        }

        public void TakeDamage(int damage, bool isNormalShot = false, bool isSpecialSpell = false)
        {
            if (isDeadOrEscaping) return;

            if (isInvulnerable)
            {
                // Feedback visual e sonoro claro de que o alvo está momentaneamente furtivo/intangível
                SpawnBossBurstFX(transform.position, new Color(0.7f, 0.55f, 1f, 0.7f), 0.9f);
                SoundManager.Instance?.PlayDryFireSound();
                return;
            }

            WasLastDamageNormalShot = isNormalShot;
            currentHealth -= damage;
            UpdateHealthBar();

            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashDamageRoutine());

            // Interrupção de ataque carregado do Chefe
            if (isChargingBossAttack && !wasBossChargeInterrupted)
            {
                bossChargeHitsReceived++;
                if (isSpecialSpell || bossChargeHitsReceived >= 2)
                {
                    wasBossChargeInterrupted = true;
                    CleanupTelegraphRing();
                    SpawnBossBurstFX(transform.position, new Color(0.25f, 1f, 0.85f, 0.9f), 1.5f);
                    OnBossAttackInterrupted?.Invoke(this, currentData != null ? currentData.displayName : "Chefe");
                }
            }

            // Fase 2 de Fúria do Chefe aos 50% de Vida
            if (currentHealth > 0 && currentData != null && WaveConfig.IsBossType(currentData.wizardType))
            {
                if (!isBossEnraged && currentHealth <= Mathf.CeilToInt(currentData.maxHealth * 0.5f))
                {
                    TriggerBossEnrage(spawnReinforcements: true);
                }
            }

            if (currentData != null && WaveConfig.IsBossType(currentData.wizardType))
            {
                OnBossHealthChanged?.Invoke(this, Mathf.Max(0, currentHealth), currentData.maxHealth, isBossEnraged);
            }

            if (currentHealth <= 0)
            {
                Die();
            }
            else
            {
                // Micro-stagger tático: acertar um goblin prestes a escapar dá breve janela extra para finalizá-lo
                if (currentData != null && !WaveConfig.IsBossType(currentData.wizardType) &&
                    remainingEscapeTime > 0f && remainingEscapeTime < 1.35f && hitStaggerExtensionsUsed < 2)
                {
                    remainingEscapeTime += 0.48f;
                    hitStaggerExtensionsUsed++;
                }

                if (currentData != null && currentData.customDamageSound != null)
                {
                    SoundManager.Instance?.PlaySFX(currentData.customDamageSound, 0.85f);
                }
                TriggerHitReaction();
            }
        }

        private void TriggerBossEnrage(bool spawnReinforcements)
        {
            if (isBossEnraged) return;
            isBossEnraged = true;
            currentSpeedMultiplier *= 1.24f;
            SpawnBossBurstFX(transform.position, new Color(1f, 0.25f, 0.05f, 0.95f), 2.5f);
            SpellEffectsManager.Instance?.SpawnFloatingText(transform.position + Vector3.up * 0.8f, "FÚRIA!", new Color(1f, 0.35f, 0.1f), 1.35f);
            SpellEffectsManager.Instance?.TriggerCameraShake(0.20f, 0.18f);
            OnBossEnraged?.Invoke(this, currentData);

            if (spawnReinforcements && currentData != null)
            {
                if (currentData.wizardType == WizardType.Chefe2)
                {
                    OnBossSummonRequested?.Invoke(this, WizardType.Fugitivo);
                }
                else if (currentData.wizardType == WizardType.Chefe3)
                {
                    OnBossSummonRequested?.Invoke(this, WizardType.Fantasma);
                }
                else if (currentData.wizardType == WizardType.Chefe4)
                {
                    OnBossSummonRequested?.Invoke(this, WizardType.Dourado);
                    OnBossSummonRequested?.Invoke(this, WizardType.Fugitivo);
                }
                else if (currentData.wizardType == WizardType.ChefeFinal)
                {
                    OnBossSummonRequested?.Invoke(this, WizardType.Dourado);
                    OnBossSummonRequested?.Invoke(this, WizardType.Fantasma);
                }
            }
        }

        private IEnumerator FlashDamageRoutine()
        {
            Color targetAfterFlash = isFrozen ? new Color(0.35f, 0.85f, 1f, 1f) : (currentData != null ? currentData.baseTint : Color.white);
            if (spriteRenderer != null) spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.08f);
            if (!isDeadOrEscaping && spriteRenderer != null)
            {
                spriteRenderer.color = isFrozen ? new Color(0.35f, 0.85f, 1f, 1f) : targetAfterFlash;
            }
            flashCoroutine = null;
        }

        private void TriggerHitReaction()
        {
            if (hitReactionCoroutine != null) StopCoroutine(hitReactionCoroutine);

            if (WaveConfig.IsBossType(currentData.wizardType))
            {
                hitReactionCoroutine = StartCoroutine(BossHitReactionRoutine());
                return;
            }

            switch (currentData.hitReaction)
            {
                case AIHitReaction.FleeAfterHit:
                    hitReactionCoroutine = StartCoroutine(FleeAfterHitRoutine());
                    break;
                case AIHitReaction.DefensiveShield:
                    hitReactionCoroutine = StartCoroutine(GoldenShieldReactionRoutine());
                    break;
                case AIHitReaction.TeleportAfterHit:
                    hitReactionCoroutine = StartCoroutine(GhostTeleportReactionRoutine());
                    break;
                case AIHitReaction.SpawnDecoy:
                    hitReactionCoroutine = StartCoroutine(SpawnDecoyReactionRoutine());
                    break;
                case AIHitReaction.None:
                    break;
                case AIHitReaction.Jiggle:
                default:
                    hitReactionCoroutine = StartCoroutine(CommonJiggleReactionRoutine());
                    break;
            }
        }

        private IEnumerator EscapeTimerRoutine(float delaySeconds)
        {
            bool isBoss = currentData != null && WaveConfig.IsBossType(currentData.wizardType);
            remainingEscapeTime = delaySeconds;

            while (remainingEscapeTime > 0f)
            {
                if (!isFrozen)
                {
                    remainingEscapeTime -= Time.deltaTime;
                }

                if (!isBoss && escapeWarningText != null)
                {
                    bool showWarn = !isDeadOrEscaping && !isFrozen && remainingEscapeTime <= 1.85f;
                    if (escapeWarningText.gameObject.activeSelf != showWarn)
                    {
                        escapeWarningText.gameObject.SetActive(showWarn);
                    }
                    if (showWarn)
                    {
                        float pulse = 0.55f + Mathf.PingPong(Time.unscaledTime * 8f, 0.45f);
                        escapeWarningText.color = new Color(1f, 0.28f, 0.12f, pulse);
                        escapeWarningText.text = (currentData != null && (currentData.wizardType == WizardType.Fugitivo || currentData.wizardType == WizardType.Dourado))
                            ? "! DANO !"
                            : "!";
                    }
                }

                yield return null;
            }

            if (escapeWarningText != null) escapeWarningText.gameObject.SetActive(false);
            if (isDeadOrEscaping) yield break;

            // Chefes lutam até serem derrotados para garantir progressão de fase:
            // Se demorarem demais, entram em Fúria e atacam com pressão extra em vez de sumir da fase!
            if (isBoss)
            {
                if (!isBossEnraged)
                {
                    TriggerBossEnrage(spawnReinforcements: true);
                }
                else
                {
                    currentSpeedMultiplier = Mathf.Min(2.4f, currentSpeedMultiplier * 1.15f);
                }
                SpawnBossBurstFX(transform.position, new Color(1f, 0.2f, 0.1f, 0.9f), 2.0f);
                remainingEscapeTime = 16.0f;
                escapeCoroutine = StartCoroutine(EscapeTimerRoutine(16.0f));
                yield break;
            }

            isDeadOrEscaping = true;
            activeGoblins.Remove(this);
            hitCollider.enabled = false;
            if (healthBarRoot != null) healthBarRoot.SetActive(false);
            StopMovementAndCombatCoroutines();

            // Anima subida e fade out
            float elapsed = 0f;
            float duration = 0.35f;
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + new Vector3(0f, 1.2f, 0f);
            Color startCol = spriteRenderer.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.position = Vector3.Lerp(startPos, endPos, t);
                spriteRenderer.color = new Color(startCol.r, startCol.g, startCol.b, Mathf.Lerp(startCol.a, 0f, t));
                if (groundShadowRenderer != null)
                {
                    groundShadowRenderer.color = new Color(0.02f, 0.02f, 0.06f, Mathf.Lerp(baseShadowAlpha, 0f, t));
                }
                yield return null;
            }

            escapeCoroutine = null;

            // Retaliação seletiva ao escapar:
            // Rápido (Fugitivo) causa 1 dano ao escapar; Dourado causa 2 dano ao escapar; Comum não ataca.
            if (currentData != null)
            {
                if (currentData.wizardType == WizardType.Fugitivo)
                {
                    OnAttackPlayer?.Invoke(this, 1, "Goblin Fugitivo");
                }
                else if (currentData.wizardType == WizardType.Dourado)
                {
                    OnAttackPlayer?.Invoke(this, 2, "Goblin Dourado");
                }
            }

            OnEscaped?.Invoke(this, currentData.escapePenalty);
            gameObject.SetActive(false);
        }

        private void Die()
        {
            DiedWhileTeleporting = IsTeleporting;
            IsTeleporting = false;
            isDeadOrEscaping = true;
            hitCollider.enabled = false;
            if (healthBarRoot != null) healthBarRoot.SetActive(false);
            if (escapeWarningText != null) escapeWarningText.gameObject.SetActive(false);
            if (flashCoroutine != null) { StopCoroutine(flashCoroutine); flashCoroutine = null; }

            if (isFrozen)
            {
                SpellEffectsManager.Instance?.SpawnIceShatter(transform.position);
                isFrozen = false;
            }

            StopMovementAndCombatCoroutines();
            if (escapeCoroutine != null)
            {
                StopCoroutine(escapeCoroutine);
                escapeCoroutine = null;
            }

            if (spriteRenderer != null && currentData != null)
            {
                spriteRenderer.color = currentData.baseTint;
            }

            if (currentData != null && currentData.deathReaction == AIDeathReaction.ExplodeOnDeath)
            {
                deathCoroutine = StartCoroutine(ExplodeOnDeathRoutine());
            }
            else
            {
                deathCoroutine = StartCoroutine(DeathAnimationRoutine());
            }
        }

        public void Freeze(float duration = 2.5f)
        {
            if (isDeadOrEscaping || isInvulnerable) return;

            if (freezeCoroutine != null) StopCoroutine(freezeCoroutine);
            freezeCoroutine = StartCoroutine(FreezeRoutine(duration));
        }

        private IEnumerator FreezeRoutine(float duration)
        {
            isFrozen = true;
            isInvulnerable = false;
            IsTeleporting = false;
            CleanupTelegraphRing();

            // Interrompe rotinas ativas de movimento, pulso cromático e ataque
            if (movementCoroutine != null) { StopCoroutine(movementCoroutine); movementCoroutine = null; }
            if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
            if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
            if (hitReactionCoroutine != null) { StopCoroutine(hitReactionCoroutine); hitReactionCoroutine = null; }
            if (flashCoroutine != null) { StopCoroutine(flashCoroutine); flashCoroutine = null; }
            if (escapeWarningText != null) escapeWarningText.gameObject.SetActive(false);

            ApplyDepthSortingAndScale();

            // Tonalidade azul-gelo
            spriteRenderer.color = new Color(0.35f, 0.85f, 1f, 1f);

            yield return new WaitForSeconds(duration);

            if (isDeadOrEscaping) yield break;

            isFrozen = false;
            isInvulnerable = false;
            spriteRenderer.color = currentData.baseTint;
            freezeCoroutine = null;
            StartBehavior();
        }

        private IEnumerator DeathAnimationRoutine()
        {
            // Toca animacao de morte no FrameAnimator (6 quadros de colapso no chão)
            if (frameAnimator != null && currentData.deathFrames != null && currentData.deathFrames.Length > 0)
            {
                frameAnimator.LockAnimation(AnimationState.Death);
            }

            bool isBoss = currentData != null && WaveConfig.IsBossType(currentData.wizardType);
            bool isFinalBoss = currentData != null && currentData.wizardType == WizardType.ChefeFinal;
            float duration = isFinalBoss ? 1.35f : (isBoss ? 1.15f : 0.70f);
            float elapsed = 0f;
            Color startCol = spriteRenderer.color;

            if (isBoss)
            {
                SpellEffectsManager.Instance?.TriggerCameraShake(isFinalBoss ? 0.38f : 0.25f, isFinalBoss ? 0.30f : 0.22f);
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Fade out suave nos ultimos 25% da animacao (quando ja esta estirado no chao)
                if (t > 0.75f)
                {
                    float fadeT = (t - 0.75f) / 0.25f;
                    spriteRenderer.color = new Color(startCol.r, startCol.g, startCol.b, Mathf.Lerp(startCol.a, 0f, fadeT));
                    if (groundShadowRenderer != null)
                    {
                        groundShadowRenderer.color = new Color(0.02f, 0.02f, 0.06f, Mathf.Lerp(baseShadowAlpha, 0f, fadeT));
                    }
                }
                yield return null;
            }

            deathCoroutine = null;
            OnDefeated?.Invoke(this, currentData.pointsOnDefeat);
            gameObject.SetActive(false);
        }

        #region Comportamentos e Personalidades de IA

        // 1. WANDER: Perambulação orgânica e errática com pausas e ataques ocasionais
        private IEnumerator WanderRoutine()
        {
            float baseSpeed = currentData.moveSpeed * currentSpeedMultiplier;

            while (true)
            {
                Vector3 target = GetRandomPointInBounds();
                Vector3 start = transform.position;
                float dist = Vector3.Distance(start, target);
                float duration = dist / Mathf.Max(0.5f, baseSpeed);
                float elapsed = 0f;

                frameAnimator.SetFacingDirection(target.x - start.x);
                frameAnimator.Play(isEnraged ? AnimationState.Run : AnimationState.Walk);

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                    ApplyDepthSortingAndScale();
                    yield return null;
                }

                transform.position = target;
                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.4f, 0.9f) / currentSpeedMultiplier);
            }
        }

        private IEnumerator PeriodicAttackRoutine(float minInterval, float maxInterval)
        {
            while (true)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(minInterval, maxInterval));
                if (isDeadOrEscaping) yield break;

                // Executa um ataque rápido com faca
                frameAnimator.PlayOneShot(AnimationState.Attack, AnimationState.Walk);
            }
        }

        private IEnumerator CommonJiggleReactionRoutine()
        {
            if (currentData.hitFrames != null && currentData.hitFrames.Length > 0)
            {
                frameAnimator.PlayOneShot(AnimationState.Hit, AnimationState.Walk);
            }

            Vector3 start = transform.position;
            for (int i = 0; i < 4; i++)
            {
                transform.position = start + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.1f);
                yield return new WaitForSeconds(0.025f);
            }
            transform.position = start;
        }

        // 2. FLEE AFTER HIT: Pulo em choque e disparada acelerada em direção oposta ao dano
        private IEnumerator FleeAfterHitRoutine()
        {
            isEnraged = true;
            if (movementCoroutine != null) StopCoroutine(movementCoroutine);

            // Reação visual inicial (sprite especial de salto ou frame de dano)
            if (currentData.specialActionSprite != null)
            {
                spriteRenderer.sprite = currentData.specialActionSprite;
            }
            else if (currentData.hitFrames != null && currentData.hitFrames.Length > 0)
            {
                frameAnimator.PlayOneShot(AnimationState.Hit, AnimationState.Run);
            }

            // Pulo de choque / pânico
            Vector3 startPos = transform.position;
            float jumpDuration = 0.22f;
            float jumpElapsed = 0f;
            float jumpHeight = 0.70f;

            while (jumpElapsed < jumpDuration)
            {
                jumpElapsed += Time.deltaTime;
                float t = jumpElapsed / jumpDuration;
                float jumpArc = Mathf.Sin(t * Mathf.PI);
                float yOffset = jumpArc * jumpHeight;
                transform.position = new Vector3(startPos.x, startPos.y + yOffset, startPos.z);
                if (groundShadowRenderer != null)
                {
                    float invScaleY = 1f / Mathf.Max(0.1f, transform.localScale.y);
                    groundShadowRenderer.transform.localPosition = baseShadowLocalPos - new Vector3(0f, yOffset * invScaleY, 0f);
                    groundShadowRenderer.transform.localScale = baseShadowLocalScale * Mathf.Lerp(1f, 0.62f, jumpArc);
                }
                yield return null;
            }
            transform.position = startPos;
            if (groundShadowRenderer != null)
            {
                groundShadowRenderer.transform.localPosition = baseShadowLocalPos;
                groundShadowRenderer.transform.localScale = baseShadowLocalScale;
            }

            // Corrida frenética (Flee) com progressão justa de agilidade conforme a fase atual
            float stageProgress = Mathf.Clamp01((currentStageNumber - 1) / 4f);
            float fleeTimer = Mathf.Max(1.2f, currentData.fleeDuration * Mathf.Lerp(0.78f, 1.05f, stageProgress));
            float progressiveFleeMult = (currentData.wizardType == WizardType.Fugitivo)
                ? Mathf.Lerp(1.38f, 1.92f, stageProgress)
                : Mathf.Max(1.5f, currentData.fleeSpeedMultiplier);
            float fleeSpeed = currentData.moveSpeed * currentSpeedMultiplier * progressiveFleeMult;

            frameAnimator.Play(AnimationState.Run);

            // Vetor de fuga oposto ao cursor / centro de dano
            Vector3 fleeOrigin = Vector3.zero;
            if (Camera.main != null)
            {
                fleeOrigin = Camera.main.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
                fleeOrigin.z = 0f;
            }
            Vector3 fleeDir = (transform.position - fleeOrigin).normalized;
            if (fleeDir.sqrMagnitude < 0.01f) fleeDir = UnityEngine.Random.insideUnitCircle.normalized;

            float margin = 0.9f;

            while (fleeTimer > 0f)
            {
                fleeTimer -= Time.deltaTime;

                Vector3 nextPos = transform.position + fleeDir * (fleeSpeed * Time.deltaTime);
                bool bounced = false;

                if (nextPos.x <= screenWorldBounds.min.x + margin || nextPos.x >= screenWorldBounds.max.x - margin)
                {
                    fleeDir.x = -fleeDir.x;
                    bounced = true;
                }
                if (nextPos.y <= screenWorldBounds.min.y + margin || nextPos.y >= screenWorldBounds.max.y - margin)
                {
                    fleeDir.y = -fleeDir.y;
                    bounced = true;
                }

                if (bounced)
                {
                    // Pequena perturbação angular para trajetória evasiva orgânica
                    fleeDir = (Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-25f, 25f)) * fleeDir).normalized;
                }

                transform.position = new Vector3(
                    Mathf.Clamp(nextPos.x, screenWorldBounds.min.x + margin, screenWorldBounds.max.x - margin),
                    Mathf.Clamp(nextPos.y, screenWorldBounds.min.y + margin, screenWorldBounds.max.y - margin),
                    0f
                );

                frameAnimator.SetFacingDirection(fleeDir.x);
                ApplyDepthSortingAndScale();
                yield return null;
            }

            isEnraged = false;
            // Após a disparada de fuga, retoma a perambulação padrão
            movementCoroutine = StartCoroutine(WanderRoutine());
        }

        // 3. STRAIGHT MOVEMENT: Marcha retilínea contínua, disciplinada e militar
        private IEnumerator StraightMovementRoutine()
        {
            float speed = currentData.moveSpeed * currentSpeedMultiplier * 1.05f;
            float margin = 1.0f;

            // Escolhe um rumo inicial reto
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            if (Mathf.Abs(dir.x) < 0.35f) dir.x = Mathf.Sign(dir.x) * 0.5f;
            dir.Normalize();

            frameAnimator.Play(AnimationState.Run);

            while (true)
            {
                frameAnimator.SetFacingDirection(dir.x);

                Vector3 nextPos = transform.position + (Vector3)(dir * (speed * Time.deltaTime));
                bool hitBoundary = false;

                if (nextPos.x <= screenWorldBounds.min.x + margin)
                {
                    nextPos.x = screenWorldBounds.min.x + margin;
                    dir.x = Mathf.Abs(dir.x);
                    hitBoundary = true;
                }
                else if (nextPos.x >= screenWorldBounds.max.x - margin)
                {
                    nextPos.x = screenWorldBounds.max.x - margin;
                    dir.x = -Mathf.Abs(dir.x);
                    hitBoundary = true;
                }

                if (nextPos.y <= screenWorldBounds.min.y + margin)
                {
                    nextPos.y = screenWorldBounds.min.y + margin;
                    dir.y = Mathf.Abs(dir.y);
                    hitBoundary = true;
                }
                else if (nextPos.y >= screenWorldBounds.max.y - margin)
                {
                    nextPos.y = screenWorldBounds.max.y - margin;
                    dir.y = -Mathf.Abs(dir.y);
                    hitBoundary = true;
                }

                transform.position = nextPos;
                ApplyDepthSortingAndScale();

                if (hitBoundary)
                {
                    // Breve postura marcial ao atingir a borda antes de continuar a patrulha
                    frameAnimator.Play(AnimationState.Idle);
                    yield return new WaitForSeconds(0.16f / currentSpeedMultiplier);
                    frameAnimator.Play(AnimationState.Run);
                }

                yield return null;
            }
        }

        private IEnumerator GoldenGlowRoutine()
        {
            while (true)
            {
                if (flashCoroutine == null && !isFrozen && !isDeadOrEscaping)
                {
                    float factor = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                    spriteRenderer.color = Color.Lerp(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.75f, 0.1f), factor);
                }
                yield return null;
            }
        }

        private IEnumerator GoldenShieldReactionRoutine()
        {
            if (currentData.specialActionSprite != null)
            {
                spriteRenderer.sprite = currentData.specialActionSprite; // Postura erguendo escudo
            }

            // Recuo defensivo absorvendo impacto (respeitando os limites da arena)
            Vector3 start = transform.position;
            Vector3 recoilDir = (UnityEngine.Random.insideUnitCircle.normalized);
            Vector3 rawTarget = start + (recoilDir * 0.45f);
            float margin = 1.0f;
            Vector3 recoilTarget = new Vector3(
                Mathf.Clamp(rawTarget.x, screenWorldBounds.min.x + margin, screenWorldBounds.max.x - margin),
                Mathf.Clamp(rawTarget.y, screenWorldBounds.min.y + margin, screenWorldBounds.max.y - margin),
                0f
            );

            float elapsed = 0f;
            float dur = 0.14f / currentSpeedMultiplier;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, recoilTarget, elapsed / dur);
                ApplyDepthSortingAndScale();
                yield return null;
            }

            yield return new WaitForSeconds(0.12f / currentSpeedMultiplier);
            StartBehavior();
        }

        // 4. SPECTRAL HOVER & TELEPORT: Flutuação etérea e salto dimensional ao tomar dano
        private IEnumerator GhostSpectralAlphaRoutine()
        {
            while (true)
            {
                if (flashCoroutine == null && !isFrozen && !isDeadOrEscaping)
                {
                    float alpha = Mathf.PingPong(Time.time * 1.2f, 0.45f) + 0.55f;
                    Color c = spriteRenderer.color;
                    spriteRenderer.color = new Color(c.r, c.g, c.b, alpha);
                }
                yield return null;
            }
        }

        private IEnumerator SpectralHoverRoutine()
        {
            while (true)
            {
                Vector3 startPos = transform.position;
                Vector3 targetPos = GetRandomPointInBounds();
                float dist = Vector3.Distance(startPos, targetPos);
                float duration = dist / Mathf.Max(0.6f, currentData.moveSpeed * currentSpeedMultiplier * 0.85f);
                float elapsed = 0f;

                frameAnimator.SetFacingDirection(targetPos.x - startPos.x);

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    Vector3 basePos = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, t));

                    float oy = Mathf.Sin(Time.time * 2.8f * currentSpeedMultiplier) * 0.32f;
                    float ox = Mathf.Cos(Time.time * 1.6f * currentSpeedMultiplier) * 0.18f;

                    transform.position = new Vector3(basePos.x + ox, basePos.y + oy, basePos.z);
                    ApplyDepthSortingAndScale();
                    yield return null;
                }

                transform.position = targetPos;
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.3f, 0.8f) / currentSpeedMultiplier);
            }
        }

        // 5. EXTENSÕES FUTURAS: Suporte, Proteção, Ilusão e Kamikaze
        private IEnumerator ProtectAllyRoutine()
        {
            while (true)
            {
                WizardController ally = null;
                float closestDist = float.MaxValue;

                foreach (var goblin in activeGoblins)
                {
                    if (goblin != this && goblin.gameObject.activeInHierarchy && !goblin.isDeadOrEscaping)
                    {
                        float d = Vector3.Distance(transform.position, goblin.transform.position);
                        if (d < closestDist)
                        {
                            closestDist = d;
                            ally = goblin;
                        }
                    }
                }

                if (ally != null)
                {
                    Vector3 protectPos = ally.transform.position + new Vector3(0.4f, 0f, 0f);
                    Vector3 start = transform.position;
                    float duration = Vector3.Distance(start, protectPos) / (currentData.moveSpeed * currentSpeedMultiplier * 1.3f);
                    float elapsed = 0f;

                    frameAnimator.Play(AnimationState.Walk);
                    frameAnimator.SetFacingDirection(protectPos.x - start.x);

                    while (elapsed < duration && ally != null && ally.gameObject.activeInHierarchy)
                    {
                        elapsed += Time.deltaTime;
                        transform.position = Vector3.Lerp(start, protectPos, elapsed / duration);
                        ApplyDepthSortingAndScale();
                        yield return null;
                    }

                    frameAnimator.Play(AnimationState.Idle);
                    yield return new WaitForSeconds(1.0f);
                }
                else
                {
                    Vector3 patrolPoint = GetRandomPointInBounds();
                    Vector3 start = transform.position;
                    float dist = Vector3.Distance(start, patrolPoint);
                    float dur = dist / Mathf.Max(0.5f, currentData.moveSpeed * currentSpeedMultiplier);
                    float elapsed = 0f;

                    frameAnimator.SetFacingDirection(patrolPoint.x - start.x);
                    frameAnimator.Play(AnimationState.Walk);
                    while (elapsed < dur)
                    {
                        elapsed += Time.deltaTime;
                        transform.position = Vector3.Lerp(start, patrolPoint, Mathf.SmoothStep(0f, 1f, elapsed / dur));
                        ApplyDepthSortingAndScale();
                        yield return null;
                    }
                    transform.position = patrolPoint;
                    frameAnimator.Play(AnimationState.Idle);
                    yield return new WaitForSeconds(0.4f);
                }

                yield return null;
            }
        }

        private IEnumerator HealerSupportRoutine()
        {
            while (true)
            {
                Vector3 retreatPoint = GetRandomPointInBounds();
                Vector3 start = transform.position;
                float duration = Vector3.Distance(start, retreatPoint) / (currentData.moveSpeed * currentSpeedMultiplier);
                float elapsed = 0f;

                frameAnimator.Play(AnimationState.Walk);
                frameAnimator.SetFacingDirection(retreatPoint.x - start.x);

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(start, retreatPoint, elapsed / duration);
                    ApplyDepthSortingAndScale();
                    yield return null;
                }

                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(1.5f);

                PulseHealingWave();
                yield return new WaitForSeconds(2.0f);
            }
        }

        private void PulseHealingWave()
        {
            float radius = currentData.supportRadius > 0f ? currentData.supportRadius : 3.5f;
            SpawnBossBurstFX(transform.position, new Color(0.2f, 1f, 0.4f, 0.8f), radius * 0.6f);

            foreach (var goblin in activeGoblins)
            {
                if (goblin != this && goblin.gameObject.activeInHierarchy && !goblin.isDeadOrEscaping)
                {
                    if (Vector3.Distance(transform.position, goblin.transform.position) <= radius)
                    {
                        goblin.Heal(1);
                    }
                }
            }
        }

        public void Heal(int amount)
        {
            if (isDeadOrEscaping || currentHealth >= currentData.maxHealth) return;
            currentHealth = Mathf.Min(currentData.maxHealth, currentHealth + amount);
            UpdateHealthBar();
            StartCoroutine(FlashHealRoutine());
        }

        private IEnumerator FlashHealRoutine()
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = Color.green;
            yield return new WaitForSeconds(0.12f);
            if (!isDeadOrEscaping) spriteRenderer.color = orig;
        }

        private IEnumerator SpawnDecoyReactionRoutine()
        {
            SpawnBossBurstFX(transform.position, new Color(0.7f, 0.5f, 1f, 0.8f), 1.2f);
            yield return StartCoroutine(CommonJiggleReactionRoutine());
        }

        private IEnumerator ExplodeOnDeathRoutine()
        {
            // Tique-taque de contagem regressiva piscando em vermelho
            for (int i = 0; i < 4; i++)
            {
                spriteRenderer.color = Color.red;
                yield return new WaitForSeconds(0.06f);
                spriteRenderer.color = Color.white;
                yield return new WaitForSeconds(0.06f);
            }

            SpellEffectsManager.Instance?.TriggerCameraShake(0.35f, 0.30f);
            SpawnBossBurstFX(transform.position, new Color(1f, 0.4f, 0.1f, 0.95f), 2.5f);

            yield return StartCoroutine(DeathAnimationRoutine());
        }

        // Aliases de compatibilidade
        private IEnumerator CommonPatrolRoutine() => WanderRoutine();
        private IEnumerator FugitiveRunRoutine() => WanderRoutine();
        private IEnumerator GoldenAggressiveRoutine() => StraightMovementRoutine();
        private IEnumerator GhostLevitateRoutine() => SpectralHoverRoutine();
        private IEnumerator FugitiveJumpReactionRoutine() => FleeAfterHitRoutine();

        private IEnumerator GhostTeleportReactionRoutine()
        {
            if (movementCoroutine != null) StopCoroutine(movementCoroutine);

            IsTeleporting = true;
            Vector3 oldPos = transform.position;
            Vector3 newPos = GetRandomPointInBounds();

            if (currentData.teleportFrames != null && currentData.teleportFrames.Length > 0)
            {
                // Reproduz animacao completa de deslocamento dimensional (8 quadros a 12 FPS)
                frameAnimator.PlayOneShot(AnimationState.Teleport, AnimationState.Walk);
                // Quadros 0..3: desmaterializacao no portal
                yield return new WaitForSeconds(0.33f);
                transform.position = newPos;
                frameAnimator.SetFacingDirection(UnityEngine.Random.value > 0.5f ? 1f : -1f);
                // Quadros 4..7: rematerializacao no destino
                yield return new WaitForSeconds(0.34f);
            }
            else
            {
                // Fallback classico com efeito de escala e portal
                SpawnPortalEffect(oldPos);

                float elapsed = 0f;
                float dur = 0.12f;
                Vector3 startScale = transform.localScale;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / dur);
                    yield return null;
                }

                SpawnPortalEffect(newPos);
                transform.position = newPos;

                elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    transform.localScale = Vector3.Lerp(Vector3.zero, normalizedScale, elapsed / dur);
                    yield return null;
                }
                transform.localScale = normalizedScale;
            }

            IsTeleporting = false;
            ApplyDepthSortingAndScale();
            StartBehavior();
        }

        private void SpawnPortalEffect(Vector3 pos)
        {
            if (currentData.specialActionSprite == null) return;

            GameObject portalGo = new GameObject("PortalFX");
            portalGo.transform.position = pos;
            portalGo.transform.localScale = normalizedScale * 1.1f;

            SpriteRenderer sr = portalGo.AddComponent<SpriteRenderer>();
            sr.sprite = currentData.specialActionSprite;
            sr.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 168;
            sr.color = new Color(0.3f, 0.8f, 1f, 0.9f);

            MonoBehaviour host = SpellEffectsManager.Instance != null ? (MonoBehaviour)SpellEffectsManager.Instance : this;
            host.StartCoroutine(PortalFadeRoutine(portalGo));
        }

        private IEnumerator PortalFadeRoutine(GameObject portalGo)
        {
            float dur = 0.35f;
            float elapsed = 0f;
            SpriteRenderer sr = portalGo != null ? portalGo.GetComponent<SpriteRenderer>() : null;

            while (elapsed < dur && portalGo != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                portalGo.transform.Rotate(0f, 0f, 180f * Time.deltaTime);
                if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Lerp(0.9f, 0f, t));
                yield return null;
            }

            if (portalGo != null) Destroy(portalGo);
        }

        private IEnumerator ExecuteTelegraphedSlamRoutine(float windupDuration, AnimationState attackAnim, Color warningColor, float burstScale, int damage, string attackName)
        {
            isChargingBossAttack = true;
            wasBossChargeInterrupted = false;
            bossChargeHitsReceived = 0;

            float effectiveWindup = isBossEnraged ? windupDuration * 0.82f : windupDuration;

            // Anel visual de aviso (Telegrafo interrompível)
            CleanupTelegraphRing();
            isChargingBossAttack = true;
            activeTelegraphRing = new GameObject("BossTelegraphRing");
            activeTelegraphRing.transform.SetParent(transform, false);
            activeTelegraphRing.transform.localPosition = Vector3.zero;

            var lr = activeTelegraphRing.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            lr.material = new Material(shader);
            lr.useWorldSpace = false;
            lr.loop = true;
            int segs = 28;
            lr.positionCount = segs;
            lr.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 20) + 5;
            lr.startWidth = 0.08f;
            lr.endWidth = 0.08f;
            lr.startColor = warningColor;
            lr.endColor = Color.white;

            Color prevTint = spriteRenderer != null ? spriteRenderer.color : Color.white;
            float elapsed = 0f;

            while (elapsed < effectiveWindup)
            {
                if (isDeadOrEscaping || isFrozen || wasBossChargeInterrupted)
                {
                    CleanupTelegraphRing();
                    if (!isDeadOrEscaping && !isFrozen && spriteRenderer != null) spriteRenderer.color = currentData.baseTint;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / effectiveWindup);
                float radius = Mathf.Lerp(1.65f, 0.35f, t);

                for (int i = 0; i < segs; i++)
                {
                    float ang = (i / (float)segs) * Mathf.PI * 2f;
                    lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f));
                }

                if (spriteRenderer != null)
                {
                    float flash = Mathf.PingPong(elapsed * 10f, 1f);
                    spriteRenderer.color = Color.Lerp(currentData.baseTint, warningColor, 0.45f + flash * 0.45f);
                }

                yield return null;
            }

            CleanupTelegraphRing();
            if (!isDeadOrEscaping && spriteRenderer != null) spriteRenderer.color = currentData.baseTint;

            if (isDeadOrEscaping || isFrozen || wasBossChargeInterrupted) yield break;

            frameAnimator.PlayOneShot(attackAnim, AnimationState.Idle);
            yield return new WaitForSeconds(0.22f);

            if (isDeadOrEscaping || isFrozen || wasBossChargeInterrupted) yield break;

            SpellEffectsManager.Instance?.TriggerCameraShake(0.18f, 0.20f);
            SpawnBossBurstFX(transform.position, warningColor, burstScale);
            OnAttackPlayer?.Invoke(this, damage, attackName);
        }

        // 5. GOBLIN CHEFE (BOSS 1): Grande Goblin (Marcha, Investida e Golpe Sísmico Telegrafado)
        private IEnumerator BossBehaviorRoutine()
        {
            float speed = currentData.moveSpeed * currentSpeedMultiplier;

            while (true)
            {
                Vector3 target = GetRandomPointInBounds();
                Vector3 start = transform.position;
                float dist = Vector3.Distance(start, target);
                float walkDuration = dist / Mathf.Max(0.5f, speed);
                float elapsed = 0f;

                frameAnimator.SetFacingDirection(target.x - start.x);
                frameAnimator.Play(AnimationState.Walk);

                while (elapsed < walkDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / walkDuration;
                    transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                    ApplyDepthSortingAndScale();
                    yield return null;
                }

                transform.position = target;
                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.3f, 0.55f) / currentSpeedMultiplier);

                if (isDeadOrEscaping) yield break;

                bool doInvestida = (currentData.attack2Frames != null && currentData.attack2Frames.Length > 0) && (UnityEngine.Random.value > 0.45f);

                if (doInvestida)
                {
                    // Investida Rápida: arremessa onda de choque sísmica interceptável ao chegar
                    Vector3 chargeTarget = GetRandomPointInBounds();
                    frameAnimator.SetFacingDirection(chargeTarget.x - transform.position.x);
                    frameAnimator.PlayOneShot(AnimationState.Attack2, AnimationState.Idle);

                    Vector3 chargeStart = transform.position;
                    float chargeDist = Vector3.Distance(chargeStart, chargeTarget);
                    float chargeDuration = chargeDist / Mathf.Max(1.0f, speed * 2.1f);
                    float chargeElapsed = 0f;

                    while (chargeElapsed < chargeDuration)
                    {
                        chargeElapsed += Time.deltaTime;
                        float t = chargeElapsed / chargeDuration;
                        transform.position = Vector3.Lerp(chargeStart, chargeTarget, t);
                        ApplyDepthSortingAndScale();
                        yield return null;
                    }

                    transform.position = chargeTarget;
                    SpellEffectsManager.Instance?.TriggerCameraShake(0.10f, 0.10f);
                    SpawnApproachingBossProjectile(transform.position, new Color(1f, 0.6f, 0.15f), 1.85f, 1, "Onda de Choque do Grande Goblin");
                }
                else if (currentData.attackFrames != null && currentData.attackFrames.Length > 0)
                {
                    // Ataque 1: Golpe Sísmico Telegrafado (pode ser interrompido!)
                    yield return StartCoroutine(ExecuteTelegraphedSlamRoutine(1.30f, AnimationState.Attack, new Color(1f, 0.55f, 0.1f, 0.95f), 1.7f, 1, "Golpe Sísmico do Grande Goblin"));
                }

                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.35f, 0.7f) / currentSpeedMultiplier);
            }
        }

        private IEnumerator BossHitReactionRoutine()
        {
            if (currentData.hitFrames != null && currentData.hitFrames.Length > 0)
            {
                frameAnimator.PlayOneShot(AnimationState.Hit, AnimationState.Walk);
            }

            Vector3 start = transform.position;
            for (int i = 0; i < 3; i++)
            {
                transform.position = start + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.08f);
                yield return new WaitForSeconds(0.025f);
            }
            transform.position = start;
        }

        // --- CHEFE 2: XAMÃ DO CAOS (Orbes de Fogo 2.5D e Labareda Telegrafada) ---
        private IEnumerator Boss2BehaviorRoutine()
        {
            float speed = currentData.moveSpeed * currentSpeedMultiplier;

            while (true)
            {
                Vector3 target = GetRandomPointInBounds();
                Vector3 start = transform.position;
                float dist = Vector3.Distance(start, target);
                float walkDuration = dist / Mathf.Max(0.5f, speed);
                float elapsed = 0f;

                frameAnimator.SetFacingDirection(target.x - start.x);
                frameAnimator.Play(AnimationState.Walk);

                while (elapsed < walkDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / walkDuration;
                    transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                    ApplyDepthSortingAndScale();
                    yield return null;
                }

                transform.position = target;
                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.28f, 0.52f) / currentSpeedMultiplier);
                if (isDeadOrEscaping) yield break;

                bool doFlameBurst = UnityEngine.Random.value > 0.55f;

                if (doFlameBurst && currentData.attack2Frames != null && currentData.attack2Frames.Length > 0)
                {
                    // Labareda em Área (Telegrafo interrompível!)
                    yield return StartCoroutine(ExecuteTelegraphedSlamRoutine(1.42f, AnimationState.Attack2, new Color(1f, 0.42f, 0.08f, 0.95f), 2.0f, 1, "Labareda do Xamã do Caos"));
                }
                else if (currentData.attackFrames != null && currentData.attackFrames.Length > 0)
                {
                    // Disparo de Projétil de Fogo vindo em direção à câmera
                    frameAnimator.PlayOneShot(AnimationState.Attack, AnimationState.Idle);
                    yield return new WaitForSeconds(0.28f);
                    SpawnApproachingBossProjectile(transform.position + Vector3.down * 0.1f, new Color(1f, 0.48f, 0.1f), isBossEnraged ? 1.72f : 2.05f, 1, "Bola de Fogo do Xamã");
                    if (isBossEnraged)
                    {
                        yield return new WaitForSeconds(0.28f);
                        SpawnApproachingBossProjectile(transform.position + Vector3.right * 0.3f, new Color(1f, 0.65f, 0.15f), 1.85f, 1, "Centelha Extra do Xamã");
                    }
                    yield return new WaitForSeconds(0.3f);
                }

                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.35f, 0.68f) / currentSpeedMultiplier);
            }
        }

        // --- CHEFE 3: FEITICEIRO ESPECTRAL (Teleporte, Furtividade Breve, Invocação e Orbes) ---
        private IEnumerator Boss3BehaviorRoutine()
        {
            float speed = currentData.moveSpeed * currentSpeedMultiplier;
            bool usedStealthLastTurn = false;

            while (true)
            {
                float roll = UnityEngine.Random.value;

                if (roll < 0.28f && currentData.teleportFrames != null && currentData.teleportFrames.Length > 0)
                {
                    usedStealthLastTurn = false;
                    IsTeleporting = true;
                    frameAnimator.PlayOneShot(AnimationState.Teleport, AnimationState.Idle);
                    SpawnBossBurstFX(transform.position, new Color(0.7f, 0.2f, 1f, 0.85f), 1.5f);
                    yield return new WaitForSeconds(0.28f);

                    Vector3 newPos = GetRandomPointInBounds();
                    transform.position = newPos;
                    ApplyDepthSortingAndScale();
                    SpawnBossBurstFX(newPos, new Color(0.7f, 0.2f, 1f, 0.85f), 1.5f);
                    yield return new WaitForSeconds(0.25f);
                    IsTeleporting = false;
                }
                else if (roll < 0.48f && !usedStealthLastTurn)
                {
                    // Furtividade breve e ágil (nunca fica preso invulnerável)
                    usedStealthLastTurn = true;
                    isInvulnerable = true;
                    Color normalColor = currentData.baseTint;
                    spriteRenderer.color = new Color(normalColor.r, normalColor.g, normalColor.b, 0.22f);
                    if (groundShadowRenderer != null) groundShadowRenderer.color = new Color(0.02f, 0.02f, 0.06f, 0.10f);

                    Vector3 stealthTarget = GetRandomPointInBounds();
                    Vector3 stealthStart = transform.position;
                    float stealthDist = Vector3.Distance(stealthStart, stealthTarget);
                    float stealthDur = Mathf.Min(1.1f, stealthDist / Mathf.Max(1.5f, speed * 1.8f));
                    float stealthElapsed = 0f;

                    frameAnimator.SetFacingDirection(stealthTarget.x - stealthStart.x);
                    frameAnimator.Play(AnimationState.Walk);

                    while (stealthElapsed < stealthDur)
                    {
                        stealthElapsed += Time.deltaTime;
                        transform.position = Vector3.Lerp(stealthStart, stealthTarget, stealthElapsed / Mathf.Max(0.01f, stealthDur));
                        ApplyDepthSortingAndScale();
                        yield return null;
                    }

                    transform.position = stealthTarget;
                    yield return new WaitForSeconds(0.22f);

                    spriteRenderer.color = normalColor;
                    if (groundShadowRenderer != null) groundShadowRenderer.color = new Color(0.02f, 0.02f, 0.06f, baseShadowAlpha);
                    isInvulnerable = false;
                    SpawnBossBurstFX(transform.position, new Color(0.55f, 0.15f, 0.95f, 0.85f), 1.4f);
                }
                else if (roll < 0.72f && currentData.summonFrames != null && currentData.summonFrames.Length > 0)
                {
                    usedStealthLastTurn = false;
                    frameAnimator.PlayOneShot(AnimationState.Summon, AnimationState.Idle);
                    yield return new WaitForSeconds(0.35f);
                    SpellEffectsManager.Instance?.TriggerCameraShake(0.12f, 0.12f);
                    SpawnBossBurstFX(transform.position + Vector3.up * 0.4f, new Color(0.8f, 0.2f, 1f, 0.9f), 2.2f);
                    OnBossSummonRequested?.Invoke(this, WizardType.Fantasma);
                    yield return new WaitForSeconds(0.35f);
                }
                else if (currentData.attackFrames != null && currentData.attackFrames.Length > 0)
                {
                    usedStealthLastTurn = false;
                    frameAnimator.PlayOneShot(AnimationState.Attack, AnimationState.Idle);
                    yield return new WaitForSeconds(0.28f);
                    SpawnApproachingBossProjectile(transform.position, new Color(0.75f, 0.2f, 1f), isBossEnraged ? 1.65f : 1.95f, 1, "Orbe Espectral do Feiticeiro");
                    yield return new WaitForSeconds(0.3f);
                }

                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.3f, 0.65f) / currentSpeedMultiplier);
            }
        }

        // --- CHEFE 4: LORDE CARMESIM (Cetro Telegrafado, Orbes Duplos e Invocação) ---
        private IEnumerator Boss4BehaviorRoutine()
        {
            float speed = currentData.moveSpeed * currentSpeedMultiplier;

            while (true)
            {
                Vector3 target = GetRandomPointInBounds();
                Vector3 start = transform.position;
                float dist = Vector3.Distance(start, target);
                float walkDuration = dist / Mathf.Max(0.5f, speed);
                float elapsed = 0f;

                frameAnimator.SetFacingDirection(target.x - start.x);
                frameAnimator.Play(AnimationState.Walk);

                while (elapsed < walkDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / walkDuration;
                    transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                    ApplyDepthSortingAndScale();
                    yield return null;
                }

                transform.position = target;
                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.25f, 0.5f) / currentSpeedMultiplier);
                if (isDeadOrEscaping) yield break;

                float actionRoll = UnityEngine.Random.value;

                if (actionRoll < 0.35f && currentData.attackFrames != null && currentData.attackFrames.Length > 0)
                {
                    // Golpe de Cetro Telegrafado (1 Dano se não interrompido!)
                    yield return StartCoroutine(ExecuteTelegraphedSlamRoutine(1.38f, AnimationState.Attack, new Color(0.95f, 0.12f, 0.18f, 0.95f), 1.9f, 1, "Golpe de Cetro do Lorde Carmesim"));
                }
                else if (actionRoll < 0.78f && currentData.attack2Frames != null && currentData.attack2Frames.Length > 0)
                {
                    // Ataque 2: Rajada Dupla de Orbes Carmesins interceptáveis
                    frameAnimator.PlayOneShot(AnimationState.Attack2, AnimationState.Idle);
                    yield return new WaitForSeconds(0.28f);
                    float flightDur = isBossEnraged ? 1.68f : 1.98f;
                    SpawnApproachingBossProjectile(transform.position + Vector3.left * 0.35f, new Color(0.95f, 0.08f, 0.2f), flightDur, 1, "Orbe Carmesim do Lorde", -2.2f);
                    yield return new WaitForSeconds(0.22f);
                    SpawnApproachingBossProjectile(transform.position + Vector3.right * 0.35f, new Color(0.95f, 0.08f, 0.2f), flightDur, 1, "Orbe Carmesim do Lorde", 2.2f);
                    yield return new WaitForSeconds(0.25f);
                }
                else if (currentData.summonFrames != null && currentData.summonFrames.Length > 0)
                {
                    frameAnimator.PlayOneShot(AnimationState.Summon, AnimationState.Idle);
                    yield return new WaitForSeconds(0.35f);
                    SpellEffectsManager.Instance?.TriggerCameraShake(0.15f, 0.15f);
                    SpawnBossBurstFX(transform.position, new Color(0.85f, 0.05f, 0.2f, 0.95f), 2.4f);
                    OnBossSummonRequested?.Invoke(this, UnityEngine.Random.value > 0.5f ? WizardType.Fugitivo : WizardType.Dourado);
                    yield return new WaitForSeconds(0.35f);
                }

                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.3f, 0.6f) / currentSpeedMultiplier);
            }
        }

        // --- CHEFE FINAL: ⚔️ GOBLIN REI SUPREMO (O Confronto Épico Supremo) ---
        private IEnumerator BossFinalBehaviorRoutine()
        {
            float speed = currentData.moveSpeed * currentSpeedMultiplier;

            while (true)
            {
                Vector3 target = GetRandomPointInBounds();
                Vector3 start = transform.position;
                float dist = Vector3.Distance(start, target);
                float walkDuration = dist / Mathf.Max(0.5f, speed);
                float elapsed = 0f;

                frameAnimator.SetFacingDirection(target.x - start.x);
                frameAnimator.Play(AnimationState.Walk);

                while (elapsed < walkDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / walkDuration;
                    transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                    ApplyDepthSortingAndScale();
                    yield return null;
                }

                transform.position = target;
                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.22f, 0.45f) / currentSpeedMultiplier);
                if (isDeadOrEscaping) yield break;

                float roll = UnityEngine.Random.value;

                if (roll < 0.25f && currentData.attackFrames != null && currentData.attackFrames.Length > 0)
                {
                    // Maça Sísmica Telegrafada
                    yield return StartCoroutine(ExecuteTelegraphedSlamRoutine(1.32f, AnimationState.Attack, new Color(1f, 0.35f, 0.05f, 0.95f), 2.3f, 2, "Terremoto Colossal do Rei"));
                }
                else if (roll < 0.50f && currentData.attack2Frames != null && currentData.attack2Frames.Length > 0)
                {
                    // Choque de Magma Telegrafado
                    yield return StartCoroutine(ExecuteTelegraphedSlamRoutine(1.28f, AnimationState.Attack2, new Color(1f, 0.58f, 0.1f, 0.95f), 2.8f, 2, "Choque de Magma do Rei"));
                }
                else if (roll < 0.82f && currentData.attack3Frames != null && currentData.attack3Frames.Length > 0)
                {
                    // Ataque 3: Rajada Tripla de Orbes Solares 2.5D
                    frameAnimator.PlayOneShot(AnimationState.Attack3, AnimationState.Idle);
                    yield return new WaitForSeconds(0.28f);

                    float flightDur = isBossEnraged ? 1.75f : 2.08f;
                    float[] xOffsets = new float[] { -3.2f, 0f, 3.2f };
                    foreach (float xo in xOffsets)
                    {
                        SpawnApproachingBossProjectile(transform.position + Vector3.up * 0.15f, new Color(1f, 0.42f, 0.05f), flightDur, 1, "Magma Solar do Rei Goblin", xo);
                    }
                    SpellEffectsManager.Instance?.TriggerCameraShake(0.14f, 0.14f);
                    yield return new WaitForSeconds(0.32f);
                }
                else if (currentData.summonFrames != null && currentData.summonFrames.Length > 0)
                {
                    frameAnimator.PlayOneShot(AnimationState.Summon, AnimationState.Idle);
                    yield return new WaitForSeconds(0.35f);
                    SpellEffectsManager.Instance?.TriggerCameraShake(0.22f, 0.22f);
                    SpawnBossBurstFX(transform.position, new Color(1f, 0.85f, 0.2f, 0.95f), 3.0f);
                    OnBossSummonRequested?.Invoke(this, UnityEngine.Random.value > 0.45f ? WizardType.Dourado : WizardType.Fantasma);
                    yield return new WaitForSeconds(0.35f);
                }

                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.28f, 0.55f) / currentSpeedMultiplier);
            }
        }

        private void SpawnApproachingBossProjectile(Vector3 startPos, Color color, float flightDuration, int damage, string attackName, float targetXBias = 0f)
        {
            float targetX = Mathf.Clamp(targetXBias + UnityEngine.Random.Range(-1.1f, 1.1f), screenWorldBounds.min.x + 1.2f, screenWorldBounds.max.x - 1.2f);
            Vector3 targetPos = new Vector3(targetX, screenWorldBounds.min.y + 0.45f, 0f);

            GameObject proj = new GameObject("BossProjectile");
            proj.transform.position = startPos;
            proj.transform.localScale = Vector3.one * 0.35f;

            SpriteRenderer sr = proj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateOrbSprite(64);
            sr.color = color;
            sr.sortingOrder = 170;

            // Núcleo interno brilhante branco-dourado para máxima visibilidade
            GameObject core = new GameObject("Core");
            core.transform.SetParent(proj.transform, false);
            core.transform.localScale = Vector3.one * 0.52f;
            var coreSr = core.AddComponent<SpriteRenderer>();
            coreSr.sprite = sr.sprite;
            coreSr.color = new Color(1f, 0.96f, 0.82f, 0.95f);
            coreSr.sortingOrder = 171;

            CircleCollider2D col = proj.AddComponent<CircleCollider2D>();
            col.radius = 0.62f;
            col.isTrigger = true;

            BossProjectileInstance item = proj.AddComponent<BossProjectileInstance>();
            RegisterBossProjectile(proj);
            item.Launch(this, startPos, targetPos, color, flightDuration, damage, attackName, (owner, dmg, atk) =>
            {
                OnAttackPlayer?.Invoke(owner, dmg, atk);
            });
        }

        private static Sprite cachedOrbSprite;
        private static Sprite CreateOrbSprite(int resolution)
        {
            if (cachedOrbSprite != null) return cachedOrbSprite;
            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float c = resolution * 0.5f;
            float r = c - 1f;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                    float norm = Mathf.Clamp01(d / r);
                    float alpha = Mathf.SmoothStep(1f, 0f, norm * norm);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            cachedOrbSprite = Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
            return cachedOrbSprite;
        }

        private void SpawnBossBurstFX(Vector3 pos, Color color, float radius)
        {
            GameObject burst = new GameObject("BossBurstFX");
            burst.transform.position = pos;
            burst.transform.localScale = Vector3.one * 0.2f;

            SpriteRenderer sr = burst.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSolidWhiteSprite();
            sr.color = color;
            sr.sortingOrder = 182;

            MonoBehaviour host = SpellEffectsManager.Instance != null ? (MonoBehaviour)SpellEffectsManager.Instance : this;
            host.StartCoroutine(BossBurstFadeRoutine(burst, color, radius));
        }

        private IEnumerator BossBurstFadeRoutine(GameObject burst, Color color, float maxScale)
        {
            float dur = 0.35f;
            float elapsed = 0f;
            SpriteRenderer sr = burst != null ? burst.GetComponent<SpriteRenderer>() : null;

            while (elapsed < dur && burst != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                burst.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, maxScale, Mathf.Sin(t * Mathf.PI * 0.5f));
                if (sr != null)
                {
                    sr.color = new Color(color.r, color.g, color.b, Mathf.Lerp(color.a, 0f, t));
                }
                yield return null;
            }

            if (burst != null) Destroy(burst);
        }

        private Vector3 GetRandomPointInBounds()
        {
            float margin = 1.0f;
            float rx = UnityEngine.Random.Range(screenWorldBounds.min.x + margin, screenWorldBounds.max.x - margin);
            float ry = UnityEngine.Random.Range(screenWorldBounds.min.y + margin, screenWorldBounds.max.y - margin);
            return new Vector3(rx, ry, 0f);
        }

        #endregion

        public WizardDataSO GetData() => currentData;
    }
}