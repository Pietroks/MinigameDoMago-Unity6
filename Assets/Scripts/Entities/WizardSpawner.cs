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
        private float spawnTimer;
        private bool isSpawning;

        // Controle de Ondas
        private readonly Dictionary<WizardType, WizardDataSO> typeLookup = new Dictionary<WizardType, WizardDataSO>();
        private readonly Queue<WizardDataSO> waveSpawnQueue = new Queue<WizardDataSO>();
        private WaveConfig currentWaveConfig;
        private int currentWaveNumber = 1;
        private int totalEnemiesInWave;
        private int activeGoblinsInWave;
        private float currentWaveSpeedMultiplier = 1.0f;
        private bool isSpawningWave;

        public event Action<WizardController, int> OnWizardDefeated;
        public event Action<WizardController, int> OnWizardEscaped;
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

            float vertExtent = mainCam.orthographicSize;
            float horzExtent = vertExtent * (float)Screen.width / Screen.height;
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
            spawnInterval = config != null ? Mathf.Max(0.2f, config.spawnInterval) : 1.5f;

            waveSpawnQueue.Clear();
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
            spawnTimer = 0.2f;

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
            var activeList = new List<WizardController>(WizardController.ActiveGoblins);
            foreach (var goblin in activeList)
            {
                if (goblin != null && goblin.gameObject.activeSelf)
                {
                    UnregisterAndRelease(goblin);
                }
            }
            activeGoblinsInWave = 0;
        }

        private void Update()
        {
            if (!isSpawning) return;

            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;

                if (isSpawningWave)
                {
                    if (waveSpawnQueue.Count > 0)
                    {
                        WizardDataSO data = waveSpawnQueue.Dequeue();
                        SpawnWizard(data, currentWaveSpeedMultiplier);
                        activeGoblinsInWave++;
                    }
                }
                else
                {
                    SpawnRandomWizard();
                }
            }
        }

        private void SpawnWizard(WizardDataSO data, float speedMultiplier)
        {
            if (data == null) return;

            Vector3 spawnPos = GetRandomWorldPoint();
            WizardController wizard = pool.Get();

            wizard.Initialize(data, spawnPos, screenBounds, speedMultiplier);
            wizard.OnDefeated += HandleDefeated;
            wizard.OnEscaped += HandleEscaped;
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
            UnregisterAndRelease(wizard);
            if (isSpawningWave)
            {
                activeGoblinsInWave = Mathf.Max(0, activeGoblinsInWave - 1);
                int remaining = waveSpawnQueue.Count + activeGoblinsInWave;
                OnWaveProgressChanged?.Invoke(remaining, totalEnemiesInWave);

                if (waveSpawnQueue.Count == 0 && activeGoblinsInWave == 0)
                {
                    isSpawning = false;
                    isSpawningWave = false;
                    OnWaveCompleted?.Invoke(currentWaveNumber);
                }
            }
            OnWizardDefeated?.Invoke(wizard, points);
        }

        private void HandleEscaped(WizardController wizard, int penalty)
        {
            UnregisterAndRelease(wizard);
            if (isSpawningWave)
            {
                activeGoblinsInWave = Mathf.Max(0, activeGoblinsInWave - 1);
                int remaining = waveSpawnQueue.Count + activeGoblinsInWave;
                OnWaveProgressChanged?.Invoke(remaining, totalEnemiesInWave);

                if (waveSpawnQueue.Count == 0 && activeGoblinsInWave == 0)
                {
                    isSpawning = false;
                    isSpawningWave = false;
                    OnWaveCompleted?.Invoke(currentWaveNumber);
                }
            }
            OnWizardEscaped?.Invoke(wizard, penalty);
        }

        private void UnregisterAndRelease(WizardController wizard)
        {
            wizard.OnDefeated -= HandleDefeated;
            wizard.OnEscaped -= HandleEscaped;
            pool.Release(wizard);
        }

        public void SetSpawnInterval(float seconds)
        {
            spawnInterval = Mathf.Clamp(seconds, 0.2f, 3.0f);
        }

        public float GetSpawnInterval() => spawnInterval;
    }
}
