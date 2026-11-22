# Documentação Técnica e Guia de Implementação: Minigame do Mago (Unity 6 / 6000.x)

Este documento contém o Game Design Document Técnico (GDD), a arquitetura de software e os códigos-fonte C# completos para desenvolver o **Minigame do Mago** na **Unity 6 (6000.x)** com **Universal Render Pipeline (URP 2D)**.

---

## 1. Visão Geral do Projeto

| Atributo | Especificação |
| :--- | :--- |
| **Gênero** | Point-and-Click / Shooting Gallery / Whack-a-Mole 2D |
| **Engine Alvo** | Unity 6 (6000.6.0f1 ou superior) |
| **Render Pipeline** | Universal Render Pipeline (URP 2D) |
| **Input System** | Unity New Input System (`com.unity.inputsystem`) |
| **Gerenciamento de Memória** | `UnityEngine.Pool.ObjectPool<T>` (Zero Garbage Collection) |
| **Arquitetura de Dados** | ScriptableObjects para parametrização dos magos |
| **Resolução Base** | 1920x1080 (Canvas Scaler: Scale with Screen Size, Match 0.5) |

---

## 2. Regras e Mecânicas de Gameplay

### 2.1 Core Loop
1. **Spawn em Ondas**: Goblins surgem em 5 ondas dinâmicas e estruturadas com taxa de spawn e velocidades aceleradas.
2. **Arsenal Mágico (5 Feitiços)**:
   - **Tiro Normal** (`LMB`): Disparo rápido com consumo de mana (1 de dano, pente de 8 tiros, recarga de 1.2s via `[R]`).
   - **Tiro Arcano** (`[1]`, `[Q]` ou `RMB`): 3 de dano direto concentrado (recarga 3.0s).
   - **Feitiço de Gelo** (`[2]` ou `[E]`): Congela o alvo por 2.5s impedindo movimentação e ataques (1 de dano, recarga 4.0s).
   - **Raio em Cadeia** (`[3]` ou `[F]`): Atinge o alvo com 2 de dano e encadeia eletrocutando até 2 alvos próximos (recarga 4.5s).
   - **Explosão em Área** (`[4]`, `[C]` ou `[Espaço]`): Onda de choque expansiva de 2.5m que atinge todos os goblins no raio com 2 de dano (recarga 5.5s).
3. **Sistema de Combos & Headshot**:
   - Multiplicadores dinâmicos de pontuação (x1 a x4) por abates consecutivos.
   - Bônus de Headshot (+1 ponto imediato) ao acertar o terço superior do goblin.
   - Erros de tiro ou fugas quebram o multiplicador de combo.
4. **Os 5 Grandes Chefes (A cada 10 goblins)**:
   - 10º: Grande Goblin (15 HP) | 20º: Xamã do Caos (25 HP) | 30º: Feiticeiro Espectral (35 HP)
   - 40º: Lorde Carmesim (50 HP) | 50º: Goblin Rei Supremo (80 HP - Derrotar = Vitória Imediata!).
5. **Sistema de Vida do Jogador & Dupla Condição de Derrota**:
   - O jogador possui **5 Pontos de Vida** (`❤️❤️❤️❤️❤️`), monitorados em tempo real no HUD.
   - **Perigo Primário (Whack-a-Mole)**: Impedir que 15 goblins escapem da arena.
   - **Perigo Secundário (Ataques Seletivos)**: Determinados goblins atacam o jogador:
     - **Comum**: 0 Dano (foco em escapar).
     - **Fugitivo**: Causa **1 Dano** ao escapar em disparada retaliatória.
     - **Dourado**: Causa **2 Danos** ao escapar com descarga de mana.
     - **Chefes**: Golpes sísmicos e projéteis arcanos desferem **1 a 2 Danos** ao jogador.
   - **Feedback Imediato**: Vinheta avermelhada em tela inteira (`DamageFlashOverlay`), tremor de câmera e som de impacto.
   - **Telas de Derrota Dinâmicas**: Apresenta motivo customizado ("VOCÊ CAIU EM COMBATE!" ou "MUITOS GOBLINS ESCAPARAM!").
6. **Sistema de Rank da Partida (S / A / B / C / D)**:
   - Avaliação pós-jogo calculada a partir de 6 fatores: mortes, precisão (%), tempo de partida, fugitivos, combo e feitiços fortes.
7. **Sistema de Power-ups (Itens Mágicos Flutuantes Raros)**:
   - Orbes mágicos que surgem como eventos especiais ocasionais (a cada **24 a 38s**, máx. 1 em tela por vez).
   - Pairam com movimentação orgânica senoidal e oscilação por **9 segundos** antes de dissipar.
   - Os 5 Power-ups:
     - 🔵 **Mana**: Restaura instantaneamente 4 tiros no pente de mana da varinha.
     - 🔥 **Fúria Arcana**: Concede +1 de dano a todos os disparos normais durante 5s (dano 2 por tiro).
     - ⏱️ **Tempo Arcano**: Desacelera o tempo do jogo em 50% durante 3s (`Time.timeScale = 0.5f`).
     - 💥 **Tempestade**: O próximo disparo conjura relâmpagos que aniquilam todos os goblins comuns em tela.
     - 🎯 **Precisão**: Encolhe o retículo de mira em 40% durante 5s, permitindo disparos cirúrgicos.

---

## 3. Especificação dos Inimigos (Goblins & Chefes)

