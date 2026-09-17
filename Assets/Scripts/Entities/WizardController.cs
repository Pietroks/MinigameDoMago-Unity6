using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WizardGame.Core;
using WizardGame.Data;

namespace WizardGame.Entities
{
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
        private bool isFrozen;
        private float remainingEscapeTime;
        private Bounds screenWorldBounds;
        private Vector3 normalizedScale = Vector3.one;
        private float currentSpeedMultiplier = 1.0f;
        private Coroutine movementCoroutine;
        private Coroutine pulseCoroutine;
        private Coroutine escapeCoroutine;
        private Coroutine deathCoroutine;
        private Coroutine hitReactionCoroutine;
        private Coroutine attackRoutine;
        private Coroutine freezeCoroutine;

        private static readonly List<WizardController> activeGoblins = new List<WizardController>();
        public static IReadOnlyList<WizardController> ActiveGoblins => activeGoblins;
        public bool IsFrozen => isFrozen;

        public event Action<WizardController, int> OnDefeated;
        public event Action<WizardController, int> OnEscaped;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (hitCollider == null) hitCollider = GetComponent<Collider2D>();
            if (frameAnimator == null) frameAnimator = GetComponent<FrameAnimator>();
            if (frameAnimator == null) frameAnimator = gameObject.AddComponent<FrameAnimator>();

            CreateHealthBarIfMissing();
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

