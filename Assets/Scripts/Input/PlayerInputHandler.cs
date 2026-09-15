using System;
using UnityEngine;
using WizardGame.Entities;

namespace WizardGame.Input
{
    public struct ShotHitInfo
    {
        public WizardController target;
        public bool isHit;
        public bool isHeadshot;
        public Vector3 hitPoint;
    }

    /// <summary>
    /// Captura entradas do jogador para mira, tiro normal, tiro forte, recarga e pausa.
    /// Detecta com precisão acertos normais, erros de tiro (para quebra de combo)
    /// e Headshots / Acertos Perfeitos (terço superior do mago).
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Configurações")]
        [Tooltip("Tempo segurando o botão esquerdo para disparar o tiro forte.")]
        [SerializeField] private float longPressThreshold = 0.35f;

        [Tooltip("Raio de tolerância do clique em unidades de mundo.")]
        [SerializeField] private float clickToleranceRadius = 0.55f;

        [Header("Referências")]
        [SerializeField] private WeaponSystem weaponSystem;

        private Camera mainCamera;
        private float pointerDownTime;
        private bool isPointerDown;
        private bool isInputBlocked;

        public event Action<ShotHitInfo> OnNormalShot;
        public event Action<ShotHitInfo> OnStrongShot;
        public event Action OnShotMissed;
        public event Action OnReloadRequested;
        public event Action OnTogglePauseRequested;

        private void Awake()
        {
            mainCamera = Camera.main;
            if (weaponSystem == null) weaponSystem = FindFirstObjectByType<WeaponSystem>();
        }

        private void Update()
        {
            if (isInputBlocked) return;

            // Teclas de Atalho
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.P))
            {
                OnTogglePauseRequested?.Invoke();
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                weaponSystem?.StartReload();
                OnReloadRequested?.Invoke();
            }

            // Tiro Forte no Botão Direito
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                TriggerShot(UnityEngine.Input.mousePosition, isStrong: true);
                return;
            }

            // Tiro no Botão Esquerdo
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                isPointerDown = true;
                pointerDownTime = Time.time;
            }

            if (UnityEngine.Input.GetMouseButtonUp(0) && isPointerDown)
            {
                isPointerDown = false;
                float duration = Time.time - pointerDownTime;
                bool isStrong = duration >= longPressThreshold;

                TriggerShot(UnityEngine.Input.mousePosition, isStrong);
            }
        }

        private void TriggerShot(Vector2 screenPosition, bool isStrong)
        {
            // Para tiro normal, verifica munição da varinha
            if (!isStrong && weaponSystem != null)
            {
                if (!weaponSystem.TryConsumeAmmo())
                {
                    return; // Sem munição ou recarregando
                }
            }

            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPosition);
            worldPos.z = 0f;

            WizardController hitWizard = FindWizardAtPosition(worldPos);

            ShotHitInfo hitInfo = new ShotHitInfo
            {
                target = hitWizard,
                isHit = hitWizard != null,
                isHeadshot = false,
                hitPoint = worldPos
            };

            if (hitWizard != null)
            {
                // Headshot / Acerto Perfeito: Se o clique atingir o terço superior do mago (cabeça/chapéu)
                float relativeY = worldPos.y - hitWizard.transform.position.y;
                if (relativeY > 0.22f)
                {
                    hitInfo.isHeadshot = true;
                }
            }
            else
            {
                // Disparo errou todos os magos: quebra de combo
                OnShotMissed?.Invoke();
            }

            if (isStrong)
            {
                OnStrongShot?.Invoke(hitInfo);
            }
            else
            {
                OnNormalShot?.Invoke(hitInfo);
            }
        }

        private WizardController FindWizardAtPosition(Vector3 worldPos)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, clickToleranceRadius);
            WizardController bestTarget = null;
            float closestDist = float.MaxValue;

            foreach (var col in hits)
            {
                var wizard = col.GetComponent<WizardController>();
                if (wizard != null)
                {
                    float dist = Vector2.Distance(worldPos, wizard.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        bestTarget = wizard;
                    }
                }
            }

            return bestTarget;
        }

        public void SetInputBlocked(bool blocked)
        {
            isInputBlocked = blocked;
        }
    }
}