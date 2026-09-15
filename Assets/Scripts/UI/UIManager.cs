using System;
using UnityEngine;
using UnityEngine.UI;
using WizardGame.Core;
using WizardGame.Entities;

namespace WizardGame.UI
{
    /// <summary>
    /// Gerencia toda a interface gráfica do jogo: HUD em tempo real, Menu Inicial,
    /// Menu de Pausa, Painel de Instruções e Tela de Fim de Jogo.
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

        [Header("Elementos de Fim de Jogo")]
        [SerializeField] private Text endTitleText;
        [SerializeField] private Text endScoreText;
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

        private void Awake()
        {
            // Vinculação de eventos dos botões
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

        public void ShowGameOver(bool won, int score, int escaped)
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
                    ? $"Magos Abatidos: {score}\nParabens pelo excelente tiro!"
                    : $"Magos Abatidos: {score}\nMagos Fugitivos: {escaped} / 15";
            }
        }

        public void UpdateScore(int score, int target)
        {
            if (scoreText != null) scoreText.text = $"Magos: {score} / {target}";
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