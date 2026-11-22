using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WizardGame.Data;

namespace WizardGame.Core
{
    /// <summary>
    /// Gerencia o cenário dinâmico por fase:
    /// - Troca o background correspondente a cada chefe mantendo Aspect Fill perfeito sem barras pretas.
    /// - Aplica color grading, zoom atmosférico sutil e partículas ambientais temáticas por bioma
    ///   (poeira lunar, vaga-lumes místicos, brasas vulcânicas, faíscas arcanas).
    /// </summary>
    public class StageEnvironmentManager : MonoBehaviour
    {
        public static StageEnvironmentManager Instance { get; private set; }

        [Header("Referências do Cenário")]
        [SerializeField] private SpriteRenderer primaryBackground;
        [SerializeField] private BackgroundScaler primaryScaler;
        [SerializeField] private Camera targetCamera;

        [Header("Backgrounds por Fase (Fallback por Índice 0..4)")]
        [SerializeField] private List<Sprite> stageBackgrounds = new List<Sprite>();

        [Header("Partículas Ambientais do Bioma")]
        [SerializeField] private int maxAmbientParticles = 18;
        [SerializeField] private float particleSpawnInterval = 0.25f;

        private readonly List<AmbientMote> activeMotes = new List<AmbientMote>(24);
        private Color currentParticleColor = new Color(0.6f, 0.85f, 1f, 0.55f);
        private float currentParticleRiseSpeed = 0.45f;
        private float particleSpawnTimer;
        private Sprite cachedMoteSprite;
        private Material cachedSpriteMaterial;
        private float currentZoom = 1.0f;
        private Color currentTint = Color.white;
        private Coroutine crossfadeCoroutine;

        private struct AmbientMote
        {
            public GameObject go;
            public SpriteRenderer sr;
            public Vector3 velocity;
            public float age;
            public float lifetime;
            public float baseAlpha;
            public float swayPhase;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            if (targetCamera == null) targetCamera = Camera.main;

            if (primaryBackground == null)
            {
                var scaler = FindAnyObjectByType<BackgroundScaler>();
                if (scaler != null)
                {
                    primaryScaler = scaler;
                    primaryBackground = scaler.GetComponent<SpriteRenderer>();
                }
            }
            else if (primaryScaler == null)
            {
                primaryScaler = primaryBackground.GetComponent<BackgroundScaler>();
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            cachedSpriteMaterial = new Material(shader);
            cachedMoteSprite = CreateSoftMoteSprite(32);
        }

        public void SetStageBackgroundList(List<Sprite> sprites)
        {
            stageBackgrounds = sprites ?? new List<Sprite>();
        }

        /// <summary>
        /// Aplica imediatamente o cenário, cor, zoom e bioma correspondentes à fase informada.
        /// </summary>
        public void ApplyStage(WaveConfig config, int stageIndexZeroBased)
        {
            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
                crossfadeCoroutine = null;
            }

            Sprite targetSprite = config != null ? config.backgroundSprite : null;
            if (targetSprite == null && stageBackgrounds != null && stageBackgrounds.Count > 0)
            {
                int idx = Mathf.Clamp(stageIndexZeroBased, 0, stageBackgrounds.Count - 1);
                targetSprite = stageBackgrounds[idx];
            }

            currentTint = config != null ? config.backgroundTint : Color.white;
            currentZoom = config != null ? Mathf.Clamp(config.backgroundZoom, 1.0f, 1.35f) : 1.0f;
            currentParticleColor = config != null ? config.ambientParticleColor : new Color(0.6f, 0.85f, 1f, 0.55f);

            // Fases vulcânicas / finais têm brasas subindo mais rápido
            currentParticleRiseSpeed = (stageIndexZeroBased >= 2) ? 0.95f : 0.38f;

            if (primaryBackground != null)
            {
                if (targetSprite != null)
                {
                    primaryBackground.sprite = targetSprite;
                }
                primaryBackground.color = currentTint;
                if (primaryScaler != null)
                {
                    primaryScaler.SetExtraZoom(currentZoom);
                }
                else
                {
                    FitBackgroundAspectFill(primaryBackground, currentZoom);
                }
            }

            RefreshAmbientMoteColors();
        }

        /// <summary>
        /// Transição suave com crossfade direto entre dois cenários sem depender da cortina UI.
        /// </summary>
        public void CrossfadeToStage(WaveConfig config, int stageIndexZeroBased, float duration = 0.8f)
        {
            if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(config, stageIndexZeroBased, duration));
        }

