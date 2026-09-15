using System;
using System.Collections;
using UnityEngine;
using WizardGame.Core;

namespace WizardGame.Entities
{
    /// <summary>
    /// Gerencia a arma POV (Varinha MÃ¡gica em primeira pessoa), a mira grÃ¡fica,
    /// a contagem de muniÃ§Ã£o (mana) e o efeito de recuo ao disparar.
    /// </summary>
    public class WeaponSystem : MonoBehaviour
    {
        [Header("ReferÃªncias Visuais")]
        [Tooltip("Transform da varinha mÃ¡gica em primeira pessoa.")]
        [SerializeField] private Transform wandTransform;

        [Tooltip("Transform do cursor de mira grÃ¡fico.")]
        [SerializeField] private Transform crosshairTransform;

        [Tooltip("Efeito ou luz na ponta da varinha ao atirar.")]
        [SerializeField] private SpriteRenderer muzzleFlash;

        [Header("ParÃ¢metros de MuniÃ§Ã£o")]
        [SerializeField] private int maxAmmo = 8;
        [SerializeField] private float reloadDuration = 1.2f;

        [Header("ParÃ¢metros de Recuo e AnimaÃ§Ã£o POV")]
        [SerializeField] private float recoilDistance = 0.4f;
        [SerializeField] private float recoilRotation = 15f;
        [SerializeField] private float recoilRecoverySpeed = 10f;
        [SerializeField] private Vector3 wandRestOffset = new Vector3(2.5f, -2.2f, 0f);

        private int currentAmmo;
        private bool isReloading;
        private Vector3 currentRecoilOffset;
        private float currentRecoilAngle;
        private Camera mainCam;

        public event Action<int, int> OnAmmoChanged;
        public event Action<bool> OnReloadStateChanged;

        public int CurrentAmmo => currentAmmo;
        public int MaxAmmo => maxAmmo;
        public bool IsReloading => isReloading;

        private void Awake()
        {
            mainCam = Camera.main;
            currentAmmo = maxAmmo;
            Cursor.visible = false; // Oculta o cursor do Windows para usar a mira do jogo

            if (muzzleFlash != null) muzzleFlash.enabled = false;
        }

        private void Start()
        {
            OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
        }

        private void Update()
        {
            UpdateCrosshairAndWand();
        }

        private void UpdateCrosshairAndWand()
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            Vector3 mousePos = UnityEngine.Input.mousePosition;
            mousePos.z = 10f;
            Vector3 worldMousePos = mainCam.ScreenToWorldPoint(mousePos);
            worldMousePos.z = 0f;

            // Atualiza mira
            if (crosshairTransform != null)
            {
                crosshairTransform.position = worldMousePos;
            }

            // Atualiza varinha POV
            if (wandTransform != null)
            {
                // PosiÃ§Ã£o base na tela (canto inferior direito em relaÃ§Ã£o Ã  cÃ¢mera)
                Vector3 camPos = mainCam.transform.position;
                Vector3 targetBasePos = camPos + wandRestOffset + currentRecoilOffset;
                wandTransform.position = Vector3.Lerp(wandTransform.position, targetBasePos, Time.deltaTime * 15f);

                // DireÃ§Ã£o para onde a varinha aponta (em direÃ§Ã£o Ã  mira)
                Vector3 aimDir = (worldMousePos - wandTransform.position).normalized;
                float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
                Quaternion targetRot = Quaternion.Euler(0f, 0f, angle - 45f + currentRecoilAngle);
                wandTransform.rotation = Quaternion.Lerp(wandTransform.rotation, targetRot, Time.deltaTime * 20f);

                // RecuperaÃ§Ã£o suave do recuo
                currentRecoilOffset = Vector3.Lerp(currentRecoilOffset, Vector3.zero, Time.deltaTime * recoilRecoverySpeed);
                currentRecoilAngle = Mathf.Lerp(currentRecoilAngle, 0f, Time.deltaTime * recoilRecoverySpeed);
            }
        }

        /// <summary>
        /// Tenta consumir 1 carga de muniÃ§Ã£o. Retorna true se o disparo foi autorizado.
        /// </summary>
        public bool TryConsumeAmmo()
        {
            if (isReloading) return false;

            if (currentAmmo <= 0)
            {
                StartReload();
                return false;
            }

            currentAmmo--;
            OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
            TriggerRecoil();

            if (currentAmmo == 0)
            {
                StartReload();
            }

            return true;
        }

        public void TriggerRecoil()
        {
            // Aplica impulso de coice na varinha
            currentRecoilOffset = new Vector3(-recoilDistance * 0.5f, -recoilDistance, 0f);
            currentRecoilAngle = recoilRotation;

            if (muzzleFlash != null)
            {
                StopCoroutine("FlashMuzzleRoutine");
                StartCoroutine("FlashMuzzleRoutine");
            }
        }

        private IEnumerator FlashMuzzleRoutine()
        {
            muzzleFlash.enabled = true;
            yield return new WaitForSeconds(0.06f);
            muzzleFlash.enabled = false;
        }

        public void StartReload()
        {
            if (isReloading || currentAmmo == maxAmmo) return;
            StartCoroutine(ReloadRoutine());
        }

        private IEnumerator ReloadRoutine()
        {
            isReloading = true;
            OnReloadStateChanged?.Invoke(true);

            float elapsed = 0f;
            while (elapsed < reloadDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            currentAmmo = maxAmmo;
            isReloading = false;
            OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
            OnReloadStateChanged?.Invoke(false);
        }
    }
}