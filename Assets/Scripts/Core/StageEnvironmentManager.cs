using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WizardGame.Data;

namespace WizardGame.Core
{
    /// <summary>
    /// Gerencia o cenário dinâmico 2.5D por fase:
    /// - Troca o background correspondente a cada chefe mantendo Aspect Fill perfeito sem barras pretas.
    /// - Aplica Parallax reativo ao mouse em múltiplos planos (fundo, obstáculos de cobertura e partículas frontais).
    /// - Posiciona obstáculos recortados (barris, ruínas, rochas vulcânicas, pilares) com ordenação por Y
    ///   para que goblins passem naturalmente por trás e pela frente dos objetos.
    /// - Fornece cor/intensidade de névoa atmosférica por bioma para profundidade dos inimigos e partículas em 2 planos.
    /// </summary>
    public class StageEnvironmentManager : MonoBehaviour
    {
        public static StageEnvironmentManager Instance { get; private set; }

        [Header("Referências do Cenário")]
        [SerializeField] private SpriteRenderer primaryBackground;
        [SerializeField] private BackgroundScaler primaryScaler;
        [SerializeField] private Camera targetCamera;

        [Header("Backgrounds e Obstáculos 2.5D por Fase (Índices 0..4)")]
        [SerializeField] private List<Sprite> stageBackgrounds = new List<Sprite>();
        [SerializeField] private List<Sprite> stageCoverProps = new List<Sprite>();

        [Header("Parallax 2.5D Reativo ao Mouse")]
        [SerializeField] private bool enableMouseParallax = true;
        [SerializeField] private float backgroundParallaxStrength = 0.26f;
        [SerializeField] private float parallaxSmoothSpeed = 5.5f;

        [Header("Partículas Ambientais em 2 Planos (Fundo + Primeiro Plano)")]
        [SerializeField] private int maxAmbientParticles = 18;
        [SerializeField] private int maxForegroundParticles = 7;
        [SerializeField] private float particleSpawnInterval = 0.24f;

        private readonly List<AmbientMote> activeMotes = new List<AmbientMote>(32);
        private readonly List<StageCoverPropInstance> activeCoverProps = new List<StageCoverPropInstance>(4);
        private Color currentParticleColor = new Color(0.6f, 0.85f, 1f, 0.55f);
        private float currentParticleRiseSpeed = 0.45f;
        private float particleSpawnTimer;
        private float fgParticleSpawnTimer;
        private Sprite cachedMoteSprite;
        private Sprite cachedShadowSprite;
        private Sprite cachedMistSprite;
        private Material cachedSpriteMaterial;
        private float currentZoom = 1.08f;
        private Color currentTint = Color.white;
        private int currentStageIndex = 0;
        private Coroutine crossfadeCoroutine;
        private Vector2 smoothedParallax = Vector2.zero;
        private SpriteRenderer bottomMistRenderer;
        private Vector3 bottomMistBasePos;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private bool hasAppliedStage = false;

        public Color CurrentDepthFogColor { get; private set; } = new Color(0.16f, 0.28f, 0.52f, 1f);
        public float CurrentDepthFogStrength { get; private set; } = 0.26f;
        public Vector2 CurrentParallaxNormalized => smoothedParallax;

        private struct AmbientMote
        {
            public GameObject go;
            public SpriteRenderer sr;
            public Vector3 basePosition;
            public Vector3 velocity;
            public float age;
            public float lifetime;
            public float baseAlpha;
            public float swayPhase;
            public float parallaxFactor;
            public bool isForeground;
        }

        private class StageCoverPropInstance
        {
            public GameObject root;
            public Vector3 basePosition;
            public float parallaxFactor;
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
            cachedShadowSprite = CreateSoftOvalShadowSprite(64, 32);
            cachedMistSprite = CreateVerticalMistSprite(16, 128);

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            CreateBottomMistLayer();
        }