        private Sprite CreateSolidWhiteSprite()
        {
            Texture2D tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        public void Initialize(WizardDataSO data, Vector3 spawnPosition, Bounds bounds, float speedMultiplier = 1.0f)
        {
            currentData = data;
            currentHealth = data.maxHealth;
            screenWorldBounds = bounds;
            currentSpeedMultiplier = Mathf.Max(0.5f, speedMultiplier);
            isDeadOrEscaping = false;
            isEnraged = false;

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
            float effectiveTargetHeight = (data.wizardType == WizardType.Chefe) ? (targetHeight * 1.35f) : targetHeight;
            float scaleFactor = effectiveTargetHeight / Mathf.Max(0.1f, spriteHeight);
            normalizedScale = new Vector3(scaleFactor, scaleFactor, 1f);
            transform.localScale = Vector3.zero;

            if (hitCollider is CircleCollider2D circleCol)
            {
                circleCol.radius = spriteHeight * 0.48f;
            }

            if (healthBarRoot != null)
            {
                float barYOffset = (data.wizardType == WizardType.Chefe) ? (spriteHeight * 0.70f) : (spriteHeight * 0.62f);
                healthBarRoot.transform.localPosition = new Vector3(0f, barYOffset, 0f);
            }

            SetSortingOrder(Mathf.RoundToInt((10f - transform.position.y) * 10f));
            UpdateHealthBar();
            StopAllActiveCoroutines();

            if (!activeGoblins.Contains(this)) activeGoblins.Add(this);
            isFrozen = false;

            StartCoroutine(SpawnScaleInRoutine());
            StartBehavior();

            float escapeTime = Mathf.Max(2.5f, data.escapeTimeSeconds / Mathf.Sqrt(currentSpeedMultiplier));
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
                float fps = (data.wizardType == WizardType.Dourado) ? 12f : 8f;
                frameAnimator.RegisterState(AnimationState.Walk, data.walkFrames, fps, true);
            }

            // Run / Dash
            if (data.runFrames != null && data.runFrames.Length > 0)
            {
                frameAnimator.RegisterState(AnimationState.Run, data.runFrames, 12f, true);
            }

            // Attack / Golpe Forte
            if (data.attackFrames != null && data.attackFrames.Length > 0)
            {
                float fps = (data.wizardType == WizardType.Chefe) ? 10f : 8f;
                frameAnimator.RegisterState(AnimationState.Attack, data.attackFrames, fps, false);
            }

            // Attack2 / Investida Brutal (Chefe)
            if (data.attack2Frames != null && data.attack2Frames.Length > 0)
            {
                frameAnimator.RegisterState(AnimationState.Attack2, data.attack2Frames, 12f, false);
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
                float fps = (data.wizardType == WizardType.Chefe) ? 7.5f : 8.5f;
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
                bool isBoss = currentData.wizardType == WizardType.Chefe;
                float barWidth = isBoss ? 1.35f : 0.86f;
                float barHeight = isBoss ? 0.16f : 0.11f;

                if (healthBarBg != null)
                {
                    healthBarBg.transform.localScale = new Vector3(barWidth + 0.08f, barHeight + 0.04f, 1f);
                    healthBarBg.color = isBoss ? new Color(0.18f, 0.02f, 0.02f, 0.92f) : new Color(0.1f, 0.1f, 0.1f, 0.85f);
                }

                float pct = Mathf.Clamp01((float)currentHealth / currentData.maxHealth);
                healthBarFill.transform.localScale = new Vector3(barWidth * pct, barHeight, 1f);
                healthBarFill.color = isBoss
                    ? Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(1f, 0.85f, 0.2f), pct)
                    : Color.Lerp(Color.red, Color.green, pct);
            }
        }

        public float GetCurrentHeight() => targetHeight;

        public void SetSortingOrder(int order)
        {
            if (spriteRenderer != null) spriteRenderer.sortingOrder = order;
            if (healthBarBg != null) healthBarBg.sortingOrder = order + 2;
            if (healthBarFill != null) healthBarFill.sortingOrder = order + 3;
        }

        private void StopMovementAndCombatCoroutines()
        {
            if (movementCoroutine != null) { StopCoroutine(movementCoroutine); movementCoroutine = null; }
            if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
            if (hitReactionCoroutine != null) { StopCoroutine(hitReactionCoroutine); hitReactionCoroutine = null; }
            if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
            if (freezeCoroutine != null) { StopCoroutine(freezeCoroutine); freezeCoroutine = null; }
        }

        private void StopAllActiveCoroutines()
        {
            StopMovementAndCombatCoroutines();
            if (escapeCoroutine != null) { StopCoroutine(escapeCoroutine); escapeCoroutine = null; }
            if (deathCoroutine != null) { StopCoroutine(deathCoroutine); deathCoroutine = null; }
            isFrozen = false;
        }

        private IEnumerator SpawnScaleInRoutine()
        {
            float elapsed = 0f;
            float duration = 0.2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float ease = Mathf.Sin(t * Mathf.PI * 0.5f);
                transform.localScale = Vector3.LerpUnclamped(Vector3.zero, normalizedScale, ease);
                yield return null;
            }
            transform.localScale = normalizedScale;
        }

        private void StartBehavior()
        {
            if (movementCoroutine != null) StopCoroutine(movementCoroutine);
            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
            if (attackRoutine != null) StopCoroutine(attackRoutine);

            switch (currentData.wizardType)
            {
                case WizardType.Comum:
                    movementCoroutine = StartCoroutine(CommonPatrolRoutine());
                    attackRoutine = StartCoroutine(PeriodicAttackRoutine(3f, 5f));
                    break;
                case WizardType.Fugitivo:
                    movementCoroutine = StartCoroutine(FugitiveRunRoutine());
                    break;
                case WizardType.Dourado:
                    pulseCoroutine = StartCoroutine(GoldenGlowRoutine());
                    movementCoroutine = StartCoroutine(GoldenAggressiveRoutine());
                    break;
                case WizardType.Fantasma:
                    pulseCoroutine = StartCoroutine(GhostSpectralAlphaRoutine());
                    movementCoroutine = StartCoroutine(GhostLevitateRoutine());
                    break;
                case WizardType.Chefe:
                    movementCoroutine = StartCoroutine(BossBehaviorRoutine());
                    break;
            }
        }

        public void TakeDamage(int damage)
        {
            if (isDeadOrEscaping) return;

            currentHealth -= damage;
            UpdateHealthBar();
            StartCoroutine(FlashDamageRoutine());

            if (currentHealth <= 0)
            {
                Die();
            }
            else
            {
                TriggerHitReaction();
            }
        }

        private IEnumerator FlashDamageRoutine()
        {
            Color original = currentData.baseTint;
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.08f);
            if (!isDeadOrEscaping)
            {
                spriteRenderer.color = original;
            }
        }

        private void TriggerHitReaction()
        {
            if (hitReactionCoroutine != null) StopCoroutine(hitReactionCoroutine);

            switch (currentData.wizardType)
            {
                case WizardType.Fugitivo:
                    hitReactionCoroutine = StartCoroutine(FugitiveJumpReactionRoutine());
                    break;
                case WizardType.Fantasma:
                    hitReactionCoroutine = StartCoroutine(GhostTeleportReactionRoutine());
                    break;
                case WizardType.Dourado:
                    hitReactionCoroutine = StartCoroutine(GoldenShieldReactionRoutine());
                    break;
                case WizardType.Chefe:
                    hitReactionCoroutine = StartCoroutine(BossHitReactionRoutine());
                    break;
                default:
                    hitReactionCoroutine = StartCoroutine(CommonJiggleReactionRoutine());
                    break;
            }
        }

        private IEnumerator EscapeTimerRoutine(float delaySeconds)
        {
            remainingEscapeTime = delaySeconds;
            while (remainingEscapeTime > 0f)
            {
                if (!isFrozen)
                {
                    remainingEscapeTime -= Time.deltaTime;
                }
                yield return null;
            }

            if (isDeadOrEscaping) yield break;

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
                yield return null;
            }

            escapeCoroutine = null;
            OnEscaped?.Invoke(this, currentData.escapePenalty);
            gameObject.SetActive(false);
        }

        private void Die()
        {
            isDeadOrEscaping = true;
            activeGoblins.Remove(this);
            hitCollider.enabled = false;
            if (healthBarRoot != null) healthBarRoot.SetActive(false);

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

            deathCoroutine = StartCoroutine(DeathAnimationRoutine());
        }

        public void Freeze(float duration = 2.5f)
        {
            if (isDeadOrEscaping) return;

            if (freezeCoroutine != null) StopCoroutine(freezeCoroutine);
            freezeCoroutine = StartCoroutine(FreezeRoutine(duration));
        }

        private IEnumerator FreezeRoutine(float duration)
        {
            isFrozen = true;

            // Interrompe rotinas ativas de movimento e ataque
            if (movementCoroutine != null) StopCoroutine(movementCoroutine);
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            if (hitReactionCoroutine != null) StopCoroutine(hitReactionCoroutine);

            // Tonalidade azul-gelo
            spriteRenderer.color = new Color(0.35f, 0.85f, 1f, 1f);

            yield return new WaitForSeconds(duration);

            if (isDeadOrEscaping) yield break;

            isFrozen = false;
            spriteRenderer.color = currentData.baseTint;
            StartBehavior();
            freezeCoroutine = null;
        }

        private IEnumerator DeathAnimationRoutine()
        {
            // Toca animacao de morte no FrameAnimator (6 quadros de colapso no chão)
            if (frameAnimator != null && currentData.deathFrames != null && currentData.deathFrames.Length > 0)
            {
                frameAnimator.LockAnimation(AnimationState.Death);
            }

            bool isBoss = currentData != null && currentData.wizardType == WizardType.Chefe;
            float duration = isBoss ? 1.15f : 0.70f;
            float elapsed = 0f;
            Color startCol = spriteRenderer.color;

            if (isBoss)
            {
                SpellEffectsManager.Instance?.TriggerCameraShake(0.25f, 0.22f);
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
                }
                yield return null;
            }

            deathCoroutine = null;
            OnDefeated?.Invoke(this, currentData.pointsOnDefeat);
            gameObject.SetActive(false);
        }

        #region Comportamentos Especificos dos Goblins

        // 1. GOBLIN COMUM: Movimento aleatorio e ataques periodicos
        private IEnumerator CommonPatrolRoutine()
        {
            while (true)
            {
                Vector3 target = GetRandomPointInBounds();
                Vector3 start = transform.position;
                float dist = Vector3.Distance(start, target);
                float duration = dist / Mathf.Max(0.5f, currentData.moveSpeed * currentSpeedMultiplier);
                float elapsed = 0f;

                frameAnimator.SetFacingDirection(target.x - start.x);
                frameAnimator.Play(AnimationState.Walk);

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                    SetSortingOrder(Mathf.RoundToInt((10f - transform.position.y) * 10f));
                    yield return null;
                }

                transform.position = target;
                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.4f, 1.0f) / currentSpeedMultiplier);
            }
        }

        private IEnumerator PeriodicAttackRoutine(float minInterval, float maxInterval)
        {
            while (true)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(minInterval, maxInterval));
                if (isDeadOrEscaping) yield break;

                // Executa um ataque rapido com faca
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

        // 2. GOBLIN FUGITIVO: Corrida rapida e pulo ao sofrer dano com disparada acelerada
        private IEnumerator FugitiveRunRoutine()
        {
            float baseSpeed = currentData.moveSpeed * currentSpeedMultiplier;
            float speed = isEnraged ? (baseSpeed * 1.8f) : baseSpeed;

            while (true)
            {
                Vector3 target = GetRandomPointInBounds();
                Vector3 start = transform.position;
                float dist = Vector3.Distance(start, target);
                float duration = dist / speed;
                float elapsed = 0f;

                frameAnimator.SetFacingDirection(target.x - start.x);
                frameAnimator.Play(isEnraged ? AnimationState.Run : AnimationState.Walk);

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                    SetSortingOrder(Mathf.RoundToInt((10f - transform.position.y) * 10f));
                    yield return null;
                }

                transform.position = target;
                yield return new WaitForSeconds(isEnraged ? (0.1f / currentSpeedMultiplier) : (0.3f / currentSpeedMultiplier));
            }
        }

        private IEnumerator FugitiveJumpReactionRoutine()
        {
            isEnraged = true;
            if (movementCoroutine != null) StopCoroutine(movementCoroutine);

            // Se tiver sprite de jump, aplica
            if (currentData.specialActionSprite != null)
            {
                spriteRenderer.sprite = currentData.specialActionSprite;
            }

            // Pulo de reacao no ar
            Vector3 startPos = transform.position;
            float jumpDuration = 0.28f;
            float elapsed = 0f;
            float jumpHeight = 0.8f;

            while (elapsed < jumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / jumpDuration;
                float yOffset = Mathf.Sin(t * Mathf.PI) * jumpHeight;
                transform.position = new Vector3(startPos.x, startPos.y + yOffset, startPos.z);
                yield return null;
            }
            transform.position = startPos;

            // Retoma corrida em disparada total
            movementCoroutine = StartCoroutine(FugitiveRunRoutine());
        }

        // 3. GOBLIN DOURADO: Brilho pulsante, investidas com escudo e dash agressivo
        private IEnumerator GoldenGlowRoutine()
        {
            while (true)
            {
                float factor = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                spriteRenderer.color = Color.Lerp(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.75f, 0.1f), factor);
                yield return null;
            }
        }

        private IEnumerator GoldenAggressiveRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.4f, 0.8f) / currentSpeedMultiplier);
                Vector3 target = GetRandomPointInBounds();
                Vector3 start = transform.position;
                float dist = Vector3.Distance(start, target);
                float duration = dist / (currentData.moveSpeed * currentSpeedMultiplier * 1.5f);
                float elapsed = 0f;

                frameAnimator.SetFacingDirection(target.x - start.x);
                frameAnimator.Play(AnimationState.Run);

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                    SetSortingOrder(Mathf.RoundToInt((10f - transform.position.y) * 10f));
                    yield return null;
                }

                transform.position = target;
                frameAnimator.Play(AnimationState.Idle);
            }
        }

        private IEnumerator GoldenShieldReactionRoutine()
        {
            if (currentData.specialActionSprite != null)
            {
                spriteRenderer.sprite = currentData.specialActionSprite; // Postura com escudo
            }

            // Recuo defensivo
            Vector3 start = transform.position;
            Vector3 recoilDir = (UnityEngine.Random.insideUnitCircle.normalized);
            Vector3 recoilTarget = start + (recoilDir * 0.5f);

            float elapsed = 0f;
            float dur = 0.15f / currentSpeedMultiplier;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, recoilTarget, elapsed / dur);
                SetSortingOrder(Mathf.RoundToInt((10f - transform.position.y) * 10f));
                yield return null;
            }

            yield return new WaitForSeconds(0.1f / currentSpeedMultiplier);
            movementCoroutine = StartCoroutine(GoldenAggressiveRoutine());
        }

        // 4. GOBLIN FANTASMA: Levitacao espectral e teleporte dimensional atraves de portais
        private IEnumerator GhostSpectralAlphaRoutine()
        {
            while (true)
            {
                float alpha = Mathf.PingPong(Time.time * 1.2f, 0.45f) + 0.55f;
                Color c = spriteRenderer.color;
                spriteRenderer.color = new Color(c.r, c.g, c.b, alpha);
                yield return null;
            }
        }

        private IEnumerator GhostLevitateRoutine()
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
                    SetSortingOrder(Mathf.RoundToInt((10f - transform.position.y) * 10f));
                    yield return null;
                }

                transform.position = targetPos;
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.3f, 0.8f) / currentSpeedMultiplier);
            }
        }

        private IEnumerator GhostTeleportReactionRoutine()
        {
            if (movementCoroutine != null) StopCoroutine(movementCoroutine);

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
            sr.sortingOrder = 8;
            sr.color = new Color(0.3f, 0.8f, 1f, 0.9f);

            StartCoroutine(PortalFadeRoutine(portalGo));
        }

        private IEnumerator PortalFadeRoutine(GameObject portalGo)
        {
            float dur = 0.35f;
            float elapsed = 0f;
            SpriteRenderer sr = portalGo.GetComponent<SpriteRenderer>();

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                portalGo.transform.Rotate(0f, 0f, 180f * Time.deltaTime);
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Lerp(0.9f, 0f, t));
                yield return null;
            }

            Destroy(portalGo);
        }

        // 5. GOBLIN CHEFE (BOSS): Passos pesados, Golpe Forte sísmico (Ataque 1) e Investidas Brutais (Ataque 2)
        private IEnumerator BossBehaviorRoutine()
        {
            float speed = currentData.moveSpeed * currentSpeedMultiplier;

            while (true)
            {
                // Fase 1: Marcha Pesada até uma posição do cenário
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
                    SetSortingOrder(Mathf.RoundToInt((10f - transform.position.y) * 10f));
                    yield return null;
                }

                transform.position = target;
                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.35f, 0.65f) / currentSpeedMultiplier);

                if (isDeadOrEscaping) yield break;

                // Alterna entre Golpe Forte (Attack 1) e Investida Brutal (Attack 2)
                bool doInvestida = (currentData.attack2Frames != null && currentData.attack2Frames.Length > 0) && (UnityEngine.Random.value > 0.45f);

                if (doInvestida)
                {
                    // Ataque 2: Investida Brutal com Dash Acelerado
                    Vector3 chargeTarget = GetRandomPointInBounds();
                    frameAnimator.SetFacingDirection(chargeTarget.x - transform.position.x);
                    frameAnimator.PlayOneShot(AnimationState.Attack2, AnimationState.Idle);

                    Vector3 chargeStart = transform.position;
                    float chargeDist = Vector3.Distance(chargeStart, chargeTarget);
                    float chargeDuration = chargeDist / Mathf.Max(1.0f, speed * 2.2f);
                    float chargeElapsed = 0f;

                    while (chargeElapsed < chargeDuration)
                    {
                        chargeElapsed += Time.deltaTime;
                        float t = chargeElapsed / chargeDuration;
                        transform.position = Vector3.Lerp(chargeStart, chargeTarget, t);
                        SetSortingOrder(Mathf.RoundToInt((10f - transform.position.y) * 10f));
                        yield return null;
                    }

                    transform.position = chargeTarget;
                    SpellEffectsManager.Instance?.TriggerCameraShake(0.12f, 0.12f);
                }
                else if (currentData.attackFrames != null && currentData.attackFrames.Length > 0)
                {
                    // Ataque 1: Golpe Forte com Clava
                    frameAnimator.PlayOneShot(AnimationState.Attack, AnimationState.Idle);
                    // No meio do golpe (~0.35s do ataque a 10fps), gera tremor de terra
                    yield return new WaitForSeconds(0.35f);
                    SpellEffectsManager.Instance?.TriggerCameraShake(0.16f, 0.18f);
                    yield return new WaitForSeconds(0.35f);
                }

                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.4f, 0.8f) / currentSpeedMultiplier);
            }
        }

        private IEnumerator BossHitReactionRoutine()
        {
            if (currentData.hitFrames != null && currentData.hitFrames.Length > 0)
            {
                frameAnimator.PlayOneShot(AnimationState.Hit, AnimationState.Walk);
            }

            Vector3 start = transform.position;
            // Estremece de dor momentaneamente
            for (int i = 0; i < 3; i++)
            {
                transform.position = start + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.08f);
                yield return new WaitForSeconds(0.025f);
            }
            transform.position = start;
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