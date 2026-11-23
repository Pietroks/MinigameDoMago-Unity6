using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using WizardGame.Data;

namespace WizardGame.Entities
{
    /// <summary>
    /// Gerencia o spawn ponderado, o Object Pool de alta performance na Unity 6 e o
    /// Sistema de Ondas táticas com progressão de velocidade, quantidade e novos inimigos.
    /// </summary>
    public class WizardSpawner : MonoBehaviour
    {
        [Header("Prefab e Configurações de Pool")]
        [SerializeField] private WizardController wizardPrefab;
        [SerializeField] private int initialPoolCapacity = 30;
        [SerializeField] private int maxPoolCapacity = 60;

        [Header("Tipos de Magos/Goblins Disponíveis")]
        [SerializeField] private List<WizardDataSO> wizardTypes = new List<WizardDataSO>();

        [Header("Parâmetros de Spawn")]
        [SerializeField] private float spawnInterval = 1.5f;

        private ObjectPool<WizardController> pool;
        private Camera mainCam;
        private Bounds screenBounds;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private float spawnTimer;
        private bool isSpawning;

        // Controle de Ondas e Progressão Dinâmica
        private readonly Dictionary<WizardType, WizardDataSO> typeLookup = new Dictionary<WizardType, WizardDataSO>();
        private readonly Queue<WizardDataSO> waveSpawnQueue = new Queue<WizardDataSO>();
        private readonly HashSet<WizardController> bossSummonedGoblins = new HashSet<WizardController>();
        private WaveConfig currentWaveConfig;
        private int currentWaveNumber = 1;
        private int totalEnemiesInWave;
        private int activeGoblinsInWave;
        private float currentWaveSpeedMultiplier = 1.0f;
        private float currentWaveFugitiveSpeedMultiplier = 0.62f;
        private float currentWaveEscapeMultiplier = 1.0f;
        private int currentMaxConcurrent = 5;
        private float currentDoubleSpawnChance = 0f;
        private float nextSpawnDelay = 1.0f;
        private bool isSpawningWave;

        public event Action<WizardController, int> OnWizardDefeated;
        public event Action<WizardController, int> OnWizardEscaped;
        public event Action<WizardController, int, string> OnPlayerDamaged;
        public event Action<WizardController, WizardDataSO> OnBossSpawned;
        public event Action<WizardController, WizardDataSO> OnBossEnraged;
        public event Action<WizardController, int, int, bool> OnBossHealthChanged;
        public event Action<WizardController, string> OnBossAttackInterrupted;
        public event Action<int, int> OnWaveProgressChanged; // (restantes, total)
        public event Action<int> OnWaveCompleted; // (numeroDaOnda)

        public int CurrentWaveNumber => currentWaveNumber;
        public int TotalEnemiesInWave => totalEnemiesInWave;
        public int RemainingEnemiesInWave => waveSpawnQueue.Count + activeGoblinsInWave;

        private void Awake()
        {
            mainCam = Camera.main;
            RecalculateBounds();
            BuildTypeLookup();

            pool = new ObjectPool<WizardController>(
                createFunc: () => {
                    var w = Instantiate(wizardPrefab, transform);
                    w.gameObject.SetActive(false);
                    return w;
                },
                actionOnGet: w => w.gameObject.SetActive(true),
                actionOnRelease: w => w.gameObject.SetActive(false),
                actionOnDestroy: w => Destroy(w.gameObject),
                collectionCheck: false,
                defaultCapacity: initialPoolCapacity,
                maxSize: maxPoolCapacity
            );
        }

        private void BuildTypeLookup()
        {
            typeLookup.Clear();
            if (wizardTypes != null)
            {
                foreach (var data in wizardTypes)
                {
                    if (data != null && !typeLookup.ContainsKey(data.wizardType))
                    {
                        typeLookup.Add(data.wizardType, data);
                    }
                }
            }
        }

        public void RecalculateBounds()
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            float vertExtent = mainCam.orthographicSize;
            float horzExtent = vertExtent * (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            Vector3 camPos = mainCam.transform.position;
            screenBounds = new Bounds(new Vector3(camPos.x, camPos.y, 0f), new Vector3(horzExtent * 2f, vertExtent * 2f, 2f));
        }