        private void Start()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            if (!hasAppliedStage)
            {
                ConfigureBiomeAtmosphere(0);
                UpdateBottomMistVisuals();
                if (primaryScaler != null && enableMouseParallax)
                {
                    primaryScaler.SetExtraZoom(Mathf.Max(1.09f, currentZoom));
                }
            }
        }

        public void SetStageBackgroundList(List<Sprite> sprites)
        {
            stageBackgrounds = sprites ?? new List<Sprite>();
        }

        public void SetStageCoverPropList(List<Sprite> sprites)
        {
            stageCoverProps = sprites ?? new List<Sprite>();
        }

        /// <summary>
        /// Aplica imediatamente o cenário, cor, zoom, névoa, obstáculos 2.5D e bioma da fase informada.
        /// </summary>
        public void ApplyStage(WaveConfig config, int stageIndexZeroBased)
        {
            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
                crossfadeCoroutine = null;
            }

            hasAppliedStage = true;
            currentStageIndex = Mathf.Max(0, stageIndexZeroBased);

            Sprite targetSprite = config != null ? config.backgroundSprite : null;
            if (targetSprite == null && stageBackgrounds != null && stageBackgrounds.Count > 0)
            {
                int idx = Mathf.Clamp(currentStageIndex, 0, stageBackgrounds.Count - 1);
                targetSprite = stageBackgrounds[idx];
            }

            currentTint = config != null ? config.backgroundTint : Color.white;
            float baseZoom = config != null ? Mathf.Clamp(config.backgroundZoom, 1.0f, 1.35f) : 1.0f;
            // Margem mínima de zoom para o parallax reativo ao mouse nunca expor bordas
            currentZoom = enableMouseParallax ? Mathf.Max(1.09f, baseZoom + 0.06f) : baseZoom;
            currentParticleColor = config != null ? config.ambientParticleColor : new Color(0.6f, 0.85f, 1f, 0.55f);

            ConfigureBiomeAtmosphere(currentStageIndex);

            // Fases vulcânicas / finais têm brasas subindo mais rápido
            currentParticleRiseSpeed = (currentStageIndex >= 2) ? 0.95f : 0.38f;

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

