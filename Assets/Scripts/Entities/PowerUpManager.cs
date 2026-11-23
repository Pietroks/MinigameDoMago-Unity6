using System;
using System.Collections.Generic;
using UnityEngine;
using WizardGame.Core;
using WizardGame.Data;
using WizardGame.UI;

namespace WizardGame.Entities
{
    /// <summary>
    /// Orquestrador central dos Power-ups especiais:
    /// - Controla o surgimento raro e orgânico dos orbes flutuantes (24s - 38s).
    /// - Gerencia os buffs ativos (Mana, Fúria Arcana, Tempo Arcano, Tempestade, Precisão).
    /// - Gerencia o Slow Motion (Time.timeScale) de forma segura.
    /// </summary>
    public class PowerUpManager : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private WeaponSystem weaponSystem;
        [SerializeField] private UIManager uiManager;

        [Header("Cadência Rara de Spawn (Eventos Especiais)")]
        [SerializeField] private float minSpawnInterval = 14.0f;
        [SerializeField] private float maxSpawnInterval = 22.0f;

        [Header("Duração dos Buffs")]
        [SerializeField] private float arcaneFuryDuration = 5.0f;
        [SerializeField] private float arcaneTimeDuration = 3.0f;
        [SerializeField] private float precisionDuration = 5.0f;

        private Camera mainCam;
        private Bounds arenaBounds;
        private float spawnTimer;
        private float currentTargetInterval;
        private bool isSpawningEnabled = false;

        private PowerUpItem currentActiveItem;

        // Estados dos Buffs
        private float arcaneFuryTimer = 0f;
        private float arcaneTimeTimer = 0f;
        private float precisionTimer = 0f;
        private bool isStormCharged = false;
        private bool isSlowMotionActive = false;

        public bool IsArcaneFuryActive => arcaneFuryTimer > 0f;
        public bool IsArcaneTimeActive => arcaneTimeTimer > 0f;
        public bool IsPrecisionActive => precisionTimer > 0f;
        public bool IsStormCharged => isStormCharged;

        public event Action<PowerUpType> OnPowerUpActivated;

        private void Awake()
        {
            mainCam = Camera.main;
            RecalculateBounds();
            ScheduleNextSpawn();
        }

        public void SetSpawningEnabled(bool enabled)
        {
            isSpawningEnabled = enabled;
            if (!enabled)
            {
                ClearActiveFloatingItem();
            }
        }

        public void RecalculateBounds()
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            float vertExtent = mainCam.orthographicSize;
            float horzExtent = vertExtent * (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            Vector3 camPos = mainCam.transform.position;
            arenaBounds = new Bounds(new Vector3(camPos.x, camPos.y, 0f), new Vector3(horzExtent * 2f, vertExtent * 2f, 2f));
        }

