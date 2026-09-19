using System;
using System.Collections;
using UnityEngine;
using WizardGame.Data;

namespace WizardGame.Entities
{
    /// <summary>
    /// Item mágico flutuante que paira pela arena de batalha.
    /// Pode ser coletado pelo jogador através de um clique direto ou disparo com a varinha.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class PowerUpItem : MonoBehaviour
    {
        [Header("Configurações do Power-up")]
        [SerializeField] private PowerUpType powerUpType = PowerUpType.Mana;
        [SerializeField] private float lifetime = 9.0f;
        [SerializeField] private float warningDuration = 2.5f;

        [Header("Flutuação Orgânica (Bobbing & Drift)")]
        [SerializeField] private float bobFrequency = 2.4f;
        [SerializeField] private float bobAmplitude = 0.35f;
        [SerializeField] private float driftSpeedX = 0.45f;

        public PowerUpType Type => powerUpType;
        public bool IsCollected { get; private set; }

        public event Action<PowerUpItem, PowerUpType> OnCollected;
        public event Action<PowerUpItem> OnExpired;

        private CircleCollider2D col;
        private SpriteRenderer orbRenderer;
        private SpriteRenderer innerGlowRenderer;
        private TextMesh labelTextMesh;
        private Vector3 basePosition;
        private Bounds bounds;
        private float lifeTimer;
        private float randomSeed;
        private int driftDirection = 1;

        public void Initialize(PowerUpType type, Vector3 spawnPos, Bounds arenaBounds)
        {
            powerUpType = type;
            basePosition = spawnPos;
            transform.position = spawnPos;
            bounds = arenaBounds;
            randomSeed = UnityEngine.Random.Range(0f, 100f);
            driftDirection = UnityEngine.Random.value > 0.5f ? 1 : -1;
            lifeTimer = 0f;
            IsCollected = false;

            SetupVisuals();
        }

        private void Awake()
        {
            col = GetComponent<CircleCollider2D>();
            if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.75f;
            col.isTrigger = true;
        }

        private void SetupVisuals()
        {
            Color primaryColor = GetColorForType(powerUpType);

            // 1. Orbe Externo Translúcido
            if (orbRenderer == null)
            {
                orbRenderer = GetComponent<SpriteRenderer>();
                if (orbRenderer == null) orbRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            orbRenderer.sprite = GetSharedCircleSprite();
            orbRenderer.color = new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.78f);
            orbRenderer.sortingOrder = 25; // Renderiza acima dos goblins normais

            // 2. Halo Interno Brilhante
            Transform halo = transform.Find("InnerGlow");
            if (halo == null)
            {
                var haloGo = new GameObject("InnerGlow");
                haloGo.transform.SetParent(transform, false);
                haloGo.transform.localScale = Vector3.one * 0.65f;
                innerGlowRenderer = haloGo.AddComponent<SpriteRenderer>();
            }
            else
            {
                innerGlowRenderer = halo.GetComponent<SpriteRenderer>();
            }

            innerGlowRenderer.sprite = GetSharedCircleSprite();
            innerGlowRenderer.color = Color.white;
            innerGlowRenderer.sortingOrder = 26;

            // 3. Rótulo de Ícone e Título
            Transform label = transform.Find("Label");
            if (label == null)
            {
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(transform, false);
                labelGo.transform.localPosition = new Vector3(0f, -0.05f, 0f);
                labelTextMesh = labelGo.AddComponent<TextMesh>();
                labelTextMesh.fontSize = 28;
                labelTextMesh.characterSize = 0.08f;
                labelTextMesh.alignment = TextAlignment.Center;
                labelTextMesh.anchor = TextAnchor.MiddleCenter;
                labelTextMesh.fontStyle = FontStyle.Bold;

                var meshRenderer = labelGo.GetComponent<MeshRenderer>();
                if (meshRenderer != null) meshRenderer.sortingOrder = 27;
            }
            else
            {
                labelTextMesh = label.GetComponent<TextMesh>();
            }

            labelTextMesh.text = GetIconTextForType(powerUpType);
            labelTextMesh.color = Color.white;
        }

        private void Update()
        {
            if (IsCollected) return;

            lifeTimer += Time.deltaTime;

            // Movimento de deriva suave e flutuação
            float elapsed = lifeTimer + randomSeed;
            basePosition.x += driftDirection * driftSpeedX * Time.deltaTime;

            // Rebate nas bordas da arena se atingir os limites
            if (basePosition.x > bounds.max.x - 1.2f)
            {
                basePosition.x = bounds.max.x - 1.2f;
                driftDirection = -1;
            }
            else if (basePosition.x < bounds.min.x + 1.2f)
            {
                basePosition.x = bounds.min.x + 1.2f;
                driftDirection = 1;
            }

            float bobY = Mathf.Sin(elapsed * bobFrequency) * bobAmplitude;
            float pulseScale = 1.0f + Mathf.Sin(elapsed * (bobFrequency * 1.5f)) * 0.08f;

            transform.position = new Vector3(basePosition.x, basePosition.y + bobY, 0f);
            transform.localScale = Vector3.one * pulseScale;

            // Pulso no Halo Interno
            if (innerGlowRenderer != null)
            {
                float innerAlpha = 0.55f + Mathf.Cos(elapsed * 4.5f) * 0.35f;
                innerGlowRenderer.color = new Color(1f, 1f, 1f, innerAlpha);
            }

            // Aviso de dissipação nos segundos finais
            if (lifeTimer >= (lifetime - warningDuration))
            {
                float remaining = lifetime - lifeTimer;
                float flicker = Mathf.PingPong(remaining * 8f, 1.0f);
                if (orbRenderer != null)
                {
                    Color baseCol = GetColorForType(powerUpType);
                    orbRenderer.color = new Color(baseCol.r, baseCol.g, baseCol.b, Mathf.Lerp(0.2f, 0.9f, flicker));
                }
            }

            if (lifeTimer >= lifetime)
            {
                Expire();
            }
        }

        public void Collect()
        {
            if (IsCollected) return;
            IsCollected = true;

            if (col != null) col.enabled = false;

            OnCollected?.Invoke(this, powerUpType);
            StartCoroutine(CollectEffectRoutine());
        }

        private IEnumerator CollectEffectRoutine()
        {
            // Efeito visual de expansão rápida e fade out
            float elapsed = 0f;
            float duration = 0.22f;
            Vector3 startScale = transform.localScale;
            Color startCol = orbRenderer != null ? orbRenderer.color : Color.white;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(startScale, startScale * 1.8f, t);

                if (orbRenderer != null)
                {
                    orbRenderer.color = new Color(startCol.r, startCol.g, startCol.b, Mathf.Lerp(startCol.a, 0f, t));
                }
                if (innerGlowRenderer != null)
                {
                    innerGlowRenderer.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, t));
                }
                if (labelTextMesh != null)
                {
                    labelTextMesh.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, t));
                }
                yield return null;
            }

            Destroy(gameObject);
        }

        private void Expire()
        {
            IsCollected = true;
            OnExpired?.Invoke(this);
            Destroy(gameObject);
        }

        public static Color GetColorForType(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Mana:
                    return new Color(0.15f, 0.85f, 1.0f); // Azul Ciano Radiante
                case PowerUpType.ArcaneFury:
                    return new Color(1.0f, 0.32f, 0.12f); // Laranja Fogo Carmesim
                case PowerUpType.ArcaneTime:
                    return new Color(1.0f, 0.88f, 0.22f); // Dourado Místico
                case PowerUpType.Storm:
                    return new Color(0.68f, 0.35f, 1.0f); // Roxo Elétrico Arcano
                case PowerUpType.Precision:
                    return new Color(0.25f, 1.0f, 0.45f); // Verde Esmeralda Sniper
                default:
                    return Color.white;
            }
        }

        public static string GetIconTextForType(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Mana:
                    return "🔵\nMANA";
                case PowerUpType.ArcaneFury:
                    return "🔥\nFÚRIA";
                case PowerUpType.ArcaneTime:
                    return "⏱️\nTEMPO";
                case PowerUpType.Storm:
                    return "💥\nRAIO";
                case PowerUpType.Precision:
                    return "🎯\nMIRA";
                default:
                    return "✨";
            }
        }

        public static string GetDisplayName(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Mana:
                    return "🔵 MANA RECARREGADA (+4 TIROS)";
                case PowerUpType.ArcaneFury:
                    return "🔥 FÚRIA ARCANA (+1 DANO POR 5s)";
                case PowerUpType.ArcaneTime:
                    return "⏱️ TEMPO ARCANO (50% MAIS LENTO)";
                case PowerUpType.Storm:
                    return "💥 TEMPESTADE PRONTA (PRÓXIMO TIRO)";
                case PowerUpType.Precision:
                    return "🎯 MIRA DE PRECISÃO (5s)";
                default:
                    return "✨ POWER-UP ATIVADO!";
            }
        }

        private static Sprite sharedCircleSprite;

        private static Sprite GetSharedCircleSprite()
        {
            if (sharedCircleSprite != null) return sharedCircleSprite;

            int res = 128;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = res * 0.5f;
            float radius = center - 2f;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist <= radius)
                    {
                        // Centro brilhante com borda suave
                        float norm = dist / radius;
                        float alpha = Mathf.SmoothStep(1f, 0f, norm * norm);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            sharedCircleSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
            return sharedCircleSprite;
        }
    }
}
