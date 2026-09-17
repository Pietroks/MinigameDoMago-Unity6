using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WizardGame.Data;

namespace WizardGame.Entities
{
    /// <summary>
    /// Gerencia a arma POV (Varinha e Mão em primeira pessoa), a mira de precisão gráfica,
    /// a contagem de mana/munição, recuo físico (recoil), inércia (mouse sway),
    /// oscilação orgânica (bobbing) e brilho dinâmico do cristal elemental.
    /// </summary>
    public class WeaponSystem : MonoBehaviour
    {
        [Header("Referências Visuais no Canvas UI")]
        [Tooltip("RectTransform da varinha com luva em primeira pessoa.")]
        [SerializeField] private RectTransform wandRect;

        [Tooltip("RectTransform do retículo de mira de alta precisão.")]
        [SerializeField] private RectTransform crosshairRect;

        [Tooltip("Imagem do brilho mágico pulsante na ponta do cristal.")]
        [SerializeField] private Image crystalGlowImage;

        [Header("Referências Legadas / Fallback")]
        [SerializeField] private Transform wandTransform;
        [SerializeField] private Transform crosshairTransform;
        [SerializeField] private SpriteRenderer muzzleFlash;

        [Header("Parâmetros de Munição (Mana)")]
        [SerializeField] private int maxAmmo = 8;
        [SerializeField] private float reloadDuration = 1.2f;

        [Header("Parâmetros de Mira & Rastreamento")]
        [SerializeField] private float aimTrackingSpeed = 16f;
        [SerializeField] private float wandBaseAngle = 118.7f; // Ângulo natural da varinha no sprite 1024x1024
        [SerializeField] private Vector2 wandBaseAnchoredPos = new Vector2(-40f, -35f);

        [Header("Sway & Inércia do Mouse")]
        [SerializeField] private float swayAmount = 24f;
        [SerializeField] private float swayRotationAmount = 4.5f;
        [SerializeField] private float swaySmoothSpeed = 12f;

        [Header("Bobbing & Respiração")]
        [SerializeField] private float bobFrequency = 2.2f;
        [SerializeField] private float bobAmountX = 5.0f;
        [SerializeField] private float bobAmountY = 7.0f;

        [Header("Recuo (Recoil)")]
        [SerializeField] private float recoilDistance = 42f;
        [SerializeField] private float recoilRotation = 14f;
        [SerializeField] private float recoilRecoverySpeed = 11f;

        [Header("Brilho Elemental do Cristal")]
        [SerializeField] private float glowPulseSpeed = 3.5f;
        [SerializeField] private float glowMinAlpha = 0.65f;
        [SerializeField] private float glowMaxAlpha = 1.0f;

        private int currentAmmo;
        private bool isReloading;
        private Vector2 currentRecoilOffset;
        private float currentRecoilAngle;
        private float crosshairRecoilScale = 1.0f;

        private Vector2 currentSwayOffset;
        private float currentSwayRotation;
        private Color currentSpellGlowColor = new Color(0.2f, 0.92f, 1f, 1f); // Arcano (Azul/Ciano)
        private bool isFlashingGlow;

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

            if (muzzleFlash != null) muzzleFlash.enabled = false;
        }

        private void Start()
        {
            OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
            SetSpellTheme(SpellType.Arcane);
        }

        private void Update()
        {
            UpdateCrosshairAndWand();
            UpdateCrystalGlow();
        }