```
+------------------------+----+--------+------------+-------+--------------------+-------------+---------------------------------------------------+
| Tipo                   | HP | Pontos | Escape (s) | Peso  | Penalidade Escape  | Dano Jogador| Mecânica Especial                                 |
+------------------------+----+--------+------------+-------+--------------------+-------------+---------------------------------------------------+
| Comum                  | 1  | 1      | 5.0s       | 60%   | 1                  | 0 HP        | Movimentação aleatória e golpes com facas.        |
| Fugitivo               | 2  | 2      | 4.5s       | 20%   | 2                  | 1 HP (Fuga) | Ao tomar dano, salta em disparada; fustiga na fuga|
| Dourado                | 3  | 5      | 6.0s       | 10%   | 1                  | 2 HP (Fuga) | Rápido, escudo impenetrável; contra-ataca na fuga |
| Fantasma               | 4  | 3      | 7.5s       | 10%   | 1                  | 0 HP        | Alpha espectral; teleporta através de portais!    |
| Chefe 1 (Grande)       | 15 | +25    | 18.0s      | Boss  | 3                  | 1 HP (Soco) | Golpe sísmico em área e investidas brutais.       |
| Chefe 2 (Xamã)         | 25 | +50    | 22.0s      | Boss  | 3                  | 1 HP (Fogo) | Projéteis de fogo e erupções vulcânicas.          |
| Chefe 3 (Espectral)    | 35 | +100   | 26.0s      | Boss  | 4                  | 1 HP (Orbe) | Teleporte contínuo, invisibilidade e servos.      |
| Chefe 4 (Carmesim)     | 50 | +150   | 30.0s      | Boss  | 4                  | 2 HP (Sang) | Cetro sísmico, orbes de sangue e rito carmesim.   |
| Chefe Final (Supremo)  | 80 | +500   | 40.0s      | Boss  | 5                  | 2 HP (Magma)| Abalo colossal, magma triplo! DERROTAR = VITÓRIA! |
+------------------------+----+--------+------------+-------+--------------------+-------------+---------------------------------------------------+
```

---

## 4. Arquitetura de Scripts C# (Scripts Prontos para Uso)

### 4.1 `Scripts/Data/WizardType.cs`
```csharp
namespace WizardGame.Data
{
    public enum WizardType
    {
        Comum,
        Fugitivo,
        Dourado,
        Fantasma,
        Chefe,
        Chefe2,
        Chefe3,
        Chefe4,
        ChefeFinal
    }
}
```

---

### 4.2 `Scripts/Data/WizardDataSO.cs`
```csharp
using UnityEngine;

namespace WizardGame.Data
{
    [CreateAssetMenu(fileName = "NewWizardData", menuName = "WizardGame/Wizard Data")]
    public class WizardDataSO : ScriptableObject
    {
        [Header("Identificação")]
        public WizardType wizardType;
        public string displayName;
        public Sprite sprite;
        public Color baseTint = Color.white;

        [Header("Atributos")]
        public int maxHealth = 1;
        public int pointsOnDefeat = 1;
        public float escapeTimeSeconds = 5.0f;
        [Range(1, 100)]
        public int spawnWeight = 50;
        public int escapePenalty = 1;

        [Header("Áudios Específicos")]
        public AudioClip escapeSound;
        public AudioClip customDamageSound;
        public AudioClip customDeathSound;
    }
}
```

---

