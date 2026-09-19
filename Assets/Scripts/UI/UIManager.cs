using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WizardGame.Core;
using WizardGame.Data;
using WizardGame.Entities;

namespace WizardGame.UI
{
    /// <summary>
    /// Gerencia a interface completa: HUD em tempo real, Arsenal Mágico (Barra de Feitiços),
    /// Sistema de Combos, Popups de Headshot, Menu Inicial, Pause e Telas de Fim de Jogo.
    /// Adaptado para o tema de caçada aos Goblins.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Painéis Principais")]
        [SerializeField] private GameObject startMenuPanel;
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject instructionsPanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject inGameHUD;

        [Header("Elementos do HUD")]
        [SerializeField] private Text scoreText;
        [SerializeField] private Text escapeText;
        [SerializeField] private Text playerHealthText;
        [SerializeField] private Text ammoText;
        [SerializeField] private Text strongShotText;
        [SerializeField] private Text controlsHintText;
        [SerializeField] private Button muteButton;
        [SerializeField] private Text muteButtonText;
        [SerializeField] private Image damageFlashOverlay;

        [Header("Arsenal Mágico (Feitiços Especiais)")]
        [SerializeField] private GameObject spellArsenalContainer;
        [SerializeField] private Image arcaneHighlight;
        [SerializeField] private Text arcaneCooldownText;
        [SerializeField] private Image iceHighlight;
        [SerializeField] private Text iceCooldownText;
        [SerializeField] private Image lightningHighlight;
        [SerializeField] private Text lightningCooldownText;
        [SerializeField] private Image areaHighlight;
        [SerializeField] private Text areaCooldownText;
        [SerializeField] private Text activeSpellNoticeText;

        [Header("Sistema de Combo Visual")]
        [SerializeField] private GameObject comboContainer;
        [SerializeField] private Text comboText;
        [SerializeField] private Text comboBreakText;

        [Header("Efeito de Headshot")]
        [SerializeField] private GameObject headshotPopupContainer;
        [SerializeField] private Text headshotPopupText;

        [Header("Sistema de Power-ups (Buffs Ativos)")]
        [SerializeField] private GameObject activeBuffsContainer;
        [SerializeField] private Text activeBuffsText;

        [Header("Sistema de Ondas (Wave System UI)")]
        [SerializeField] private Text waveText;
        [SerializeField] private GameObject waveBannerContainer;
        [SerializeField] private Text waveBannerTitle;
        [SerializeField] private Text waveBannerSubtitle;

        [Header("Elementos de Fim de Jogo")]
        [SerializeField] private Text endTitleText;
        [SerializeField] private Text endRankGradeText;
        [SerializeField] private Text endScoreText;
        [SerializeField] private Text endStatsText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button endMainMenuButton;