        /// <summary>
        /// Inicia uma onda estruturada com a configuração e multiplicador de velocidade fornecidos.
        /// </summary>
        public void StartWave(WaveConfig config, int waveNumber)
        {
            BuildTypeLookup();
            RecalculateBounds();

            currentWaveConfig = config;
            currentWaveNumber = waveNumber;
            currentWaveSpeedMultiplier = config != null ? Mathf.Max(0.5f, config.speedMultiplier) : 1.0f;
            currentWaveFugitiveSpeedMultiplier = (config != null && config.fugitiveSpeedMultiplier > 0.01f)
                ? config.fugitiveSpeedMultiplier
                : WizardController.GetProgressiveFugitiveSpeedMultiplier(waveNumber);
            currentWaveEscapeMultiplier = config != null && config.escapeTimerMultiplier > 0f ? config.escapeTimerMultiplier : 1.0f;
            currentMaxConcurrent = config != null ? Mathf.Max(2, config.maxConcurrentEnemies) : 5;
            currentDoubleSpawnChance = config != null ? Mathf.Clamp01(config.doubleSpawnChance) : 0f;
            spawnInterval = config != null ? Mathf.Max(0.2f, config.spawnInterval) : 1.5f;
            nextSpawnDelay = 0.4f;

            waveSpawnQueue.Clear();
            bossSummonedGoblins.Clear();
            if (config != null)
            {
                List<WizardType> typesToSpawn = config.GenerateShuffledEnemyList();
                foreach (var t in typesToSpawn)
                {
                    if (typeLookup.TryGetValue(t, out var data))
                    {
                        waveSpawnQueue.Enqueue(data);
                    }
                    else if (wizardTypes != null && wizardTypes.Count > 0)
                    {
                        waveSpawnQueue.Enqueue(wizardTypes[0]);
                    }
                }
            }

            totalEnemiesInWave = waveSpawnQueue.Count;
            activeGoblinsInWave = 0;
            isSpawningWave = true;
            isSpawning = true;
            spawnTimer = 0f;

            OnWaveProgressChanged?.Invoke(totalEnemiesInWave, totalEnemiesInWave);
        }

        /// <summary>
        /// Modo de spawn contínuo legado / fallback.
        /// </summary>
        public void StartSpawning()
        {
            RecalculateBounds();
            BuildTypeLookup();
            isSpawningWave = false;
            isSpawning = true;
            spawnTimer = 0f;
        }

        public void StopSpawning()
        {
            isSpawning = false;
            isSpawningWave = false;
            if (waveSpawnQueue != null) waveSpawnQueue.Clear();
        }

        public void ClearActiveGoblins()
        {
            StopSpawning();
            WizardController.ClearAllBossProjectiles();
            bossSummonedGoblins.Clear();
            var activeList = new List<WizardController>(WizardController.ActiveGoblins);
            foreach (var goblin in activeList)
            {
                if (goblin != null)
                {
                    goblin.CancelAndDespawnImmediately();
                    UnregisterAndRelease(goblin);
                }
            }
            activeGoblinsInWave = 0;
        }

        private void Update()
        {
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                RecalculateBounds();
            }

            if (!isSpawning) return;

            if (isSpawningWave)
            {
                if (waveSpawnQueue.Count == 0) return;

                WizardDataSO nextData = waveSpawnQueue.Peek();
                bool nextIsBoss = nextData != null && WaveConfig.IsBossType(nextData.wizardType);

                // Chefe aguarda os lacaios da fase terminarem para fazer sua entrada triunfal!
                if (nextIsBoss && activeGoblinsInWave > 0)
                {
                    return;
                }

                // Controle de superlotação + aceleração se a arena estiver vazia
                if (!nextIsBoss && activeGoblinsInWave >= currentMaxConcurrent)
                {
                    return;
                }

                float timerRate = (!nextIsBoss && activeGoblinsInWave == 0) ? 1.85f : 1.0f;
                spawnTimer += Time.deltaTime * timerRate;

                if (spawnTimer >= nextSpawnDelay)
                {
                    spawnTimer = 0f;
                    nextSpawnDelay = spawnInterval * UnityEngine.Random.Range(0.78f, 1.15f);

                    WizardDataSO data = waveSpawnQueue.Dequeue();
                    Vector3 firstPos = GetRandomWorldPoint();
                    SpawnWizard(data, currentWaveSpeedMultiplier, currentWaveEscapeMultiplier, firstPos);
                    activeGoblinsInWave++;

                    // Surto dinâmico em ondas mais avançadas: chance de spawn duplo simultâneo de lacaios!
                    if (!nextIsBoss &&
                        waveSpawnQueue.Count > 0 &&
                        !WaveConfig.IsBossType(waveSpawnQueue.Peek().wizardType) &&
                        activeGoblinsInWave < currentMaxConcurrent &&
                        UnityEngine.Random.value < currentDoubleSpawnChance)
                    {
                        WizardDataSO secondData = waveSpawnQueue.Dequeue();
                        Vector3 secondPos = GetRandomWorldPoint();
                        // Separa horizontalmente do primeiro spawn para visual limpo
                        if (Mathf.Abs(secondPos.x - firstPos.x) < 2.2f)
                        {
                            secondPos.x = (firstPos.x >= screenBounds.center.x)
                                ? UnityEngine.Random.Range(screenBounds.min.x + 1.3f, screenBounds.center.x - 0.8f)
                                : UnityEngine.Random.Range(screenBounds.center.x + 0.8f, screenBounds.max.x - 1.3f);
                        }
                        SpawnWizard(secondData, currentWaveSpeedMultiplier, currentWaveEscapeMultiplier, secondPos);
                        activeGoblinsInWave++;
                    }
                }
            }
            else
            {
                spawnTimer += Time.deltaTime;
                if (spawnTimer >= spawnInterval)
                {
                    spawnTimer = 0f;
                    SpawnRandomWizard();
                }
            }
        }

