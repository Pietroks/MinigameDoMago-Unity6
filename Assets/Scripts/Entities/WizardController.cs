using System;
using System.Collections;
using UnityEngine;
using WizardGame.Data;

namespace WizardGame.Entities
{
    /// <summary>
    /// Controlador do ciclo de vida, movimentação e dano de cada mago.
    /// Garante tamanho normalizado proporcional à câmera e hitbox precisa.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
    public class WizardController : MonoBehaviour
    {
        [Header("Componentes")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Collider2D hitCollider;

        [Header("Tamanho Normalizado")]
        [Tooltip("Altura alvo do mago em unidades de mundo da câmera (ortográfica = 5, altura tela = 10).")]
        [SerializeField] private float targetHeight = 1.5f;

        private WizardDataSO currentData;
        private int currentHealth;
        private bool isDeadOrEscaping;
        private Bounds screenWorldBounds;
        private Vector3 normalizedScale = Vector3.one;

        private Coroutine movementCoroutine;
        private Coroutine pulseCoroutine;
        private Coroutine escapeCoroutine;
        private Coroutine hitReactionCoroutine;

        public event Action<WizardController, int> OnDefeated;
        public event Action<WizardController, int> OnEscaped;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (hitCollider == null) hitCollider = GetComponent<Collider2D>();
        }

        public void Initialize(WizardDataSO data, Vector3 spawnPosition, Bounds bounds)
        {
            currentData = data;
            currentHealth = data.maxHealth;
            screenWorldBounds = bounds;
            isDeadOrEscaping = false;

            transform.position = spawnPosition;
            transform.rotation = Quaternion.identity;

            spriteRenderer.sprite = data.sprite;
            spriteRenderer.color = data.baseTint;
            hitCollider.enabled = true;

            // Normalizar escala proporcionalmente ao tamanho original do sprite
            float spriteHeight = 1f;
            if (data.sprite != null && data.sprite.bounds.size.y > 0.05f)
            {
                spriteHeight = data.sprite.bounds.size.y;
            }
            float scaleFactor = targetHeight / spriteHeight;
            normalizedScale = new Vector3(scaleFactor, scaleFactor, 1f);
            transform.localScale = Vector3.zero;

            // Ajustar collider dinamicamente aos limites do sprite
            if (hitCollider is CircleCollider2D circleCol)
            {
                circleCol.radius = spriteHeight * 0.45f;
            }

            StopAllActiveCoroutines();

            StartCoroutine(SpawnScaleInRoutine());
            StartBehavior();

            escapeCoroutine = StartCoroutine(EscapeTimerRoutine(data.escapeTimeSeconds));
        }

        private void StopAllActiveCoroutines()
        {
            if (movementCoroutine != null) StopCoroutine(movementCoroutine);
            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
            if (escapeCoroutine != null) StopCoroutine(escapeCoroutine);
            if (hitReactionCoroutine != null) StopCoroutine(hitReactionCoroutine);
        }

        private IEnumerator SpawnScaleInRoutine()
        {
            float elapsed = 0f;
            float duration = 0.22f;

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

            switch (currentData.wizardType)
            {
                case WizardType.Comum:
                    movementCoroutine = StartCoroutine(GentleFloatRoutine());
                    break;
                case WizardType.Rapido:
                    movementCoroutine = StartCoroutine(FastPendulumRoutine());
                    break;
                case WizardType.Dourado:
                    pulseCoroutine = StartCoroutine(GoldenPulseRoutine());
                    movementCoroutine = StartCoroutine(GoldenDashRoutine());
                    break;
                case WizardType.Fantasma:
                    pulseCoroutine = StartCoroutine(GhostAlphaRoutine());
                    movementCoroutine = StartCoroutine(GhostVerticalRoutine());
                    break;
            }
        }

        public void TakeDamage(int damage)
        {
            if (isDeadOrEscaping) return;

            currentHealth -= damage;
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
            yield return new WaitForSeconds(0.1f);
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
                case WizardType.Rapido:
                    hitReactionCoroutine = StartCoroutine(FastHitReactionRoutine());
                    break;
                case WizardType.Fantasma:
                    hitReactionCoroutine = StartCoroutine(GhostTeleportRoutine());
                    break;
                default:
                    hitReactionCoroutine = StartCoroutine(JiggleRoutine());
                    break;
            }
        }

        private IEnumerator EscapeTimerRoutine(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            if (isDeadOrEscaping) yield break;

            isDeadOrEscaping = true;
            hitCollider.enabled = false;
            StopAllActiveCoroutines();

            float elapsed = 0f;
            float duration = 0.4f;
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
            StopAllActiveCoroutines();

            StartCoroutine(DeathAnimationRoutine());
        }