        [Header("Elementos do Menu Inicial")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button instructionsButton;
        [SerializeField] private Button closeInstructionsButton;
        [SerializeField] private Button quitButton;

        [Header("Elementos do Menu de Pausa")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseRestartButton;
        [SerializeField] private Button pauseMainMenuButton;

        [Header("Controle de Volume (Menu Inicial)")]
        [SerializeField] private Slider startBgmSlider;
        [SerializeField] private Text startBgmText;
        [SerializeField] private Slider startSfxSlider;
        [SerializeField] private Text startSfxText;

        [Header("Controle de Volume (Menu de Pausa)")]
        [SerializeField] private Slider pauseBgmSlider;
        [SerializeField] private Text pauseBgmText;
        [SerializeField] private Slider pauseSfxSlider;
        [SerializeField] private Text pauseSfxText;

        public event Action OnPlayClicked;
        public event Action OnResumeClicked;
        public event Action OnRestartClicked;
        public event Action OnMainMenuClicked;

        private Coroutine comboPulseCoroutine;
        private Coroutine comboBreakCoroutine;
        private Coroutine headshotCoroutine;
        private Coroutine waveBannerCoroutine;
        private Coroutine damageFlashCoroutine;

        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(() => OnPlayClicked?.Invoke());
            if (resumeButton != null) resumeButton.onClick.AddListener(() => OnResumeClicked?.Invoke());
            if (restartButton != null) restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
            if (pauseRestartButton != null) pauseRestartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
            if (endMainMenuButton != null) endMainMenuButton.onClick.AddListener(() => OnMainMenuClicked?.Invoke());
            if (pauseMainMenuButton != null) pauseMainMenuButton.onClick.AddListener(() => OnMainMenuClicked?.Invoke());

            if (muteButton != null) muteButton.onClick.AddListener(ToggleMute);
            if (instructionsButton != null) instructionsButton.onClick.AddListener(() => ShowInstructions(true));
            if (closeInstructionsButton != null) closeInstructionsButton.onClick.AddListener(() => ShowInstructions(false));
            if (quitButton != null) quitButton.onClick.AddListener(() => Application.Quit());

            if (comboContainer != null) comboContainer.SetActive(false);
            if (headshotPopupContainer != null) headshotPopupContainer.SetActive(false);
            if (comboBreakText != null) comboBreakText.gameObject.SetActive(false);
            if (waveBannerContainer != null) waveBannerContainer.SetActive(false);
            if (damageFlashOverlay != null) damageFlashOverlay.gameObject.SetActive(false);
            if (activeBuffsContainer != null) activeBuffsContainer.SetActive(false);
        }

        public void ShowStartMenu()
        {
            if (startMenuPanel != null) startMenuPanel.SetActive(true);
            if (inGameHUD != null) inGameHUD.SetActive(false);
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (instructionsPanel != null) instructionsPanel.SetActive(false);
            if (waveBannerContainer != null) waveBannerContainer.SetActive(false);
            Cursor.visible = true;
        }

        public void ShowGameplayHUD()
        {
            if (startMenuPanel != null) startMenuPanel.SetActive(false);
            if (inGameHUD != null) inGameHUD.SetActive(true);
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (instructionsPanel != null) instructionsPanel.SetActive(false);
            Cursor.visible = false;
        }

        public void ShowPauseMenu(bool paused)
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(paused);
            Cursor.visible = paused;
        }

        public void ShowInstructions(bool show)
        {
            if (instructionsPanel != null) instructionsPanel.SetActive(show);
        }

        public void ShowGameOver(GamePerformanceStats stats)
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            if (inGameHUD != null) inGameHUD.SetActive(false);
            Cursor.visible = true;

            string colorHex = ColorUtility.ToHtmlStringRGB(stats.rankColor);

            if (endTitleText != null)
            {
                if (stats.won)
                {
                    endTitleText.text = "VITÓRIA";
                    endTitleText.color = new Color(1f, 0.88f, 0.2f);
                }
                else
                {
                    string reason = !string.IsNullOrEmpty(stats.defeatReason) ? $"\n<size=18><color=#FF7777>{stats.defeatReason}</color></size>" : "";
                    endTitleText.text = $"DERROTA{reason}";
                    endTitleText.color = new Color(1f, 0.35f, 0.35f);
                }
            }

            if (endRankGradeText != null)
            {
                endRankGradeText.text = stats.rankGrade;
                endRankGradeText.color = stats.rankColor;
            }

            if (endScoreText != null)
            {
                if (endRankGradeText != null)
                {
                    endScoreText.text = $"<size=22><b>{stats.rankBadge} {stats.rankTitle.ToUpper()}</b></size>";
                    endScoreText.color = stats.rankColor;
                }
                else
                {
                    // Fallback visual caso endRankGradeText não tenha sido associado no Inspector
                    endScoreText.text = $"<size=74><b>{stats.rankGrade}</b></size>\n" +
                                        $"<size=20><b>{stats.rankBadge} {stats.rankTitle.ToUpper()}</b></size>";
                    endScoreText.color = stats.rankColor;
                }
            }

