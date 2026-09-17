using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WizardGame.Entities;

namespace WizardGame.Core
{
    /// <summary>
    /// Gerenciador de Efeitos Visuais (VFX) do Arsenal Mágico:
    /// - ❄️ Efeito de Gelo: Bloco/aura de gelo cristalino sobre o goblin, partículas de geada e estilhaçamento (Ice Shatter)
    /// - ⚡ Relâmpago em Cadeia: Arco elétrico dinâmico (LineRenderer) crepitante entre alvos e fagulhas elétricas
    /// - 🌀 Explosão Mágica em Área: Onda de choque radial expansiva (shockwave ring), clarão místico, fagulhas arcanas e tremor de câmera
    /// </summary>
    public class SpellEffectsManager : MonoBehaviour
    {
        private static SpellEffectsManager _instance;
        public static SpellEffectsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<SpellEffectsManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("SpellEffectsManager");
                        _instance = go.AddComponent<SpellEffectsManager>();
                    }
                }
                return _instance;
            }
        }

        private Material spriteMaterial;
        private Sprite circleSprite;
        private Sprite diamondSprite;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            spriteMaterial = new Material(shader);

            circleSprite = CreateCircleSprite(64);
            diamondSprite = CreateDiamondSprite(32, 48);
        }

        #region ❄️ Efeito de Gelo (Ice VFX)

        public void SpawnIceEffect(WizardController goblin, float duration = 2.5f)
        {
            if (goblin == null) return;
            StartCoroutine(IceEffectRoutine(goblin, duration));
        }

        private IEnumerator IceEffectRoutine(WizardController goblin, float duration)
        {
            // Cria container do efeito de gelo anexado ao goblin
            GameObject iceGo = new GameObject("IceCrystalVFX");
            iceGo.transform.SetParent(goblin.transform, false);
            iceGo.transform.localPosition = Vector3.zero;

            // Cristal principal translúcido
            var sr = iceGo.AddComponent<SpriteRenderer>();
            sr.sprite = diamondSprite;
            sr.material = spriteMaterial;
            sr.color = new Color(0.45f, 0.85f, 1f, 0.72f);
            sr.sortingOrder = 7; // Logo à frente do goblin (ordem 5)

            // Ajusta tamanho para cobrir o goblin
            iceGo.transform.localScale = new Vector3(1.6f, 1.9f, 1f);

            // Spawna fagulhas de gelo enquanto congelado
            float elapsed = 0f;
            float sparkTimer = 0f;

            while (elapsed < duration)
            {
                if (goblin == null || !goblin.gameObject.activeInHierarchy || !goblin.IsFrozen)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                sparkTimer += Time.deltaTime;

                // Pulso suave do brilho do gelo
                float pulse = 0.65f + Mathf.PingPong(elapsed * 3f, 0.25f);
                sr.color = new Color(0.45f, 0.85f, 1f, pulse);

                // Emite cristais flutuantes
                if (sparkTimer >= 0.18f)
                {
                    sparkTimer = 0f;
                    SpawnFloatingFrostParticle(goblin.transform.position);
                }

                yield return null;
            }

            Vector3 burstPos = (goblin != null) ? goblin.transform.position : iceGo.transform.position;
            Destroy(iceGo);

            // Explosão de estilhaços de gelo ao quebrar/morrer
            SpawnIceShatter(burstPos);
        }

        public void SpawnIceShatter(Vector3 position)
        {
            int shardCount = 10;
            for (int i = 0; i < shardCount; i++)
            {
                float angle = (i / (float)shardCount) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float speed = UnityEngine.Random.Range(2.0f, 4.5f);
                StartCoroutine(IceShardRoutine(position, dir * speed));
            }
        }

        private IEnumerator IceShardRoutine(Vector3 startPos, Vector2 velocity)
        {
            GameObject shard = new GameObject("IceShard");
            shard.transform.position = startPos;
            var sr = shard.AddComponent<SpriteRenderer>();
            sr.sprite = diamondSprite;
            sr.material = spriteMaterial;
            sr.color = new Color(0.7f, 0.95f, 1f, 0.95f);
            sr.sortingOrder = 25;
            shard.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.25f, 0.45f);

            float rotSpeed = UnityEngine.Random.Range(-360f, 360f);
            float elapsed = 0f;
            float duration = 0.35f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                velocity.y -= 9.8f * Time.deltaTime * 0.4f; // Gravidade leve
                shard.transform.position += (Vector3)(velocity * Time.deltaTime);
                shard.transform.Rotate(0f, 0f, rotSpeed * Time.deltaTime);

                sr.color = new Color(0.7f, 0.95f, 1f, Mathf.Lerp(0.95f, 0f, t));
                yield return null;
            }

            Destroy(shard);
        }

        private void SpawnFloatingFrostParticle(Vector3 center)
        {
            Vector3 pos = center + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.45f);
            StartCoroutine(FloatingFrostRoutine(pos));
        }

        private IEnumerator FloatingFrostRoutine(Vector3 pos)
        {
            GameObject p = new GameObject("FrostSpark");
            p.transform.position = pos;
            var sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.material = spriteMaterial;
            sr.color = new Color(0.6f, 0.95f, 1f, 0.8f);
            sr.sortingOrder = 20;
            p.transform.localScale = Vector3.one * 0.12f;

            float elapsed = 0f;
            float duration = 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                p.transform.position += Vector3.up * (Time.deltaTime * 0.8f);
                sr.color = new Color(0.6f, 0.95f, 1f, Mathf.Lerp(0.8f, 0f, t));
                yield return null;
            }

            Destroy(p);
        }

        #endregion

        #region ⚡ Relâmpago em Cadeia (Lightning Chain VFX)

        public void SpawnLightningChain(Vector3 origin, List<Vector3> chainPoints)
        {
            if (chainPoints == null || chainPoints.Count == 0) return;

            Vector3 prev = origin;
            for (int i = 0; i < chainPoints.Count; i++)
            {
                Vector3 next = chainPoints[i];
                StartCoroutine(LightningBoltRoutine(prev, next, i * 0.04f));
                SpawnSparkBurst(next, new Color(0.4f, 0.85f, 1f, 1f));
                prev = next;
            }
        }

        private IEnumerator LightningBoltRoutine(Vector3 start, Vector3 end, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            GameObject boltGo = new GameObject("LightningBolt");
            var lr = boltGo.AddComponent<LineRenderer>();
            lr.material = spriteMaterial;
            lr.startColor = new Color(0.9f, 0.98f, 1f, 1f); // Branco-azulado brilhante
            lr.endColor = new Color(0.2f, 0.6f, 1f, 0.9f);   // Azul elétrico
            lr.startWidth = 0.18f;
            lr.endWidth = 0.10f;
            lr.sortingOrder = 40;
            lr.positionCount = 8;
            lr.useWorldSpace = true;

            float duration = 0.22f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Gera formato irregular/quebrado de raio (jitter dinâmico)
                Vector3 dir = end - start;
                Vector3 perp = Vector3.Cross(dir.normalized, Vector3.forward);
                float dist = dir.magnitude;

                lr.SetPosition(0, start);
                for (int i = 1; i < lr.positionCount - 1; i++)
                {
                    float frac = i / (float)(lr.positionCount - 1);
                    Vector3 pt = Vector3.Lerp(start, end, frac);
                    float offset = UnityEngine.Random.Range(-0.32f, 0.32f) * (1f - Mathf.Abs(frac - 0.5f) * 0.5f);
                    pt += perp * offset;
                    lr.SetPosition(i, pt);
                }
                lr.SetPosition(lr.positionCount - 1, end);

                float alpha = Mathf.Lerp(1f, 0f, t);
                lr.startColor = new Color(0.9f, 0.98f, 1f, alpha);
                lr.endColor = new Color(0.2f, 0.6f, 1f, alpha * 0.8f);

                yield return new WaitForSeconds(0.025f);
            }

            Destroy(boltGo);
        }

        public void SpawnSparkBurst(Vector3 pos, Color color)
        {
            int count = 8;
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
                float speed = UnityEngine.Random.Range(2.5f, 5.0f);
                StartCoroutine(SparkParticleRoutine(pos, dir * speed, color));
            }
        }

        private IEnumerator SparkParticleRoutine(Vector3 start, Vector2 velocity, Color color)
        {
            GameObject spark = new GameObject("Spark");
            spark.transform.position = start;
            var sr = spark.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.material = spriteMaterial;
            sr.color = color;
            sr.sortingOrder = 45;
            spark.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.12f, 0.22f);

            float elapsed = 0f;
            float duration = 0.25f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                spark.transform.position += (Vector3)(velocity * Time.deltaTime);
                sr.color = new Color(color.r, color.g, color.b, Mathf.Lerp(color.a, 0f, t));
                yield return null;
            }

            Destroy(spark);
        }

        #endregion

        #region 🌀 Explosão Mágica de Área (Area Blast VFX)

        public void SpawnAreaExplosion(Vector3 center, float radius = 2.5f)
        {
            StartCoroutine(AreaExplosionRoutine(center, radius));
        }

        private IEnumerator AreaExplosionRoutine(Vector3 center, float maxRadius)
        {
            // 1. Clarão Central Arcano
            GameObject flashGo = new GameObject("ArcaneFlash");
            flashGo.transform.position = center;
            var flashSr = flashGo.AddComponent<SpriteRenderer>();
            flashSr.sprite = circleSprite;
            flashSr.material = spriteMaterial;
            flashSr.color = new Color(1f, 0.4f, 1f, 0.95f); // Magenta brilhante
            flashSr.sortingOrder = 48;
            flashGo.transform.localScale = Vector3.one * 0.5f;

            // 2. Onda de Choque Circular (Shockwave Ring via LineRenderer)
            GameObject ringGo = new GameObject("ShockwaveRing");
            ringGo.transform.position = center;
            var lr = ringGo.AddComponent<LineRenderer>();
            lr.material = spriteMaterial;
            lr.sortingOrder = 49;
            lr.useWorldSpace = true;
            lr.loop = true;
            int segments = 36;
            lr.positionCount = segments;
            lr.startColor = new Color(0.85f, 0.3f, 1f, 0.95f);
            lr.endColor = new Color(0.4f, 0.9f, 1f, 0.85f);
            lr.startWidth = 0.18f;
            lr.endWidth = 0.18f;

            // 3. Spawna partículas de faíscas arcanas em 360 graus
            int sparkCount = 20;
            for (int i = 0; i < sparkCount; i++)
            {
                float ang = (i / (float)sparkCount) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.1f, 0.1f);
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float spd = UnityEngine.Random.Range(3.5f, 6.5f);
                Color sparkCol = Color.Lerp(new Color(1f, 0.3f, 0.9f), new Color(0.4f, 0.9f, 1f), UnityEngine.Random.value);
                StartCoroutine(SparkParticleRoutine(center, dir * spd, sparkCol));
            }

            // Tremor de câmera sutil e punchy
            StartCoroutine(CameraShakeRoutine(0.12f, 0.14f));

            float duration = 0.35f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);

                // Expande anel até o raio máximo
                float currentRadius = Mathf.Lerp(0.3f, maxRadius, easeOut);
                for (int i = 0; i < segments; i++)
                {
                    float rad = (i / (float)segments) * Mathf.PI * 2f;
                    Vector3 pt = center + new Vector3(Mathf.Cos(rad) * currentRadius, Mathf.Sin(rad) * currentRadius, 0f);
                    lr.SetPosition(i, pt);
                }

                // Clarão cresce rápido e some
                float flashScale = Mathf.Lerp(0.5f, maxRadius * 0.8f, Mathf.Clamp01(t * 2.5f));
                flashGo.transform.localScale = Vector3.one * flashScale;
                flashSr.color = new Color(1f, 0.4f, 1f, Mathf.Lerp(0.95f, 0f, t));

                // Anel atenua largura e alpha
                float alpha = Mathf.Lerp(1f, 0f, t);
                lr.startWidth = Mathf.Lerp(0.18f, 0.02f, t);
                lr.endWidth = lr.startWidth;
                lr.startColor = new Color(0.85f, 0.3f, 1f, alpha);
                lr.endColor = new Color(0.4f, 0.9f, 1f, alpha * 0.8f);

                yield return null;
            }

            Destroy(flashGo);
            Destroy(ringGo);
        }

        private IEnumerator CameraShakeRoutine(float duration, float magnitude)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;

            Vector3 originalPos = cam.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float damp = 1f - (elapsed / duration);
                float x = UnityEngine.Random.Range(-1f, 1f) * magnitude * damp;
                float y = UnityEngine.Random.Range(-1f, 1f) * magnitude * damp;
                cam.transform.position = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
                yield return null;
            }

            cam.transform.position = originalPos;
        }

        #endregion

        #region Geradores Procedurais de Sprites (Zero Dependências Externas)

        private Sprite CreateCircleSprite(int resolution)
        {
            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = resolution * 0.5f;
            float radius = center - 1f;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist <= radius)
                    {
                        float alpha = Mathf.Clamp01((radius - dist) / 2.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite CreateDiamondSprite(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Abs(x - halfW) / halfW;
                    float dy = Mathf.Abs(y - halfH) / halfH;
                    if (dx + dy <= 1.0f)
                    {
                        float alpha = Mathf.Clamp01((1.0f - (dx + dy)) * 4.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        #endregion
    }
}
