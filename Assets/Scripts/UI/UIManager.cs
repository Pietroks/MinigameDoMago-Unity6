using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private GameObject settingsPanel;
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

        [Header("Ícones Visuais de Vida, Mana e Medalhão")]
        [SerializeField] private List<Image> healthHeartImages = new List<Image>();
        [SerializeField] private Sprite heartFullSprite;
        [SerializeField] private Sprite heartEmptySprite;
        [SerializeField] private List<Image> manaCrystalImages = new List<Image>();
        [SerializeField] private Sprite manaCrystalOnSprite;
        [SerializeField] private Sprite manaCrystalOffSprite;
        [SerializeField] private Image manaOrbImage;
        [SerializeField] private Image spellMedallionGlowImage;

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

        [Header("Sistema de Ondas e Transição de Fases")]
        [SerializeField] private Text waveText;
        [SerializeField] private GameObject waveBannerContainer;
        [SerializeField] private Text waveBannerTitle;
        [SerializeField] private Text waveBannerSubtitle;
        [SerializeField] private GameObject stageTransitionPanel;
        [SerializeField] private Text stageTransitionTitleText;
        [SerializeField] private Text stageTransitionSubtitleText;
        [SerializeField] private Text stageTransitionDetailText;

        [Header("Barra de Vida do Chefe & Recorde")]
        [SerializeField] private GameObject bossBarContainer;
        [SerializeField] private Text bossBarLabelText;
        [SerializeField] private RectTransform bossBarFillRect;
        [SerializeField] private Image bossBarFillImage;
        [SerializeField] private Text menuHighScoreText;

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
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button closeSettingsButton;
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

        private SpellType currentlySelectedSpell = SpellType.Arcane;
        private readonly Dictionary<SpellType, bool> spellReadyMap = new Dictionary<SpellType, bool>();
        private readonly Dictionary<SpellType, float> spellCooldownMap = new Dictionary<SpellType, float>();
        private bool isLowHealthWarningActive = false;
        private float targetBossHealthFill = 1f;
        private float currentBossHealthFill = 1f;
        private bool isBossBarEnraged = false;

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
            if (settingsButton != null) settingsButton.onClick.AddListener(() => ShowSettings(true));
            if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(() => ShowSettings(false));
            if (quitButton != null) quitButton.onClick.AddListener(() => Application.Quit());

            if (comboContainer != null) comboContainer.SetActive(false);
            if (headshotPopupContainer != null) headshotPopupContainer.SetActive(false);
            if (comboBreakText != null) comboBreakText.gameObject.SetActive(false);
            if (waveBannerContainer != null) waveBannerContainer.SetActive(false);
            if (stageTransitionPanel != null) stageTransitionPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (damageFlashOverlay != null) damageFlashOverlay.gameObject.SetActive(false);
            if (activeBuffsContainer != null) activeBuffsContainer.SetActive(false);
            if (bossBarContainer != null) bossBarContainer.SetActive(false);
        }

        public void ShowStartMenu()
        {
            isLowHealthWarningActive = false;
            if (startMenuPanel != null) startMenuPanel.SetActive(true);
            if (inGameHUD != null) inGameHUD.SetActive(false);
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (instructionsPanel != null) instructionsPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (waveBannerContainer != null) waveBannerContainer.SetActive(false);
            if (stageTransitionPanel != null) stageTransitionPanel.SetActive(false);
            if (bossBarContainer != null) bossBarContainer.SetActive(false);
            if (damageFlashOverlay != null) damageFlashOverlay.gameObject.SetActive(false);
            Cursor.visible = true;
        }

        public void ShowGameplayHUD()
        {
            if (startMenuPanel != null) startMenuPanel.SetActive(false);
            if (inGameHUD != null) inGameHUD.SetActive(true);
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (instructionsPanel != null) instructionsPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
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
            if (show && settingsPanel != null) settingsPanel.SetActive(false);
        }

        public void ShowSettings(bool show)
        {
            if (settingsPanel != null) settingsPanel.SetActive(show);
            if (show && instructionsPanel != null) instructionsPanel.SetActive(false);
        }

        public void ShowGameOver(GamePerformanceStats stats)
        {
            isLowHealthWarningActive = false;
            if (bossBarContainer != null) bossBarContainer.SetActive(false);
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
                    endScoreText.text = $"<size=22><b>{stats.rankTitle.ToUpper()}</b></size>";
                    endScoreText.color = stats.rankColor;
                }
                else
                {
                    endScoreText.text = $"<size=74><b>{stats.rankGrade}</b></size>\n" +
                                        $"<size=20><b>{stats.rankTitle.ToUpper()}</b></size>";
                    endScoreText.color = stats.rankColor;
                }
            }

            if (endStatsText != null)
            {
                int minutes = Mathf.FloorToInt(stats.matchDuration / 60f);
                int seconds = Mathf.FloorToInt(stats.matchDuration % 60f);
                string timeFormatted = $"{minutes:00}:{seconds:00}";

                string recordBadge = stats.isNewRecord
                    ? "   <size=18><color=#FFE047><b>• NOVO RECORDE! •</b></color></size>"
                    : (stats.bestScore > 0 ? $"   <size=15><color=#A8B6C8>(Recorde: {stats.bestScore:D5} • Rank {stats.bestRankGrade})</color></size>" : "");

                endStatsText.text =
                    $"<size=23><b>{stats.wizardsDefeated} INIMIGOS ABATIDOS   •   {stats.accuracy:F0}% PRECISÃO   •   COMBO ×{stats.maxStreak}</b></size>\n\n" +
                    $"<size=32><color=#{colorHex}><b>{stats.finalScore:D5} PONTOS</b></color></size>{recordBadge}\n\n" +
                    $"<size=15><color=#B8C4D4>Tempo: {timeFormatted}   •   Magias Especiais: {stats.strongShotsCount}   •   Bônus: {stats.skillBonusesCount} (+{stats.skillBonusTotalPoints} pts)\n" +
                    $"Disparos: {stats.totalShotsFired}   •   Acertos: {stats.totalHits}   •   Dano Total: {stats.totalDamageDealt}   •   Fugas: {stats.escapedCount}/{(stats.maxEscapes > 0 ? stats.maxEscapes : 15)}</color></size>";
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
                maxEscapes = 15,
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
                defeatReason = won ? "" : $"MUITOS GOBLINS ESCAPARAM ({escaped}/15)!"
            };
            ShowGameOver(stats);
        }

        public void UpdateScore(int score, int target = -1)
        {
            if (scoreText != null)
            {
                scoreText.text = target > 0 ? $"{score:D5} / {target}" : $"{score:D5}";
            }
        }

        public void UpdateEscapes(int current, int max)
        {
            if (escapeText != null)
            {
                escapeText.text = $"{current} / {max}";
                escapeText.color = current >= (max - 3)
                    ? new Color(1f, 0.35f, 0.35f)
                    : new Color(0.95f, 0.85f, 0.72f);
            }
        }

        public void UpdatePlayerHealth(int current, int max)
        {
            if (healthHeartImages != null && healthHeartImages.Count > 0)
            {
                for (int i = 0; i < healthHeartImages.Count; i++)
                {
                    if (healthHeartImages[i] == null) continue;
                    bool visible = i < max;
                    healthHeartImages[i].gameObject.SetActive(visible);
                    if (visible)
                    {
                        bool filled = i < current;
                        healthHeartImages[i].sprite = filled ? heartFullSprite : heartEmptySprite;
                        healthHeartImages[i].color = filled ? Color.white : new Color(0.75f, 0.75f, 0.85f, 0.85f);
                    }
                }
            }

            isLowHealthWarningActive = (current == 1 && max > 1);
            if (!isLowHealthWarningActive && damageFlashCoroutine == null && damageFlashOverlay != null)
            {
                damageFlashOverlay.gameObject.SetActive(false);
            }

            if (playerHealthText == null) return;

            if (healthHeartImages != null && healthHeartImages.Count > 0)
            {
                playerHealthText.text = $"{Mathf.Max(0, current)}/{max}";
            }
            else
            {
                string hearts = "";
                for (int i = 0; i < max; i++)
                {
                    hearts += (i < current) ? "♥" : "·";
                }
                playerHealthText.text = $"VIDA: {hearts} ({Mathf.Max(0, current)}/{max})";
            }

            if (current <= 1) playerHealthText.color = new Color(1f, 0.3f, 0.3f);
            else if (current <= 2) playerHealthText.color = new Color(1f, 0.72f, 0.28f);
            else playerHealthText.color = new Color(0.95f, 0.9f, 0.82f);
        }

        public void UpdateAmmo(int current, int max, bool isReloading)
        {
            if (manaCrystalImages != null && manaCrystalImages.Count > 0)
            {
                for (int i = 0; i < manaCrystalImages.Count; i++)
                {
                    if (manaCrystalImages[i] == null) continue;
                    bool activeSlot = i < max;
                    manaCrystalImages[i].gameObject.SetActive(activeSlot);
                    if (!activeSlot) continue;

                    bool hasCharge = !isReloading && i < current;
                    if (hasCharge && manaCrystalOnSprite != null)
                    {
                        manaCrystalImages[i].sprite = manaCrystalOnSprite;
                        manaCrystalImages[i].color = Color.white;
                    }
                    else if (manaCrystalOffSprite != null)
                    {
                        manaCrystalImages[i].sprite = manaCrystalOffSprite;
                        manaCrystalImages[i].color = isReloading
                            ? new Color(1f, 0.85f, 0.35f, 0.7f)
                            : new Color(0.7f, 0.75f, 0.9f, 0.75f);
                    }
                }
            }

            if (manaOrbImage != null)
            {
                if (isReloading) manaOrbImage.color = new Color(1f, 0.82f, 0.32f, 1f);
                else if (current <= 2) manaOrbImage.color = new Color(1f, 0.4f, 0.4f, 1f);
                else manaOrbImage.color = Color.white;
            }

            if (ammoText == null) return;

            if (isReloading)
            {
                ammoText.text = "RECARREGANDO...";
                ammoText.color = new Color(1f, 0.85f, 0.25f);
            }
            else
            {
                ammoText.text = $"{current}/{max}  [R]";
                ammoText.color = current <= 2 ? new Color(1f, 0.35f, 0.35f) : new Color(0.55f, 0.92f, 1f);
            }
        }

        public void UpdateStrongCooldown(bool ready, float remainingSeconds = 0f)
        {
            UpdateSpellCooldown(SpellType.Arcane, ready, remainingSeconds);
        }

        public void UpdateSelectedSpell(SpellType spell)
        {
            currentlySelectedSpell = spell;
            Color activeColor = new Color(1f, 0.82f, 0.25f, 1f);
            Color inactiveColor = new Color(0.45f, 0.38f, 0.3f, 0.72f);

            if (arcaneHighlight != null) arcaneHighlight.color = (spell == SpellType.Arcane) ? activeColor : inactiveColor;
            if (iceHighlight != null) iceHighlight.color = (spell == SpellType.Ice) ? activeColor : inactiveColor;
            if (lightningHighlight != null) lightningHighlight.color = (spell == SpellType.Lightning) ? activeColor : inactiveColor;
            if (areaHighlight != null) areaHighlight.color = (spell == SpellType.Area) ? activeColor : inactiveColor;

            if (activeSpellNoticeText != null)
            {
                string spellName = spell switch
                {
                    SpellType.Arcane => "TIRO ARCANO",
                    SpellType.Ice => "FEITIÇO DE GELO",
                    SpellType.Lightning => "RELÂMPAGO",
                    SpellType.Area => "EXPLOSÃO ÁREA",
                    _ => "FEITIÇO"
                };
                activeSpellNoticeText.text = spellName;
            }

            RefreshSelectedMedallionState();
        }

        public void UpdateSpellCooldown(SpellType spell, bool ready, float remainingSeconds = 0f)
        {
            bool wasCoolingDown = spellReadyMap.TryGetValue(spell, out bool prevReady) && !prevReady;
            spellReadyMap[spell] = ready;
            spellCooldownMap[spell] = remainingSeconds;

            Text targetText = spell switch
            {
                SpellType.Arcane => arcaneCooldownText,
                SpellType.Ice => iceCooldownText,
                SpellType.Lightning => lightningCooldownText,
                SpellType.Area => areaCooldownText,
                _ => null
            };

            if (targetText != null)
            {
                if (ready)
                {
                    targetText.text = "PRONTO";
                    targetText.color = new Color(0.35f, 0.98f, 0.52f);
                }
                else
                {
                    targetText.text = $"{remainingSeconds:F1}s";
                    targetText.color = new Color(1f, 0.46f, 0.46f);
                }
            }

            if (ready && wasCoolingDown)
            {
                Image slotImg = spell switch
                {
                    SpellType.Arcane => arcaneHighlight,
                    SpellType.Ice => iceHighlight,
                    SpellType.Lightning => lightningHighlight,
                    SpellType.Area => areaHighlight,
                    _ => null
                };
                if (slotImg != null && slotImg.gameObject.activeInHierarchy)
                {
                    StartCoroutine(PunchScaleRoutine(slotImg.transform));
                }
                if (spell == currentlySelectedSpell && spellMedallionGlowImage != null && spellMedallionGlowImage.gameObject.activeInHierarchy)
                {
                    StartCoroutine(PunchScaleRoutine(spellMedallionGlowImage.transform.parent));
                }
            }

            if (spell == currentlySelectedSpell)
            {
                RefreshSelectedMedallionState();
            }
        }

        private void RefreshSelectedMedallionState()
        {
            bool ready = !spellReadyMap.TryGetValue(currentlySelectedSpell, out bool r) || r;
            float rem = spellCooldownMap.TryGetValue(currentlySelectedSpell, out float s) ? s : 0f;

            if (strongShotText != null)
            {
                if (ready)
                {
                    strongShotText.text = "[RMB] PRONTO";
                    strongShotText.color = new Color(0.38f, 1f, 0.55f);
                }
                else
                {
                    strongShotText.text = $"CD: {rem:F1}s";
                    strongShotText.color = new Color(1f, 0.55f, 0.35f);
                }
            }

            if (spellMedallionGlowImage != null)
            {
                Color spellCol = currentlySelectedSpell switch
                {
                    SpellType.Arcane => new Color(0.35f, 0.9f, 1f, 1f),
                    SpellType.Ice => new Color(0.62f, 0.95f, 1f, 1f),
                    SpellType.Lightning => new Color(1f, 0.88f, 0.25f, 1f),
                    SpellType.Area => new Color(1f, 0.38f, 0.22f, 1f),
                    _ => Color.white
                };
                spellMedallionGlowImage.color = ready ? spellCol : new Color(spellCol.r * 0.45f, spellCol.g * 0.45f, spellCol.b * 0.45f, 0.7f);
            }
        }

        #region Sistema de Combo

        public void UpdateCombo(int streak, int multiplier)
        {
            if (comboContainer == null || comboText == null) return;

            if (streak >= 2)
            {
                comboContainer.SetActive(true);
                comboText.text = $"COMBO ×{multiplier}  •  {streak} ABATES";

                if (multiplier >= 4) comboText.color = new Color(1f, 0.35f, 0.95f);
                else if (multiplier == 3) comboText.color = new Color(1f, 0.42f, 0.18f);
                else if (multiplier == 2) comboText.color = new Color(1f, 0.78f, 0.22f);
                else comboText.color = new Color(1f, 0.92f, 0.45f);

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
            comboBreakText.color = new Color(1f, 0.32f, 0.32f, 1f);

            var cg = comboBreakText.GetComponent<CanvasGroup>();
            if (cg == null) cg = comboBreakText.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            comboBreakText.transform.localScale = new Vector3(1.18f, 1.18f, 1f);

            float elapsed = 0f;
            float dur = 1.05f;
            while (elapsed < dur)
            {
                if (Time.timeScale > 0.0001f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / dur);
                    comboBreakText.transform.localScale = Vector3.Lerp(new Vector3(1.18f, 1.18f, 1f), Vector3.one, Mathf.Clamp01(t * 4f));
                    cg.alpha = t > 0.55f ? 1f - ((t - 0.55f) / 0.45f) : 1f;
                }
                yield return null;
            }

            cg.alpha = 0f;
            comboBreakText.gameObject.SetActive(false);
            comboBreakCoroutine = null;
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
            ShowSkillBonusPopup("HEADSHOT! +2", new Color(0.32f, 1f, 0.45f));
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
            var cg = headshotPopupContainer.GetComponent<CanvasGroup>();
            if (cg == null) cg = headshotPopupContainer.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

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
                rect.localScale = new Vector3(1.28f, 1.28f, 1f);
            }

            float elapsed = 0f;
            float dur = 1.05f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                if (rect != null)
                {
                    rect.anchoredPosition = initialPos + new Vector2(0f, 48f * Mathf.Sin(t * Mathf.PI * 0.5f));
                    rect.localScale = Vector3.Lerp(new Vector3(1.28f, 1.28f, 1f), Vector3.one, Mathf.Clamp01(t * 3.5f));
                }
                if (t > 0.65f)
                {
                    cg.alpha = 1f - ((t - 0.65f) / 0.35f);
                }
                yield return null;
            }

            cg.alpha = 0f;
            headshotPopupContainer.SetActive(false);
        }

        #endregion

        private void Start()
        {
            InitializeVolumeSliders();
            UpdateMuteUI();
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.OnMuteChanged += HandleMuteChanged;
            }
        }

        private void OnDestroy()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.OnMuteChanged -= HandleMuteChanged;
            }
        }

        private void Update()
        {
            if (startMenuPanel != null && startMenuPanel.activeSelf && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (settingsPanel != null && settingsPanel.activeSelf)
                {
                    ShowSettings(false);
                }
                else if (instructionsPanel != null && instructionsPanel.activeSelf)
                {
                    ShowInstructions(false);
                }
            }

            // Pulso suave de alerta quando o jogador está com apenas 1 ponto de vida
            if (isLowHealthWarningActive && damageFlashCoroutine == null && damageFlashOverlay != null && inGameHUD != null && inGameHUD.activeSelf)
            {
                if (!damageFlashOverlay.gameObject.activeSelf) damageFlashOverlay.gameObject.SetActive(true);
                float pulse = 0.045f + (Mathf.Sin(Time.unscaledTime * 5.2f) * 0.5f + 0.5f) * 0.11f;
                damageFlashOverlay.color = new Color(0.85f, 0.05f, 0.05f, pulse);
            }

            // Animação suave da Barra de Vida do Chefe
            if (bossBarContainer != null && bossBarContainer.activeSelf && bossBarFillRect != null)
            {
                currentBossHealthFill = Mathf.Lerp(currentBossHealthFill, targetBossHealthFill, Time.unscaledDeltaTime * 11f);
                bossBarFillRect.anchorMax = new Vector2(Mathf.Clamp01(currentBossHealthFill), 1f);

                if (bossBarFillImage != null)
                {
                    if (isBossBarEnraged)
                    {
                        float p = Mathf.Sin(Time.unscaledTime * 9f) * 0.5f + 0.5f;
                        bossBarFillImage.color = Color.Lerp(new Color(0.95f, 0.14f, 0.08f, 1f), new Color(1f, 0.62f, 0.12f, 1f), p);
                    }
                    else
                    {
                        bossBarFillImage.color = Color.Lerp(new Color(0.88f, 0.16f, 0.12f, 1f), new Color(0.96f, 0.36f, 0.16f, 1f), currentBossHealthFill);
                    }
                }
            }
        }

        public void UpdateMenuHighScore(int bestScore, string bestRank)
        {
            if (menuHighScoreText == null && startMenuPanel != null && bestScore > 0)
            {
                Font f = waveText != null ? waveText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var go = new GameObject("MenuHighScoreText");
                go.transform.SetParent(startMenuPanel.transform, false);
                var r = go.AddComponent<RectTransform>();
                r.anchorMin = new Vector2(0f, 0.5f);
                r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(335f, -305f);
                r.sizeDelta = new Vector2(400f, 34f);
                menuHighScoreText = go.AddComponent<Text>();
                menuHighScoreText.font = f;
                menuHighScoreText.fontSize = 15;
                menuHighScoreText.fontStyle = FontStyle.Bold;
                menuHighScoreText.color = new Color(1f, 0.86f, 0.36f, 0.95f);
                menuHighScoreText.alignment = TextAnchor.MiddleCenter;
                menuHighScoreText.raycastTarget = false;
                var ol = go.AddComponent<Outline>();
                ol.effectColor = new Color(0f, 0f, 0f, 0.92f);
                ol.effectDistance = new Vector2(1.4f, -1.4f);
            }

            if (menuHighScoreText != null)
            {
                bool hasRecord = bestScore > 0;
                menuHighScoreText.gameObject.SetActive(hasRecord);
                if (hasRecord)
                {
                    menuHighScoreText.text = $"RECORDE: {bestScore:D5} PTS  •  RANK {bestRank}";
                }
            }
        }

        public void ShowBossHealthBar(string bossName, int currentHp, int maxHp, bool enraged = false)
        {
            EnsureBossHealthBar();
            if (bossBarContainer == null) return;

            bossBarContainer.SetActive(true);
            float pct = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
            currentBossHealthFill = pct;
            targetBossHealthFill = pct;
            isBossBarEnraged = enraged;

            if (bossBarFillRect != null)
            {
                bossBarFillRect.anchorMax = new Vector2(pct, 1f);
            }
            UpdateBossHealthLabel(bossName, currentHp, maxHp, enraged);
        }

        public void UpdateBossHealthBar(string bossName, int currentHp, int maxHp, bool enraged)
        {
            if (bossBarContainer == null || !bossBarContainer.activeSelf)
            {
                ShowBossHealthBar(bossName, currentHp, maxHp, enraged);
                return;
            }
            targetBossHealthFill = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
            isBossBarEnraged = enraged;
            UpdateBossHealthLabel(bossName, currentHp, maxHp, enraged);
        }

        public void HideBossHealthBar()
        {
            if (bossBarContainer != null) bossBarContainer.SetActive(false);
            isBossBarEnraged = false;
        }

        private void UpdateBossHealthLabel(string bossName, int currentHp, int maxHp, bool enraged)
        {
            if (bossBarLabelText == null) return;
            string rageTag = enraged ? "  •  FÚRIA!" : "";
            bossBarLabelText.text = $"CHEFE: {bossName.ToUpper()}   •   {Mathf.Max(0, currentHp)}/{maxHp} HP{rageTag}";
            bossBarLabelText.color = enraged ? new Color(1f, 0.52f, 0.22f) : new Color(1f, 0.90f, 0.58f);
        }

        private void EnsureBossHealthBar()
        {
            if (bossBarContainer != null || inGameHUD == null) return;

            Font titleF = waveText != null ? waveText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            bossBarContainer = new GameObject("BossHealthBar_HUD");
            bossBarContainer.transform.SetParent(inGameHUD.transform, false);
            var rect = bossBarContainer.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -76f);
            rect.sizeDelta = new Vector2(540f, 52f);

            var bgImg = bossBarContainer.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.04f, 0.06f, 0.92f);
            bgImg.raycastTarget = false;
            var border = bossBarContainer.AddComponent<Outline>();
            border.effectColor = new Color(0.88f, 0.62f, 0.20f, 0.95f);
            border.effectDistance = new Vector2(2f, -2f);

            // Trilha interna da barra
            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(bossBarContainer.transform, false);
            var tr = trackGo.AddComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 0f);
            tr.pivot = new Vector2(0.5f, 0f);
            tr.offsetMin = new Vector2(14f, 8f);
            tr.offsetMax = new Vector2(-14f, 22f);
            var trImg = trackGo.AddComponent<Image>();
            trImg.color = new Color(0.18f, 0.05f, 0.06f, 0.96f);
            trImg.raycastTarget = false;

            // Preenchimento dinâmico
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(trackGo.transform, false);
            bossBarFillRect = fillGo.AddComponent<RectTransform>();
            bossBarFillRect.anchorMin = Vector2.zero;
            bossBarFillRect.anchorMax = Vector2.one;
            bossBarFillRect.offsetMin = Vector2.zero;
            bossBarFillRect.offsetMax = Vector2.zero;
            bossBarFillImage = fillGo.AddComponent<Image>();
            bossBarFillImage.color = new Color(0.92f, 0.22f, 0.14f, 1f);
            bossBarFillImage.raycastTarget = false;

            // Nome + HP do Chefe
            var lblGo = new GameObject("BossLabel");
            lblGo.transform.SetParent(bossBarContainer.transform, false);
            var lr = lblGo.AddComponent<RectTransform>();
            lr.anchorMin = new Vector2(0f, 1f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.pivot = new Vector2(0.5f, 1f);
            lr.offsetMin = new Vector2(12f, -26f);
            lr.offsetMax = new Vector2(-12f, -2f);
            bossBarLabelText = lblGo.AddComponent<Text>();
            bossBarLabelText.font = titleF;
            bossBarLabelText.fontSize = 15;
            bossBarLabelText.fontStyle = FontStyle.Bold;
            bossBarLabelText.alignment = TextAnchor.MiddleCenter;
            bossBarLabelText.color = new Color(1f, 0.90f, 0.58f);
            bossBarLabelText.raycastTarget = false;
            var lol = lblGo.AddComponent<Outline>();
            lol.effectColor = new Color(0f, 0f, 0f, 0.95f);
            lol.effectDistance = new Vector2(1.4f, -1.4f);

            bossBarContainer.SetActive(false);
        }

        private void HandleMuteChanged(bool _)
        {
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
                if (active)
                {
                    var rt = activeBuffsContainer.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        bool bossVisible = bossBarContainer != null && bossBarContainer.activeSelf;
                        rt.anchoredPosition = new Vector2(0f, bossVisible ? -134f : -74f);
                    }
                }
            }
            if (activeBuffsText != null)
            {
                activeBuffsText.text = buffText;
            }
        }

        #region Sistema de Fases & Ondas UI

        public void UpdateWaveProgress(int currentWave, int totalWaves, int remainingInWave, int totalInWave)
        {
            if (waveText != null)
            {
                waveText.text = $"FASE {currentWave}/{totalWaves}   •   RESTAM {remainingInWave}/{totalInWave}";
            }
        }

        public void ShowWaveStartBanner(int waveNumber, string waveName, string description)
        {
            if (waveBannerContainer == null) return;

            if (waveBannerCoroutine != null) StopCoroutine(waveBannerCoroutine);
            waveBannerCoroutine = StartCoroutine(WaveBannerRoutine(
                title: $"FASE {waveNumber}: {waveName.ToUpper()}",
                subtitle: description,
                titleColor: new Color(1f, 0.85f, 0.25f),
                duration: 2.8f
            ));
        }

        public void ShowWaveClearedBanner(int waveNumber, int bonusPoints)
        {
            if (waveBannerContainer == null) return;

            if (waveBannerCoroutine != null) StopCoroutine(waveBannerCoroutine);
            waveBannerCoroutine = StartCoroutine(WaveBannerRoutine(
                title: $"FASE {waveNumber} CONCLUÍDA!",
                subtitle: $"+{bonusPoints} Pontos de Bônus   •   Mana Restaurada!",
                titleColor: new Color(0.32f, 1f, 0.52f),
                duration: 2.2f
            ));
        }

        public void ShowBossSpawnBanner(string bossName, string description)
        {
            if (waveBannerContainer == null) return;

            if (waveBannerCoroutine != null) StopCoroutine(waveBannerCoroutine);
            waveBannerCoroutine = StartCoroutine(WaveBannerRoutine(
                title: $"CHEFE DA FASE: {bossName.ToUpper()}!",
                subtitle: description,
                titleColor: new Color(1f, 0.3f, 0.25f),
                duration: 3.2f
            ));
        }

        public void ShowBossEnragedBanner(string bossName)
        {
            if (waveBannerContainer == null) return;

            if (waveBannerCoroutine != null) StopCoroutine(waveBannerCoroutine);
            waveBannerCoroutine = StartCoroutine(WaveBannerRoutine(
                title: $"{bossName.ToUpper()} ENTROU EM FÚRIA!",
                subtitle: "Velocidade aumentada e reforços convocados!",
                titleColor: new Color(1f, 0.48f, 0.14f),
                duration: 2.4f
            ));
        }

        public void ShowBossDefeatedBanner(string bossName, int bonusPoints, string nextStageHint = "")
        {
            if (waveBannerContainer == null) return;

            string sub = string.IsNullOrEmpty(nextStageHint)
                ? $"{bossName.ToUpper()} FOI ANIQUILADO COM MAESTRIA!"
                : $"{bossName.ToUpper()} DERROTADO!   •   {nextStageHint}";

            if (waveBannerCoroutine != null) StopCoroutine(waveBannerCoroutine);
            waveBannerCoroutine = StartCoroutine(WaveBannerRoutine(
                title: $"CHEFE DERROTADO!  +{bonusPoints} PTS",
                subtitle: sub,
                titleColor: new Color(1f, 0.85f, 0.22f),
                duration: 2.6f
            ));
        }

        public void HideStageTransitionImmediate()
        {
            if (stageTransitionPanel != null) stageTransitionPanel.SetActive(false);
        }

        public IEnumerator PlayStageTransitionRoutine(int nextStageNumber, string stageName, string bossName, string bonusAndDifficultyNote, Action onMidpointSwap)
        {
            EnsureStageTransitionPanel();
            if (stageTransitionPanel == null)
            {
                onMidpointSwap?.Invoke();
                yield break;
            }

            if (stageTransitionTitleText != null)
            {
                stageTransitionTitleText.text = $"FASE {nextStageNumber}";
            }
            if (stageTransitionSubtitleText != null)
            {
                stageTransitionSubtitleText.text = stageName.ToUpper();
            }
            if (stageTransitionDetailText != null)
            {
                stageTransitionDetailText.text = $"Chefe da Fase: {bossName}\n{bonusAndDifficultyNote}";
            }

            var cg = stageTransitionPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = stageTransitionPanel.AddComponent<CanvasGroup>();

            cg.alpha = 0f;
            stageTransitionPanel.SetActive(true);
            stageTransitionPanel.transform.SetAsLastSibling();

            // 1. Fade-in da cortina cinematográfica
            float fadeIn = 0.45f;
            float elapsed = 0f;
            while (elapsed < fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(elapsed / fadeIn);
                yield return null;
            }
            cg.alpha = 1f;

            // 2. No auge da transição, troca cenário, bioma e prepara próxima fase
            onMidpointSwap?.Invoke();

            // 3. Pausa breve de apresentação da nova fase
            yield return new WaitForSecondsRealtime(1.45f);

            // 4. Fade-out revelando o novo cenário
            float fadeOut = 0.55f;
            elapsed = 0f;
            while (elapsed < fadeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = 1f - Mathf.Clamp01(elapsed / fadeOut);
                yield return null;
            }
            cg.alpha = 0f;
            stageTransitionPanel.SetActive(false);
        }

        private void EnsureStageTransitionPanel()
        {
            if (stageTransitionPanel != null) return;

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null) parentCanvas = FindAnyObjectByType<Canvas>();
            if (parentCanvas == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            stageTransitionPanel = new GameObject("StageTransitionPanel");
            stageTransitionPanel.transform.SetParent(parentCanvas.transform, false);
            var rect = stageTransitionPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = stageTransitionPanel.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.03f, 0.07f, 0.96f);
            bg.raycastTarget = true;

            stageTransitionTitleText = CreateFallbackText(stageTransitionPanel.transform, "TransitionTitle", "FASE 2", 68, new Color(1f, 0.85f, 0.2f), new Vector2(0f, 85f), font);
            stageTransitionTitleText.fontStyle = FontStyle.Bold;
            stageTransitionSubtitleText = CreateFallbackText(stageTransitionPanel.transform, "TransitionSubtitle", "RUÍNAS ILUMINADAS PELA LUA", 34, Color.white, new Vector2(0f, 5f), font);
            stageTransitionSubtitleText.fontStyle = FontStyle.Bold;
            stageTransitionDetailText = CreateFallbackText(stageTransitionPanel.transform, "TransitionDetail", "Chefe: Xamã do Caos", 22, new Color(0.75f, 0.9f, 1f), new Vector2(0f, -80f), font);

            stageTransitionPanel.SetActive(false);
        }

        private Text CreateFallbackText(Transform parent, string name, string text, int size, Color col, Vector2 pos, Font font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(1200f, 140f);
            r.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = size;
            t.color = col;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
            ol.effectDistance = new Vector2(2f, -2f);
            return t;
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

            // Fade In com leve pop de escala (tempo real ativo: ignora slow-mo mas respeita pausa)
            float elapsed = 0f;
            float fadeInTime = 0.25f;
            waveBannerContainer.transform.localScale = Vector3.one * 0.82f;

            while (elapsed < fadeInTime)
            {
                if (Time.timeScale > 0.0001f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeInTime);
                    canvasGroup.alpha = t;
                    waveBannerContainer.transform.localScale = Vector3.Lerp(Vector3.one * 0.82f, Vector3.one, t);
                }
                yield return null;
            }

            canvasGroup.alpha = 1f;
            waveBannerContainer.transform.localScale = Vector3.one;

            float holdDuration = Mathf.Max(0.5f, duration - 0.55f);
            float holdElapsed = 0f;
            while (holdElapsed < holdDuration)
            {
                if (Time.timeScale > 0.0001f)
                {
                    holdElapsed += Time.unscaledDeltaTime;
                }
                yield return null;
            }

            // Fade Out
            elapsed = 0f;
            float fadeOutTime = 0.35f;
            while (elapsed < fadeOutTime)
            {
                if (Time.timeScale > 0.0001f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeOutTime);
                    canvasGroup.alpha = 1f - t;
                }
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