        private void ScheduleNextSpawn()
        {
            spawnTimer = 0f;
            currentTargetInterval = UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval);
        }

        private void Update()
        {
            // Não consome duração de buffs nem spawna itens enquanto o jogo estiver pausado (timeScale == 0)
            if (Time.timeScale <= 0.0001f) return;

            // 1. Atualização dos Buffs Ativos (usando unscaledDeltaTime para garantir duração real em slow motion)
            UpdateActiveBuffs();

            // 2. Timer de Spawn de Power-up Raro
            if (!isSpawningEnabled) return;

            // Se já houver um item flutuando ativo, aguarda ele ser coletado ou expirar
            if (currentActiveItem != null) return;

            spawnTimer += Time.deltaTime;
            if (spawnTimer >= currentTargetInterval)
            {
                SpawnRandomPowerUp();
                ScheduleNextSpawn();
            }
        }

        private void UpdateActiveBuffs()
        {
            bool anyBuffActive = false;
            string buffSummary = "";

            // Fúria Arcana
            if (arcaneFuryTimer > 0f)
            {
                arcaneFuryTimer -= Time.unscaledDeltaTime;
                anyBuffActive = true;
                buffSummary += $"FÚRIA: {Mathf.CeilToInt(arcaneFuryTimer)}s   ";
                if (arcaneFuryTimer <= 0f)
                {
                    arcaneFuryTimer = 0f;
                }
            }

            // Tempo Arcano (Slow Motion)
            if (arcaneTimeTimer > 0f)
            {
                arcaneTimeTimer -= Time.unscaledDeltaTime;
                anyBuffActive = true;
                buffSummary += $"TEMPO: {Mathf.CeilToInt(arcaneTimeTimer)}s   ";
                if (arcaneTimeTimer <= 0f)
                {
                    arcaneTimeTimer = 0f;
                    ApplySlowMotion(false);
                }
            }

            // Precisão (Crosshair Sniper)
            if (precisionTimer > 0f)
            {
                precisionTimer -= Time.unscaledDeltaTime;
                anyBuffActive = true;
                buffSummary += $"PRECISÃO: {Mathf.CeilToInt(precisionTimer)}s   ";
                if (precisionTimer <= 0f)
                {
                    precisionTimer = 0f;
                    weaponSystem?.SetPrecisionMode(false);
                }
            }

            // Tempestade Carregada
            if (isStormCharged)
            {
                anyBuffActive = true;
                buffSummary += "TEMPESTADE PRONTA!   ";
            }

            uiManager?.UpdateActiveBuffs(anyBuffActive, buffSummary.TrimEnd());
        }

        public void SpawnRandomPowerUp()
        {
            if (currentActiveItem != null) return;

            RecalculateBounds();

            // Posição de spawn segura na arena
            float marginX = 2.0f;
            float rx = UnityEngine.Random.Range(arenaBounds.min.x + marginX, arenaBounds.max.x - marginX);
            // Evita barras de topo e rodapé
            float minY = arenaBounds.min.y + 1.8f;
            float maxY = arenaBounds.max.y - 1.8f;
            float ry = UnityEngine.Random.Range(minY, maxY);
            Vector3 spawnPos = new Vector3(rx, ry, 0f);

            // Sorteia um dos 5 tipos
            Array values = Enum.GetValues(typeof(PowerUpType));
            PowerUpType randomType = (PowerUpType)values.GetValue(UnityEngine.Random.Range(0, values.Length));

            GameObject itemGo = new GameObject($"PowerUp_{randomType}");
            currentActiveItem = itemGo.AddComponent<PowerUpItem>();
            currentActiveItem.Initialize(randomType, spawnPos, arenaBounds);

            currentActiveItem.OnCollected += HandleItemCollected;
            currentActiveItem.OnExpired += (item) => {
                if (currentActiveItem == item) currentActiveItem = null;
            };

            // Som sutil de aparição mágica
            SoundManager.Instance?.PlaySFX(SoundManager.Instance.teleportSound);
        }

        public void HandleItemCollected(PowerUpItem item, PowerUpType type)
        {
            if (currentActiveItem == item)
            {
                currentActiveItem = null;
            }

            // Efeitos sonoros e visuais de coleta
            SoundManager.Instance?.PlaySFX(SoundManager.Instance.victorySound);
            SpellEffectsManager.Instance?.TriggerCameraShake(0.18f, 0.15f);
            if (item != null)
            {
                string shortLabel = type switch
                {
                    PowerUpType.Mana => "+4 MANA!",
                    PowerUpType.ArcaneFury => "+1 DANO (5s)!",
                    PowerUpType.ArcaneTime => "TEMPO 50%!",
                    PowerUpType.Storm => "TEMPESTADE!",
                    PowerUpType.Precision => "PRECISÃO!",
                    _ => "POWER-UP!"
                };
                SpellEffectsManager.Instance?.SpawnFloatingText(item.transform.position, shortLabel, PowerUpItem.GetColorForType(type), 1.15f);
            }

            ApplyPowerUpEffect(type);
            OnPowerUpActivated?.Invoke(type);
        }

        private void ApplyPowerUpEffect(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Mana:
                    weaponSystem?.AddAmmo(4);
                    uiManager?.ShowPowerUpBanner("MANA RECARREGADA!", "+4 Disparos Adicionados à Varinha!", PowerUpItem.GetColorForType(type));
                    break;

                case PowerUpType.ArcaneFury:
                    arcaneFuryTimer = arcaneFuryDuration;
                    uiManager?.ShowPowerUpBanner("FÚRIA ARCANA ATIVADA!", "+1 Dano em Todos os Tiros durante 5s!", PowerUpItem.GetColorForType(type));
                    break;

                case PowerUpType.ArcaneTime:
                    arcaneTimeTimer = arcaneTimeDuration;
                    ApplySlowMotion(true);
                    uiManager?.ShowPowerUpBanner("TEMPO ARCANO!", "O Mundo fica 50% Mais Lento por 3s!", PowerUpItem.GetColorForType(type));
                    break;

                case PowerUpType.Storm:
                    isStormCharged = true;
                    uiManager?.ShowPowerUpBanner("TEMPESTADE CARREGADA!", "Seu Próximo Tiro Aniquilará os Goblins!", PowerUpItem.GetColorForType(type));
                    break;

                case PowerUpType.Precision:
                    precisionTimer = precisionDuration;
                    weaponSystem?.SetPrecisionMode(true);
                    uiManager?.ShowPowerUpBanner("MIRA DE PRECISÃO!", "Retículo Compacto e Cirúrgico durante 5s!", PowerUpItem.GetColorForType(type));
                    break;
            }
        }

        /// <summary>
        /// Aciona e consome o poder da Tempestade, aniquilando todos os goblins comuns em tela.
        /// </summary>
        public int ConsumeStormAndAnnihilateCommonGoblins()
        {
            if (!isStormCharged) return 0;
            isStormCharged = false;

            int killedCount = 0;
            var activeGoblins = new List<WizardController>(WizardController.ActiveGoblins);
            var stormTargets = new List<Vector3>();

            foreach (var goblin in activeGoblins)
            {
                if (goblin != null && goblin.gameObject.activeSelf && !goblin.IsDeadOrEscaping && goblin.GetData() != null)
                {
                    Vector3 gPos = goblin.transform.position;
                    stormTargets.Add(gPos);
                    var wType = goblin.GetData().wizardType;
                    if (wType == WizardType.Comum)
                    {
                        killedCount++;
                        SpellEffectsManager.Instance?.SpawnFloatingText(gPos, "TEMPESTADE!", new Color(0.78f, 0.52f, 1f), 1.05f);
                        goblin.TakeDamage(goblin.CurrentHealth);
                    }
                    else if (!WaveConfig.IsBossType(wType))
                    {
                        // Fases avançadas: causa 2 de dano em goblins de elite em vez de ficar inútil
                        if (goblin.CurrentHealth <= 2) killedCount++;
                        SpellEffectsManager.Instance?.SpawnFloatingText(gPos, "-2 RAIO!", new Color(0.78f, 0.52f, 1f), 1.05f);
                        goblin.TakeDamage(2);
                    }
                    else
                    {
                        SpellEffectsManager.Instance?.SpawnFloatingText(gPos, "-3 RAIO!", new Color(0.78f, 0.52f, 1f), 1.15f);
                        goblin.TakeDamage(3);
                    }
                }
            }

            // Feedback épico de relâmpago divino conectando todos os alvos atingidos
            if (stormTargets.Count > 0)
            {
                Vector3 skyOrigin = new Vector3(0f, 5.2f, 0f);
                SpellEffectsManager.Instance?.SpawnLightningChain(skyOrigin, stormTargets);
            }
            SpellEffectsManager.Instance?.TriggerCameraShake(0.35f, 0.28f);
            SoundManager.Instance?.PlaySFX(SoundManager.Instance.lightningCastSound);

            return killedCount;
        }

        private void ApplySlowMotion(bool active)
        {
            isSlowMotionActive = active;
            if (Time.timeScale > 0f) // Não altera se estiver pausado
            {
                Time.timeScale = active ? 0.5f : 1.0f;
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
            }
        }

        public void ClearActiveFloatingItem()
        {
            if (currentActiveItem != null)
            {
                Destroy(currentActiveItem.gameObject);
                currentActiveItem = null;
            }
        }

        public void ResetAllBuffs()
        {
            arcaneFuryTimer = 0f;
            arcaneTimeTimer = 0f;
            precisionTimer = 0f;
            isStormCharged = false;

            ApplySlowMotion(false);
            weaponSystem?.SetPrecisionMode(false);
            ClearActiveFloatingItem();
            ScheduleNextSpawn();

            uiManager?.UpdateActiveBuffs(false, "");
        }
    }
}
