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
        [SerializeField] private Text ammoText;
        [SerializeField] private Text strongShotText;
        [SerializeField] private Text controlsHintText;
        [SerializeField] private Button muteButton;
        [SerializeField] private Text muteButtonText;

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

        [Header("Elementos de Fim de Jogo")]
        [SerializeField] private Text endTitleText;
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
        }

        public void ShowStartMenu()
        {
            if (startMenuPanel != null) startMenuPanel.SetActive(true);
            if (inGameHUD != null) inGameHUD.SetActive(false);
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (instructionsPanel != null) instructionsPanel.SetActive(false);
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

        public void ShowGameOver(bool won, int score, int escaped, int maxStreak, int headshots)
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            if (inGameHUD != null) inGameHUD.SetActive(false);
            Cursor.visible = true;

            if (endTitleText != null)
            {
                endTitleText.text = won ? "VITORIA! TODOS OS GOBLINS FORAM DERROTADOS!" : "DERROTA! OS GOBLINS ESCAPARAM!";
                endTitleText.color = won ? Color.yellow : Color.red;
            }

            if (endScoreText != null)
            {
                endScoreText.text = won
                    ? $"Pontos Totais: {score}  |  Goblins Abatidos com Maestria!"
                    : $"Pontos Totais: {score}  |  Goblins Fugitivos: {escaped} / 15";
            }

            if (endStatsText != null)
            {
                endStatsText.text = $"Estatisticas de Desempenho:\n" +
                                    $"  - Maior Combo Sequencial: {maxStreak} abates\n" +
                                    $"  - Tiros Perfeitos (Headshots): {headshots}";
            }
        }

        public void UpdateScore(int score, int target)
        {
            if (scoreText != null) scoreText.text = $"Pontos: {score} / {target}";
        }

        public void UpdateEscapes(int current, int max)
        {
            if (escapeText != null)
            {
                escapeText.text = $"Fugiram: {current} / {max}";
                escapeText.color = current >= (max - 3) ? Color.red : new Color(1f, 0.6f, 0.6f);
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

        #region Efeito de Headshot

        public void ShowHeadshotPopup(Vector3 worldPoint)
        {
            if (headshotPopupContainer == null) return;

            if (headshotCoroutine != null) StopCoroutine(headshotCoroutine);
            headshotCoroutine = StartCoroutine(HeadshotPopupRoutine());
        }

        private IEnumerator HeadshotPopupRoutine()
        {
            headshotPopupContainer.SetActive(true);
            if (headshotPopupText != null)
            {
                headshotPopupText.text = "HEADSHOT! +1 PONTO";
                headshotPopupText.color = new Color(0.2f, 1f, 0.3f);
            }

            RectTransform rect = headshotPopupContainer.GetComponent<RectTransform>();
            Vector2 initialPos = new Vector2(0f, 140f);
            if (rect != null) rect.anchoredPosition = initialPos;

            float elapsed = 0f;
            float dur = 0.85f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / dur;
                if (rect != null) rect.anchoredPosition = initialPos + new Vector2(0f, 40f * t);
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
    }
}