### 4.3 `Scripts/Entities/WizardController.cs`
```csharp
using System;
using System.Collections;
using UnityEngine;
using WizardGame.Data;

namespace WizardGame.Entities
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
    public class WizardController : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Collider2D col;

        private WizardDataSO currentData;
        private int currentHealth;
        private float escapeTimer;
        private bool isDeadOrEscaping;

        // Limites de tela para movimentação
        private Bounds cameraWorldBounds;
        private Coroutine movementCoroutine;
        private Coroutine pulseCoroutine;
        private Coroutine escapeCoroutine;

        // Ações e Callbacks
        public event Action<WizardController, int> OnDefeated;
        public event Action<WizardController, int> OnEscaped;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (col == null) col = GetComponent<Collider2D>();
        }

        public void Initialize(WizardDataSO data, Vector3 spawnPosition, Bounds screenBounds)
        {
            currentData = data;
            currentHealth = data.maxHealth;
            cameraWorldBounds = screenBounds;
            isDeadOrEscaping = false;

            transform.position = spawnPosition;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.zero;

            spriteRenderer.sprite = data.sprite;
            spriteRenderer.color = data.baseTint;
            col.enabled = true;

            // Animação de Entrada (Scale up)
            StartCoroutine(SpawnPopRoutine());

            // Iniciar Comportamento Específico
            StartMovementBehavior();

            // Temporizador de Fuga
            escapeCoroutine = StartCoroutine(EscapeCountdownRoutine(data.escapeTimeSeconds));
        }

        private IEnumerator SpawnPopRoutine()
        {
            float elapsed = 0f;
            float duration = 0.25f;
            Vector3 targetScale = Vector3.one;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float ease = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease Out Sine
                transform.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, ease);
                yield return null;
            }
            transform.localScale = targetScale;
        }

        private void StartMovementBehavior()
        {
            StopActiveBehaviors();

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

        private void StopActiveBehaviors()
        {
            if (movementCoroutine != null) StopCoroutine(movementCoroutine);
            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        }

        public void TakeDamage(int damage)
        {
            if (isDeadOrEscaping) return;

            currentHealth -= damage;
            StartCoroutine(FlashRedRoutine());

            if (currentHealth <= 0)
            {
                Die();
            }
            else
            {
                HandleSurviveHitReaction();
            }
        }

        private void HandleSurviveHitReaction()
        {
            switch (currentData.wizardType)
            {
                case WizardType.Rapido:
                    StartCoroutine(FastHitReactionRoutine());
                    break;
                case WizardType.Fantasma:
                    StartCoroutine(GhostTeleportRoutine());
                    break;
                default:
                    StartCoroutine(SmallJiggleRoutine());
                    break;
            }
        }

        private IEnumerator FlashRedRoutine()
        {
            Color original = currentData.baseTint;
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.12f);
            if (!isDeadOrEscaping)
            {
                spriteRenderer.color = original;
            }
        }

        private IEnumerator EscapeCountdownRoutine(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (isDeadOrEscaping) yield break;

            isDeadOrEscaping = true;
            col.enabled = false;
            StopActiveBehaviors();

            // Animação de Fuga (Sobe e desvanece)
            float elapsed = 0f;
            float duration = 0.45f;
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + new Vector3(0f, 1.2f, 0f);
            Color startColor = spriteRenderer.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.position = Vector3.Lerp(startPos, endPos, t);
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
                yield return null;
            }

            OnEscaped?.Invoke(this, currentData.escapePenalty);
        }

        private void Die()
        {
            isDeadOrEscaping = true;
            col.enabled = false;
            if (escapeCoroutine != null) StopCoroutine(escapeCoroutine);
            StopActiveBehaviors();

            StartCoroutine(DeathAnimationRoutine());
        }

        private IEnumerator DeathAnimationRoutine()
        {
            float elapsed = 0f;
            float duration = 0.4f;
            Vector3 startScale = transform.localScale;

            if (currentData.wizardType == WizardType.Fantasma)
            {
                // Fantasma sobe desvanecendo
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
                // Morte com rotação de 360° e encolhimento
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

        #region Comportamentos Matemáticos de Movimento

        private IEnumerator GentleFloatRoutine()
        {
            Vector3 origin = transform.position;
            float seed = UnityEngine.Random.Range(0f, 100f);
            while (true)
            {
                float time = (Time.time + seed) * 1.5f;
                float offsetX = Mathf.Sin(time) * 0.4f;
                float offsetY = Mathf.Cos(time * 0.8f) * 0.3f;
                transform.position = origin + new Vector3(offsetX, offsetY, 0f);
                yield return null;
            }
        }

        private IEnumerator FastPendulumRoutine()
        {
            Vector3 origin = transform.position;
            float seed = UnityEngine.Random.Range(0f, 100f);
            while (true)
            {
                float time = (Time.time + seed) * 4f;
                float offsetX = Mathf.Sin(time) * 0.7f;
                float offsetY = Mathf.Sin(time * 2f) * 0.2f;
                transform.position = origin + new Vector3(offsetX, offsetY, 0f);
                yield return null;
            }
        }

        private IEnumerator FastHitReactionRoutine()
        {
            col.enabled = false;
            // Squash & Stretch
            Vector3 origScale = transform.localScale;
            transform.localScale = new Vector3(origScale.x * 1.3f, origScale.y * 0.7f, origScale.z);
            yield return new WaitForSeconds(0.1f);
            transform.localScale = origScale;

            // Corrida rápida para nova posição
            Vector3 targetPos = GetRandomWorldPointInsideBounds();
            float elapsed = 0f;
            float dashDur = 0.2f;
            Vector3 startPos = transform.position;

            while (elapsed < dashDur)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, targetPos, elapsed / dashDur);
                yield return null;
            }
            transform.position = targetPos;
            col.enabled = true;
            movementCoroutine = StartCoroutine(FastPendulumRoutine());
        }

        private IEnumerator GoldenPulseRoutine()
        {
            while (true)
            {
                float t = (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f;
                spriteRenderer.color = Color.Lerp(Color.yellow, new Color(1f, 0.84f, 0f), t);
                yield return null;
            }
        }

        private IEnumerator GoldenDashRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.6f, 1.4f));
                Vector3 target = GetRandomWorldPointInsideBounds();
                Vector3 start = transform.position;
                float duration = UnityEngine.Random.Range(0.35f, 0.6f);
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
                float offsetY = Mathf.Sin(Time.time * 2f) * 0.35f;
                transform.position = new Vector3(origin.x, origin.y + offsetY, origin.z);
                yield return null;
            }
        }

        private IEnumerator GhostTeleportRoutine()
        {
            col.enabled = false;
            StopActiveBehaviors();

            // Desvanecer e encolher
            float elapsed = 0f;
            float dur = 0.15f;
            Vector3 startScale = transform.localScale;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            // Nova posição aleatória
            transform.position = GetRandomWorldPointInsideBounds();

            // Reaparecer
            elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
                yield return null;
            }
            transform.localScale = Vector3.one;
            col.enabled = true;

            StartMovementBehavior();
        }

        private IEnumerator SmallJiggleRoutine()
        {
            Vector3 start = transform.position;
            for (int i = 0; i < 4; i++)
            {
                transform.position = start + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.1f);
                yield return new WaitForSeconds(0.02f);
            }
            transform.position = start;
        }

        private Vector3 GetRandomWorldPointInsideBounds()
        {
            float margin = 0.8f;
            float rx = UnityEngine.Random.Range(cameraWorldBounds.min.x + margin, cameraWorldBounds.max.x - margin);
            float ry = UnityEngine.Random.Range(cameraWorldBounds.min.y + margin, cameraWorldBounds.max.y - margin);
            return new Vector3(rx, ry, 0f);
        }

        #endregion

        public WizardDataSO GetData() => currentData;
    }
}
```

---

