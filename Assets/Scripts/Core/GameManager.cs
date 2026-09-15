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
    /// Orquestrador principal com Sistema de Combos, Multiplicadores Dinâmicos
    /// e Bônus de Precisão por Headshot / Acerto Perfeito.
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

        // --- SISTEMA DE COMBOS E ESTATÍSTICAS ---
        private int currentStreak;
        private int currentMultiplier;
        private int maxStreak;
        private int headshotsCount;

        private void Start()
        {
            Time.timeScale = 1f;

            if (inputHandler != null)
            {
                inputHandler.OnNormalShot += HandleNormalShot;
                inputHandler.OnStrongShot += HandleStrongShot;
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
            currentStreak = 0;
            currentMultiplier = 1;
            maxStreak = 0;
            headshotsCount = 0;

            isStrongReady = true;
            strongCooldownTimer = 0f;

            uiManager?.UpdateScore(currentScore, winScoreTarget);
            uiManager?.UpdateEscapes(currentEscaped, maxEscapesAllowed);
            uiManager?.UpdateStrongCooldown(true);
            uiManager?.UpdateCombo(currentStreak, currentMultiplier);

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

        private void HandleNormalShot(ShotHitInfo hitInfo)
        {
            if (currentState != GameState.Playing) return;

            SoundManager.Instance?.PlaySFX(SoundManager.Instance.normalShotSound);

            if (hitInfo.isHit && hitInfo.target != null)
            {
                // Bônus de Precisão: Headshot / Acerto Perfeito
                if (hitInfo.isHeadshot)
                {
                    currentScore += 1;
                    headshotsCount++;
                    SoundManager.Instance?.PlayHeadshotSound();
                    uiManager?.ShowHeadshotPopup(hitInfo.hitPoint);
                    uiManager?.UpdateScore(currentScore, winScoreTarget);
                }

                hitInfo.target.TakeDamage(1);
            }
        }

        private void HandleStrongShot(ShotHitInfo hitInfo)
        {
            if (currentState != GameState.Playing || !isStrongReady) return;

            isStrongReady = false;
            strongCooldownTimer = strongShotCooldown;
            uiManager?.UpdateStrongCooldown(false, strongShotCooldown);

            SoundManager.Instance?.PlaySFX(SoundManager.Instance.strongShotSound);
            weaponSystem?.TriggerRecoil();

            if (hitInfo.isHit && hitInfo.target != null)
            {
                // Bônus de Headshot com tiro forte
                if (hitInfo.isHeadshot)
                {
                    currentScore += 1;
                    headshotsCount++;
                    SoundManager.Instance?.PlayHeadshotSound();
                    uiManager?.ShowHeadshotPopup(hitInfo.hitPoint);
                    uiManager?.UpdateScore(currentScore, winScoreTarget);
                }

                hitInfo.target.TakeDamage(3);
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

        private void HandleWizardDefeated(WizardController wizard, int basePoints)
        {
            if (currentState != GameState.Playing) return;

            // Incrementa sequência de combo
            currentStreak++;
            if (currentStreak > maxStreak) maxStreak = currentStreak;

            currentMultiplier = CalculateMultiplier(currentStreak);

            // Aplica multiplicador na pontuação
            int pointsEarned = basePoints * currentMultiplier;
            currentScore += pointsEarned;

            uiManager?.UpdateScore(currentScore, winScoreTarget);
            uiManager?.UpdateCombo(currentStreak, currentMultiplier);

            // Som de Morte
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

            uiManager?.ShowGameOver(won, currentScore, currentEscaped, maxStreak, headshotsCount);
        }
    }
}