        private void SpawnWizard(WizardDataSO data, float speedMultiplier, float escapeMult = 1.0f, Vector3? customPos = null, bool isBossSummon = false)
        {
            if (data == null) return;

            Vector3 spawnPos = customPos ?? GetRandomWorldPoint();
            WizardController wizard = pool.Get();

            wizard.Initialize(
                data,
                spawnPos,
                screenBounds,
                speedMultiplier,
                escapeMult,
                currentWaveNumber,
                currentWaveFugitiveSpeedMultiplier);
            wizard.OnDefeated += HandleDefeated;
            wizard.OnEscaped += HandleEscaped;
            wizard.OnAttackPlayer += HandleAttackPlayer;
            wizard.OnBossSummonRequested += HandleBossSummonRequested;
            wizard.OnBossAttackInterrupted += HandleBossAttackInterrupted;
            wizard.OnBossEnraged += HandleBossEnraged;
            wizard.OnBossHealthChanged += HandleBossHealthChanged;

            if (isBossSummon)
            {
                bossSummonedGoblins.Add(wizard);
            }

            if (WaveConfig.IsBossType(data.wizardType))
            {
                OnBossSpawned?.Invoke(wizard, data);
            }
        }

        private void SpawnRandomWizard()
        {
            if (wizardTypes == null || wizardTypes.Count == 0) return;

            WizardDataSO selected = PickWeightedRandom();
            if (selected == null) return;

            SpawnWizard(selected, 1.0f);
        }

        private WizardDataSO PickWeightedRandom()
        {
            int totalWeight = 0;
            for (int i = 0; i < wizardTypes.Count; i++)
            {
                if (wizardTypes[i] != null) totalWeight += wizardTypes[i].spawnWeight;
            }

            if (totalWeight <= 0) return wizardTypes[0];

            int rnd = UnityEngine.Random.Range(1, totalWeight + 1);
            int accum = 0;

            for (int i = 0; i < wizardTypes.Count; i++)
            {
                if (wizardTypes[i] == null) continue;
                accum += wizardTypes[i].spawnWeight;
                if (rnd <= accum) return wizardTypes[i];
            }

            return wizardTypes[0];
        }

        private Vector3 GetRandomWorldPoint()
        {
            float marginX = 1.2f;
            float rx = UnityEngine.Random.Range(screenBounds.min.x + marginX, screenBounds.max.x - marginX);
            // Área jogável adaptada ao cenário: evita sobreposição com o TopBar (topo) e Barra de Feitiços (base)
            float minY = screenBounds.min.y + 1.4f;
            float maxY = screenBounds.max.y - 1.4f;
            float ry = UnityEngine.Random.Range(minY, maxY);
            return new Vector3(rx, ry, 0f);
        }

        private void HandleDefeated(WizardController wizard, int points)
        {
            bool wasBossSummon = bossSummonedGoblins.Remove(wizard);
            bool isBoss = wizard != null && wizard.GetData() != null && WaveConfig.IsBossType(wizard.GetData().wizardType);

            UnregisterAndRelease(wizard);
            OnWizardDefeated?.Invoke(wizard, points);

            if (isSpawningWave && !wasBossSummon)
            {
                activeGoblinsInWave = Mathf.Max(0, activeGoblinsInWave - 1);
                bool stageCleared = isBoss || (waveSpawnQueue.Count == 0 && activeGoblinsInWave == 0);

                if (stageCleared)
                {
                    if (isBoss)
                    {
                        ClearBossSummonedReinforcements();
                        WizardController.ClearAllBossProjectiles();
                        waveSpawnQueue.Clear();
                        activeGoblinsInWave = 0;
                    }

                    isSpawning = false;
                    isSpawningWave = false;
                    OnWaveProgressChanged?.Invoke(0, totalEnemiesInWave);
                    OnWaveCompleted?.Invoke(currentWaveNumber);
                }
                else
                {
                    int remaining = waveSpawnQueue.Count + activeGoblinsInWave;
                    OnWaveProgressChanged?.Invoke(remaining, totalEnemiesInWave);
                }
            }
        }

