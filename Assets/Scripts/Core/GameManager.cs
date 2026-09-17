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
        [SerializeField] private int winScoreTarget = 50;
        [SerializeField] private int maxEscapesAllowed = 15;

        [Header("Tempos de Recarga dos Feitiços Especiais")]
        [SerializeField] private float arcaneCooldownDuration = 3.0f;
        [SerializeField] private float iceCooldownDuration = 4.0f;
        [SerializeField] private float lightningCooldownDuration = 4.5f;
        [SerializeField] private float areaCooldownDuration = 5.5f;

        private GameState currentState;
        private int currentScore;
        private int currentEscaped;

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
                    spawner?.StopSpawning();
                    inputHandler?.SetInputBlocked(true);
                    uiManager?.ShowStartMenu();
                    SoundManager.Instance?.PlayMenuMusic();
                    break;

                case GameState.Playing:
                    Time.timeScale = 1f;
                    inputHandler?.SetInputBlocked(false);
                    uiManager?.ShowGameplayHUD();
                    spawner?.StartSpawning();
                    SoundManager.Instance?.PlayGameplayMusic();
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

            isArcaneReady = true;
            isIceReady = true;
            isLightningReady = true;
            isAreaReady = true;
            arcaneTimer = 0f;
            iceTimer = 0f;
            lightningTimer = 0f;
            areaTimer = 0f;

            uiManager?.UpdateScore(currentScore, winScoreTarget);
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
                    isArcaneReady = false;
                    arcaneTimer = arcaneCooldownDuration;
                    uiManager?.UpdateSpellCooldown(SpellType.Arcane, false, arcaneCooldownDuration);
                    SoundManager.Instance?.PlaySFX(SoundManager.Instance.strongShotSound);
                    weaponSystem?.TriggerRecoil();

                    if (hitInfo.isHit && hitInfo.target != null)
                    {
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
                    break;

                case SpellType.Ice:
                    if (!isIceReady) return;
                    isIceReady = false;
                    iceTimer = iceCooldownDuration;
                    uiManager?.UpdateSpellCooldown(SpellType.Ice, false, iceCooldownDuration);
                    SoundManager.Instance?.PlayIceCastSound();
                    weaponSystem?.TriggerRecoil();

                    if (hitInfo.isHit && hitInfo.target != null)
                    {
                        if (hitInfo.isHeadshot)
                        {
                            currentScore += 1;
                            headshotsCount++;
                            SoundManager.Instance?.PlayHeadshotSound();
                            uiManager?.ShowHeadshotPopup(hitInfo.hitPoint);
                            uiManager?.UpdateScore(currentScore, winScoreTarget);
                        }
                        hitInfo.target.TakeDamage(1);
                        hitInfo.target.Freeze(2.5f);
                    }
                    break;

                case SpellType.Lightning:
                    if (!isLightningReady) return;
                    isLightningReady = false;
                    lightningTimer = lightningCooldownDuration;
                    uiManager?.UpdateSpellCooldown(SpellType.Lightning, false, lightningCooldownDuration);
                    SoundManager.Instance?.PlayLightningCastSound();
                    weaponSystem?.TriggerRecoil();

                    Vector3 strikePos = hitInfo.isHit && hitInfo.target != null ? hitInfo.target.transform.position : hitInfo.hitPoint;
                    strikePos.z = 0f;

                    if (hitInfo.isHit && hitInfo.target != null)
                    {
                        if (hitInfo.isHeadshot)
                        {
                            currentScore += 1;
                            headshotsCount++;
                            SoundManager.Instance?.PlayHeadshotSound();
                            uiManager?.ShowHeadshotPopup(hitInfo.hitPoint);
                            uiManager?.UpdateScore(currentScore, winScoreTarget);
                        }
                        hitInfo.target.TakeDamage(2);
                    }

                    // Relâmpago em Cadeia: Atinge até 2 outros goblins próximos em um raio de 3.5m
                    int chainCount = 0;
                    foreach (var goblin in WizardController.ActiveGoblins)
                    {
                        if (goblin == null || goblin == hitInfo.target) continue;
                        if (Vector2.Distance(strikePos, goblin.transform.position) <= 3.5f)
                        {
                            goblin.TakeDamage(1);
                            chainCount++;
                            if (chainCount >= 2) break;
                        }
                    }
                    break;

                case SpellType.Area:
                    if (!isAreaReady) return;
                    isAreaReady = false;
                    areaTimer = areaCooldownDuration;
                    uiManager?.UpdateSpellCooldown(SpellType.Area, false, areaCooldownDuration);
                    SoundManager.Instance?.PlayAreaCastSound();
                    weaponSystem?.TriggerRecoil();

                    Vector3 center = hitInfo.isHit && hitInfo.target != null ? hitInfo.target.transform.position : hitInfo.hitPoint;
                    center.z = 0f;

                    if (hitInfo.isHeadshot)
                    {
                        currentScore += 1;
                        headshotsCount++;
                        SoundManager.Instance?.PlayHeadshotSound();
                        uiManager?.ShowHeadshotPopup(hitInfo.hitPoint);
                        uiManager?.UpdateScore(currentScore, winScoreTarget);
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