### 4.4 `Scripts/Entities/WizardSpawner.cs`
```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using WizardGame.Data;

namespace WizardGame.Entities
{
    public class WizardSpawner : MonoBehaviour
    {
        [Header("Configurações do Pool e Prefab")]
        [SerializeField] private WizardController wizardPrefab;
        [SerializeField] private int defaultPoolSize = 25;
        [SerializeField] private int maxPoolSize = 50;

        [Header("Dados dos Magos")]
        [SerializeField] private List<WizardDataSO> wizardTypes;

        [Header("Parâmetros de Spawn")]
        [SerializeField] private float spawnInterval = 1.5f;

        private ObjectPool<WizardController> pool;
        private Camera mainCamera;
        private Bounds screenBounds;
        private float timer;
        private bool isSpawningActive;

        public event System.Action<WizardController, int> OnWizardDefeated;
        public event System.Action<WizardController, int> OnWizardEscaped;

        private void Awake()
        {
            mainCamera = Camera.main;
            CalculateScreenBounds();

            pool = new ObjectPool<WizardController>(
                createFunc: () => {
                    var wizard = Instantiate(wizardPrefab, transform);
                    wizard.gameObject.SetActive(false);
                    return wizard;
                },
                actionOnGet: wizard => wizard.gameObject.SetActive(true),
                actionOnRelease: wizard => wizard.gameObject.SetActive(false),
                actionOnDestroy: wizard => Destroy(wizard.gameObject),
                collectionCheck: false,
                defaultCapacity: defaultPoolSize,
                maxSize: maxPoolSize
            );
        }

        private void CalculateScreenBounds()
        {
            float vertExtent = mainCamera.orthographicSize;
            float horzExtent = vertExtent * Screen.width / Screen.height;
            screenBounds = new Bounds(mainCamera.transform.position, new Vector3(horzExtent * 2f, vertExtent * 2f, 10f));
        }

        public void StartSpawning()
        {
            isSpawningActive = true;
            timer = 0f;
        }

        public void StopSpawning()
        {
            isSpawningActive = false;
        }

        private void Update()
        {
            if (!isSpawningActive) return;

            timer += Time.deltaTime;
            if (timer >= spawnInterval)
            {
                timer = 0f;
                SpawnRandomWizard();
            }
        }

        private void SpawnRandomWizard()
        {
            WizardDataSO selected = SelectWeightedWizard();
            if (selected == null) return;

            Vector3 spawnPos = GetRandomWorldPoint();
            WizardController wizard = pool.Get();

            wizard.Initialize(selected, spawnPos, screenBounds);
            wizard.OnDefeated += HandleWizardDefeated;
            wizard.OnEscaped += HandleWizardEscaped;
        }

        private WizardDataSO SelectWeightedWizard()
        {
            int totalWeight = 0;
            foreach (var w in wizardTypes) totalWeight += w.spawnWeight;

            int rnd = UnityEngine.Random.Range(1, totalWeight + 1);
            int cursor = 0;

            foreach (var w in wizardTypes)
            {
                cursor += w.spawnWeight;
                if (rnd <= cursor) return w;
            }

            return wizardTypes[0];
        }

        private Vector3 GetRandomWorldPoint()
        {
            float margin = 1.0f;
            float x = UnityEngine.Random.Range(screenBounds.min.x + margin, screenBounds.max.x - margin);
            float y = UnityEngine.Random.Range(screenBounds.min.y + margin, screenBounds.max.y - margin);
            return new Vector3(x, y, 0f);
        }

        private void HandleWizardDefeated(WizardController wizard, int points)
        {
            UnsubscribeAndRelease(wizard);
            OnWizardDefeated?.Invoke(wizard, points);
        }

        private void HandleWizardEscaped(WizardController wizard, int penalty)
        {
            UnsubscribeAndRelease(wizard);
            OnWizardEscaped?.Invoke(wizard, penalty);
        }

        private void UnsubscribeAndRelease(WizardController wizard)
        {
            wizard.OnDefeated -= HandleWizardDefeated;
            wizard.OnEscaped -= HandleWizardEscaped;
            pool.Release(wizard);
        }

        public void SetSpawnInterval(float newInterval)
        {
            spawnInterval = Mathf.Clamp(newInterval, 0.3f, 3.0f);
        }

        public float GetSpawnInterval() => spawnInterval;
    }
}
```

---

### 4.5 `Scripts/Input/PlayerInputHandler.cs`
```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using WizardGame.Entities;

namespace WizardGame.Input
{
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Configurações")]
        [SerializeField] private float longPressThreshold = 0.35f;
        [SerializeField] private LayerMask wizardLayer;
        [SerializeField] private Texture2D wandCursorTexture;

        private Camera mainCamera;
        private float pointerDownTime;
        private bool isPointerDown;

        public event System.Action<WizardController> OnNormalShot;
        public event System.Action<WizardController> OnStrongShot;

        private void Awake()
        {
            mainCamera = Camera.main;
            if (wandCursorTexture != null)
            {
                Cursor.SetCursor(wandCursorTexture, new Vector2(10f, 10f), CursorMode.Auto);
            }
        }

        public void OnPointerDown(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                pointerDownTime = Time.time;
                isPointerDown = true;
            }
            else if (context.canceled && isPointerDown)
            {
                isPointerDown = false;
                float duration = Time.time - pointerDownTime;

                Vector2 screenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Touchscreen.current.primaryTouch.position.ReadValue();
                WizardController target = RaycastWizard(screenPos);

                if (duration >= longPressThreshold)
                {
                    OnStrongShot?.Invoke(target);
                }
                else
                {
                    OnNormalShot?.Invoke(target);
                }
            }
        }

        public void OnRightClick(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                Vector2 screenPos = Mouse.current.position.ReadValue();
                WizardController target = RaycastWizard(screenPos);
                OnStrongShot?.Invoke(target);
            }
        }

        private WizardController RaycastWizard(Vector2 screenPosition)
        {
            Vector3 worldPoint = mainCamera.ScreenToWorldPoint(screenPosition);
            RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero, Mathf.Infinity, wizardLayer);

            if (hit.collider != null)
            {
                return hit.collider.GetComponent<WizardController>();
            }
            return null;
        }
    }
}
```

---

