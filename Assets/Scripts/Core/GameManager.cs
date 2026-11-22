using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WizardGame.Data;
using WizardGame.Entities;
using WizardGame.Input;
using WizardGame.UI;

namespace WizardGame.Core
{
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        GameOver
    }

    [System.Serializable]
    public struct GamePerformanceStats
    {
        public bool won;
        public int finalScore;
        public int escapedCount;
        public int maxEscapes;           // Limite configurável de fugitivos (ex: 15)
        public int wizardsDefeated;      // Magos abatidos
        public int totalShotsFired;      // Tiros disparados
        public int totalHits;            // Acertos
        public float accuracy;           // Precisão % (ex: 83.5f)
        public int strongShotsCount;     // Tiros fortes
        public int totalDamageDealt;     // Dano causado
        public int maxStreak;            // Melhor combo
        public int headshotsCount;       // Tiros perfeitos
        public int skillBonusesCount;    // Jogadas de habilidade conquistadas
        public int skillBonusTotalPoints;// Total de pontos obtidos via bônus especiais
        public float matchDuration;      // Duração da partida em segundos
        public string rankGrade;         // "S", "A", "B", "C", "D"
        public int rankScore;            // Pontuação combinada do Rank
        public string rankTitle;         // "Arquimago Supremo", "Feiticeiro", etc.
        public string rankBadge;         // "👑", "🔮", "⚡", "📜", "🔰"
        public Color rankColor;
        public string defeatReason;      // Motivo detalhado da derrota
    }

    /// <summary>
    /// Orquestrador principal com Arsenal Mágico (Tiro Normal, Arcano, Gelo, Relâmpago e Área),
    /// Sistema de Combos, Multiplicadores Dinâmicos e Bônus de Precisão por Headshot.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private WizardSpawner spawner;
        [SerializeField] private PlayerInputHandler inputHandler;
        [SerializeField] private WeaponSystem weaponSystem;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private StageEnvironmentManager stageEnvironment;

        [Header("Regras de Partida")]
        [SerializeField] private int maxEscapesAllowed = 15;
        [SerializeField] private int maxPlayerHealth = 5;

        [Header("Configurações das Fases (Stage & Wave System)")]
        [SerializeField] private List<WaveConfig> waves = new List<WaveConfig>();
        [SerializeField] private List<Sprite> stageBackgroundSprites = new List<Sprite>();
        [SerializeField] private List<AudioClip> stageDefaultMusicClips = new List<AudioClip>();
        [SerializeField] private int waveClearBonusBase = 60;
        [SerializeField] private float delayBetweenWaves = 1.6f;

        [Header("Tempos de Recarga dos Feitiços Especiais")]
        [SerializeField] private float arcaneCooldownDuration = 2.6f;
        [SerializeField] private float iceCooldownDuration = 3.8f;
        [SerializeField] private float lightningCooldownDuration = 4.2f;
        [SerializeField] private float areaCooldownDuration = 5.0f;

        private GameState currentState;
        private int currentScore;
        private int currentEscaped;
        private int currentPlayerHealth;
        private int currentWaveIndex = 0;
        private Coroutine waveTransitionCoroutine;
        private bool isStageEndingOrTransitioning;
        private static bool autoStartNextLoad = false;

        // Timers e Estados dos Feitiços
        private float arcaneTimer;
        private float iceTimer;
        private float lightningTimer;
        private float areaTimer;

        private bool isArcaneReady = true;
        private bool isIceReady = true;
        private bool isLightningReady = true;
        private bool isAreaReady = true;

        // --- SISTEMA DE COMBOS E ESTATÍSTICAS ---
        private int currentStreak;
        private int currentMultiplier;
        private int maxStreak;
        private int headshotsCount;

        // --- TELEMETRIA E SISTEMA DE PRECISÃO ---
        private int wizardsDefeated;
        private int totalShotsFired;
        private int totalHits;
        private int strongShotsCount;
        private int totalDamageDealt;
        private float matchDuration;

        // --- SISTEMA DE BÔNUS DE HABILIDADE (SKILL SHOTS / TRICK BONUSES) ---
        private bool isFirstShotAttempt = true;
        private int skillBonusesCount;
        private int skillBonusTotalPoints;
        private readonly List<float> recentKillTimestamps = new List<float>(16);
        private readonly List<WizardController> cachedAreaTargets = new List<WizardController>(32);

        private void AwardBonus(int points, string bonusName, Color color, AudioClip customSound = null)
        {
            currentScore += points;
            skillBonusesCount++;
            skillBonusTotalPoints += points;

            uiManager?.ShowSkillBonusPopup(bonusName, color);
            uiManager?.UpdateScore(currentScore);

            if (customSound != null)
            {
                SoundManager.Instance?.PlaySFX(customSound);
            }
        }

        private void Start()
        {
            Time.timeScale = 1f;

            if (inputHandler != null)
            {
                inputHandler.OnNormalShot += HandleNormalShot;
                inputHandler.OnSpecialShot += HandleSpecialShot;
                inputHandler.OnSpecialSpellSelected += HandleSpecialSpellSelected;
                inputHandler.OnShotMissed += HandleShotMissed;
                inputHandler.OnTogglePauseRequested += TogglePause;
            }

            if (weaponSystem != null)
            {
                weaponSystem.OnAmmoChanged += (ammo, max) => uiManager?.UpdateAmmo(ammo, max, weaponSystem.IsReloading);
                weaponSystem.OnReloadStateChanged += (reloading) => uiManager?.UpdateAmmo(weaponSystem.CurrentAmmo, weaponSystem.MaxAmmo, reloading);
            }

            if (stageEnvironment == null) stageEnvironment = FindAnyObjectByType<StageEnvironmentManager>();
            if (stageEnvironment == null)
            {
                var envGo = new GameObject("StageEnvironmentManager");
                stageEnvironment = envGo.AddComponent<StageEnvironmentManager>();
            }
            if (stageEnvironment != null && stageBackgroundSprites != null && stageBackgroundSprites.Count > 0)
            {
                stageEnvironment.SetStageBackgroundList(stageBackgroundSprites);
            }

            if (spawner != null)
            {
                spawner.OnWizardDefeated += HandleWizardDefeated;
                spawner.OnWizardEscaped += HandleWizardEscaped;
                spawner.OnBossSpawned += HandleBossSpawned;
                spawner.OnBossEnraged += HandleBossEnraged;
                spawner.OnBossAttackInterrupted += HandleBossAttackInterrupted;
                spawner.OnWaveProgressChanged += HandleWaveProgressChanged;
                spawner.OnWaveCompleted += HandleWaveCompleted;
                spawner.OnPlayerDamaged += (wizard, damage, source) => DamagePlayer(damage, source);
            }

            if (uiManager != null)
            {
                uiManager.OnPlayClicked += StartGameplay;
                uiManager.OnResumeClicked += ResumeGame;
                uiManager.OnRestartClicked += RestartGame;
                uiManager.OnMainMenuClicked += ReturnToMainMenu;
            }

            if (autoStartNextLoad)
            {
                autoStartNextLoad = false;
                StartGameplay();
            }
            else
            {
                SetState(GameState.Menu);
            }
        }

        private void Update()
        {
            if (currentState == GameState.Playing)
            {
                if (!isStageEndingOrTransitioning)
                {
                    matchDuration += Time.deltaTime;
                }
                UpdateCooldown(ref arcaneTimer, ref isArcaneReady, SpellType.Arcane);
                UpdateCooldown(ref iceTimer, ref isIceReady, SpellType.Ice);
                UpdateCooldown(ref lightningTimer, ref isLightningReady, SpellType.Lightning);
                UpdateCooldown(ref areaTimer, ref isAreaReady, SpellType.Area);
            }
        }

        private void UpdateCooldown(ref float timer, ref bool isReady, SpellType spell)
        {
            if (!isReady)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    timer = 0f;
                    isReady = true;
                    uiManager?.UpdateSpellCooldown(spell, true);
                }
                else
                {
                    uiManager?.UpdateSpellCooldown(spell, false, timer);
                }
            }
        }

        public void SetState(GameState newState)
        {
            currentState = newState;

            switch (currentState)
            {
                case GameState.Menu:
                    Time.timeScale = 1f;
                    Time.fixedDeltaTime = 0.02f;
                    if (waveTransitionCoroutine != null) StopCoroutine(waveTransitionCoroutine);
                    powerUpManager?.ResetAllBuffs();
                    powerUpManager?.SetSpawningEnabled(false);
                    spawner?.ClearActiveGoblins();
                    inputHandler?.SetInputBlocked(true);
                    uiManager?.ShowStartMenu();
                    SoundManager.Instance?.PlayMenuMusic();
                    break;

                case GameState.Playing:
                    if (powerUpManager != null && powerUpManager.IsArcaneTimeActive)
                    {
                        Time.timeScale = 0.5f;
                    }
                    else
                    {
                        Time.timeScale = 1f;
                    }
                    Time.fixedDeltaTime = 0.02f * Time.timeScale;
                    powerUpManager?.SetSpawningEnabled(!isStageEndingOrTransitioning);
                    inputHandler?.SetInputBlocked(false);
                    uiManager?.ShowGameplayHUD();
                    break;

                case GameState.Paused:
                    Time.timeScale = 0f;
                    powerUpManager?.SetSpawningEnabled(false);
                    inputHandler?.SetInputBlocked(true);
                    uiManager?.ShowPauseMenu(true);
                    break;

                case GameState.GameOver:
                    Time.timeScale = 1f;
                    Time.fixedDeltaTime = 0.02f;
                    if (waveTransitionCoroutine != null) StopCoroutine(waveTransitionCoroutine);
                    powerUpManager?.ResetAllBuffs();
                    powerUpManager?.SetSpawningEnabled(false);
                    spawner?.ClearActiveGoblins();
                    inputHandler?.SetInputBlocked(true);
                    break;
            }
        }

        public void StartGameplay()
        {
            currentScore = 0;
            currentEscaped = 0;
            currentStreak = 0;
            currentMultiplier = 1;
            maxStreak = 0;
            headshotsCount = 0;
            currentWaveIndex = 0;

            // Reset telemetria e precisão
            wizardsDefeated = 0;
            totalShotsFired = 0;
            totalHits = 0;
            strongShotsCount = 0;
            totalDamageDealt = 0;
            matchDuration = 0f;

            // Reset bônus de habilidade
            isFirstShotAttempt = true;
            isStageEndingOrTransitioning = false;
            skillBonusesCount = 0;
            skillBonusTotalPoints = 0;
            recentKillTimestamps.Clear();

            if (waveTransitionCoroutine != null)
            {
                StopCoroutine(waveTransitionCoroutine);
                waveTransitionCoroutine = null;
            }

            if (waves == null || waves.Count == 0)
            {
                PopulateDefaultWaves();
            }

            spawner?.ClearActiveGoblins();
            powerUpManager?.ResetAllBuffs();
            powerUpManager?.SetSpawningEnabled(true);

            isArcaneReady = true;
            isIceReady = true;
            isLightningReady = true;
            isAreaReady = true;
            arcaneTimer = 0f;
            iceTimer = 0f;
            lightningTimer = 0f;
            areaTimer = 0f;

            currentPlayerHealth = maxPlayerHealth;
            uiManager?.UpdateScore(currentScore);
            uiManager?.UpdateEscapes(currentEscaped, maxEscapesAllowed);
            uiManager?.UpdatePlayerHealth(currentPlayerHealth, maxPlayerHealth);
            uiManager?.UpdateCombo(currentStreak, currentMultiplier);

            uiManager?.UpdateSpellCooldown(SpellType.Arcane, true);
            uiManager?.UpdateSpellCooldown(SpellType.Ice, true);
            uiManager?.UpdateSpellCooldown(SpellType.Lightning, true);
            uiManager?.UpdateSpellCooldown(SpellType.Area, true);

            if (inputHandler != null)
            {
                uiManager?.UpdateSelectedSpell(inputHandler.CurrentSpecialSpell);
            }

            if (weaponSystem != null)
            {
                weaponSystem.RefillAmmo();
            }

            uiManager?.HideStageTransitionImmediate();
            SetState(GameState.Playing);
            if (waves != null && waves.Count > 0)
            {
                stageEnvironment?.ApplyStage(waves[0], 0);
            }
            StartNextWave();
        }

        public void TogglePause()
        {
            if (currentState == GameState.Playing)
            {
                SetState(GameState.Paused);
            }
            else if (currentState == GameState.Paused)
            {
                ResumeGame();
            }
        }

        public void ResumeGame()
        {
            if (currentState == GameState.Paused)
            {
                uiManager?.ShowPauseMenu(false);
                SetState(GameState.Playing);
            }
        }

        public void ReturnToMainMenu()
        {
            autoStartNextLoad = false;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }

        public void RestartGame()
        {
            autoStartNextLoad = true;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }

        private void HandleNormalShot(ShotHitInfo hitInfo)
        {
            if (currentState != GameState.Playing) return;

            totalShotsFired++;
            SoundManager.Instance?.PlaySFX(SoundManager.Instance.normalShotSound);

            // Coleta de Power-up flutuante atingido pelo disparo
            if (hitInfo.powerUpTarget != null)
            {
                totalHits++;
                SpellEffectsManager.Instance?.SpawnImpactEffect(hitInfo.powerUpTarget.transform.position, true, false, PowerUpItem.GetColorForType(hitInfo.powerUpTarget.Type));
                hitInfo.powerUpTarget.Collect();
                return;
            }

            // Interceptação de Projétil de Chefe
            if (hitInfo.bossProjectileTarget != null)
            {
                totalHits++;
                Vector3 projPos = hitInfo.bossProjectileTarget.transform.position;
                SpellEffectsManager.Instance?.SpawnImpactEffect(projPos, true, true, new Color(0.3f, 1f, 0.85f));
                SpellEffectsManager.Instance?.SpawnFloatingText(projPos, "+5 INTERCEPT!", new Color(0.3f, 1f, 0.85f), 1.05f);
                AwardBonus(5, "INTERCEPTAÇÃO! +5", new Color(0.3f, 1f, 0.8f), SoundManager.Instance?.headshotSound);
                return;
            }

            // Power-up: Tempestade (aniquila todos os magos comuns na tela)
            if (powerUpManager != null && powerUpManager.IsStormCharged)
            {
                int stormKills = powerUpManager.ConsumeStormAndAnnihilateCommonGoblins();
                if (stormKills > 0)
                {
                    AwardBonus(10, $"TEMPESTADE! {stormKills} ABATES (+10)", new Color(0.75f, 0.45f, 1f), SoundManager.Instance?.lightningCastSound);
                }
            }

            if (hitInfo.isHit && hitInfo.target != null)
            {
                totalHits++;
                int bonusDamage = (powerUpManager != null && powerUpManager.IsArcaneFuryActive) ? 1 : 0;
                int critBonus = hitInfo.isHeadshot ? 1 : 0;
                int damageToDeal = 1 + bonusDamage + critBonus;
                totalDamageDealt += damageToDeal;

                Vector3 hitPos = hitInfo.hitPoint;
                SpellEffectsManager.Instance?.SpawnImpactEffect(hitPos, true, hitInfo.isHeadshot);

                // Bônus 1: Primeiro Tiro (+5)
                if (isFirstShotAttempt)
                {
                    isFirstShotAttempt = false;
                    AwardBonus(5, "PRIMEIRO TIRO! +5", new Color(1f, 0.85f, 0.25f), SoundManager.Instance?.headshotSound);
                }

                // Bônus 2: Headshot Crítico (+1 Dano e +2 Pontos)
                if (hitInfo.isHeadshot)
                {
                    headshotsCount++;
                    AwardBonus(2, "HEADSHOT CRÍTICO! +2", new Color(0.3f, 1f, 0.45f), SoundManager.Instance?.headshotSound);
                    SpellEffectsManager.Instance?.SpawnFloatingText(hitPos, $"-{damageToDeal} CRIT!", new Color(1f, 0.88f, 0.2f), 1.18f);
                }
                else
                {
                    Color dmgColor = bonusDamage > 0 ? new Color(1f, 0.48f, 0.18f) : Color.white;
                    SpellEffectsManager.Instance?.SpawnFloatingText(hitPos, $"-{damageToDeal}", dmgColor, 0.95f);
                }

                hitInfo.target.TakeDamage(damageToDeal, isNormalShot: true);
            }
            else
            {
                SpellEffectsManager.Instance?.SpawnImpactEffect(hitInfo.hitPoint, false, false);
                if (isFirstShotAttempt)
                {
                    isFirstShotAttempt = false;
                }
            }
        }

        private void HandleSpecialSpellSelected(SpellType spell)
        {
            uiManager?.UpdateSelectedSpell(spell);
        }

        private void HandleStrongShot(ShotHitInfo hitInfo)
        {
            HandleSpecialShot(SpellType.Arcane, hitInfo);
        }

        private bool IsSpellReady(SpellType spell)
        {
            return spell switch
            {
                SpellType.Arcane => isArcaneReady,
                SpellType.Ice => isIceReady,
                SpellType.Lightning => isLightningReady,
                SpellType.Area => isAreaReady,
                _ => false
            };
        }

        private void HandleSpecialShot(SpellType spell, ShotHitInfo hitInfo)
        {
            if (currentState != GameState.Playing) return;

            if (!IsSpellReady(spell))
            {
                SoundManager.Instance?.PlayDryFireSound();
                return;
            }

            // Bônus 1: Primeiro Tiro (+5)
            if (isFirstShotAttempt)
            {
                if (hitInfo.isHit && hitInfo.target != null)
                {
                    AwardBonus(5, "PRIMEIRO TIRO! +5", new Color(1f, 0.85f, 0.25f), SoundManager.Instance?.headshotSound);
                }
                isFirstShotAttempt = false;
            }

            bool registeredSpellHit = false;

            if (hitInfo.powerUpTarget != null)
            {
                registeredSpellHit = true;
                hitInfo.powerUpTarget.Collect();
            }

            if (hitInfo.bossProjectileTarget != null)
            {
                registeredSpellHit = true;
                hitInfo.bossProjectileTarget.Intercept();
                AwardBonus(5, "INTERCEPTAÇÃO! +5", new Color(0.3f, 1f, 0.8f), SoundManager.Instance?.headshotSound);
            }

            if (powerUpManager != null && powerUpManager.IsStormCharged)
            {
                int stormKills = powerUpManager.ConsumeStormAndAnnihilateCommonGoblins();
                if (stormKills > 0)
                {
                    AwardBonus(10, $"TEMPESTADE! {stormKills} ABATES (+10)", new Color(0.75f, 0.45f, 1f), SoundManager.Instance?.lightningCastSound);
                }
            }

            int furyBonus = (powerUpManager != null && powerUpManager.IsArcaneFuryActive) ? 1 : 0;

            switch (spell)
            {
                case SpellType.Arcane:
                    totalShotsFired++;
                    strongShotsCount++;
                    isArcaneReady = false;
                    arcaneTimer = arcaneCooldownDuration;
                    uiManager?.UpdateSpellCooldown(SpellType.Arcane, false, arcaneCooldownDuration);
                    SoundManager.Instance?.PlaySFX(SoundManager.Instance.strongShotSound);
                    weaponSystem?.TriggerRecoil();

                    if (hitInfo.isHit)
                    {
                        weaponSystem?.TriggerHitMarker(hitInfo.isHeadshot);
                    }

                    if (hitInfo.isHit && hitInfo.target != null)
                    {
                        int dmg = 4 + furyBonus + (hitInfo.isHeadshot ? 1 : 0);
                        registeredSpellHit = true;
                        totalDamageDealt += dmg;
                        SpellEffectsManager.Instance?.SpawnImpactEffect(hitInfo.hitPoint, true, hitInfo.isHeadshot, new Color(0.25f, 0.92f, 1f));
                        if (hitInfo.isHeadshot)
                        {
                            headshotsCount++;
                            AwardBonus(2, "HEADSHOT CRÍTICO! +2", new Color(0.3f, 1f, 0.45f), SoundManager.Instance?.headshotSound);
                            SpellEffectsManager.Instance?.SpawnFloatingText(hitInfo.hitPoint, $"-{dmg} CRIT!", new Color(1f, 0.88f, 0.2f), 1.28f);
                        }
                        else
                        {
                            SpellEffectsManager.Instance?.SpawnFloatingText(hitInfo.hitPoint, $"-{dmg}", new Color(0.35f, 0.94f, 1f), 1.15f);
                        }
                        hitInfo.target.TakeDamage(dmg, isNormalShot: false, isSpecialSpell: true);
                    }
                    else
                    {
                        SpellEffectsManager.Instance?.SpawnImpactEffect(hitInfo.hitPoint, false, false, new Color(0.25f, 0.92f, 1f, 0.5f));
                    }
                    break;

                case SpellType.Ice:
                    totalShotsFired++;
                    strongShotsCount++;
                    isIceReady = false;
                    iceTimer = iceCooldownDuration;
                    uiManager?.UpdateSpellCooldown(SpellType.Ice, false, iceCooldownDuration);
                    SoundManager.Instance?.PlayIceCastSound();
                    weaponSystem?.TriggerRecoil();

                    if (hitInfo.isHit)
                    {
                        weaponSystem?.TriggerHitMarker(hitInfo.isHeadshot);
                    }

                    if (hitInfo.isHit && hitInfo.target != null)
                    {
                        int dmg = 1 + furyBonus + (hitInfo.isHeadshot ? 1 : 0);
                        registeredSpellHit = true;
                        totalDamageDealt += dmg;
                        SpellEffectsManager.Instance?.SpawnImpactEffect(hitInfo.hitPoint, true, hitInfo.isHeadshot, new Color(0.55f, 0.95f, 1f));
                        if (hitInfo.isHeadshot)
                        {
                            headshotsCount++;
                            AwardBonus(2, "HEADSHOT CRÍTICO! +2", new Color(0.3f, 1f, 0.45f), SoundManager.Instance?.headshotSound);
                        }
                        SpellEffectsManager.Instance?.SpawnFloatingText(hitInfo.target.transform.position, $"CONGELADO! -{dmg}", new Color(0.58f, 0.96f, 1f), 1.08f);
                        hitInfo.target.TakeDamage(dmg, isNormalShot: false, isSpecialSpell: true);
                        hitInfo.target.Freeze(2.8f);
                        SpellEffectsManager.Instance?.SpawnIceEffect(hitInfo.target, 2.8f);
                    }
                    else
                    {
                        SpellEffectsManager.Instance?.SpawnImpactEffect(hitInfo.hitPoint, false, false, new Color(0.55f, 0.95f, 1f, 0.5f));
                    }
                    break;

                case SpellType.Lightning:
                    totalShotsFired++;
                    strongShotsCount++;
                    isLightningReady = false;
                    lightningTimer = lightningCooldownDuration;
                    uiManager?.UpdateSpellCooldown(SpellType.Lightning, false, lightningCooldownDuration);
                    SoundManager.Instance?.PlayLightningCastSound();
                    weaponSystem?.TriggerRecoil();

                    if (hitInfo.isHit)
                    {
                        weaponSystem?.TriggerHitMarker(hitInfo.isHeadshot);
                    }

                    Vector3 strikePos = hitInfo.isHit && hitInfo.target != null ? hitInfo.target.transform.position : hitInfo.hitPoint;
                    strikePos.z = 0f;

                    var lightningChainPoints = new List<Vector3>();
                    int lightningLethalHits = 0;

                    if (hitInfo.isHit && hitInfo.target != null)
                    {
                        int primaryDmg = 2 + furyBonus + (hitInfo.isHeadshot ? 1 : 0);
                        registeredSpellHit = true;
                        totalDamageDealt += primaryDmg;
                        if (hitInfo.isHeadshot)
                        {
                            headshotsCount++;
                            AwardBonus(2, "HEADSHOT CRÍTICO! +2", new Color(0.3f, 1f, 0.45f), SoundManager.Instance?.headshotSound);
                        }
                        SpellEffectsManager.Instance?.SpawnFloatingText(hitInfo.target.transform.position, $"-{primaryDmg}", new Color(1f, 0.92f, 0.28f), 1.12f);
                        if (hitInfo.target.CurrentHealth <= primaryDmg) lightningLethalHits++;
                        hitInfo.target.TakeDamage(primaryDmg, isNormalShot: false, isSpecialSpell: true);
                        lightningChainPoints.Add(hitInfo.target.transform.position);
                    }
                    else
                    {
                        lightningChainPoints.Add(strikePos);
                    }

                    // Relâmpago em Cadeia: Atinge até 3 outros goblins próximos em um raio de 3.8m causando 2 de dano
                    int chainCount = 0;
                    int chainDmg = 2 + furyBonus;
                    foreach (var goblin in WizardController.ActiveGoblins)
                    {
                        if (goblin == null || goblin == hitInfo.target || goblin.IsDeadOrEscaping) continue;
                        if (Vector2.Distance(strikePos, goblin.transform.position) <= 3.8f)
                        {
                            if (goblin.CurrentHealth <= chainDmg) lightningLethalHits++;
                            SpellEffectsManager.Instance?.SpawnFloatingText(goblin.transform.position, $"-{chainDmg}", new Color(0.5f, 0.92f, 1f), 1.0f);
                            goblin.TakeDamage(chainDmg, isNormalShot: false, isSpecialSpell: true);
                            totalDamageDealt += chainDmg;
                            registeredSpellHit = true;
                            lightningChainPoints.Add(goblin.transform.position);
                            chainCount++;
                            if (chainCount >= 3) break;
                        }
                    }

                    // Bônus 6: Matar dois com um feitiço (+10)
                    if (lightningLethalHits >= 2)
                    {
                        AwardBonus(10, "FEITIÇO DUPLO! +10", new Color(0.25f, 0.94f, 1f), SoundManager.Instance?.lightningCastSound);
                    }

                    SpellEffectsManager.Instance?.SpawnLightningChain(strikePos, lightningChainPoints);
                    break;

                case SpellType.Area:
                    totalShotsFired++;
                    strongShotsCount++;
                    isAreaReady = false;
                    areaTimer = areaCooldownDuration;
                    uiManager?.UpdateSpellCooldown(SpellType.Area, false, areaCooldownDuration);
                    SoundManager.Instance?.PlayAreaCastSound();
                    weaponSystem?.TriggerRecoil();

                    if (hitInfo.isHit)
                    {
                        weaponSystem?.TriggerHitMarker(hitInfo.isHeadshot);
                    }

                    Vector3 center = hitInfo.isHit && hitInfo.target != null ? hitInfo.target.transform.position : hitInfo.hitPoint;
                    center.z = 0f;

                    // Aciona o efeito visual expansivo de explosão em área
                    SpellEffectsManager.Instance?.SpawnAreaExplosion(center, 2.8f);

                    if (hitInfo.isHeadshot)
                    {
                        headshotsCount++;
                        AwardBonus(2, "HEADSHOT CRÍTICO! +2", new Color(0.3f, 1f, 0.45f), SoundManager.Instance?.headshotSound);
                    }

                    // Feitiço de Área: Explosão radial de 2.8m de raio causando 3 de dano a todos os goblins no raio (Zero-GC)
                    cachedAreaTargets.Clear();
                    foreach (var goblin in WizardController.ActiveGoblins)
                    {
                        if (goblin == null || goblin.IsDeadOrEscaping) continue;
                        if (Vector2.Distance(center, goblin.transform.position) <= 2.8f)
                        {
                            cachedAreaTargets.Add(goblin);
                        }
                    }
                    int areaDmg = 3 + furyBonus;
                    if (cachedAreaTargets.Count > 0)
                    {
                        registeredSpellHit = true;
                    }
                    int areaLethalHits = 0;
                    foreach (var goblin in cachedAreaTargets)
                    {
                        int perTargetDmg = areaDmg + (hitInfo.isHeadshot && goblin == hitInfo.target ? 1 : 0);
                        totalDamageDealt += perTargetDmg;
                        if (goblin.CurrentHealth <= perTargetDmg) areaLethalHits++;
                        SpellEffectsManager.Instance?.SpawnFloatingText(goblin.transform.position, $"-{perTargetDmg}", new Color(1f, 0.45f, 0.2f), 1.12f);
                        goblin.TakeDamage(perTargetDmg, isNormalShot: false, isSpecialSpell: true);
                    }

                    // Bônus 6: Matar dois com um feitiço (+10)
                    if (areaLethalHits >= 2)
                    {
                        AwardBonus(10, "FEITIÇO DUPLO! +10", new Color(0.25f, 0.94f, 1f), SoundManager.Instance?.lightningCastSound);
                    }
                    break;
            }

            if (registeredSpellHit)
            {
                totalHits++;
            }
        }

        private void HandleShotMissed()
        {
            if (currentState != GameState.Playing || isStageEndingOrTransitioning) return;

            isFirstShotAttempt = false;

            // Errar o disparo quebra a sequência de combo!
            if (currentStreak > 0)
            {
                BreakCombo("ERROU O TIRO");
            }
        }

        private void HandleBossSpawned(WizardController wizard, WizardDataSO data)
        {
            if (currentState != GameState.Playing) return;

            SoundManager.Instance?.PlaySFX(SoundManager.Instance.strongShotSound);
            SpellEffectsManager.Instance?.TriggerCameraShake(0.24f, 0.22f);
            uiManager?.ShowBossSpawnBanner(data.displayName, "INTERROMPA GOLPES E INTERCEPTE OS ORBES PARA NÃO SOFRER DANO!");
        }

        private void HandleBossEnraged(WizardController wizard, WizardDataSO data)
        {
            if (currentState != GameState.Playing) return;
            SoundManager.Instance?.PlaySFX(SoundManager.Instance.strongShotSound);
            uiManager?.ShowBossEnragedBanner(data != null ? data.displayName : "Chefe");
        }

        private void HandleBossAttackInterrupted(WizardController wizard, string bossName)
        {
            if (currentState != GameState.Playing) return;
            AwardBonus(8, "CONTRA-ATAQUE! +8", new Color(0.25f, 1f, 0.85f), SoundManager.Instance?.headshotSound);
        }

        private void HandleWizardDefeated(WizardController wizard, int basePoints)
        {
            if (currentState != GameState.Playing) return;

            wizardsDefeated++;

            // Incrementa sequência de combo
            int previousMultiplier = currentMultiplier;
            currentStreak++;
            if (currentStreak > maxStreak) maxStreak = currentStreak;

            currentMultiplier = CalculateMultiplier(currentStreak);

            // Recompensa de ritmo: atingir novo patamar de multiplicador (5, 10, 20 abates) restaura +2 Mana!
            if (currentMultiplier > previousMultiplier && weaponSystem != null)
            {
                weaponSystem.AddAmmo(2);
                uiManager?.ShowSkillBonusPopup($"COMBO ×{currentMultiplier}! +2 MANA", new Color(0.35f, 0.92f, 1f));
            }

            // Aplica multiplicador na pontuação
            int pointsEarned = basePoints * currentMultiplier;
            currentScore += pointsEarned;

            if (wizard != null)
            {
                string ptsLabel = currentMultiplier > 1 ? $"+{pointsEarned} (×{currentMultiplier})" : $"+{pointsEarned}";
                SpellEffectsManager.Instance?.SpawnFloatingText(
                    wizard.transform.position + Vector3.up * 0.45f,
                    ptsLabel,
                    new Color(1f, 0.88f, 0.25f),
                    currentMultiplier >= 3 ? 1.15f : 0.98f,
                    1.1f);
            }

            uiManager?.UpdateScore(currentScore);
            uiManager?.UpdateCombo(currentStreak, currentMultiplier);

            WizardDataSO data = wizard.GetData();
            bool isBoss = data != null && WaveConfig.IsBossType(data.wizardType);

            // Bônus 3: Matar 3 rapidamente (dentro de 2.0s) (+5)
            float now = Time.time;
            for (int i = recentKillTimestamps.Count - 1; i >= 0; i--)
            {
                if (now - recentKillTimestamps[i] > 2.0f)
                {
                    recentKillTimestamps.RemoveAt(i);
                }
            }
            recentKillTimestamps.Add(now);
            if (recentKillTimestamps.Count >= 3)
            {
                recentKillTimestamps.Clear();
                AwardBonus(5, "TRIPLO ABATE RÁPIDO! +5", new Color(1f, 0.5f, 0.15f), SoundManager.Instance?.strongShotSound);
            }

            // Bônus 4: Matar durante teleporte (+10)
            if (wizard != null && (wizard.DiedWhileTeleporting || wizard.IsTeleporting))
            {
                AwardBonus(10, "INTERRUPÇÃO DIMENSIONAL! +10", new Color(0.75f, 0.4f, 1f), SoundManager.Instance?.strongShotSound);
            }

            // Bônus 5: Matar Dourado com tiro normal (+3)
            if (data != null && data.wizardType == WizardType.Dourado && wizard != null && wizard.WasLastDamageNormalShot)
            {
                AwardBonus(3, "DESAFIO DOURADO! +3", new Color(1f, 0.88f, 0.2f), SoundManager.Instance?.headshotSound);
            }

            if (isBoss)
            {
                // Ao derrotar o Chefe da fase: para a música ambiente e toca APENAS a fanfarra de vitória!
                SpellEffectsManager.Instance?.TriggerCameraShake(0.35f, 0.32f);
                SoundManager.Instance?.StopMusic(0.20f);
                SoundManager.Instance?.PlaySFX(SoundManager.Instance.victorySound, 1.0f);

                // No Chefe Final: Derrotar ele = VITÓRIA IMEDIATA!
                if (data.wizardType == WizardType.ChefeFinal)
                {
                    isStageEndingOrTransitioning = true;
                    powerUpManager?.SetSpawningEnabled(false);
                    uiManager?.ShowBossDefeatedBanner(data.displayName, pointsEarned, "A FORTALEZA FOI LIBERTADA!");
                    StartCoroutine(FinalBossVictorySequenceRoutine());
                    return;
                }

                isStageEndingOrTransitioning = true;
                string nextHint = (currentWaveIndex + 1 < waves.Count)
                    ? $"Avançando para a Fase {currentWaveIndex + 2}: {waves[currentWaveIndex + 1].waveName}!"
                    : "";
                uiManager?.ShowBossDefeatedBanner(data.displayName, pointsEarned, nextHint);
                return;
            }

            // Som de Morte (para lacaios comuns e especiais; chefes usam a fanfarra de vitória acima sem sobreposição)
            if (data != null && data.customDeathSound != null)
            {
                SoundManager.Instance?.PlaySFX(data.customDeathSound);
            }
            else
            {
                SoundManager.Instance?.PlayRandomDeathSound();
            }
        }

        private IEnumerator FinalBossVictorySequenceRoutine()
        {
            yield return new WaitForSeconds(1.2f);
            EndGame(won: true, playEndAudio: false);
        }

        private void HandleWizardEscaped(WizardController wizard, int penalty)
        {
            if (currentState != GameState.Playing) return;

            // Deixar mago escapar quebra a sequência de combo!
            if (currentStreak > 0)
            {
                BreakCombo("MAGO ESCAPOU");
            }

            currentEscaped += penalty;
            uiManager?.UpdateEscapes(currentEscaped, maxEscapesAllowed);

            if (wizard.GetData() != null && wizard.GetData().escapeSound != null)
            {
                SoundManager.Instance?.PlaySFX(wizard.GetData().escapeSound);
            }

            if (currentEscaped >= maxEscapesAllowed)
            {
                EndGame(won: false, defeatReason: $"MUITOS GOBLINS ESCAPARAM ({currentEscaped}/{maxEscapesAllowed})!");
            }
        }

        public void DamagePlayer(int damage, string source = "")
        {
            if (currentState != GameState.Playing) return;
            if (damage <= 0) return;

            currentPlayerHealth = Mathf.Max(0, currentPlayerHealth - damage);
            uiManager?.UpdatePlayerHealth(currentPlayerHealth, maxPlayerHealth);
            uiManager?.TriggerDamageFlash();

            SpellEffectsManager.Instance?.TriggerCameraShake(0.28f, 0.25f);
            if (currentPlayerHealth > 0)
            {
                SoundManager.Instance?.PlaySFX(SoundManager.Instance.defeatSound, 0.72f);
            }

            if (currentPlayerHealth <= 0)
            {
                string reason = !string.IsNullOrEmpty(source)
                    ? $"VOCÊ CAIU EM COMBATE ({source.ToUpper()})!"
                    : "VOCÊ CAIU EM COMBATE (VIDA ESGOTADA)!";
                EndGame(won: false, defeatReason: reason);
            }
        }

        private void BreakCombo(string reason)
        {
            bool hadActiveCombo = currentStreak >= 2;
            currentStreak = 0;
            currentMultiplier = 1;

            if (hadActiveCombo)
            {
                SoundManager.Instance?.PlayComboBreakSound();
                uiManager?.NotifyComboBroken(reason);
            }
            else
            {
                uiManager?.UpdateCombo(0, 1);
            }
        }

        private int CalculateMultiplier(int streak)
        {
            // Escala conforme solicitado pelo usuário:
            // 1–4 kills → x1
            // 5–9 kills → x2
            // 10–19 kills → x3
            // 20+ kills → x4
            if (streak >= 20) return 4;
            if (streak >= 10) return 3;
            if (streak >= 5) return 2;
            return 1;
        }

        /// <summary>
        /// Algoritmo de Rank composto da partida (S / A / B / C / D) calculado a partir de 6 fatores:
        /// 1. Quantidade de mortes (magos abatidos)
        /// 2. Precisão (% acertos)
        /// 3. Tempo de partida (duração)
        /// 4. Magos escapados (penalidade e bônus sem fugas)
        /// 5. Maior combo sequencial mantido
        /// 6. Uso tático de feitiços fortes (tiros fortes)
        /// </summary>
        public static (string grade, string title, string badge, Color color, int rankScore) CalculateMatchRank(
            int kills,
            float accuracy,
            float durationSeconds,
            int escapes,
            int maxStreak,
            int strongShots)
        {
            // 1. Fator Mortes (Magos Abatidos): até 520 pts (8 pts por mago)
            int killsScore = Mathf.Min(kills * 8, 520);

            // 2. Fator Precisão (% de acertos): até 250 pts
            int accuracyScore = Mathf.RoundToInt((Mathf.Clamp(accuracy, 0f, 100f) / 100f) * 250f);

            // 3. Fator Tempo: até 200 pts de bônus por rapidez e eficiência (<= 110s máximo; decai até 350s)
            float timeFactor = Mathf.Clamp01(1f - (Mathf.Max(0f, durationSeconds - 110f) / 240f));
            float progressRatio = Mathf.Clamp01(kills / 55f);
            int timeScore = Mathf.RoundToInt(timeFactor * 200f * progressRatio);

            // 4. Fator Magos Escapados:
            // Bônus de 150 pts se nenhum mago escapou (flawless)
            // Penalidade de -30 pts por cada mago escapado
            int escapeScore = (escapes == 0 && kills > 0) ? 150 : -(escapes * 30);

            // 5. Fator Combo (Maior sequência): até 240 pts
            int comboScore = Mathf.Min(maxStreak * 12, 240);

            // 6. Fator Feitiços Fortes: até 150 pts (15 pts por magia especial usada)
            int strongShotsScore = Mathf.Min(strongShots * 15, 150);

            // Pontuação combinada do Rank
            int totalRankScore = killsScore + accuracyScore + timeScore + escapeScore + comboScore + strongShotsScore;
            if (totalRankScore < 0) totalRankScore = 0;

            string grade;
            string title;
            string badge;
            Color color;

            if (totalRankScore >= 1100)
            {
                grade = "S";
                title = "Arquimago Supremo";
                badge = "";
                color = new Color(1f, 0.84f, 0.0f); // Dourado Radiante
            }
            else if (totalRankScore >= 850)
            {
                grade = "A";
                title = "Mago Mestre";
                badge = "";
                color = new Color(0.78f, 0.35f, 1f); // Púrpura Arcano
            }
            else if (totalRankScore >= 650)
            {
                grade = "B";
                title = "Feiticeiro Adepto";
                badge = "";
                color = new Color(0.15f, 0.82f, 1f); // Ciano Brilhante
            }
            else if (totalRankScore >= 450)
            {
                grade = "C";
                title = "Mago Aprendiz";
                badge = "";
                color = new Color(0.4f, 0.95f, 0.4f); // Verde Esmeralda
            }
            else
            {
                grade = "D";
                title = "Iniciante Novato";
                badge = "";
                color = new Color(0.7f, 0.7f, 0.75f); // Cinza Prateado
            }

            return (grade, title, badge, color, totalRankScore);
        }

        public static (string title, string badge, Color color) CalculateRank(float accuracy)
        {
            var rank = CalculateMatchRank(50, accuracy, 100f, 0, 10, 5);
            return (rank.title, rank.badge, rank.color);
        }

        private void EndGame(bool won, string defeatReason = "", bool playEndAudio = true)
        {
            SetState(GameState.GameOver);

            if (waveTransitionCoroutine != null)
            {
                StopCoroutine(waveTransitionCoroutine);
                waveTransitionCoroutine = null;
            }

            spawner?.ClearActiveGoblins();
            SoundManager.Instance?.StopMusic(0.30f);

            if (playEndAudio)
            {
                if (won)
                {
                    SoundManager.Instance?.PlaySFX(SoundManager.Instance.victorySound, 1.0f);
                }
                else
                {
                    SoundManager.Instance?.PlaySFX(SoundManager.Instance.defeatSound, 1.0f);
                }
            }

            if (!won && string.IsNullOrEmpty(defeatReason))
            {
                if (currentPlayerHealth <= 0)
                {
                    defeatReason = "SUA VIDA FOI ESGOTADA PELOS INIMIGOS!";
                }
                else
                {
                    defeatReason = $"MUITOS GOBLINS ESCAPARAM ({currentEscaped}/{maxEscapesAllowed})!";
                }
            }

            float accuracy = totalShotsFired > 0 ? ((float)totalHits / totalShotsFired) * 100f : 0f;
            var rank = CalculateMatchRank(wizardsDefeated, accuracy, matchDuration, currentEscaped, maxStreak, strongShotsCount);

            var stats = new GamePerformanceStats
            {
                won = won,
                finalScore = currentScore,
                escapedCount = currentEscaped,
                maxEscapes = maxEscapesAllowed,
                wizardsDefeated = wizardsDefeated,
                totalShotsFired = totalShotsFired,
                totalHits = totalHits,
                accuracy = accuracy,
                strongShotsCount = strongShotsCount,
                totalDamageDealt = totalDamageDealt,
                maxStreak = maxStreak,
                headshotsCount = headshotsCount,
                skillBonusesCount = skillBonusesCount,
                skillBonusTotalPoints = skillBonusTotalPoints,
                matchDuration = matchDuration,
                rankGrade = rank.grade,
                rankScore = rank.rankScore,
                rankTitle = rank.title,
                rankBadge = rank.badge,
                rankColor = rank.color,
                defeatReason = defeatReason
            };

            uiManager?.ShowGameOver(stats);
        }

        #region Wave System Progression

        private void StartNextWave()
        {
            if (currentWaveIndex >= waves.Count)
            {
                EndGame(won: true);
                return;
            }

            isStageEndingOrTransitioning = false;
            int waveNumber = currentWaveIndex + 1;
            WaveConfig config = waves[currentWaveIndex];

            spawner?.StartWave(config, waveNumber);
            uiManager?.ShowWaveStartBanner(waveNumber, config.waveName, config.waveDescription);
            uiManager?.UpdateWaveProgress(waveNumber, waves.Count, config.TotalEnemies, config.TotalEnemies);

            // Inicia a música padrão dedicada desta fase (e alterna para a playlist caso a fase demore)
            AudioClip stageMusic = config.defaultMusicClip;
            if (stageMusic == null && stageDefaultMusicClips != null && currentWaveIndex < stageDefaultMusicClips.Count)
            {
                stageMusic = stageDefaultMusicClips[currentWaveIndex];
            }
            SoundManager.Instance?.PlayStageMusic(currentWaveIndex, stageMusic);

            // Som de início / alerta de onda
            SoundManager.Instance?.PlaySFX(SoundManager.Instance.strongShotSound);
        }

        private void HandleWaveProgressChanged(int remaining, int total)
        {
            int waveNumber = currentWaveIndex + 1;
            uiManager?.UpdateWaveProgress(waveNumber, waves.Count, remaining, total);
        }

        private void HandleWaveCompleted(int completedWaveNumber)
        {
            if (currentState != GameState.Playing) return;

            bool wasAlreadyTransitioning = isStageEndingOrTransitioning;
            isStageEndingOrTransitioning = true;

            // Garante parada da música ambiente e fanfarra de vitória mesmo se a fase acabar sem chefe
            if (!wasAlreadyTransitioning)
            {
                SoundManager.Instance?.StopMusic(0.20f);
                SoundManager.Instance?.PlaySFX(SoundManager.Instance.victorySound, 1.0f);
            }

            if (waveTransitionCoroutine != null) StopCoroutine(waveTransitionCoroutine);
            waveTransitionCoroutine = StartCoroutine(WaveTransitionRoutine(completedWaveNumber));
        }

        private IEnumerator WaveTransitionRoutine(int completedWaveNumber)
        {
            // Se era o Chefe Final (última fase), a sequência final de vitória assume
            if (currentWaveIndex + 1 >= waves.Count)
            {
                yield break;
            }

            powerUpManager?.SetSpawningEnabled(false);

            // 1. Deixa o jogador curtir a explosão do Chefe e o banner de vitória da fase
            yield return new WaitForSeconds(delayBetweenWaves);

            if (currentState != GameState.Playing) yield break;

            // 2. Recompensa de Fase: Pontos bônus, Mana restaurada e +1 HP recuperado!
            int bonus = waveClearBonusBase * completedWaveNumber;
            currentScore += bonus;
            uiManager?.UpdateScore(currentScore);
            weaponSystem?.RefillAmmo();

            bool healed = false;
            if (currentPlayerHealth < maxPlayerHealth)
            {
                currentPlayerHealth++;
                healed = true;
                uiManager?.UpdatePlayerHealth(currentPlayerHealth, maxPlayerHealth);
            }

            currentWaveIndex++;
            WaveConfig nextStage = waves[currentWaveIndex];

            string healNote = healed ? "+1 Vida Recuperada  •  " : "";
            string diffNote = $"+{bonus} Pontos  •  {healNote}Mana Cheia!  •  Velocidade {nextStage.speedMultiplier:F2}x";

            // 3. Cortina cinematográfica de mudança de fase com troca de cenário no ponto médio
            inputHandler?.SetInputBlocked(true);
            WizardController.ClearAllBossProjectiles();

            if (uiManager != null)
            {
                yield return StartCoroutine(uiManager.PlayStageTransitionRoutine(
                    nextStage.stageNumber,
                    nextStage.waveName,
                    nextStage.bossDisplayName,
                    diffNote,
                    onMidpointSwap: () => {
                        stageEnvironment?.ApplyStage(nextStage, currentWaveIndex);
                        uiManager?.UpdateWaveProgress(currentWaveIndex + 1, waves.Count, nextStage.TotalEnemies, nextStage.TotalEnemies);
                    }
                ));
            }
            else
            {
                stageEnvironment?.ApplyStage(nextStage, currentWaveIndex);
            }

            if (currentState != GameState.Playing) yield break;

            inputHandler?.SetInputBlocked(false);
            powerUpManager?.SetSpawningEnabled(true);
            StartNextWave();
        }

        public void PopulateDefaultWaves()
        {
            Sprite bg1 = (stageBackgroundSprites != null && stageBackgroundSprites.Count > 0) ? stageBackgroundSprites[0] : null;
            Sprite bg2 = (stageBackgroundSprites != null && stageBackgroundSprites.Count > 1) ? stageBackgroundSprites[1] : bg1;
            Sprite bg3 = (stageBackgroundSprites != null && stageBackgroundSprites.Count > 2) ? stageBackgroundSprites[2] : bg2;
            Sprite bg4 = (stageBackgroundSprites != null && stageBackgroundSprites.Count > 3) ? stageBackgroundSprites[3] : bg3;
            Sprite bg5 = (stageBackgroundSprites != null && stageBackgroundSprites.Count > 4) ? stageBackgroundSprites[4] : bg4;

            AudioClip m1 = (stageDefaultMusicClips != null && stageDefaultMusicClips.Count > 0) ? stageDefaultMusicClips[0] : null;
            AudioClip m2 = (stageDefaultMusicClips != null && stageDefaultMusicClips.Count > 1) ? stageDefaultMusicClips[1] : null;
            AudioClip m3 = (stageDefaultMusicClips != null && stageDefaultMusicClips.Count > 2) ? stageDefaultMusicClips[2] : null;
            AudioClip m4 = (stageDefaultMusicClips != null && stageDefaultMusicClips.Count > 3) ? stageDefaultMusicClips[3] : null;
            AudioClip m5 = (stageDefaultMusicClips != null && stageDefaultMusicClips.Count > 4) ? stageDefaultMusicClips[4] : null;

            // Preserva sprites e músicas já vinculados caso PopulateDefaultWaves seja rechamado
            if (waves != null)
            {
                if (bg1 == null && waves.Count > 0) bg1 = waves[0].backgroundSprite;
                if (bg2 == null && waves.Count > 1) bg2 = waves[1].backgroundSprite;
                if (bg3 == null && waves.Count > 2) bg3 = waves[2].backgroundSprite;
                if (bg4 == null && waves.Count > 3) bg4 = waves[3].backgroundSprite;
                if (bg5 == null && waves.Count > 4) bg5 = waves[4].backgroundSprite;

                if (m1 == null && waves.Count > 0) m1 = waves[0].defaultMusicClip;
                if (m2 == null && waves.Count > 1) m2 = waves[1].defaultMusicClip;
                if (m3 == null && waves.Count > 2) m3 = waves[2].defaultMusicClip;
                if (m4 == null && waves.Count > 3) m4 = waves[3].defaultMusicClip;
                if (m5 == null && waves.Count > 4) m5 = waves[4].defaultMusicClip;
            }

            waves = new List<WaveConfig>
            {
                // FASE 1: Muralhas da Fortaleza — Chefe: Grande Goblin (Fugitivo 0.62x)
                new WaveConfig(
                    stageNumber: 1,
                    name: "Muralhas da Fortaleza",
                    desc: "7 Comuns + 3 Fugitivos   •   CHEFE: GRANDE GOBLIN!",
                    bossDisplayName: "Grande Goblin",
                    backgroundSprite: bg1,
                    backgroundTint: Color.white,
                    backgroundZoom: 1.0f,
                    ambientParticleColor: new Color(0.6f, 0.85f, 1f, 0.55f),
                    defaultMusicClip: m1,
                    interval: 1.30f,
                    speedMult: 1.00f,
                    fugitiveSpeedMult: 0.62f,
                    escapeTimerMult: 1.00f,
                    maxConcurrent: 4,
                    doubleSpawnChance: 0f,
                    (WizardType.Comum, 7),
                    (WizardType.Fugitivo, 3),
                    (WizardType.Chefe, 1)),

                // FASE 2: Ruínas Iluminadas pela Lua — Chefe: Xamã do Caos (Fugitivo 0.78x)
                new WaveConfig(
                    stageNumber: 2,
                    name: "Ruínas Iluminadas pela Lua",
                    desc: "4 Comuns + 5 Fugitivos + 3 Dourados   •   CHEFE: XAMÃ DO CAOS!",
                    bossDisplayName: "Xamã do Caos",
                    backgroundSprite: bg2,
                    backgroundTint: Color.white,
                    backgroundZoom: 1.0f,
                    ambientParticleColor: new Color(0.4f, 1f, 0.8f, 0.6f),
                    defaultMusicClip: m2,
                    interval: 1.08f,
                    speedMult: 1.14f,
                    fugitiveSpeedMult: 0.78f,
                    escapeTimerMult: 0.94f,
                    maxConcurrent: 5,
                    doubleSpawnChance: 0.18f,
                    (WizardType.Comum, 4),
                    (WizardType.Fugitivo, 5),
                    (WizardType.Dourado, 3),
                    (WizardType.Chefe2, 1)),

                // FASE 3: Fortaleza Vulcânica — Chefe: Feiticeiro Espectral (Fugitivo 0.96x)
                new WaveConfig(
                    stageNumber: 3,
                    name: "Fortaleza Vulcânica",
                    desc: "3 Comuns + 4 Fugitivos + 3 Dourados + 4 Fantasmas   •   CHEFE: FEITICEIRO ESPECTRAL!",
                    bossDisplayName: "Feiticeiro Espectral",
                    backgroundSprite: bg3,
                    backgroundTint: Color.white,
                    backgroundZoom: 1.0f,
                    ambientParticleColor: new Color(1f, 0.45f, 0.12f, 0.72f),
                    defaultMusicClip: m3,
                    interval: 0.94f,
                    speedMult: 1.26f,
                    fugitiveSpeedMult: 0.96f,
                    escapeTimerMult: 0.88f,
                    maxConcurrent: 6,
                    doubleSpawnChance: 0.28f,
                    (WizardType.Comum, 3),
                    (WizardType.Fugitivo, 4),
                    (WizardType.Dourado, 3),
                    (WizardType.Fantasma, 4),
                    (WizardType.Chefe3, 1)),

                // FASE 4: Salão do Trono em Ruínas — Chefe: Lorde Carmesim (Fugitivo 1.15x)
                new WaveConfig(
                    stageNumber: 4,
                    name: "Salão do Trono em Ruínas",
                    desc: "2 Comuns + 5 Fugitivos + 4 Dourados + 5 Fantasmas   •   CHEFE: LORDE CARMESIM!",
                    bossDisplayName: "Lorde Carmesim",
                    backgroundSprite: bg4,
                    backgroundTint: new Color(0.84f, 0.82f, 1.0f, 1f),
                    backgroundZoom: 1.0f,
                    ambientParticleColor: new Color(0.82f, 0.35f, 1f, 0.68f),
                    defaultMusicClip: m4,
                    interval: 0.82f,
                    speedMult: 1.38f,
                    fugitiveSpeedMult: 1.15f,
                    escapeTimerMult: 0.84f,
                    maxConcurrent: 7,
                    doubleSpawnChance: 0.38f,
                    (WizardType.Comum, 2),
                    (WizardType.Fugitivo, 5),
                    (WizardType.Dourado, 4),
                    (WizardType.Fantasma, 5),
                    (WizardType.Chefe4, 1)),

                // FASE 5: Altar do Rei Supremo — Chefe Final: Goblin Rei Supremo (Fugitivo 1.35x)
                new WaveConfig(
                    stageNumber: 5,
                    name: "Altar do Rei Supremo",
                    desc: "5 Fugitivos + 6 Dourados + 7 Fantasmas   •   CHEFE FINAL: GOBLIN REI SUPREMO!",
                    bossDisplayName: "Goblin Rei Supremo",
                    backgroundSprite: bg5,
                    backgroundTint: new Color(1.0f, 0.72f, 0.65f, 1f),
                    backgroundZoom: 1.12f,
                    ambientParticleColor: new Color(1f, 0.22f, 0.15f, 0.8f),
                    defaultMusicClip: m5,
                    interval: 0.72f,
                    speedMult: 1.50f,
                    fugitiveSpeedMult: 1.35f,
                    escapeTimerMult: 0.80f,
                    maxConcurrent: 8,
                    doubleSpawnChance: 0.48f,
                    (WizardType.Fugitivo, 5),
                    (WizardType.Dourado, 6),
                    (WizardType.Fantasma, 7),
                    (WizardType.ChefeFinal, 1))
            };
        }

        #endregion
    }
}