        private IEnumerator DeathAnimationRoutine()
        {
            float elapsed = 0f;
            float duration = 0.35f;
            Vector3 startScale = transform.localScale;

            if (currentData.wizardType == WizardType.Fantasma)
            {
                Vector3 startPos = transform.position;
                Vector3 endPos = startPos + new Vector3(0f, 1.5f, 0f);
                Color startCol = spriteRenderer.color;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    transform.position = Vector3.Lerp(startPos, endPos, t);
                    spriteRenderer.color = new Color(startCol.r, startCol.g, startCol.b, Mathf.Lerp(startCol.a, 0f, t));
                    yield return null;
                }
            }
            else
            {
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                    transform.Rotate(0f, 0f, 360f * (Time.deltaTime / duration));
                    yield return null;
                }
            }

            OnDefeated?.Invoke(this, currentData.pointsOnDefeat);
        }

        #region Comportamentos de Movimentação

        private IEnumerator GentleFloatRoutine()
        {
            Vector3 origin = transform.position;
            float seed = UnityEngine.Random.Range(0f, 100f);
            while (true)
            {
                float t = (Time.time + seed) * 1.5f;
                float ox = Mathf.Sin(t) * 0.4f;
                float oy = Mathf.Cos(t * 0.8f) * 0.3f;
                transform.position = origin + new Vector3(ox, oy, 0f);
                yield return null;
            }
        }

        private IEnumerator FastPendulumRoutine()
        {
            Vector3 origin = transform.position;
            float seed = UnityEngine.Random.Range(0f, 100f);
            while (true)
            {
                float t = (Time.time + seed) * 4f;
                float ox = Mathf.Sin(t) * 0.7f;
                float oy = Mathf.Sin(t * 2f) * 0.2f;
                transform.position = origin + new Vector3(ox, oy, 0f);
                yield return null;
            }
        }

        private IEnumerator FastHitReactionRoutine()
        {
            hitCollider.enabled = false;
            Vector3 origScale = normalizedScale;

            transform.localScale = new Vector3(origScale.x * 1.3f, origScale.y * 0.7f, origScale.z);
            yield return new WaitForSeconds(0.08f);
            transform.localScale = origScale;

            Vector3 target = GetRandomPointInBounds();
            Vector3 start = transform.position;
            float elapsed = 0f;
            float dashDuration = 0.16f;

            while (elapsed < dashDuration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, target, elapsed / dashDuration);
                yield return null;
            }
            transform.position = target;
            hitCollider.enabled = true;
            movementCoroutine = StartCoroutine(FastPendulumRoutine());
        }

        private IEnumerator GoldenPulseRoutine()
        {
            while (true)
            {
                float factor = (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f;
                spriteRenderer.color = Color.Lerp(new Color(1f, 0.95f, 0.4f), new Color(1f, 0.75f, 0f), factor);
                yield return null;
            }
        }

        private IEnumerator GoldenDashRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.5f, 1.2f));
                Vector3 target = GetRandomPointInBounds();
                Vector3 start = transform.position;
                float duration = UnityEngine.Random.Range(0.3f, 0.5f);
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float smooth = Mathf.SmoothStep(0f, 1f, t);
                    transform.position = Vector3.Lerp(start, target, smooth);
                    yield return null;
                }
                transform.position = target;
            }
        }

        private IEnumerator GhostAlphaRoutine()
        {
            while (true)
            {
                float alpha = Mathf.PingPong(Time.time * 0.8f, 0.5f) + 0.5f;
                Color c = spriteRenderer.color;
                spriteRenderer.color = new Color(c.r, c.g, c.b, alpha);
                yield return null;
            }
        }

        private IEnumerator GhostVerticalRoutine()
        {
            Vector3 origin = transform.position;
            while (true)
            {
                float oy = Mathf.Sin(Time.time * 2f) * 0.35f;
                transform.position = new Vector3(origin.x, origin.y + oy, origin.z);
                yield return null;
            }
        }

        private IEnumerator GhostTeleportRoutine()
        {
            hitCollider.enabled = false;
            StopBehaviorCoroutines();

            float elapsed = 0f;
            float dur = 0.12f;
            Vector3 startScale = transform.localScale;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            transform.position = GetRandomPointInBounds();

            elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                transform.localScale = Vector3.Lerp(Vector3.zero, normalizedScale, t);
                yield return null;
            }
            transform.localScale = normalizedScale;
            hitCollider.enabled = true;

            StartBehavior();
        }

        private void StopBehaviorCoroutines()
        {
            if (movementCoroutine != null) StopCoroutine(movementCoroutine);
            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        }

        private IEnumerator JiggleRoutine()
        {
            Vector3 start = transform.position;
            for (int i = 0; i < 4; i++)
            {
                transform.position = start + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.1f);
                yield return new WaitForSeconds(0.02f);
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