### 4.6 `Scripts/Core/SoundManager.cs`
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace WizardGame.Core
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Fontes de Áudio")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Clipes Globais")]
        public AudioClip normalShotSound;
        public AudioClip strongShotSound;
        public AudioClip victorySound;
        public AudioClip defeatSound;
        public AudioClip teleportSound;

        [Header("16 Sons de Morte Cômicos")]
        [SerializeField] private List<AudioClip> commonDeathSounds;

        private bool isMuted = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null) return;
            bgmSource.clip = clip;
            bgmSource.loop = true;
            if (!isMuted) bgmSource.Play();
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
            bgmSource.mute = isMuted;
            sfxSource.mute = isMuted;
        }

        public bool IsMuted => isMuted;

        public void PlaySFX(AudioClip clip, float volume = 1f)
        {
            if (clip == null || isMuted) return;
            sfxSource.PlayOneShot(clip, volume);
        }

        public void PlayRandomDeathSound()
        {
            if (commonDeathSounds == null || commonDeathSounds.Count == 0 || isMuted) return;
            int idx = Random.Range(0, commonDeathSounds.Count);
            PlaySFX(commonDeathSounds[idx]);
        }
    }
}
```

---

### 4.7 `Scripts/Core/GameManager.cs`
```csharp
using System.Collections;
using UnityEngine;
using WizardGame.Entities;
using WizardGame.Input;
using WizardGame.UI;

namespace WizardGame.Core
{
    public class GameManager : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private WizardSpawner spawner;
        [SerializeField] private PlayerInputHandler inputHandler;
        [SerializeField] private UIManager uiManager;

        [Header("Regras de Pontuação")]
        [SerializeField] private int winTarget = 50;
        [SerializeField] private int escapeLimit = 15;
        [SerializeField] private float strongShotCooldown = 3.0f;

        private int currentScore = 0;
        private int currentEscaped = 0;
        private bool isStrongReady = true;
        private bool isGameOver = false;

        private void Start()
        {
            inputHandler.OnNormalShot += HandleNormalShot;
            inputHandler.OnStrongShot += HandleStrongShot;

            spawner.OnWizardDefeated += HandleWizardDefeated;
            spawner.OnWizardEscaped += HandleWizardEscaped;

            StartGame();
        }

        public void StartGame()
        {
            currentScore = 0;
            currentEscaped = 0;
            isGameOver = false;
            isStrongReady = true;

            uiManager.UpdateScore(currentScore);
            uiManager.UpdateEscapes(currentEscaped, escapeLimit);
            uiManager.UpdateStrongCooldown(true);

            spawner.StartSpawning();
        }

        private void HandleNormalShot(WizardController target)
        {
            if (isGameOver) return;
            SoundManager.Instance.PlaySFX(SoundManager.Instance.normalShotSound);

            if (target != null)
            {
                target.TakeDamage(1);
            }
        }

        private void HandleStrongShot(WizardController target)
        {
            if (isGameOver || !isStrongReady) return;

            isStrongReady = false;
            uiManager.UpdateStrongCooldown(false);
            SoundManager.Instance.PlaySFX(SoundManager.Instance.strongShotSound);

            if (target != null)
            {
                target.TakeDamage(3);
            }

            StartCoroutine(StrongCooldownRoutine());
        }

        private IEnumerator StrongCooldownRoutine()
        {
            yield return new WaitForSeconds(strongShotCooldown);
            isStrongReady = true;
            uiManager.UpdateStrongCooldown(true);
        }

        private void HandleWizardDefeated(WizardController wizard, int points)
        {
            if (isGameOver) return;

            currentScore += points;
            uiManager.UpdateScore(currentScore);

            if (wizard.GetData().customDeathSound != null)
            {
                SoundManager.Instance.PlaySFX(wizard.GetData().customDeathSound);
            }
            else
            {
                SoundManager.Instance.PlayRandomDeathSound();
            }

            if (currentScore >= winTarget)
            {
                EndGame(true);
            }
        }

        private void HandleWizardEscaped(WizardController wizard, int penalty)
        {
            if (isGameOver) return;

            currentEscaped += penalty;
            uiManager.UpdateEscapes(currentEscaped, escapeLimit);

            if (wizard.GetData().escapeSound != null)
            {
                SoundManager.Instance.PlaySFX(wizard.GetData().escapeSound);
            }

            if (currentEscaped >= escapeLimit)
            {
                EndGame(false);
            }
        }

        private void EndGame(bool won)
        {
            isGameOver = true;
            spawner.StopSpawning();

            if (won)
            {
                SoundManager.Instance.PlaySFX(SoundManager.Instance.victorySound);
                uiManager.ShowVictoryScreen(currentScore);
            }
            else
            {
                SoundManager.Instance.PlaySFX(SoundManager.Instance.defeatSound);
                uiManager.ShowDefeatScreen(currentEscaped);
            }
        }

        public void RestartGame()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
```

---

### 4.8 `Scripts/UI/UIManager.cs`
```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WizardGame.Core;
using WizardGame.Entities;

namespace WizardGame.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI escapeText;
        [SerializeField] private TextMeshProUGUI strongShotText;
        [SerializeField] private Button muteButton;
        [SerializeField] private TextMeshProUGUI muteIconText;

        [Header("Painel de Fim de Jogo")]
        [SerializeField] private GameObject endPanel;
        [SerializeField] private TextMeshProUGUI endTitleText;
        [SerializeField] private TextMeshProUGUI endDescriptionText;
        [SerializeField] private Button restartButton;

        [Header("Painel de Balanceamento (Debug)")]
        [SerializeField] private WizardSpawner spawner;
        [SerializeField] private TextMeshProUGUI spawnDelayText;
        [SerializeField] private Button decreaseSpawnBtn;
        [SerializeField] private Button increaseSpawnBtn;

        private void Start()
        {
            muteButton.onClick.AddListener(OnMuteClicked);
            restartButton.onClick.AddListener(() => FindFirstObjectByType<GameManager>().RestartGame());

            decreaseSpawnBtn.onClick.AddListener(() => ChangeSpawnRate(0.1f));
            increaseSpawnBtn.onClick.AddListener(() => ChangeSpawnRate(-0.1f));
            UpdateSpawnRateUI();

            endPanel.SetActive(false);
        }