            UpdateBottomMistVisuals();
            RebuildStageCoverProps(currentStageIndex);
            RefreshAmbientMoteColors();
        }

        private void ConfigureBiomeAtmosphere(int stageIndex)
        {
            switch (stageIndex)
            {
                case 0: // Muralhas da Fortaleza (noite lunar)
                    CurrentDepthFogColor = new Color(0.18f, 0.30f, 0.54f, 1f);
                    CurrentDepthFogStrength = 0.24f;
                    break;
                case 1: // Ruínas Iluminadas pela Lua
                    CurrentDepthFogColor = new Color(0.16f, 0.36f, 0.50f, 1f);
                    CurrentDepthFogStrength = 0.27f;
                    break;
                case 2: // Fortaleza Vulcânica
                    CurrentDepthFogColor = new Color(0.76f, 0.30f, 0.12f, 1f);
                    CurrentDepthFogStrength = 0.28f;
                    break;
                case 3: // Salão do Trono em Ruínas
                    CurrentDepthFogColor = new Color(0.36f, 0.16f, 0.44f, 1f);
                    CurrentDepthFogStrength = 0.27f;
                    break;
                default: // Altar do Rei Supremo (Fase 5)
                    CurrentDepthFogColor = new Color(0.72f, 0.16f, 0.14f, 1f);
                    CurrentDepthFogStrength = 0.30f;
                    break;
            }
        }

        private void CreateBottomMistLayer()
        {
            if (bottomMistRenderer != null) return;
            GameObject mistGo = new GameObject("StageDepthMist_Foreground");
            mistGo.transform.SetParent(transform, false);
            bottomMistRenderer = mistGo.AddComponent<SpriteRenderer>();
            bottomMistRenderer.sprite = cachedMistSprite;
            bottomMistRenderer.material = cachedSpriteMaterial;
            bottomMistRenderer.sortingOrder = 160;
            UpdateBottomMistVisuals();
        }

        private void UpdateBottomMistVisuals()
        {
            if (bottomMistRenderer == null) return;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            float vert = targetCamera.orthographicSize;
            float horz = vert * (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);

            Vector2 spSize = bottomMistRenderer.sprite.bounds.size;
            float targetW = horz * 2.4f;
            float targetH = vert * 0.72f;
            bottomMistRenderer.transform.localScale = new Vector3(targetW / Mathf.Max(0.01f, spSize.x), targetH / Mathf.Max(0.01f, spSize.y), 1f);
            bottomMistBasePos = new Vector3(targetCamera.transform.position.x, targetCamera.transform.position.y - vert + (targetH * 0.46f), 0f);
            bottomMistRenderer.transform.position = bottomMistBasePos;
            bottomMistRenderer.color = new Color(CurrentDepthFogColor.r, CurrentDepthFogColor.g, CurrentDepthFogColor.b, 0.22f);
        }

        private void ClearCoverProps()
        {
            for (int i = 0; i < activeCoverProps.Count; i++)
            {
                if (activeCoverProps[i]?.root != null)
                {
                    Destroy(activeCoverProps[i].root);
                }
            }
            activeCoverProps.Clear();
        }

        private void RebuildStageCoverProps(int stageIndex)
        {
            ClearCoverProps();

            if (stageCoverProps == null || stageCoverProps.Count == 0) return;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            Sprite propSprite = stageCoverProps[Mathf.Clamp(stageIndex, 0, stageCoverProps.Count - 1)];
            if (propSprite == null) return;

            float vert = targetCamera.orthographicSize;
            float horz = vert * (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);

            // Obstáculo 1 (Esquerda, plano mais próximo): goblins atrás passam ocultos parcialmente, goblins à frente passam por cima
            SpawnCoverProp(
                name: $"CoverProp_Left_Stage{stageIndex + 1}",
                sprite: propSprite,
                worldCenter: new Vector3(-horz * 0.47f, -vert * 0.30f, 0f),
                targetHeight: 1.95f,
                flipX: false,
                parallaxFactor: 0.46f,
                depthFogBlend: 0.04f,
                stageIndex: stageIndex
            );

            // Obstáculo 2 (Direita, plano intermediário mais fundo): menor por perspectiva 2.5D, espelhado para variedade orgânica
            SpawnCoverProp(
                name: $"CoverProp_Right_Stage{stageIndex + 1}",
                sprite: propSprite,
                worldCenter: new Vector3(horz * 0.46f, -vert * 0.05f, 0f),
                targetHeight: 1.60f,
                flipX: true,
                parallaxFactor: 0.35f,
                depthFogBlend: 0.16f,
                stageIndex: stageIndex
            );
        }

        private void SpawnCoverProp(string name, Sprite sprite, Vector3 worldCenter, float targetHeight, bool flipX, float parallaxFactor, float depthFogBlend, int stageIndex)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.position = worldCenter;

            float spH = Mathf.Max(0.1f, sprite.bounds.size.y);
            float spW = Mathf.Max(0.1f, sprite.bounds.size.x);
            float scale = targetHeight / spH;
            root.transform.localScale = new Vector3(scale, scale, 1f);

            // Base real do obstáculo para cálculo exato do sortingOrder (mesma fórmula Y do WizardController!)
            float baseY = worldCenter.y - (targetHeight * 0.42f);
            int propOrder = Mathf.RoundToInt((10f - baseY) * 10f);

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.sortingOrder = propOrder;

            // Mescla levemente com o tint da fase + perspectiva atmosférica
            Color baseTint = (stageIndex >= 4) ? new Color(1f, 0.78f, 0.72f, 1f) : Color.Lerp(Color.white, currentTint, 0.55f);
            sr.color = Color.Lerp(baseTint, CurrentDepthFogColor, depthFogBlend);

            // Sombra de contato suave sob a base do obstáculo (escala exata baseada no bounds do sprite oval)
            GameObject shadowGo = new GameObject("PropGroundShadow");
            shadowGo.transform.SetParent(root.transform, false);
            shadowGo.transform.localPosition = new Vector3(0f, -spH * 0.44f, 0f);
            Vector2 shSize = cachedShadowSprite != null ? (Vector2)cachedShadowSprite.bounds.size : Vector2.one;
            shadowGo.transform.localScale = new Vector3(
                (spW * 0.94f) / Mathf.Max(0.01f, shSize.x),
                (spH * 0.22f) / Mathf.Max(0.01f, shSize.y),
                1f
            );
            var shadowSr = shadowGo.AddComponent<SpriteRenderer>();
            shadowSr.sprite = cachedShadowSprite;
            shadowSr.material = cachedSpriteMaterial;
            shadowSr.color = new Color(0.02f, 0.02f, 0.06f, 0.52f);
            shadowSr.sortingOrder = propOrder - 1;

            activeCoverProps.Add(new StageCoverPropInstance
            {
                root = root,
                basePosition = worldCenter,
                parallaxFactor = parallaxFactor
            });
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

            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                lastScreenWidth = Screen.width;
                lastScreenHeight = Screen.height;
                UpdateBottomMistVisuals();
                if (hasAppliedStage)
                {
                    RebuildStageCoverProps(currentStageIndex);
                }
            }

            UpdateParallax();
            UpdateParticles();
        }

        private void UpdateParallax()
        {
            if (!enableMouseParallax)
            {
                smoothedParallax = Vector2.zero;
                return;
            }

            float w = Mathf.Max(1f, Screen.width);
            float h = Mathf.Max(1f, Screen.height);
            Vector3 mPos = UnityEngine.Input.mousePosition;
            float nx = Mathf.Clamp((mPos.x / w - 0.5f) * 2f, -1f, 1f);
            float ny = Mathf.Clamp((mPos.y / h - 0.5f) * 2f, -1f, 1f);
            Vector2 targetNorm = new Vector2(nx, ny);

            smoothedParallax = Vector2.Lerp(smoothedParallax, targetNorm, Time.unscaledDeltaTime * parallaxSmoothSpeed);

            // 1. Fundo distante move suavemente na direção oposta à mira
            if (primaryScaler != null)
            {
                primaryScaler.SetParallaxOffset(new Vector2(-smoothedParallax.x * backgroundParallaxStrength, -smoothedParallax.y * backgroundParallaxStrength * 0.65f));
            }

            // 2. Obstáculos 2.5D movem um pouco mais que o fundo, gerando paralaxe tridimensional real
            for (int i = 0; i < activeCoverProps.Count; i++)
            {
                var prop = activeCoverProps[i];
                if (prop?.root == null) continue;
                Vector3 offset = new Vector3(
                    -smoothedParallax.x * prop.parallaxFactor,
                    -smoothedParallax.y * prop.parallaxFactor * 0.5f,
                    0f
                );
                prop.root.transform.position = prop.basePosition + offset;
            }

            // 3. Névoa frontal no rodapé acompanha plano próximo
            if (bottomMistRenderer != null)
            {
                bottomMistRenderer.transform.position = bottomMistBasePos + new Vector3(-smoothedParallax.x * 0.55f, -smoothedParallax.y * 0.18f, 0f);
            }
        }

        private void UpdateParticles()
        {
            int backCount = 0;
            int fgCount = 0;
            for (int i = 0; i < activeMotes.Count; i++)
            {
                if (activeMotes[i].isForeground) fgCount++;
                else backCount++;
            }

            particleSpawnTimer += Time.unscaledDeltaTime;
            if (particleSpawnTimer >= particleSpawnInterval && backCount < maxAmbientParticles)
            {
                particleSpawnTimer = 0f;
                SpawnAmbientMote(isForeground: false);
            }

            fgParticleSpawnTimer += Time.unscaledDeltaTime;
            if (fgParticleSpawnTimer >= particleSpawnInterval * 2.1f && fgCount < maxForegroundParticles)
            {
                fgParticleSpawnTimer = 0f;
                SpawnAmbientMote(isForeground: true);
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

                float sway = Mathf.Sin((mote.age * 2.2f) + mote.swayPhase) * (mote.isForeground ? 0.55f : 0.35f);
                mote.basePosition += (mote.velocity + new Vector3(sway, 0f, 0f)) * Time.unscaledDeltaTime;

                Vector3 pOffset = new Vector3(
                    -smoothedParallax.x * mote.parallaxFactor,
                    -smoothedParallax.y * mote.parallaxFactor * 0.6f,
                    0f
                );
                mote.go.transform.position = mote.basePosition + pOffset;

                float t = mote.age / mote.lifetime;
                float fade = Mathf.Sin(t * Mathf.PI); // Entra suave e desaparece suave
                mote.sr.color = new Color(currentParticleColor.r, currentParticleColor.g, currentParticleColor.b, mote.baseAlpha * fade);

                activeMotes[i] = mote;
            }
        }

        private void SpawnAmbientMote(bool isForeground)
        {
            if (targetCamera == null) return;

            float vert = targetCamera.orthographicSize;
            float horz = vert * (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);

            float x = Random.Range(-horz * 0.98f, horz * 0.98f);
            float y = isForeground
                ? Random.Range(-vert * 0.95f, vert * 0.35f)
                : Random.Range(-vert * 0.9f, vert * 0.65f);

            GameObject go = new GameObject(isForeground ? "StageForegroundBokeh" : "StageAmbientMote");
            go.transform.SetParent(transform, false);
            Vector3 startPos = new Vector3(x, y, 0f);
            go.transform.position = startPos;

            float scale = isForeground ? Random.Range(0.42f, 0.78f) : Random.Range(0.14f, 0.32f);
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = cachedMoteSprite;
            sr.material = cachedSpriteMaterial;
            // Plano traseiro (-20) atrás dos goblins; plano frontal (164) desfocado passando NA FRENTE dos goblins
            sr.sortingOrder = isForeground ? 164 : -20;
            float baseAlpha = currentParticleColor.a * (isForeground ? Random.Range(0.24f, 0.42f) : Random.Range(0.55f, 1.0f));
            sr.color = new Color(currentParticleColor.r, currentParticleColor.g, currentParticleColor.b, 0f);

            float speedScale = isForeground ? 1.55f : 1.0f;
            activeMotes.Add(new AmbientMote
            {
                go = go,
                sr = sr,
                basePosition = startPos,
                velocity = new Vector3(Random.Range(-0.26f, 0.26f) * speedScale, currentParticleRiseSpeed * Random.Range(0.6f, 1.35f) * speedScale, 0f),
                age = 0f,
                lifetime = isForeground ? Random.Range(2.0f, 3.4f) : Random.Range(2.6f, 4.6f),
                baseAlpha = baseAlpha,
                swayPhase = Random.Range(0f, Mathf.PI * 2f),
                parallaxFactor = isForeground ? 0.78f : 0.22f,
                isForeground = isForeground
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

        private Sprite CreateSoftOvalShadowSprite(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = width * 0.5f;
            float cy = height * 0.5f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - cx) / (cx - 1f);
                    float dy = (y - cy) / (cy - 1f);
                    float norm = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float alpha = Mathf.Pow(1f - norm, 2.0f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite CreateVerticalMistSprite(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < height; y++)
            {
                float ny = (float)y / Mathf.Max(1, height - 1);
                // Mais denso na base (y=0), dissipando suavemente para cima (y=1)
                float alpha = Mathf.Pow(1f - ny, 1.85f);
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