        private void HandleEscaped(WizardController wizard, int penalty)
        {
            bool wasBossSummon = bossSummonedGoblins.Remove(wizard);
            UnregisterAndRelease(wizard);

            bool waveStillActive = isSpawningWave;
            if (waveStillActive && !wasBossSummon)
            {
                activeGoblinsInWave = Mathf.Max(0, activeGoblinsInWave - 1);
                int remaining = waveSpawnQueue.Count + activeGoblinsInWave;
                OnWaveProgressChanged?.Invoke(remaining, totalEnemiesInWave);
            }

            // Dispara fuga antes de concluir a onda: se esta fuga causar Game Over (15/15 ou dano letal),
            // ClearActiveGoblins() zera isSpawningWave e impede transição falsa de fase.
            OnWizardEscaped?.Invoke(wizard, penalty);

            if (isSpawningWave && waveStillActive && !wasBossSummon && waveSpawnQueue.Count == 0 && activeGoblinsInWave == 0)
            {
                isSpawning = false;
                isSpawningWave = false;
                OnWaveCompleted?.Invoke(currentWaveNumber);
            }
        }

        private void ClearBossSummonedReinforcements()
        {
            if (bossSummonedGoblins.Count == 0) return;
            var toClear = new List<WizardController>(bossSummonedGoblins);
            bossSummonedGoblins.Clear();
            foreach (var summoned in toClear)
            {
                if (summoned != null && summoned.gameObject.activeInHierarchy)
                {
                    summoned.CancelAndDespawnImmediately();
                    UnregisterAndRelease(summoned);
                }
            }
        }

        private void HandleBossSummonRequested(WizardController boss, WizardType summonType)
        {
            if (!isSpawningWave || boss == null || bossSummonedGoblins.Count >= 2) return;

            if (!typeLookup.TryGetValue(summonType, out var data))
            {
                if (wizardTypes != null && wizardTypes.Count > 0) data = wizardTypes[0];
            }
            if (data == null) return;

            Vector3 offset = (Vector3)(UnityEngine.Random.insideUnitCircle.normalized * 1.45f);
            Vector3 summonPos = new Vector3(
                Mathf.Clamp(boss.transform.position.x + offset.x, screenBounds.min.x + 1.2f, screenBounds.max.x - 1.2f),
                Mathf.Clamp(boss.transform.position.y + offset.y, screenBounds.min.y + 1.5f, screenBounds.max.y - 1.5f),
                0f
            );

            SpawnWizard(data, currentWaveSpeedMultiplier, currentWaveEscapeMultiplier, summonPos, isBossSummon: true);
        }

        private void HandleBossAttackInterrupted(WizardController boss, string bossName)
        {
            OnBossAttackInterrupted?.Invoke(boss, bossName);
        }

        private void HandleBossEnraged(WizardController boss, WizardDataSO data)
        {
            OnBossEnraged?.Invoke(boss, data);
        }

        private void HandleBossHealthChanged(WizardController boss, int currentHp, int maxHp, bool enraged)
        {
            OnBossHealthChanged?.Invoke(boss, currentHp, maxHp, enraged);
        }

        private void HandleAttackPlayer(WizardController wizard, int damage, string source)
        {
            OnPlayerDamaged?.Invoke(wizard, damage, source);
        }

        private void UnregisterAndRelease(WizardController wizard)
        {
            if (wizard == null) return;
            wizard.OnDefeated -= HandleDefeated;
            wizard.OnEscaped -= HandleEscaped;
            wizard.OnAttackPlayer -= HandleAttackPlayer;
            wizard.OnBossSummonRequested -= HandleBossSummonRequested;
            wizard.OnBossAttackInterrupted -= HandleBossAttackInterrupted;
            wizard.OnBossEnraged -= HandleBossEnraged;
            wizard.OnBossHealthChanged -= HandleBossHealthChanged;
            pool.Release(wizard);
        }

        public void SetSpawnInterval(float seconds)
        {
            spawnInterval = Mathf.Clamp(seconds, 0.2f, 3.0f);
        }

        public float GetSpawnInterval() => spawnInterval;
    }
}