        public void UpdateScore(int score)
        {
            scoreText.text = $"Magos: {score}";
        }

        public void UpdateEscapes(int current, int max)
        {
            escapeText.text = $"Escaparam: {current} / {max}";
        }

        public void UpdateStrongCooldown(bool ready)
        {
            if (ready)
            {
                strongShotText.text = "Tiro Forte: PRONTO";
                strongShotText.color = Color.green;
            }
            else
            {
                strongShotText.text = "Tiro Forte: RECARREGANDO...";
                strongShotText.color = Color.red;
            }
        }

        private void OnMuteClicked()
        {
            SoundManager.Instance.ToggleMute();
            muteIconText.text = SoundManager.Instance.IsMuted ? "🔈" : "🔊";
        }

        private void ChangeSpawnRate(float delta)
        {
            float current = spawner.GetSpawnInterval();
            spawner.SetSpawnInterval(current + delta);
            UpdateSpawnRateUI();
        }

        private void UpdateSpawnRateUI()
        {
            spawnDelayText.text = $"{Mathf.RoundToInt(spawner.GetSpawnInterval() * 1000f)} ms";
        }

        public void ShowVictoryScreen(int score)
        {
            endPanel.SetActive(true);
            endTitleText.text = "VOCÊ VENCEU!";
            endDescriptionText.text = $"Total de Magos Abatidos: {score}";
        }

