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
        public int wizardsDefeated;      // Magos abatidos
        public int totalShotsFired;      // Tiros disparados
        public int totalHits;            // Acertos
        public float accuracy;           // Precisão % (ex: 83.5f)
        public int strongShotsCount;     // Tiros fortes
        public int totalDamageDealt;     // Dano causado
        public int maxStreak;            // Melhor combo
        public int headshotsCount;       // Tiros perfeitos
        public float matchDuration;      // Duração da partida em segundos
        public string rankGrade;         // "S", "A", "B", "C", "D"
        public int rankScore;            // Pontuação combinada do Rank
        public string rankTitle;         // "Arquimago Supremo", "Feiticeiro", etc.
        public string rankBadge;         // "👑", "🔮", "⚡", "📜", "🔰"
        public Color rankColor;
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

        [Header("Regras de Partida")]
        [SerializeField] private int maxEscapesAllowed = 15;

        [Header("Configurações das Ondas (Wave System)")]
        [SerializeField] private List<WaveConfig> waves = new List<WaveConfig>();
        [SerializeField] private int waveClearBonusBase = 50;
        [SerializeField] private float delayBetweenWaves = 2.5f;

        [Header("Tempos de Recarga dos Feitiços Especiais")]
        [SerializeField] private float arcaneCooldownDuration = 3.0f;
        [SerializeField] private float iceCooldownDuration = 4.0f;
        [SerializeField] private float lightningCooldownDuration = 4.5f;
        [SerializeField] private float areaCooldownDuration = 5.5f;

        private GameState currentState;
        private int currentScore;
        private int currentEscaped;
        private int currentWaveIndex = 0;
        private Coroutine waveTransitionCoroutine;

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

            if (spawner != null)
            {
                spawner.OnWizardDefeated += HandleWizardDefeated;
                spawner.OnWizardEscaped += HandleWizardEscaped;
                spawner.OnBossSpawned += HandleBossSpawned;
                spawner.OnWaveProgressChanged += HandleWaveProgressChanged;
                spawner.OnWaveCompleted += HandleWaveCompleted;
            }

            if (uiManager != null)
            {
                uiManager.OnPlayClicked += StartGameplay;
                uiManager.OnResumeClicked += ResumeGame;
                uiManager.OnRestartClicked += RestartGame;
                uiManager.OnMainMenuClicked += ReturnToMainMenu;
            }

            SetState(GameState.Menu);
        }

        private void Update()
        {
            if (currentState == GameState.Playing)
            {
                matchDuration += Time.deltaTime;
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
                    if (waveTransitionCoroutine != null) StopCoroutine(waveTransitionCoroutine);
                    spawner?.ClearActiveGoblins();
                    inputHandler?.SetInputBlocked(true);
                    uiManager?.ShowStartMenu();
                    SoundManager.Instance?.PlayMenuMusic();
                    break;

                case GameState.Playing:
                    Time.timeScale = 1f;
                    inputHandler?.SetInputBlocked(false);
                    uiManager?.ShowGameplayHUD();
                    SoundManager.Instance?.PlayGameplayMusic();
                    break;

                case GameState.Paused:
                    Time.timeScale = 0f;
                    inputHandler?.SetInputBlocked(true);
                    uiManager?.ShowPauseMenu(true);
                    break;

                case GameState.GameOver:
                    Time.timeScale = 1f;
                    if (waveTransitionCoroutine != null) StopCoroutine(waveTransitionCoroutine);
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

            isArcaneReady = true;
            isIceReady = true;
            isLightningReady = true;
            isAreaReady = true;
            arcaneTimer = 0f;
            iceTimer = 0f;
            lightningTimer = 0f;
            areaTimer = 0f;

            uiManager?.UpdateScore(currentScore);
            uiManager?.UpdateEscapes(currentEscaped, maxEscapesAllowed);
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

            SetState(GameState.Playing);
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
            RestartGame();
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }

        private void HandleNormalShot(ShotHitInfo hitInfo)
        {
            if (currentState != GameState.Playing) return;

            totalShotsFired++;
            SoundManager.Instance?.PlaySFX(SoundManager.Instance.normalShotSound);

            if (hitInfo.isHit && hitInfo.target != null)
            {
                totalHits++;
                totalDamageDealt += 1;

                // Bônus de Precisão: Headshot / Acerto Perfeito
                if (hitInfo.isHeadshot)
                {
                    currentScore += 1;
                    headshotsCount++;
                    SoundManager.Instance?.PlayHeadshotSound();
                    uiManager?.ShowHeadshotPopup(hitInfo.hitPoint);
                    uiManager?.UpdateScore(currentScore);
                }

                hitInfo.target.TakeDamage(1);
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

        private void HandleSpecialShot(SpellType spell, ShotHitInfo hitInfo)
        {
            if (currentState != GameState.Playing) return;

            switch (spell)
            {
                case SpellType.Arcane:
                    if (!isArcaneReady) return;
                    totalShotsFired++;
                    strongShotsCount++;
                    isArcaneReady = false;
                    arcaneTimer = arcaneCooldownDuration;
                    uiManager?.UpdateSpellCooldown(SpellType.Arcane, false, arcaneCooldownDuration);
                    SoundManager.Instance?.PlaySFX(SoundManager.Instance.strongShotSound);
                    weaponSystem?.TriggerRecoil();

                    if (hitInfo.isHit && hitInfo.target != null)
                    {
                        totalHits++;
                        totalDamageDealt += 3;
                        if (hitInfo.isHeadshot)
                        {
                            currentScore += 1;
                            headshotsCount++;
                            SoundManager.Instance?.PlayHeadshotSound();
                            uiManager?.ShowHeadshotPopup(hitInfo.hitPoint);
                            uiManager?.UpdateScore(currentScore);
                        }
                        hitInfo.target.TakeDamage(3);
                    }
                    break;

                case SpellType.Ice:
                    if (!isIceReady) return;
                    totalShotsFired++;
                    strongShotsCount++;
                    isIceReady = false;
                    iceTimer = iceCooldownDuration;
                    uiManager?.UpdateSpellCooldown(SpellType.Ice, false, iceCooldownDuration);
                    SoundManager.Instance?.PlayIceCastSound();
                    weaponSystem?.TriggerRecoil();

                    if (hitInfo.isHit && hitInfo.target != null)
                    {
                        totalHits++;
                        totalDamageDealt += 1;
                        if (hitInfo.isHeadshot)
                        {
                            currentScore += 1;
                            headshotsCount++;
                            SoundManager.Instance?.PlayHeadshotSound();
                            uiManager?.ShowHeadshotPopup(hitInfo.hitPoint);
                            uiManager?.UpdateScore(currentScore);
                        }
                        hitInfo.target.TakeDamage(1);
                        hitInfo.target.Freeze(2.5f);
                        SpellEffectsManager.Instance?.SpawnIceEffect(hitInfo.target, 2.5f);
                    }
                    break;

                case SpellType.Lightning:
                    if (!isLightningReady) return;
                    totalShotsFired++;
                    strongShotsCount++;
                    isLightningReady = false;
                    lightningTimer = lightningCooldownDuration;
                    uiManager?.UpdateSpellCooldown(SpellType.Lightning, false, lightningCooldownDuration);
                    SoundManager.Instance?.PlayLightningCastSound();
                    weaponSystem?.TriggerRecoil();

                    Vector3 strikePos = hitInfo.isHit && hitInfo.target != null ? hitInfo.target.transform.position : hitInfo.hitPoint;
                    strikePos.z = 0f;

                    var lightningChainPoints = new List<Vector3>();

                    if (hitInfo.isHit && hitInfo.target != null)
                    {
                        totalHits++;
                        totalDamageDealt += 2;
                        if (hitInfo.isHeadshot)
                        {
                            currentScore += 1;
                            headshotsCount++;
                            SoundManager.Instance?.PlayHeadshotSound();
                            uiManager?.ShowHeadshotPopup(hitInfo.hitPoint);
                            uiManager?.UpdateScore(currentScore);
                        }
                        hitInfo.target.TakeDamage(2);
                        lightningChainPoints.Add(hitInfo.target.transform.position);
                    }
                    else
                    {
                        lightningChainPoints.Add(strikePos);
                    }

                    // Relâmpago em Cadeia: Atinge até 2 outros goblins próximos em um raio de 3.5m
                    int chainCount = 0;
                    foreach (var goblin in WizardController.ActiveGoblins)
                    {
                        if (goblin == null || goblin == hitInfo.target) continue;
                        if (Vector2.Distance(strikePos, goblin.transform.position) <= 3.5f)
                        {
                            goblin.TakeDamage(1);
                            totalDamageDealt += 1;
                            totalHits++;
                            lightningChainPoints.Add(goblin.transform.position);
                            chainCount++;
                            if (chainCount >= 2) break;
                        }
                    }

                    SpellEffectsManager.Instance?.SpawnLightningChain(strikePos, lightningChainPoints);
                    break;

                case SpellType.Area:
                    if (!isAreaReady) return;
                    totalShotsFired++;
                    strongShotsCount++;
                    isAreaReady = false;
                    areaTimer = areaCooldownDuration;
                    uiManager?.UpdateSpellCooldown(SpellType.Area, false, areaCooldownDuration);
                    SoundManager.Instance?.PlayAreaCastSound();
                    weaponSystem?.TriggerRecoil();

                    Vector3 center = hitInfo.isHit && hitInfo.target != null ? hitInfo.target.transform.position : hitInfo.hitPoint;
                    center.z = 0f;

                    // Aciona o efeito visual expansivo de explosão em área
                    SpellEffectsManager.Instance?.SpawnAreaExplosion(center, 2.5f);

                    if (hitInfo.isHeadshot)
                    {
                        currentScore += 1;
                        headshotsCount++;
                        SoundManager.Instance?.PlayHeadshotSound();
                        uiManager?.ShowHeadshotPopup(hitInfo.hitPoint);
                        uiManager?.UpdateScore(currentScore);
                    }

                    // Feitiço de Área: Explosão radial de 2.5m de raio causando 2 de dano a todos os goblins no raio
                    var affectedGoblins = new List<WizardController>();
                    foreach (var goblin in WizardController.ActiveGoblins)
                    {
                        if (goblin == null) continue;
                        if (Vector2.Distance(center, goblin.transform.position) <= 2.5f)
                        {
                            affectedGoblins.Add(goblin);
                        }
                    }
                    if (affectedGoblins.Count > 0)
                    {
                        totalHits += affectedGoblins.Count;
                        totalDamageDealt += affectedGoblins.Count * 2;
                    }
                    foreach (var goblin in affectedGoblins)
                    {
                        goblin.TakeDamage(2);
                    }
                    break;
            }
        }

        private void HandleShotMissed()
        {
            if (currentState != GameState.Playing) return;

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
            uiManager?.ShowBossSpawnBanner(data.displayName, "O CHEFE ENTROU NO CAMPO DE BATALHA!");
        }

        private void HandleWizardDefeated(WizardController wizard, int basePoints)
        {
            if (currentState != GameState.Playing) return;

            wizardsDefeated++;

            // Incrementa sequência de combo
            currentStreak++;
            if (currentStreak > maxStreak) maxStreak = currentStreak;

            currentMultiplier = CalculateMultiplier(currentStreak);

            // Aplica multiplicador na pontuação
            int pointsEarned = basePoints * currentMultiplier;
            currentScore += pointsEarned;

            uiManager?.UpdateScore(currentScore);
            uiManager?.UpdateCombo(currentStreak, currentMultiplier);

            WizardDataSO data = wizard.GetData();
            bool isBoss = data != null && WaveConfig.IsBossType(data.wizardType);

            if (isBoss)
            {
                // Celebração de Boss Derrotado
                SpellEffectsManager.Instance?.TriggerCameraShake(0.35f, 0.32f);
                SoundManager.Instance?.PlaySFX(SoundManager.Instance.victorySound);
                uiManager?.ShowBossDefeatedBanner(data.displayName, pointsEarned);

                // No 50º goblin (Chefe Final): Derrotar ele = VITÓRIA IMEDIATA!
                if (data.wizardType == WizardType.ChefeFinal)
                {
                    StartCoroutine(FinalBossVictorySequenceRoutine());
                    return;
                }
            }

            // Som de Morte
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
            EndGame(won: true);
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
                EndGame(won: false);
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
            // 1. Fator Mortes (Magos Abatidos): até 500 pts (10 pts por mago)
            int killsScore = kills * 10;

            // 2. Fator Precisão (% de acertos): até 250 pts
            int accuracyScore = Mathf.RoundToInt((Mathf.Clamp(accuracy, 0f, 100f) / 100f) * 250f);

            // 3. Fator Tempo: até 200 pts de bônus por rapidez e eficiência
            // <= 60s dá 200 pts; decai gradativamente até 240s; multiplicado pelo progresso de abates
            float timeFactor = Mathf.Clamp01(1f - (Mathf.Max(0f, durationSeconds - 60f) / 180f));
            float progressRatio = Mathf.Clamp01(kills / 40f);
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
                badge = "👑";
                color = new Color(1f, 0.84f, 0.0f); // Dourado Radiante
            }
            else if (totalRankScore >= 850)
            {
                grade = "A";
                title = "Mago Mestre";
                badge = "🔮";
                color = new Color(0.78f, 0.35f, 1f); // Púrpura Arcano
            }
            else if (totalRankScore >= 650)
            {
                grade = "B";
                title = "Feiticeiro Adepto";
                badge = "⚡";
                color = new Color(0.15f, 0.82f, 1f); // Ciano Brilhante
            }
            else if (totalRankScore >= 450)
            {
                grade = "C";
                title = "Mago Aprendiz";
                badge = "📜";
                color = new Color(0.4f, 0.95f, 0.4f); // Verde Esmeralda
            }
            else
            {
                grade = "D";
                title = "Iniciante Novato";
                badge = "🔰";
                color = new Color(0.7f, 0.7f, 0.75f); // Cinza Prateado
            }

            return (grade, title, badge, color, totalRankScore);
        }

        public static (string title, string badge, Color color) CalculateRank(float accuracy)
        {
            var rank = CalculateMatchRank(50, accuracy, 100f, 0, 10, 5);
            return (rank.title, rank.badge, rank.color);
        }

        private void EndGame(bool won)
        {
            SetState(GameState.GameOver);

            if (waveTransitionCoroutine != null)
            {
                StopCoroutine(waveTransitionCoroutine);
                waveTransitionCoroutine = null;
            }

            spawner?.ClearActiveGoblins();

            if (won)
            {
                SoundManager.Instance?.PlaySFX(SoundManager.Instance.victorySound);
            }
            else
            {
                SoundManager.Instance?.PlaySFX(SoundManager.Instance.defeatSound);
            }

            float accuracy = totalShotsFired > 0 ? ((float)totalHits / totalShotsFired) * 100f : 0f;
            var rank = CalculateMatchRank(wizardsDefeated, accuracy, matchDuration, currentEscaped, maxStreak, strongShotsCount);

            var stats = new GamePerformanceStats
            {
                won = won,
                finalScore = currentScore,
                escapedCount = currentEscaped,
                wizardsDefeated = wizardsDefeated,
                totalShotsFired = totalShotsFired,
                totalHits = totalHits,
                accuracy = accuracy,
                strongShotsCount = strongShotsCount,
                totalDamageDealt = totalDamageDealt,
                maxStreak = maxStreak,
                headshotsCount = headshotsCount,
                matchDuration = matchDuration,
                rankGrade = rank.grade,
                rankScore = rank.rankScore,
                rankTitle = rank.title,
                rankBadge = rank.badge,
                rankColor = rank.color
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

            int waveNumber = currentWaveIndex + 1;
            WaveConfig config = waves[currentWaveIndex];

            spawner?.StartWave(config, waveNumber);
            uiManager?.ShowWaveStartBanner(waveNumber, config.waveName, config.waveDescription);
            uiManager?.UpdateWaveProgress(waveNumber, waves.Count, config.TotalEnemies, config.TotalEnemies);

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

            if (waveTransitionCoroutine != null) StopCoroutine(waveTransitionCoroutine);
            waveTransitionCoroutine = StartCoroutine(WaveTransitionRoutine(completedWaveNumber));
        }

        private IEnumerator WaveTransitionRoutine(int completedWaveNumber)
        {
            // Bônus de pontuação proporcional à onda e restauração completa de mana
            int bonus = waveClearBonusBase * completedWaveNumber;
            currentScore += bonus;
            uiManager?.UpdateScore(currentScore);

            weaponSystem?.RefillAmmo();

            // Fanfarra de conclusão de onda
            SoundManager.Instance?.PlaySFX(SoundManager.Instance.victorySound);
            uiManager?.ShowWaveClearedBanner(completedWaveNumber, bonus);

            yield return new WaitForSeconds(delayBetweenWaves);

            currentWaveIndex++;
            if (currentWaveIndex >= waves.Count)
            {
                EndGame(won: true);
            }
            else
            {
                StartNextWave();
            }
        }

        public void PopulateDefaultWaves()
        {
            waves = new List<WaveConfig>
            {
                // Onda 1 (Goblins 1 a 10): 6 Comuns + 3 Fugitivos + 1 Grande Goblin (10º Goblin)
                new WaveConfig("Fortaleza Sob Cerco", "6 Comuns + 3 Fugitivos + 👑 GRANDE GOBLIN (10º Goblin)!", 1.35f, 1.0f,
                    (WizardType.Comum, 6),
                    (WizardType.Fugitivo, 3),
                    (WizardType.Chefe, 1)),

                // Onda 2 (Goblins 11 a 20): 4 Comuns + 3 Fugitivos + 2 Dourados + 1 Xamã do Caos (20º Goblin)
                new WaveConfig("Fúria Flamejante", "4 Comuns + 3 Fugitivos + 2 Dourados + 🔥 XAMÃ DO CAOS (20º Goblin)!", 1.15f, 1.12f,
                    (WizardType.Comum, 4),
                    (WizardType.Fugitivo, 3),
                    (WizardType.Dourado, 2),
                    (WizardType.Chefe2, 1)),

                // Onda 3 (Goblins 21 a 30): 3 Comuns + 3 Dourados + 3 Fantasmas + 1 Feiticeiro Espectral (30º Goblin)
                new WaveConfig("Sombras Espectrais", "3 Comuns + 3 Dourados + 3 Fantasmas + 🔮 FEITICEIRO ESPECTRAL (30º Goblin)!", 1.0f, 1.22f,
                    (WizardType.Comum, 3),
                    (WizardType.Dourado, 3),
                    (WizardType.Fantasma, 3),
                    (WizardType.Chefe3, 1)),

                // Onda 4 (Goblins 31 a 40): 2 Comuns + 3 Fugitivos + 2 Dourados + 2 Fantasmas + 1 Lorde Carmesim (40º Goblin)
                new WaveConfig("Legião Carmesim", "2 Comuns + 3 Fugitivos + 2 Dourados + 2 Fantasmas + 🩸 LORDE CARMESIM (40º Goblin)!", 0.9f, 1.32f,
                    (WizardType.Comum, 2),
                    (WizardType.Fugitivo, 3),
                    (WizardType.Dourado, 2),
                    (WizardType.Fantasma, 2),
                    (WizardType.Chefe4, 1)),

                // Onda 5 (Goblins 41 a 50): 2 Fugitivos + 3 Dourados + 4 Fantasmas + ⚔️ GOBLIN REI SUPREMO (50º Goblin)
                new WaveConfig("O Confronto Supremo", "2 Fugitivos + 3 Dourados + 4 Fantasmas + ⚔️ GOBLIN REI SUPREMO (50º Goblin)!", 0.8f, 1.4f,
                    (WizardType.Fugitivo, 2),
                    (WizardType.Dourado, 3),
                    (WizardType.Fantasma, 4),
                    (WizardType.ChefeFinal, 1))
            };
        }

        #endregion
    }
}