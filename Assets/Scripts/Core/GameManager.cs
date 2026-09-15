using System.Collections;
using UnityEngine;
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

    /// <summary>
    /// Orquestrador do fluxo do jogo, pontuação, cooldowns e estados de menu/pause/gameover.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private WizardSpawner spawner;
        [SerializeField] private PlayerInputHandler inputHandler;
        [SerializeField] private WeaponSystem weaponSystem;
        [SerializeField] private UIManager uiManager;

        [Header("Regras de Partida")]
        [SerializeField] private int winScoreTarget = 50;
        [SerializeField] private int maxEscapesAllowed = 15;
        [SerializeField] private float strongShotCooldown = 3.0f;

        private GameState currentState;
        private int currentScore;
        private int currentEscaped;
        private float strongCooldownTimer;
        private bool isStrongReady;

        private void Start()
        {
            Time.timeScale = 1f;

            if (inputHandler != null)
            {
                inputHandler.OnNormalShot += HandleNormalShot;
                inputHandler.OnStrongShot += HandleStrongShot;
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
            }

            if (uiManager != null)
            {
                uiManager.OnPlayClicked += StartGameplay;
                uiManager.OnResumeClicked += ResumeGame;
                uiManager.OnRestartClicked += RestartGame;
                uiManager.OnMainMenuClicked += ReturnToMainMenu;
            }

            // Iniciar no Menu Principal
            SetState(GameState.Menu);
        }

        private void Update()
        {
            if (currentState == GameState.Playing)
            {
                if (!isStrongReady)
                {
                    strongCooldownTimer -= Time.deltaTime;
                    if (strongCooldownTimer <= 0f)
                    {
                        isStrongReady = true;
                        uiManager?.UpdateStrongCooldown(true);
                    }
                    else
                    {
                        uiManager?.UpdateStrongCooldown(false, strongCooldownTimer);
                    }
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
                    spawner?.StopSpawning();
                    inputHandler?.SetInputBlocked(true);
                    uiManager?.ShowStartMenu();
                    break;

                case GameState.Playing:
                    Time.timeScale = 1f;
                    inputHandler?.SetInputBlocked(false);
                    uiManager?.ShowGameplayHUD();
                    spawner?.StartSpawning();
                    break;

                case GameState.Paused:
                    Time.timeScale = 0f;
                    inputHandler?.SetInputBlocked(true);
                    uiManager?.ShowPauseMenu(true);
                    break;

                case GameState.GameOver:
                    Time.timeScale = 1f;
                    spawner?.StopSpawning();
                    inputHandler?.SetInputBlocked(true);
                    break;
            }
        }

        public void StartGameplay()
        {
            currentScore = 0;
            currentEscaped = 0;
            isStrongReady = true;
            strongCooldownTimer = 0f;

            uiManager?.UpdateScore(currentScore, winScoreTarget);
            uiManager?.UpdateEscapes(currentEscaped, maxEscapesAllowed);
            uiManager?.UpdateStrongCooldown(true);

            if (weaponSystem != null)
            {
                uiManager?.UpdateAmmo(weaponSystem.CurrentAmmo, weaponSystem.MaxAmmo, false);
            }

            SetState(GameState.Playing);
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

        private void HandleNormalShot(WizardController target)
        {
            if (currentState != GameState.Playing) return;

            SoundManager.Instance?.PlaySFX(SoundManager.Instance.normalShotSound);

            if (target != null)
            {
                target.TakeDamage(1);
            }
        }

        private void HandleStrongShot(WizardController target)
        {
            if (currentState != GameState.Playing || !isStrongReady) return;

            isStrongReady = false;
            strongCooldownTimer = strongShotCooldown;
            uiManager?.UpdateStrongCooldown(false, strongShotCooldown);

            SoundManager.Instance?.PlaySFX(SoundManager.Instance.strongShotSound);
            weaponSystem?.TriggerRecoil();

            if (target != null)
            {
                target.TakeDamage(3);
            }
        }

        private void HandleWizardDefeated(WizardController wizard, int points)
        {
            if (currentState != GameState.Playing) return;

            currentScore += points;
            uiManager?.UpdateScore(currentScore, winScoreTarget);

            if (wizard.GetData() != null && wizard.GetData().customDeathSound != null)
            {
                SoundManager.Instance?.PlaySFX(wizard.GetData().customDeathSound);
            }
            else
            {
                SoundManager.Instance?.PlayRandomDeathSound();
            }

            if (currentScore >= winScoreTarget)
            {
                EndGame(won: true);
            }
        }

        private void HandleWizardEscaped(WizardController wizard, int penalty)
        {
            if (currentState != GameState.Playing) return;

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

        private void EndGame(bool won)
        {
            SetState(GameState.GameOver);

            if (won)
            {
                SoundManager.Instance?.PlaySFX(SoundManager.Instance.victorySound);
            }
            else
            {
                SoundManager.Instance?.PlaySFX(SoundManager.Instance.defeatSound);
            }

            uiManager?.ShowGameOver(won, currentScore, currentEscaped);
        }
    }
}