        private IEnumerator CrossfadeRoutine(WaveConfig config, int stageIndexZeroBased, float duration)
        {
            if (primaryBackground == null) yield break;

            GameObject tempOverlay = new GameObject("BgCrossfadeTemp");
            var tempSr = tempOverlay.AddComponent<SpriteRenderer>();
            tempSr.sprite = primaryBackground.sprite;
            tempSr.color = primaryBackground.color;
            tempSr.sortingOrder = primaryBackground.sortingOrder + 1;
            tempOverlay.transform.position = primaryBackground.transform.position;
            tempOverlay.transform.localScale = primaryBackground.transform.localScale;

            ApplyStage(config, stageIndexZeroBased);

            float elapsed = 0f;
            Color startCol = tempSr.color;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                tempSr.color = new Color(startCol.r, startCol.g, startCol.b, Mathf.Lerp(startCol.a, 0f, t));
                yield return null;
            }

            Destroy(tempOverlay);
            crossfadeCoroutine = null;
        }

        private void FitBackgroundAspectFill(SpriteRenderer sr, float extraZoom)
        {
            if (sr == null || sr.sprite == null) return;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            float worldScreenHeight = targetCamera.orthographicSize * 2f;
            float worldScreenWidth = worldScreenHeight / Mathf.Max(1, Screen.height) * Mathf.Max(1, Screen.width);

            Vector2 spriteSize = sr.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            float scaleX = worldScreenWidth / spriteSize.x;
            float scaleY = worldScreenHeight / spriteSize.y;
            float scale = Mathf.Max(scaleX, scaleY) * Mathf.Max(1.0f, extraZoom);

            sr.transform.localScale = new Vector3(scale, scale, 1f);
            sr.transform.position = new Vector3(targetCamera.transform.position.x, targetCamera.transform.position.y, 0f);
        }

        private void RefreshAmbientMoteColors()
        {
            for (int i = 0; i < activeMotes.Count; i++)
            {
                var mote = activeMotes[i];
                if (mote.sr != null)
                {
                    mote.sr.color = new Color(currentParticleColor.r, currentParticleColor.g, currentParticleColor.b, mote.sr.color.a);
                }
            }
        }

        private void Update()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            particleSpawnTimer += Time.unscaledDeltaTime;
            if (particleSpawnTimer >= particleSpawnInterval && activeMotes.Count < maxAmbientParticles)
            {
                particleSpawnTimer = 0f;
                SpawnAmbientMote();
            }

            for (int i = activeMotes.Count - 1; i >= 0; i--)
            {
                var mote = activeMotes[i];
                if (mote.go == null)
                {
                    activeMotes.RemoveAt(i);
                    continue;
                }

                mote.age += Time.unscaledDeltaTime;
                if (mote.age >= mote.lifetime)
                {
                    Destroy(mote.go);
                    activeMotes.RemoveAt(i);
                    continue;
                }

                float sway = Mathf.Sin((mote.age * 2.2f) + mote.swayPhase) * 0.35f;
                Vector3 pos = mote.go.transform.position;
                pos += (mote.velocity + new Vector3(sway, 0f, 0f)) * Time.unscaledDeltaTime;
                mote.go.transform.position = pos;

                float t = mote.age / mote.lifetime;
                float fade = Mathf.Sin(t * Mathf.PI); // Entra suave e desaparece suave
                mote.sr.color = new Color(currentParticleColor.r, currentParticleColor.g, currentParticleColor.b, mote.baseAlpha * fade);

                activeMotes[i] = mote;
            }
        }

        private void SpawnAmbientMote()
        {
            if (targetCamera == null) return;

            float vert = targetCamera.orthographicSize;
            float horz = vert * (float)Screen.width / Mathf.Max(1, Screen.height);

            float x = Random.Range(-horz * 0.95f, horz * 0.95f);
            float y = Random.Range(-vert * 0.9f, vert * 0.6f);

            GameObject go = new GameObject("StageAmbientMote");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(x, y, 0f);
            float scale = Random.Range(0.14f, 0.34f);
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = cachedMoteSprite;
            sr.material = cachedSpriteMaterial;
            sr.sortingOrder = -20; // Entre o fundo (-50) e os goblins (>= 0)
            float baseAlpha = currentParticleColor.a * Random.Range(0.55f, 1.0f);
            sr.color = new Color(currentParticleColor.r, currentParticleColor.g, currentParticleColor.b, 0f);

            activeMotes.Add(new AmbientMote
            {
                go = go,
                sr = sr,
                velocity = new Vector3(Random.Range(-0.22f, 0.22f), currentParticleRiseSpeed * Random.Range(0.6f, 1.35f), 0f),
                age = 0f,
                lifetime = Random.Range(2.4f, 4.4f),
                baseAlpha = baseAlpha,
                swayPhase = Random.Range(0f, Mathf.PI * 2f)
            });
        }

        private Sprite CreateSoftMoteSprite(int resolution)
        {
            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float c = resolution * 0.5f;
            float r = c - 1f;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                    float norm = Mathf.Clamp01(d / r);
                    float alpha = Mathf.Pow(1f - norm, 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