            if (endStatsText != null)
            {
                int minutes = Mathf.FloorToInt(stats.matchDuration / 60f);
                int seconds = Mathf.FloorToInt(stats.matchDuration % 60f);
                string timeFormatted = $"{minutes:00}:{seconds:00}";

                endStatsText.text =
                    $"<size=24><b>{stats.wizardsDefeated} MAGOS</b></size>\n" +
                    $"<size=24><b>{stats.accuracy:F0}% PRECISÃO</b></size>\n" +
                    $"<size=24><b>COMBO x{stats.maxStreak}</b></size>\n\n" +
                    $"<size=30><color=#{colorHex}><b>{stats.finalScore} PONTOS</b></color></size>\n\n" +
                    $"<size=15><color=#A8B2C0>Tempo: {timeFormatted}   •   Tiros Fortes: {stats.strongShotsCount}   •   Bônus de Habilidade: {stats.skillBonusesCount} (+{stats.skillBonusTotalPoints} pts)\n" +
                    $"Disparos: {stats.totalShotsFired}   |   Acertos: {stats.totalHits}   |   Dano: {stats.totalDamageDealt}   |   Fugitivos: {stats.escapedCount}/15</color></size>";
            }
        }

        public void ShowGameOver(bool won, int score, int escaped, int maxStreak, int headshots)
        {
            float fallbackAcc = 100f;
            var rank = GameManager.CalculateMatchRank(score, fallbackAcc, 90f, escaped, maxStreak, 0);
            var stats = new GamePerformanceStats
            {
                won = won,
                finalScore = score,
                escapedCount = escaped,
                wizardsDefeated = score,
                totalShotsFired = Math.Max(1, score),
                totalHits = score,
                accuracy = fallbackAcc,
                strongShotsCount = 0,
                totalDamageDealt = score,
                maxStreak = maxStreak,
                headshotsCount = headshots,
                skillBonusesCount = 0,
                skillBonusTotalPoints = 0,
                matchDuration = 90f,
                rankGrade = rank.grade,
                rankScore = rank.rankScore,
                rankTitle = rank.title,
                rankBadge = rank.badge,
                rankColor = rank.color,
                defeatReason = won ? "" : "MUITOS GOBLINS ESCAPARAM (15/15)!"
            };
            ShowGameOver(stats);
        }

        public void UpdateScore(int score, int target = -1)
        {
            if (scoreText != null)
            {
                scoreText.text = target > 0 ? $"Pontos: {score} / {target}" : $"Pontos: {score}";
            }
        }

        public void UpdateEscapes(int current, int max)
        {
            if (escapeText != null)
            {
                escapeText.text = $"Fugiram: {current} / {max}";
                escapeText.color = current >= (max - 3) ? Color.red : new Color(1f, 0.6f, 0.6f);
            }
        }

        public void UpdatePlayerHealth(int current, int max)
        {
            if (playerHealthText == null) return;

            string hearts = "";
            for (int i = 0; i < max; i++)
            {
                hearts += (i < current) ? "❤️" : "🖤";
            }

            playerHealthText.text = $"VIDA: {hearts} ({Mathf.Max(0, current)}/{max})";
            if (current <= 1)
            {
                playerHealthText.color = new Color(1f, 0.25f, 0.25f);
            }
            else if (current <= 2)
            {
                playerHealthText.color = new Color(1f, 0.65f, 0.2f);
            }
            else
            {
                playerHealthText.color = new Color(0.4f, 1f, 0.4f);
            }
        }

        public void UpdateAmmo(int current, int max, bool isReloading)
        {
            if (ammoText == null) return;

            if (isReloading)
            {
                ammoText.text = "MANA: [RECARREGANDO...]";
                ammoText.color = Color.yellow;
            }
            else
            {
                ammoText.text = $"MANA: {current} / {max}  [R]";
                ammoText.color = current <= 2 ? Color.red : Color.cyan;
            }
        }

        public void UpdateStrongCooldown(bool ready, float remainingSeconds = 0f)
        {
            if (strongShotText != null)
            {
                if (ready)
                {
                    strongShotText.text = "TIRO FORTE [RMB]: PRONTO!";
                    strongShotText.color = Color.green;
                }
                else
                {
                    strongShotText.text = $"TIRO FORTE [RMB]: {remainingSeconds:F1}s";
                    strongShotText.color = new Color(1f, 0.4f, 0.4f);
                }
            }

            UpdateSpellCooldown(SpellType.Arcane, ready, remainingSeconds);
        }

        public void UpdateSelectedSpell(SpellType spell)
        {
            Color activeColor = new Color(1f, 0.85f, 0.2f, 1f);
            Color inactiveColor = new Color(0.25f, 0.25f, 0.35f, 0.65f);

            if (arcaneHighlight != null) arcaneHighlight.color = (spell == SpellType.Arcane) ? activeColor : inactiveColor;
            if (iceHighlight != null) iceHighlight.color = (spell == SpellType.Ice) ? activeColor : inactiveColor;
            if (lightningHighlight != null) lightningHighlight.color = (spell == SpellType.Lightning) ? activeColor : inactiveColor;
            if (areaHighlight != null) areaHighlight.color = (spell == SpellType.Area) ? activeColor : inactiveColor;

            if (activeSpellNoticeText != null)
            {
                string spellDesc = spell switch
                {
                    SpellType.Arcane => "💥 TIRO ARCANO (Dano 3)",
                    SpellType.Ice => "❄️ FEITIÇO DE GELO (Congela 2.5s)",
                    SpellType.Lightning => "⚡ RELÂMPAGO (Corrente em até 3)",
                    SpellType.Area => "🌀 FEITIÇO DE ÁREA (Explosão mágica)",
                    _ => "TIRO ESPECIAL"
                };
                activeSpellNoticeText.text = $"[RMB]: {spellDesc}";
            }
        }

        public void UpdateSpellCooldown(SpellType spell, bool ready, float remainingSeconds = 0f)
        {
            Text targetText = spell switch
            {
                SpellType.Arcane => arcaneCooldownText,
                SpellType.Ice => iceCooldownText,
                SpellType.Lightning => lightningCooldownText,
                SpellType.Area => areaCooldownText,
                _ => null
            };

            if (targetText == null) return;

            if (ready)
            {
                targetText.text = "PRONTO";
                targetText.color = new Color(0.2f, 1f, 0.35f);
            }
            else
            {
                targetText.text = $"{remainingSeconds:F1}s";
                targetText.color = new Color(1f, 0.45f, 0.45f);
            }
        }

        #region Sistema de Combo

        public void UpdateCombo(int streak, int multiplier)
        {
            if (comboContainer == null || comboText == null) return;

            if (streak >= 2)
            {
                comboContainer.SetActive(true);
                comboText.text = $"COMBO x{multiplier} - {streak} ABATES";

                if (multiplier >= 4) comboText.color = new Color(1f, 0.2f, 1f); // Magenta
                else if (multiplier == 3) comboText.color = new Color(1f, 0.3f, 0.1f); // Vermelho
                else if (multiplier == 2) comboText.color = new Color(1f, 0.7f, 0f); // Laranja
                else comboText.color = new Color(1f, 0.95f, 0.4f); // Amarelo

                if (comboPulseCoroutine != null) StopCoroutine(comboPulseCoroutine);
                comboPulseCoroutine = StartCoroutine(PunchScaleRoutine(comboContainer.transform));
            }
            else
            {
                comboContainer.SetActive(false);
            }
        }

        public void NotifyComboBroken(string reason)
        {
            if (comboContainer != null) comboContainer.SetActive(false);

            if (comboBreakText != null)
            {
                if (comboBreakCoroutine != null) StopCoroutine(comboBreakCoroutine);
                comboBreakCoroutine = StartCoroutine(ShowComboBreakRoutine(reason));
            }
        }

        private IEnumerator ShowComboBreakRoutine(string reason)
        {
            comboBreakText.gameObject.SetActive(true);
            comboBreakText.text = $"COMBO QUEBRADO! ({reason})";
            comboBreakText.color = Color.red;

            float elapsed = 0f;
            float dur = 0.9f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            comboBreakText.gameObject.SetActive(false);
        }

        private IEnumerator PunchScaleRoutine(Transform target)
        {
            target.localScale = new Vector3(1.25f, 1.25f, 1f);
            float elapsed = 0f;
            float dur = 0.15f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / dur;
                target.localScale = Vector3.Lerp(new Vector3(1.25f, 1.25f, 1f), Vector3.one, t);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        #endregion

        #region Efeito de Headshot e Bônus de Habilidade

        public void ShowHeadshotPopup(Vector3 worldPoint)
        {
            ShowSkillBonusPopup("🎯 HEADSHOT! +2", new Color(0.25f, 1f, 0.35f));
        }

        public void ShowSkillBonusPopup(string text, Color color)
        {
            if (headshotPopupContainer == null) return;

            if (headshotCoroutine != null) StopCoroutine(headshotCoroutine);
            headshotCoroutine = StartCoroutine(SkillBonusPopupRoutine(text, color));
        }

        private IEnumerator SkillBonusPopupRoutine(string text, Color color)
        {
            headshotPopupContainer.SetActive(true);
            if (headshotPopupText != null)
            {
                headshotPopupText.text = text;
                headshotPopupText.color = color;
            }

            RectTransform rect = headshotPopupContainer.GetComponent<RectTransform>();
            Vector2 initialPos = new Vector2(0f, 130f);
            if (rect != null)
            {
                rect.anchoredPosition = initialPos;
                rect.localScale = new Vector3(1.25f, 1.25f, 1f);
            }

            float elapsed = 0f;
            float dur = 0.95f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / dur;
                if (rect != null)
                {
                    rect.anchoredPosition = initialPos + new Vector2(0f, 45f * t);
                    rect.localScale = Vector3.Lerp(new Vector3(1.25f, 1.25f, 1f), Vector3.one, Mathf.Clamp01(t * 3f));
                }
                yield return null;
            }

            headshotPopupContainer.SetActive(false);
        }

        #endregion

        private void Start()
        {
            InitializeVolumeSliders();
            UpdateMuteUI();
        }

        private void InitializeVolumeSliders()
        {
            if (SoundManager.Instance == null) return;

            float bgm = SoundManager.Instance.BGMVolume;
            float sfx = SoundManager.Instance.SFXVolume;

            if (startBgmSlider != null)
            {
                startBgmSlider.value = bgm;
                startBgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
            }
            if (pauseBgmSlider != null)
            {
                pauseBgmSlider.value = bgm;
                pauseBgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
            }

            if (startSfxSlider != null)
            {
                startSfxSlider.value = sfx;
                startSfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            }
            if (pauseSfxSlider != null)
            {
                pauseSfxSlider.value = sfx;
                pauseSfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            }

            UpdateBgmLabels(bgm);
            UpdateSfxLabels(sfx);
        }

        private void OnBgmSliderChanged(float val)
        {
            SoundManager.Instance?.SetBGMVolume(val);
            if (startBgmSlider != null && Mathf.Abs(startBgmSlider.value - val) > 0.001f) startBgmSlider.value = val;
            if (pauseBgmSlider != null && Mathf.Abs(pauseBgmSlider.value - val) > 0.001f) pauseBgmSlider.value = val;
            UpdateBgmLabels(val);
        }

        private void OnSfxSliderChanged(float val)
        {
            SoundManager.Instance?.SetSFXVolume(val);
            if (startSfxSlider != null && Mathf.Abs(startSfxSlider.value - val) > 0.001f) startSfxSlider.value = val;
            if (pauseSfxSlider != null && Mathf.Abs(pauseSfxSlider.value - val) > 0.001f) pauseSfxSlider.value = val;
            UpdateSfxLabels(val);
        }

        private void UpdateBgmLabels(float val)
        {
            int pct = Mathf.RoundToInt(val * 100f);
            string txt = $"MÚSICA: {pct}%";
            if (startBgmText != null) startBgmText.text = txt;
            if (pauseBgmText != null) pauseBgmText.text = txt;
        }

        private void UpdateSfxLabels(float val)
        {
            int pct = Mathf.RoundToInt(val * 100f);
            string txt = $"EFEITOS: {pct}%";
            if (startSfxText != null) startSfxText.text = txt;
            if (pauseSfxText != null) pauseSfxText.text = txt;
        }

        private void ToggleMute()
        {
            SoundManager.Instance?.ToggleMute();
            UpdateMuteUI();
        }

        public void UpdateMuteUI()
        {
            if (muteButtonText != null && SoundManager.Instance != null)
            {
                muteButtonText.text = SoundManager.Instance.IsMuted ? "SOM: MUDO" : "SOM: ATIVO";
            }
        }

        public void UpdateActiveBuffs(bool active, string buffText)
        {
            if (activeBuffsContainer != null)
            {
                activeBuffsContainer.SetActive(active);
            }
            if (activeBuffsText != null)
            {
                activeBuffsText.text = buffText;
            }
        }

        #region Sistema de Ondas UI

        public void UpdateWaveProgress(int currentWave, int totalWaves, int remainingInWave, int totalInWave)
        {
            if (waveText != null)
            {
                waveText.text = $"⚔️ ONDA {currentWave} / {totalWaves}  •  Restam: {remainingInWave} / {totalInWave}";
            }
        }

        public void ShowWaveStartBanner(int waveNumber, string waveName, string description)
        {
            if (waveBannerContainer == null) return;

            if (waveBannerCoroutine != null) StopCoroutine(waveBannerCoroutine);
            waveBannerCoroutine = StartCoroutine(WaveBannerRoutine(
                title: $"⚔️ ONDA {waveNumber}: {waveName.ToUpper()}",
                subtitle: description,
                titleColor: new Color(1f, 0.85f, 0.2f),
                duration: 2.8f
            ));
        }

        public void ShowWaveClearedBanner(int waveNumber, int bonusPoints)
        {
            if (waveBannerContainer == null) return;

            if (waveBannerCoroutine != null) StopCoroutine(waveBannerCoroutine);
            waveBannerCoroutine = StartCoroutine(WaveBannerRoutine(
                title: $"🎉 ONDA {waveNumber} CONCLUÍDA!",
                subtitle: $"+{bonusPoints} Pontos de Bônus!  •  Mana Totalmente Restaurada!",
                titleColor: new Color(0.25f, 1f, 0.45f),
                duration: 2.4f
            ));
        }

        public void ShowBossSpawnBanner(string bossName, string description)
        {
            if (waveBannerContainer == null) return;

            if (waveBannerCoroutine != null) StopCoroutine(waveBannerCoroutine);
            waveBannerCoroutine = StartCoroutine(WaveBannerRoutine(
                title: $"⚠️ ALERTA DE BOSS: {bossName.ToUpper()}!",
                subtitle: description,
                titleColor: new Color(1f, 0.25f, 0.25f),
                duration: 3.2f
            ));
        }

        public void ShowBossDefeatedBanner(string bossName, int bonusPoints)
        {
            if (waveBannerContainer == null) return;

            if (waveBannerCoroutine != null) StopCoroutine(waveBannerCoroutine);
            waveBannerCoroutine = StartCoroutine(WaveBannerRoutine(
                title: $"👑 BOSS DERROTADO! +{bonusPoints}",
                subtitle: $"{bossName.ToUpper()} FOI ANIQUILADO COM MAESTRIA!",
                titleColor: new Color(1f, 0.85f, 0.15f),
                duration: 3.0f
            ));
        }

        public void ShowPowerUpBanner(string title, string subtitle, Color color)
        {
            if (waveBannerContainer == null) return;

            if (waveBannerCoroutine != null) StopCoroutine(waveBannerCoroutine);
            waveBannerCoroutine = StartCoroutine(WaveBannerRoutine(
                title: title,
                subtitle: subtitle,
                titleColor: color,
                duration: 2.2f
            ));
        }

        private IEnumerator WaveBannerRoutine(string title, string subtitle, Color titleColor, float duration)
        {
            waveBannerContainer.SetActive(true);
            if (waveBannerTitle != null)
            {
                waveBannerTitle.text = title;
                waveBannerTitle.color = titleColor;
            }
            if (waveBannerSubtitle != null)
            {
                waveBannerSubtitle.text = subtitle;
            }

            var canvasGroup = waveBannerContainer.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = waveBannerContainer.AddComponent<CanvasGroup>();

            // Fade In com leve pop de escala
            float elapsed = 0f;
            float fadeInTime = 0.25f;
            waveBannerContainer.transform.localScale = Vector3.one * 0.82f;

            while (elapsed < fadeInTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeInTime;
                canvasGroup.alpha = t;
                waveBannerContainer.transform.localScale = Vector3.Lerp(Vector3.one * 0.82f, Vector3.one, t);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            waveBannerContainer.transform.localScale = Vector3.one;

            yield return new WaitForSeconds(Mathf.Max(0.5f, duration - 0.55f));

            // Fade Out
            elapsed = 0f;
            float fadeOutTime = 0.35f;
            while (elapsed < fadeOutTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutTime;
                canvasGroup.alpha = 1f - t;
                yield return null;
            }

            canvasGroup.alpha = 0f;
            waveBannerContainer.SetActive(false);
            waveBannerCoroutine = null;
        }

        public void TriggerDamageFlash()
        {
            if (damageFlashOverlay == null) return;
            if (damageFlashCoroutine != null)
            {
                StopCoroutine(damageFlashCoroutine);
            }
            damageFlashCoroutine = StartCoroutine(DamageFlashRoutine());
        }

        private IEnumerator DamageFlashRoutine()
        {
            damageFlashOverlay.gameObject.SetActive(true);
            Color baseColor = new Color(0.9f, 0.08f, 0.08f, 0.38f);
            damageFlashOverlay.color = baseColor;

            float elapsed = 0f;
            float duration = 0.35f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                damageFlashOverlay.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(baseColor.a, 0f, t));
                yield return null;
            }

            damageFlashOverlay.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            damageFlashOverlay.gameObject.SetActive(false);
            damageFlashCoroutine = null;
        }

        #endregion
    }
}