        public void ShowDefeatScreen(int escaped)
        {
            endPanel.SetActive(true);
            endTitleText.text = "VOCÊ PERDEU!";
            endDescriptionText.text = $"Magos que escaparam: {escaped}";
        }
    }
}
```

---

## 5. Guia Passo a Passo de Configuração no Editor da Unity 6

### Passo 1: Criação do Projeto
1. Abra o **Unity Hub** (localizado em `C:\Program Files\Unity Hub\Unity Hub.exe`).
2. Clique em **New project**.
3. Selecione a versão **6000.6.0f1**.
4. Escolha o template **2D (URP)**.
5. Nomeie o projeto como `MinigameDoMago_Unity6`.

### Passo 2: Importação de Assets
1. Copie a pasta `assets/img` para `Assets/Sprites/` no projeto Unity:
   - Selecione todas as imagens no Unity Inspector.
   - Configure **Texture Type**: `Sprite (2D and UI)`.
   - Configure **Sprite Mode**: `Single`.
   - Ajuste **Pixels Per Unit** para `100` (ou o tamanho padrão desejado).
   - Clique em **Apply**.
2. Copie a pasta `assets/audio` para `Assets/Audio/`:
   - Para efeitos curtos (`morte_comum`, `tiro`): Configure **Load Type**: `Decompress On Load`.
   - Para música de fundo (`background-music`): Configure **Load Type**: `Streaming` e **Compression Format**: `Vorbis`.

### Passo 3: Criação dos ScriptableObjects
Na pasta `Assets/ScriptableObjects/WizardData/`, clique com botão direito > **Create > WizardGame > Wizard Data**:
1. `MagoComum`: Sprite = `maguinho`, HP = 1, Pontos = 1, Escape = 5s, Peso = 65.
2. `MagoRapido`: Sprite = `maguinho2`, HP = 2, Pontos = 2, Escape = 6s, Peso = 20, Penalidade = 2, Som de Dano = `Zé-Wilker...`.
3. `MagoDourado`: Sprite = `mago-nivel-3`, HP = 3, Pontos = 5, Escape = 7s, Peso = 10, Som de Morte = `peppino-angry-scream...`.
4. `MagoFantasma`: Sprite = `mago-1-2`, HP = 4, Pontos = 3, Escape = 9s, Peso = 15, Tint = Azul suave, Som de Dano = `dbz-teleport`.

### Passo 4: Prefab do Mago
1. Crie um GameObject vazio na cena chamado `Wizard_Base`.
2. Adicione os componentes:
   - `SpriteRenderer`
   - `CircleCollider2D` (ou `BoxCollider2D`)
   - `WizardController.cs`
3. Crie uma **Layer** chamada `Wizard` e atribua ao GameObject.
4. Arraste para `Assets/Prefabs/` para gerar o Prefab e delete da cena.

### Passo 5: Configuração do Canvas (UI)
1. Crie um Canvas UI na cena.
2. No componente `Canvas Scaler`:
   - **UI Scale Mode**: `Scale With Screen Size`.
   - **Reference Resolution**: `1920 x 1080`.
   - **Match**: `0.5`.
3. Crie os elementos de texto com **TextMeshPro - Text**:
   - Pontuação (topo à esquerda).
   - Escapes (topo à esquerda, abaixo da pontuação).
   - Status do Tiro Forte (topo à direita).
   - Botão de Mute (topo à direita).
   - Painel de Balanceamento (canto inferior direito).
   - Painel de Fim de Jogo (centralizado, desativado por padrão).

---

## 6. Benefícios da Migração para Unity 6

1. **Desempenho Nativo sem Garbage Collection**: Com `UnityEngine.Pool.ObjectPool`, a taxa de quadros permanece constante a 60/120+ FPS sem engasgos de GC.
2. **Multiplataforma Pronta**: O código arquitetado roda sem alterações no Windows, WebGL, Android e iOS graças ao New Input System.
3. **Pós-processamento URP 2D**: Permite adicionar luzes 2D, partículas de impacto e Bloom cinematográfico aos efeitos de teleporte e ouro.

---

## 7. Sistema de IA com Personalidade (`AIPersonality`)

A inteligência dos inimigos foi desacoplada em arquétipos e padrões modulares no `WizardDataSO`:

### 7.1 Arquétipos Implementados
- **Goblin Comum (`CommonWanderer`)**:
  - Padrão: `Wander` (perambulação orgânica errática, paradas naturais de 0.4s a 0.9s e ataques periódicos).
  - Reação ao dano: `Jiggle` (vibração de impacto).
- **Goblin Fugitivo (`FastFleeAfterHit`)**:
  - Padrão: `Wander` acelerado enquanto não ferido.
  - Reação ao dano: `FleeAfterHit` (pulo de pânico, arranque em disparada com 2.2x de velocidade na direção oposta ao cursor/tiro com duração de 2.5s, retornando ao estado calmo caso sobreviva).
- **Goblin Dourado (`GoldenStraight`)**:
  - Padrão: `StraightMovement` (patrulha retilínea militar contínua, com reflexão/inversão nas bordas e postura marcial).
  - Reação ao dano: `DefensiveShield` (ergue escudo defensivo e absorve recuo).
- **Goblin Fantasma (`GhostTeleporter`)**:
  - Padrão: `SpectralHover` (flutuação senoidal contínua com translucidez ondulante).
  - Reação ao dano: `TeleportAfterHit` (salto dimensional instantâneo por portal para novo setor da tela).

### 7.2 Preparação Nativa para Futuras Expansões
- `Escudo / ShieldWizard` (`ProtectAlly`): Avança na direção de aliados feridos para erguer barreira protetora.
- `Curandeiro / HealerWizard` (`HealerSupport`): Mantém recuo estratégico e emite ondas restaurando vida de aliados.
- `Ilusionista / IllusionWizard` (`SpawnDecoy`): Invoca cópias ilusórias de distração.
- `Bombardeiro / BomberWizard` (`ExplodeOnDeath`): Contagem regressiva em flash e detonação arcana em área com tremor de câmera.

---

## 8. Correções Críticas, Robustez de Arquitetura e Interatividade de Projéteis dos Chefes

Após a análise detalhada e auditoria de código de ponta a ponta, todos os gargalos técnicos, bugs de transição de estado e melhorias de gameplay foram implementados:

### 8.1 Correção dos 5 Bugs Críticos
1. **Invulnerabilidade Infinita do Chefe 3 (Mago da Fumaça)**:
   - *Causa*: Quando o Boss 3 entrava em modo furtivo (`isInvulnerable = true`) e recebia um Feitiço de Gelo (`FreezeRoutine`) ou a onda era finalizada (`StopAllActiveCoroutines()`), a flag de invulnerabilidade e a transparência ficavam presas indefinidamente.
   - *Solução*: `FreezeRoutine`, `StopAllActiveCoroutines()` e `OnDisable()` em `WizardController.cs` agora forçam `isInvulnerable = false` e restauram a cor/alfa original do sprite.
2. **Desincronização de ObjectPool e Vazamento de Corrotinas de Morte**:
   - *Causa*: O método `Die()` removia prematuramente o goblin da lista `activeGoblins` enquanto a animação e corrotina de morte ainda rodavam por 1.0s. Se uma nova onda começasse ou a limpeza de onda (`ClearActiveGoblins`) ocorresse, a corrotina antiga continuava viva e chamava `OnDefeated`, computando pontos e mortes fantasma na onda seguinte.
   - *Solução*: Criado o método `CancelAndDespawnImmediately()` que desliga corrotinas ativas de morte, previne invocação fantasma de eventos e desativa o GameObject de forma atômica. `ClearActiveGoblins()` agora itera sobre todas as instâncias em campo e invoca essa limpeza imediata antes de devolver ao pool.
3. **Deriva da Câmera por Múltiplos Tremores Concorrentes (Camera Shake Drift)**:
   - *Causa*: `SpellEffectsManager.TriggerCameraShake` calculava offsets a partir da posição momentânea da câmera. Múltiplas explosões simultâneas faziam o retorno final da câmera ficar permanentemente deslocado do centro.
   - *Solução*: Ancoragem absoluta em `cameraAnchorPos = new Vector3(0f, 0f, -10f)` com cancelamento prévio da corrotina ativa (`activeShakeCoroutine`) antes de iniciar um novo tremor.
4. **Inconsistência no Cancelamento de Recarga do `WeaponSystem`**:
   - *Causa*: O cancelamento por string `StopCoroutine("ReloadRoutine")` falhava silenciosamente quando a corrotina era iniciada com parâmetros ou sob diferentes assinaturas do compilador Mono/IL2CPP.
   - *Solução*: Uso exclusivo de referência tipada `Coroutine activeReloadCoroutine`, cancelada de forma determinística em `StartReload()`, `RefillAmmo()` e `AddAmmo()`.
5. **Animações Travadas no Reuso do ObjectPool (`FrameAnimator`)**:
   - *Causa*: Se um goblin fosse devolvido ao pool no meio de uma animação travada com `isLocked = true` (ex: impacto ou ataque), ele renascia com o lock ativo ou com a corrotina anterior ainda referenciada.
   - *Solução*: Implementação do método `OnDisable()` no `FrameAnimator`, cancelando `activeAnimCoroutine`, destravando `isLocked = false` e resetando o estado para `AnimationState.Idle`.

### 8.2 Interatividade e Gameplay Ativo
1. **Interceptação de Projéteis dos Chefes (Mechanic: Spell Interception)**:
   - Projéteis disparados pelos Chefes 2, 3, 4 e Final agora recebem a classe componente `BossProjectileInstance` com `CircleCollider2D` na layer `Wizard`.
   - O jogador pode clicar diretamente na bola de fogo ou projétil para destruí-lo no ar, ganhando bônus de habilidade (+5 pontos de precisão e reflexo) e evitando sofrer dano à barra de vida.
   - Caso o projétil atinja o jogador (alcance da tela), ele causa dano real com base no chefe respectivo (1 a 3 pontos de dano) e som de impacto.
2. **Feedback Fiel de Feitiços e Prevenção de Falsos Hitmarkers**:
   - `PlayerInputHandler` não dispara mais som de acerto ou animação de hitmarker na tela quando o feitiço está em cooldown ou sem mana/mágica. A confirmação de acerto é concedida exclusivamente após autorização e validação de dano pelo `GameManager`.
3. **Efeito Sonoro de Disparo Vazio (Dry-Fire SFX)**:
   - Quando o jogador tenta atirar sem munição suficiente ou feitiço em recarga, é emitido um estalo sonoro sutil de disparo vazio (`SoundManager.PlayDryFireSound()`), guiando a intuição do jogador para recarregar ou trocar de estratégia.
4. **Sincronização Física em Câmera Lenta**:
   - Durante efeitos de desaceleração temporal (Power-up *Tempo Arcano*), `Time.fixedDeltaTime` é dinamicamente sincronizado com `0.02f * Time.timeScale` em todos os estados de transição do jogo, eliminando oscilações visuais e interpolação engasgada de física 2D.
5. **Zero-Allocation Garbage Collection em Feitiços de Área**:
   - Pré-alocação e reutilização contínua do buffer `cachedAreaTargets` no `GameManager`, eliminando coletas periódicas de lixo (GC spikes) durante tempestades mágicas ou explosões com dezenas de goblins em cena.
6. **Interface Dinâmica de Condição de Derrota**:
   - A tela de derrota do `UIManager` agora exibe dinamicamente o número máximo de fugitivos permitidos da partida (`escapedCount / maxEscapes`), respeitando customizações e modificadores de dificuldade.

---

## 9. Fases por Chefe, Transição Cinematográfica, Balanceamento Dinâmico e Profundidade 2.5D

### 9.1 Uma Fase Dedicada por Chefe (`StageEnvironmentManager`)
Cada um dos 5 grandes Chefes agora comanda sua própria fase com bioma, partículas ambientais, iluminação e ritmo progressivo configurados diretamente no `WaveConfig`:
1. **Fase 1 — Muralhas da Fortaleza** (`cenario1.png`): Poeira lunar fria; foco em Comuns e Fugitivos; Chefe: **👑 Grande Goblin** (20 HP).
2. **Fase 2 — Ruínas Iluminadas pela Lua** (`Ruínas Iluminadas pela Lua.png`): Vaga-lumes místicos; introdução do Goblin Dourado e spawns duplos (18%); Chefe: **🔥 Xamã do Caos** (30 HP).
3. **Fase 3 — Fortaleza Vulcânica** (`Fortaleza Vulcânica da Fase 3.png`): Brasas vulcânicas ascendentes; estreia do Goblin Fantasma e ritmo acelerado (28% spawn duplo); Chefe: **🔮 Feiticeiro Espectral** (42 HP).
4. **Fase 4 — Salão do Trono em Ruínas** (`Salão do Trono em Ruínas.png`, atmosfera sombria violeta): Faíscas arcanas; pressão contínua (38% spawn duplo, máx. 7 simultâneos); Chefe: **🩸 Lorde Carmesim** (56 HP).
5. **Fase 5 — Altar do Rei Supremo** (`Salão do Trono em Ruínas.png`, iluminação rubra e zoom dramático `1.12x`): Brasas de magma intensas; elite pura sem Goblins Comuns (48% spawn duplo, velocidade `1.50x`); Chefe Final: **⚔️ Goblin Rei Supremo** (85 HP).

### 9.2 Transição Cinematográfica de Mudança de Fase
- Quando todos os lacaios de uma fase são abatidos/escapam, o Chefe realiza sua entrada triunfal sozinho na arena (`WizardSpawner`).
- Ao derrotar o Chefe atual, eventuais reforços invocados e projéteis são limpos instantaneamente, o jogador recebe o bônus da fase, **Mana 100% recarregada** e **+1 ❤️ de Vida recuperado**.
- Em seguida, `UIManager.PlayStageTransitionRoutine` exibe a cortina de transição (*Fade In → Troca de Cenário e Bioma em `StageEnvironmentManager` → Fade Out*), revelando a nova fase.

### 9.3 Combate Justo, Dinâmico e Balanceamento
- **Fim do Dano Inevitável de Chefes**: Golpes pesados (`Golpe Sísmico`, `Labareda`, `Golpe de Cetro`, `Terremoto`, `Choque de Magma`) agora exibem um **anel telegrafado de aviso** (`ExecuteTelegraphedSlamRoutine`), podendo ser **interrompidos** com qualquer Feitiço Especial ou 2 acertos rápidos (**+8 pts CONTRA-ATAQUE!**).
- **Orbes com Aproximação 2.5D**: Projéteis de chefes crescem de escala (`0.35x → 1.15x`) enquanto voam na direção da câmera/jogador, permitindo leitura clara e interceptação com clique (**+5 pts**).
- **Fúria dos Chefes (50% HP)**: Ao atingir metade da vida, Chefes ganham `+24%` de velocidade e invocam até 2 reforços para manter a pressão.
- **Arsenal & Economia Calibrados**:
  - Mana máxima aumentada para **10 disparos** com recarga rápida de **1.0s** e cadência anti-macro de `0.11s`.
  - **Arcano**: 4 Dano (`2.6s` CD) • **Gelo**: 1 Dano + `2.8s` congelamento (`3.8s` CD) • **Relâmpago**: 2 Dano em até 4 alvos (`4.2s` CD) • **Área**: 3 Dano em raio `2.8m` (`5.0s` CD).
  - Power-up **Tempestade** agora também aplica 2 de dano em goblins de elite e 3 em chefes.

### 9.4 Profundidade 2.5D por Eixo Y
- Em `WizardController.ApplyDepthSortingAndScale()`, o `sortingOrder` e a escala de cada goblin variam dinamicamente conforme sua posição `Y` na tela (`1.07x` próximo à borda inferior, `0.89x` próximo ao fundo), preparando a arquitetura diretamente para oclusão atrás de objetos recortados em camadas.
