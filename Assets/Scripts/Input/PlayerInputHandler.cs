using System;
using UnityEngine;
using WizardGame.Entities;

namespace WizardGame.Input
{
    /// <summary>
    /// Captura entradas do jogador para mira, tiro normal, tiro forte, recarga e pausa.
    /// Utiliza OverlapCircle com tolerância de acerto arcade para garantir que o clique sempre funcione.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Configurações")]
        [Tooltip("Tempo segurando o botão esquerdo para disparar o tiro forte.")]
        [SerializeField] private float longPressThreshold = 0.35f;

        [Tooltip("Raio de tolerância do clique em unidades de mundo.")]
        [SerializeField] private float clickToleranceRadius = 0.5f;

        [Header("Referências")]
        [SerializeField] private WeaponSystem weaponSystem;

        private Camera mainCamera;
        private float pointerDownTime;
        private bool isPointerDown;
        private bool isInputBlocked;

        public event Action<WizardController> OnNormalShot;
        public event Action<WizardController> OnStrongShot;
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

            // Tiro Forte Imediato no Botão Direito
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

            WizardController hitWizard = FindWizardAtPosition(screenPosition);

            if (isStrong)
            {
                OnStrongShot?.Invoke(hitWizard);
            }
            else
            {
                OnNormalShot?.Invoke(hitWizard);
            }
        }

        private WizardController FindWizardAtPosition(Vector2 screenPosition)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return null;

            Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPosition);
            worldPos.z = 0f;

            // Busca por sobreposição circular com raio de tolerância arcade
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