using System;
using UnityEngine;
using WizardGame.Data;
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
    /// Captura entradas do jogador para mira, tiro normal (LMB), seleção de feitiços
    /// especiais (1, 2, 3, 4, Scroll) e disparo de feitiços (RMB ou atalhos).
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Configurações")]
        [Tooltip("Raio de tolerância do clique em unidades de mundo.")]
        [SerializeField] private float clickToleranceRadius = 0.55f;

        [Header("Referências")]
        [SerializeField] private WeaponSystem weaponSystem;

        private Camera mainCamera;
        private bool isInputBlocked;
        private SpellType currentSpecialSpell = SpellType.Arcane;

        public SpellType CurrentSpecialSpell => currentSpecialSpell;

        public event Action<ShotHitInfo> OnNormalShot;
        public event Action<SpellType, ShotHitInfo> OnSpecialShot;
        public event Action<SpellType> OnSpecialSpellSelected;
        public event Action OnShotMissed;
        public event Action OnReloadRequested;
        public event Action OnTogglePauseRequested;

        private readonly SpellType[] specialSpellList = new SpellType[]
        {
            SpellType.Arcane,
            SpellType.Ice,
            SpellType.Lightning,
            SpellType.Area
        };
        private int selectedSpellIndex = 0;

        private void Awake()
        {
            mainCamera = Camera.main;
            if (weaponSystem == null) weaponSystem = FindAnyObjectByType<WeaponSystem>();
        }

        private void Start()
        {
            weaponSystem?.SetSpellTheme(currentSpecialSpell);
        }

        private void Update()
        {
            if (isInputBlocked) return;

            // Pausa
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.P))
            {
                OnTogglePauseRequested?.Invoke();
                return;
            }

            // Recarga manual da varinha
            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                weaponSystem?.StartReload();
                OnReloadRequested?.Invoke();
            }

            // Seleção rápida de feitiços especiais (Teclas 1, 2, 3, 4 ou Q, E, F, C)
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1) || UnityEngine.Input.GetKeyDown(KeyCode.Q))
            {
                SelectSpecialSpell(SpellType.Arcane);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2) || UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                SelectSpecialSpell(SpellType.Ice);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3) || UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                SelectSpecialSpell(SpellType.Lightning);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad4) || UnityEngine.Input.GetKeyDown(KeyCode.C) || UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                SelectSpecialSpell(SpellType.Area);
            }

            // Alternância de feitiço pela roda do mouse (Scroll Wheel)
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0.05f)
            {
                CycleSpecialSpell(-1);
            }
            else if (scroll < -0.05f)
            {
                CycleSpecialSpell(1);
            }

            // Disparo do feitiço especial ativo via Botão Direito (RMB)
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                TriggerSpecialShot(currentSpecialSpell, UnityEngine.Input.mousePosition);
                return;
            }

            // Tiro Normal via Botão Esquerdo (LMB)
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                TriggerNormalShot(UnityEngine.Input.mousePosition);
            }
        }

        public void SelectSpecialSpell(SpellType spell)
        {
            currentSpecialSpell = spell;
            for (int i = 0; i < specialSpellList.Length; i++)
            {
                if (specialSpellList[i] == spell)
                {
                    selectedSpellIndex = i;
                    break;
                }
            }
            weaponSystem?.SetSpellTheme(currentSpecialSpell);
            OnSpecialSpellSelected?.Invoke(currentSpecialSpell);
        }

        private void CycleSpecialSpell(int direction)
        {
            selectedSpellIndex = (selectedSpellIndex + direction + specialSpellList.Length) % specialSpellList.Length;
            currentSpecialSpell = specialSpellList[selectedSpellIndex];
            weaponSystem?.SetSpellTheme(currentSpecialSpell);
            OnSpecialSpellSelected?.Invoke(currentSpecialSpell);
        }

        private void TriggerNormalShot(Vector2 screenPosition)
        {
            if (weaponSystem != null && !weaponSystem.TryConsumeAmmo())
            {
                return;
            }

            ShotHitInfo hitInfo = CalculateHitInfo(screenPosition);
            if (!hitInfo.isHit)
            {
                OnShotMissed?.Invoke();
            }
            else
            {
                weaponSystem?.TriggerHitMarker(hitInfo.isHeadshot);
            }
            OnNormalShot?.Invoke(hitInfo);
        }

        private void TriggerSpecialShot(SpellType spell, Vector2 screenPosition)
        {
            weaponSystem?.TriggerRecoil();
            ShotHitInfo hitInfo = CalculateHitInfo(screenPosition);
            if (hitInfo.isHit)
            {
                weaponSystem?.TriggerHitMarker(hitInfo.isHeadshot);
            }
            OnSpecialShot?.Invoke(spell, hitInfo);
        }

        private ShotHitInfo CalculateHitInfo(Vector2 screenPosition)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            Vector3 worldPos = Vector3.zero;
            if (mainCamera != null)
            {
                worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -mainCamera.transform.position.z));
            }
            worldPos.z = 0f;

            WizardController hitWizard = FindWizardAtPosition(worldPos);
            bool isHeadshot = false;

            if (hitWizard != null)
            {
                float relativeY = worldPos.y - hitWizard.transform.position.y;
                float headshotThreshold = hitWizard.GetCurrentHeight() * 0.22f;
                if (relativeY > headshotThreshold)
                {
                    isHeadshot = true;
                }
            }

            return new ShotHitInfo
            {
                target = hitWizard,
                isHit = hitWizard != null,
                isHeadshot = isHeadshot,
                hitPoint = worldPos
            };
        }

        private WizardController FindWizardAtPosition(Vector3 worldPos)
        {
            // 1. Prioridade absoluta: Clique direto no colisor do goblin
            Collider2D[] directHits = Physics2D.OverlapPointAll(worldPos);
            WizardController bestDirect = null;
            int highestDirectOrder = int.MinValue;

            foreach (var col in directHits)
            {
                var wizard = col.GetComponent<WizardController>();
                if (wizard != null)
                {
                    var sr = wizard.GetComponent<SpriteRenderer>();
                    int order = sr != null ? sr.sortingOrder : 0;
                    if (order > highestDirectOrder)
                    {
                        highestDirectOrder = order;
                        bestDirect = wizard;
                    }
                }
            }

            if (bestDirect != null) return bestDirect;

            // 2. Fallback: Raio de tolerância (clickToleranceRadius), priorizando quem estiver na frente
            Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, clickToleranceRadius);
            WizardController bestTarget = null;
            float closestDist = float.MaxValue;
            int highestSortingOrder = int.MinValue;

            foreach (var col in hits)
            {
                var wizard = col.GetComponent<WizardController>();
                if (wizard != null)
                {
                    var sr = wizard.GetComponent<SpriteRenderer>();
                    int order = sr != null ? sr.sortingOrder : 0;
                    float dist = Vector2.Distance(worldPos, wizard.transform.position);

                    if (order > highestSortingOrder || (order == highestSortingOrder && dist < closestDist))
                    {
                        highestSortingOrder = order;
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