        private void UpdateCrosshairAndWand()
        {
            bool isCursorMenuVisible = Cursor.visible;

            // 1. Atualiza Retículo de Mira
            if (crosshairRect != null)
            {
                if (isCursorMenuVisible)
                {
                    crosshairRect.gameObject.SetActive(false);
                }
                else
                {
                    crosshairRect.gameObject.SetActive(true);
                    crosshairRect.position = UnityEngine.Input.mousePosition;
                    crosshairRect.localScale = Vector3.one * crosshairRecoilScale;
                    crosshairRecoilScale = Mathf.Lerp(crosshairRecoilScale, 1.0f, Time.deltaTime * 18f);
                }
            }
            else if (crosshairTransform != null)
            {
                if (mainCam == null) mainCam = Camera.main;
                if (mainCam != null)
                {
                    Vector3 mousePos = UnityEngine.Input.mousePosition;
                    mousePos.z = 10f;
                    Vector3 worldPos = mainCam.ScreenToWorldPoint(mousePos);
                    worldPos.z = 0f;
                    crosshairTransform.position = worldPos;
                }
            }

            // 2. Atualiza Varinha POV (UI Canvas)
            if (wandRect != null)
            {
                if (isCursorMenuVisible)
                {
                    wandRect.gameObject.SetActive(false);
                    return;
                }
                wandRect.gameObject.SetActive(true);

                // Mouse Sway & Inércia
                float mouseX = UnityEngine.Input.GetAxis("Mouse X");
                float mouseY = UnityEngine.Input.GetAxis("Mouse Y");
                Vector2 targetSway = new Vector2(-mouseX * swayAmount, -mouseY * swayAmount);
                float targetSwayRot = -mouseX * swayRotationAmount;

                currentSwayOffset = Vector2.Lerp(currentSwayOffset, targetSway, Time.deltaTime * swaySmoothSpeed);
                currentSwayRotation = Mathf.Lerp(currentSwayRotation, targetSwayRot, Time.deltaTime * swaySmoothSpeed);

                // Idle Breathing Bobbing
                float bobTime = Time.time * bobFrequency;
                float bobX = Mathf.Sin(bobTime) * bobAmountX;
                float bobY = Mathf.Cos(bobTime * 2f) * bobAmountY;
                Vector2 bobOffset = new Vector2(bobX, bobY);

                // Posição final ancorada combinando base, bobbing, sway e recoil
                Vector2 targetPos = wandBaseAnchoredPos + bobOffset + currentSwayOffset + currentRecoilOffset;
                wandRect.anchoredPosition = Vector2.Lerp(wandRect.anchoredPosition, targetPos, Time.deltaTime * 20f);

                // Rotação dinâmica acompanhando a mira do mouse
                Vector2 wandScreenOrigin = wandRect.position;
                Vector2 aimDir = ((Vector2)UnityEngine.Input.mousePosition - wandScreenOrigin);
                if (aimDir.sqrMagnitude > 4f)
                {
                    float targetAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
                    float rotDelta = targetAngle - wandBaseAngle;
                    float totalAngle = rotDelta + currentRecoilAngle + currentSwayRotation;

                    Quaternion targetRot = Quaternion.Euler(0f, 0f, totalAngle);
                    wandRect.localRotation = Quaternion.Slerp(wandRect.localRotation, targetRot, Time.deltaTime * aimTrackingSpeed);
                }

                // Recuperação elástica do recuo
                currentRecoilOffset = Vector2.Lerp(currentRecoilOffset, Vector2.zero, Time.deltaTime * recoilRecoverySpeed);
                currentRecoilAngle = Mathf.Lerp(currentRecoilAngle, 0f, Time.deltaTime * recoilRecoverySpeed);
            }
            else if (wandTransform != null)
            {
                // Fallback legado em world space
                if (mainCam == null) mainCam = Camera.main;
                if (mainCam != null)
                {
                    Vector3 camPos = mainCam.transform.position;
                    Vector3 targetBasePos = camPos + new Vector3(wandBaseAnchoredPos.x * 0.01f, wandBaseAnchoredPos.y * 0.01f, 0f) + (Vector3)currentRecoilOffset;
                    wandTransform.position = Vector3.Lerp(wandTransform.position, targetBasePos, Time.deltaTime * 15f);

                    Vector3 mousePos = UnityEngine.Input.mousePosition;
                    mousePos.z = 10f;
                    Vector3 worldMousePos = mainCam.ScreenToWorldPoint(mousePos);
                    worldMousePos.z = 0f;

                    Vector3 aimDir = (worldMousePos - wandTransform.position).normalized;
                    float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
                    Quaternion targetRot = Quaternion.Euler(0f, 0f, angle - 45f + currentRecoilAngle);
                    wandTransform.rotation = Quaternion.Lerp(wandTransform.rotation, targetRot, Time.deltaTime * 20f);

                    currentRecoilOffset = Vector2.Lerp(currentRecoilOffset, Vector2.zero, Time.deltaTime * recoilRecoverySpeed);
                    currentRecoilAngle = Mathf.Lerp(currentRecoilAngle, 0f, Time.deltaTime * recoilRecoverySpeed);
                }
            }
        }

        private void UpdateCrystalGlow()
        {
            if (crystalGlowImage == null) return;

            float pulse = Mathf.Sin(Time.time * glowPulseSpeed) * 0.5f + 0.5f;
            float alpha = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, pulse);
            Color c = currentSpellGlowColor;
            c.a = isFlashingGlow ? 1.0f : alpha;
            crystalGlowImage.color = c;

            float scale = isFlashingGlow ? 1.7f : (0.92f + pulse * 0.22f);
            crystalGlowImage.transform.localScale = Vector3.one * scale;
        }

        /// <summary>
        /// Altera a cor e tema do brilho do cristal conforme o feitiço selecionado.
        /// </summary>
        public void SetSpellTheme(SpellType spell)
        {
            switch (spell)
            {
                case SpellType.Arcane:
                    currentSpellGlowColor = new Color(0.15f, 0.9f, 1f, 1f); // Azul Arcano Ciano
                    break;
                case SpellType.Ice:
                    currentSpellGlowColor = new Color(0.6f, 0.96f, 1f, 1f); // Ciano Geada
                    break;
                case SpellType.Lightning:
                    currentSpellGlowColor = new Color(1f, 0.92f, 0.2f, 1f); // Relâmpago Dourado
                    break;
                case SpellType.Area:
                    currentSpellGlowColor = new Color(1f, 0.35f, 0.15f, 1f); // Fogo Explosão Rubro
                    break;
            }
        }

        /// <summary>
        /// Tenta consumir 1 carga de mana/munição. Retorna true se o disparo foi autorizado.
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

        /// <summary>
        /// Dispara o coice físico na varinha e o clarão mágico no cristal e na mira.
        /// </summary>
        public void TriggerRecoil()
        {
            // Impulso de coice na varinha (recuo para trás e para baixo, com rotação ascendente)
            currentRecoilOffset += new Vector2(-recoilDistance * 0.35f, -recoilDistance);
            currentRecoilAngle += recoilRotation;
            crosshairRecoilScale = 1.35f;

            if (crystalGlowImage != null)
            {
                StopCoroutine("FlashGlowRoutine");
                StartCoroutine("FlashGlowRoutine");
            }

            if (muzzleFlash != null)
            {
                StopCoroutine("FlashMuzzleRoutine");
                StartCoroutine("FlashMuzzleRoutine");
            }
        }

        private IEnumerator FlashGlowRoutine()
        {
            isFlashingGlow = true;
            yield return new WaitForSeconds(0.09f);
            isFlashingGlow = false;
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