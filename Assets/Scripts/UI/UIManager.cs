using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WizardGame.Core;
using WizardGame.Entities;

namespace WizardGame.UI
{
    /// <summary>
    /// Gerencia a interface completa: HUD em tempo real, Sistema de Combos,
    /// Popups de Headshot, Menu Inicial, Pause e Telas de Fim de Jogo com Estatísticas.
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
                endTitleText.text = won ? "VITORIA! O REINO ESTA SALVO!" : "DERROTA! OS MAGOS ESCAPARAM!";
                endTitleText.color = won ? Color.yellow : Color.red;
            }

            if (endScoreText != null)
            {
                endScoreText.text = won
                    ? $"Pontos Totais: {score}  |  Magos Abatidos com Sucesso!"
                    : $"Pontos Totais: {score}  |  Magos que Fugiram: {escaped} / 15";
            }

            if (endStatsText != null)
            {
                endStatsText.text = $"Estatisticas de Precisao:\n" +
                                    $"  • Maior Combo Sequencial: {maxStreak} abates\n" +
                                    $"  • Tiros Perfeitos (Headshots): {headshots}";
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
                escapeText.text = $"Escaparam: {current} / {max}";
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
            if (strongShotText == null) return;

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

        #region Sistema de Combo

        public void UpdateCombo(int streak, int multiplier)
        {
            if (comboContainer == null || comboText == null) return;

            if (streak >= 2)
            {
                comboContainer.SetActive(true);
                comboText.text = $"COMBO x{multiplier} — {streak} ABATES";

                // Cores dinâmicas
                if (multiplier >= 4) comboText.color = new Color(1f, 0.2f, 1f); // Magenta/Roxo
                else if (multiplier == 3) comboText.color = new Color(1f, 0.3f, 0.1f); // Vermelho fogo
                else if (multiplier == 2) comboText.color = new Color(1f, 0.7f, 0f); // Laranja
                else comboText.color = new Color(1f, 0.95f, 0.4f); // Amarelo suave

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
                headshotPopupText.text = "🎯 HEADSHOT! +1 PONTO";
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