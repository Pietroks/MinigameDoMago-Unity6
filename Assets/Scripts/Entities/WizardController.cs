using System;
using System.Collections;
using UnityEngine;
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
        [SerializeField] private float targetHeight = 1.4f;

        private WizardDataSO currentData;
        private int currentHealth;
        private bool isDeadOrEscaping;
        private bool isEnraged; // Para o Goblin Fugitivo ao tomar dano
        private Bounds screenWorldBounds;
        private Vector3 normalizedScale = Vector3.one;

        private Coroutine movementCoroutine;
        private Coroutine pulseCoroutine;
        private Coroutine escapeCoroutine;
        private Coroutine hitReactionCoroutine;
        private Coroutine attackRoutine;

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

        public void Initialize(WizardDataSO data, Vector3 spawnPosition, Bounds bounds)
        {
            currentData = data;
            currentHealth = data.maxHealth;
            screenWorldBounds = bounds;
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
            float scaleFactor = targetHeight / Mathf.Max(0.1f, spriteHeight);
            normalizedScale = new Vector3(scaleFactor, scaleFactor, 1f);
            transform.localScale = Vector3.zero;

            if (hitCollider is CircleCollider2D circleCol)
            {
                circleCol.radius = spriteHeight * 0.45f;
            }

            UpdateHealthBar();
            StopAllActiveCoroutines();

            StartCoroutine(SpawnScaleInRoutine());
            StartBehavior();

            escapeCoroutine = StartCoroutine(EscapeTimerRoutine(data.escapeTimeSeconds));
        }

        private void SetupAnimations(WizardDataSO data)
        {
            frameAnimator.UnlockAnimation();

            // Idle
            if (data.idleFrames != null && data.idleFrames.Length > 0)
            {
                frameAnimator.RegisterState(AnimationState.Idle, data.idleFrames, 6f, true);
            }

            // Walk / Levitation
            if (data.walkFrames != null && data.walkFrames.Length > 0)
            {
                float fps = (data.wizardType == WizardType.Dourado) ? 10f : 7f;
                frameAnimator.RegisterState(AnimationState.Walk, data.walkFrames, fps, true);
            }

            // Run / Dash
            if (data.runFrames != null && data.runFrames.Length > 0)
            {
                frameAnimator.RegisterState(AnimationState.Run, data.runFrames, 12f, true);
            }

            // Attack
            if (data.attackFrames != null && data.attackFrames.Length > 0)
            {
                frameAnimator.RegisterState(AnimationState.Attack, data.attackFrames, 8f, false);
            }

            // Death
            if (data.deathFrames != null && data.deathFrames.Length > 0)
            {
                frameAnimator.RegisterState(AnimationState.Death, data.deathFrames, 8f, false);
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
                float pct = Mathf.Clamp01((float)currentHealth / currentData.maxHealth);
                healthBarFill.transform.localScale = new Vector3(0.86f * pct, 0.11f, 1f);
                healthBarFill.color = Color.Lerp(Color.red, Color.green, pct);
            }
        }

        private void StopAllActiveCoroutines()
        {
            if (movementCoroutine != null) StopCoroutine(movementCoroutine);
            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
            if (escapeCoroutine != null) StopCoroutine(escapeCoroutine);
            if (hitReactionCoroutine != null) StopCoroutine(hitReactionCoroutine);
            if (attackRoutine != null) StopCoroutine(attackRoutine);
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
                default:
                    hitReactionCoroutine = StartCoroutine(CommonJiggleReactionRoutine());
                    break;
            }
        }

        private IEnumerator EscapeTimerRoutine(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            if (isDeadOrEscaping) yield break;

            isDeadOrEscaping = true;
            hitCollider.enabled = false;
            if (healthBarRoot != null) healthBarRoot.SetActive(false);
            StopAllActiveCoroutines();

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

            OnEscaped?.Invoke(this, currentData.escapePenalty);
        }

        private void Die()
        {
            isDeadOrEscaping = true;
            hitCollider.enabled = false;
            if (healthBarRoot != null) healthBarRoot.SetActive(false);
            StopAllActiveCoroutines();

            StartCoroutine(DeathAnimationRoutine());
        }

        private IEnumerator DeathAnimationRoutine()
        {
            // Toca animacao de morte no FrameAnimator
            if (frameAnimator != null && currentData.deathFrames != null && currentData.deathFrames.Length > 0)
            {
                frameAnimator.LockAnimation(AnimationState.Death);
            }

            float duration = 0.45f;
            float elapsed = 0f;
            Color startCol = spriteRenderer.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Fade out suave nos ultimos 40% da animacao
                if (t > 0.6f)
                {
                    float fadeT = (t - 0.6f) / 0.4f;
                    spriteRenderer.color = new Color(startCol.r, startCol.g, startCol.b, Mathf.Lerp(startCol.a, 0f, fadeT));
                }
                yield return null;
            }

            OnDefeated?.Invoke(this, currentData.pointsOnDefeat);
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
                float duration = dist / Mathf.Max(0.5f, currentData.moveSpeed);
                float elapsed = 0f;

                frameAnimator.SetFacingDirection(target.x - start.x);
                frameAnimator.Play(AnimationState.Walk);

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }

                transform.position = target;
                frameAnimator.Play(AnimationState.Idle);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.4f, 1.0f));
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
            float speed = isEnraged ? (currentData.moveSpeed * 1.8f) : currentData.moveSpeed;

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
                    transform.position = Vector3.Lerp(start, target, elapsed / duration);
                    yield return null;
                }

                transform.position = target;
                yield return new WaitForSeconds(isEnraged ? 0.1f : 0.3f);
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
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.4f, 0.8f));
                Vector3 target = GetRandomPointInBounds();
                Vector3 start = transform.position;
                float dist = Vector3.Distance(start, target);
                float duration = dist / (currentData.moveSpeed * 1.5f);
                float elapsed = 0f;

                frameAnimator.SetFacingDirection(target.x - start.x);
                frameAnimator.Play(AnimationState.Run);

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
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
            float dur = 0.15f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, recoilTarget, elapsed / dur);
                yield return null;
            }

            yield return new WaitForSeconds(0.1f);
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
            Vector3 origin = transform.position;
            while (true)
            {
                float oy = Mathf.Sin(Time.time * 2.5f) * 0.35f;
                float ox = Mathf.Cos(Time.time * 1.5f) * 0.25f;
                transform.position = new Vector3(origin.x + ox, origin.y + oy, origin.z);
                yield return null;
            }
        }

        private IEnumerator GhostTeleportReactionRoutine()
        {
            if (movementCoroutine != null) StopCoroutine(movementCoroutine);

            Vector3 oldPos = transform.position;
            Vector3 newPos = GetRandomPointInBounds();

            // Spawn portal no ponto de saida
            SpawnPortalEffect(oldPos);

            // Desaparece
            float elapsed = 0f;
            float dur = 0.12f;
            Vector3 startScale = transform.localScale;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / dur);
                yield return null;
            }

            // Spawn portal no ponto de entrada e move
            SpawnPortalEffect(newPos);
            transform.position = newPos;

            // Reaparece no destino
            elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(Vector3.zero, normalizedScale, elapsed / dur);
                yield return null;
            }
            transform.localScale = normalizedScale;

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