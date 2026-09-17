using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using WizardGame.Data;

namespace WizardGame.Entities
{
    /// <summary>
    /// Gerencia o spawn ponderado e o Object Pool de alta performance na Unity 6.
    /// Evita instanciar/destruir objetos em tempo de execução, garantindo zero alocação de GC.
    /// </summary>
    public class WizardSpawner : MonoBehaviour
    {
        [Header("Prefab e Configurações de Pool")]
        [SerializeField] private WizardController wizardPrefab;
        [SerializeField] private int initialPoolCapacity = 30;
        [SerializeField] private int maxPoolCapacity = 60;

        [Header("Tipos de Magos Disponíveis")]
        [SerializeField] private List<WizardDataSO> wizardTypes = new List<WizardDataSO>();

        [Header("Parâmetros de Spawn")]
        [SerializeField] private float spawnInterval = 1.5f;

        private ObjectPool<WizardController> pool;
        private Camera mainCam;
        private Bounds screenBounds;
        private float spawnTimer;
        private bool isSpawning;

        public event Action<WizardController, int> OnWizardDefeated;
        public event Action<WizardController, int> OnWizardEscaped;

        private void Awake()
        {
            mainCam = Camera.main;
            RecalculateBounds();

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

        public void RecalculateBounds()
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            float vertExtent = mainCam.orthographicSize;
            float horzExtent = vertExtent * (float)Screen.width / Screen.height;
            Vector3 camPos = mainCam.transform.position;
            screenBounds = new Bounds(new Vector3(camPos.x, camPos.y, 0f), new Vector3(horzExtent * 2f, vertExtent * 2f, 2f));
        }

        public void StartSpawning()
        {
            RecalculateBounds();
            isSpawning = true;
            spawnTimer = 0f;
        }

        public void StopSpawning()
        {
            isSpawning = false;
        }

        private void Update()
        {
            if (!isSpawning) return;

            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                SpawnWizard();
            }
        }

        private void SpawnWizard()
        {
            if (wizardTypes == null || wizardTypes.Count == 0) return;

            WizardDataSO selected = PickWeightedRandom();
            if (selected == null) return;

            Vector3 spawnPos = GetRandomWorldPoint();
            WizardController wizard = pool.Get();

            wizard.Initialize(selected, spawnPos, screenBounds);
            wizard.OnDefeated += HandleDefeated;
            wizard.OnEscaped += HandleEscaped;
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
            OnWizardDefeated?.Invoke(wizard, points);
        }

        private void HandleEscaped(WizardController wizard, int penalty)
        {
            UnregisterAndRelease(wizard);
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
            spawnInterval = Mathf.Clamp(seconds, 0.3f, 3.0f);
        }

        public float GetSpawnInterval() => spawnInterval;
    }
}
