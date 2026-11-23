using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WizardGame.Core;
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
        [SerializeField] private Image crosshairImage;

        [Tooltip("Imagem do brilho mágico pulsante na ponta do cristal.")]
        [SerializeField] private Image crystalGlowImage;

        [Header("Referências Legadas / Fallback")]
        [SerializeField] private Transform wandTransform;
        [SerializeField] private Transform crosshairTransform;
        [SerializeField] private SpriteRenderer muzzleFlash;

        [Header("Parâmetros de Munição (Mana)")]
        [SerializeField] private int maxAmmo = 10;
        [SerializeField] private float reloadDuration = 0.78f;

        [Header("Parâmetros de Mira & Rastreamento")]
        [SerializeField] private float aimTrackingSpeed = 24f;
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
        private Coroutine activeReloadCoroutine;
        private Vector2 currentRecoilOffset;
        private float currentRecoilAngle;
        private float crosshairRecoilScale = 1.0f;
        private float targetCrosshairBaseScale = 1.0f;
        private float currentCrosshairBaseScale = 1.0f;
        private float hitMarkerTimer = 0f;
        private Color currentHitMarkerColor = Color.white;
        private bool isPrecisionActive = false;

        private Vector2 currentSwayOffset;
        private float currentSwayRotation;
        private Color currentSpellGlowColor = new Color(0.2f, 0.92f, 1f, 1f); // Arcano (Azul/Ciano)
        private bool isFlashingGlow;

        private Camera mainCam;
        private static readonly Collider2D[] hoverBuffer = new Collider2D[12];
        private static readonly ContactFilter2D hoverNoFilter = ContactFilter2D.noFilter;

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
            float dt = (Time.timeScale > 0.0001f) ? Time.unscaledDeltaTime : 0f;

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
                    currentCrosshairBaseScale = Mathf.Lerp(currentCrosshairBaseScale, targetCrosshairBaseScale, dt * 12f);
                    crosshairRect.localScale = Vector3.one * (crosshairRecoilScale * currentCrosshairBaseScale);
                    crosshairRecoilScale = Mathf.Lerp(crosshairRecoilScale, 1.0f, dt * 18f);

                    if (isReloading)
                    {
                        crosshairRect.Rotate(0f, 0f, -260f * dt);
                    }
                    else
                    {
                        crosshairRect.localRotation = Quaternion.Lerp(crosshairRect.localRotation, Quaternion.identity, dt * 16f);
                    }

                    if (crosshairImage == null)
                    {
                        crosshairImage = crosshairRect.GetComponent<Image>();
                    }

                    if (crosshairImage != null)
                    {
                        if (hitMarkerTimer > 0f)
                        {
                            hitMarkerTimer -= dt;
                            crosshairImage.color = currentHitMarkerColor;
                        }
                        else if (isReloading)
                        {
                            Color reloadColor = new Color(1f, 0.78f, 0.28f, 0.78f);
                            crosshairImage.color = Color.Lerp(crosshairImage.color, reloadColor, dt * 20f);
                        }
                        else
                        {
                            bool isHovering = false;
                            if (mainCam == null) mainCam = Camera.main;
                            if (mainCam != null)
                            {
                                Vector3 mPos = UnityEngine.Input.mousePosition;
                                mPos.z = -mainCam.transform.position.z;
                                Vector3 wPos = mainCam.ScreenToWorldPoint(mPos);
                                wPos.z = 0f;

                                int hoverCount = Physics2D.OverlapPoint(wPos, hoverNoFilter, hoverBuffer);
                                var env = StageEnvironmentManager.Instance;
                                for (int i = 0; i < hoverCount; i++)
                                {
                                    Collider2D hoverCol = hoverBuffer[i];
                                    if (hoverCol == null) continue;
                                    if (hoverCol.TryGetComponent<BossProjectileInstance>(out _) || hoverCol.TryGetComponent<PowerUpItem>(out _))
                                    {
                                        isHovering = true;
                                        break;
                                    }
                                    if (hoverCol.TryGetComponent<WizardController>(out var hoverWiz) && !hoverWiz.IsDeadOrEscaping)
                                    {
                                        bool blocked = env != null &&
                                            (env.IsPointBlockedByCover(wPos, hoverWiz.CurrentSortingOrder) || hoverWiz.IsBehindCover());
                                        if (!blocked)
                                        {
                                            isHovering = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            Color idleColor = isPrecisionActive ? new Color(0.45f, 1f, 0.65f, 0.98f) : new Color(1f, 1f, 1f, 0.96f);
                            Color targetColor = isHovering ? new Color(1f, 0.42f, 0.42f, 1f) : idleColor;
                            crosshairImage.color = Color.Lerp(crosshairImage.color, targetColor, dt * 20f);
                        }
                    }
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

                currentSwayOffset = Vector2.Lerp(currentSwayOffset, targetSway, dt * swaySmoothSpeed);
                currentSwayRotation = Mathf.Lerp(currentSwayRotation, targetSwayRot, dt * swaySmoothSpeed);

                // Idle Breathing Bobbing
                float bobTime = Time.unscaledTime * bobFrequency;
                float bobX = Mathf.Sin(bobTime) * bobAmountX;
                float bobY = Mathf.Cos(bobTime * 2f) * bobAmountY;
                Vector2 bobOffset = new Vector2(bobX, bobY);

                // Posição final ancorada combinando base, bobbing, sway e recoil
                Vector2 targetPos = wandBaseAnchoredPos + bobOffset + currentSwayOffset + currentRecoilOffset;
                wandRect.anchoredPosition = Vector2.Lerp(wandRect.anchoredPosition, targetPos, dt * 20f);

                // Rotação dinâmica acompanhando a mira do mouse
                Vector2 wandScreenOrigin = wandRect.position;
                Vector2 aimDir = ((Vector2)UnityEngine.Input.mousePosition - wandScreenOrigin);
                if (aimDir.sqrMagnitude > 4f)
                {
                    float targetAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
                    float rotDelta = targetAngle - wandBaseAngle;
                    rotDelta = Mathf.Clamp(rotDelta, -45f, 40f);
                    float totalAngle = rotDelta + currentRecoilAngle + currentSwayRotation;

                    Quaternion targetRot = Quaternion.Euler(0f, 0f, totalAngle);
                    wandRect.localRotation = Quaternion.Slerp(wandRect.localRotation, targetRot, dt * aimTrackingSpeed);
                }

                // Recuperação elástica do recuo
                currentRecoilOffset = Vector2.Lerp(currentRecoilOffset, Vector2.zero, dt * recoilRecoverySpeed);
                currentRecoilAngle = Mathf.Lerp(currentRecoilAngle, 0f, dt * recoilRecoverySpeed);
            }
            else if (wandTransform != null)
            {
                // Fallback legado em world space
                if (mainCam == null) mainCam = Camera.main;
                if (mainCam != null)
                {
                    Vector3 camPos = mainCam.transform.position;
                    Vector3 targetBasePos = camPos + new Vector3(wandBaseAnchoredPos.x * 0.01f, wandBaseAnchoredPos.y * 0.01f, 0f) + (Vector3)currentRecoilOffset;
                    wandTransform.position = Vector3.Lerp(wandTransform.position, targetBasePos, dt * 15f);

                    Vector3 mousePos = UnityEngine.Input.mousePosition;
                    mousePos.z = 10f;
                    Vector3 worldMousePos = mainCam.ScreenToWorldPoint(mousePos);
                    worldMousePos.z = 0f;

                    Vector3 aimDir = (worldMousePos - wandTransform.position).normalized;
                    float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
                    Quaternion targetRot = Quaternion.Euler(0f, 0f, angle - 45f + currentRecoilAngle);
                    wandTransform.rotation = Quaternion.Lerp(wandTransform.rotation, targetRot, dt * 20f);

                    currentRecoilOffset = Vector2.Lerp(currentRecoilOffset, Vector2.zero, dt * recoilRecoverySpeed);
                    currentRecoilAngle = Mathf.Lerp(currentRecoilAngle, 0f, dt * recoilRecoverySpeed);
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
        /// Ativa o feedback visual de impacto (hit marker) no retículo de mira.
        /// </summary>
        public void TriggerHitMarker(bool isHeadshot)
        {
            hitMarkerTimer = 0.14f;
            currentHitMarkerColor = isHeadshot ? new Color(1f, 0.92f, 0.2f, 1f) : new Color(1f, 0.22f, 0.22f, 1f);
            crosshairRecoilScale = isHeadshot ? 1.55f : 1.38f;
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
            if (activeReloadCoroutine != null) StopCoroutine(activeReloadCoroutine);
            activeReloadCoroutine = StartCoroutine(ReloadRoutine());
        }

        private IEnumerator ReloadRoutine()
        {
            isReloading = true;
            OnReloadStateChanged?.Invoke(true);

            float elapsed = 0f;
            while (elapsed < reloadDuration)
            {
                if (Time.timeScale > 0.0001f)
                {
                    elapsed += Time.unscaledDeltaTime;
                }
                yield return null;
            }

            currentAmmo = maxAmmo;
            isReloading = false;
            activeReloadCoroutine = null;
            OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
            OnReloadStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Recarrega instantaneamente toda a mana do mago (usado como bônus ao concluir uma onda).
        /// </summary>
        public void RefillAmmo()
        {
            if (activeReloadCoroutine != null)
            {
                StopCoroutine(activeReloadCoroutine);
                activeReloadCoroutine = null;
                isReloading = false;
                OnReloadStateChanged?.Invoke(false);
            }
            currentAmmo = maxAmmo;
            OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
        }

        /// <summary>
        /// Restaura munição instantaneamente (usado pelo power-up de Mana).
        /// </summary>
        public void AddAmmo(int amount)
        {
            if (activeReloadCoroutine != null)
            {
                StopCoroutine(activeReloadCoroutine);
                activeReloadCoroutine = null;
                isReloading = false;
                OnReloadStateChanged?.Invoke(false);
            }
            currentAmmo = Mathf.Clamp(currentAmmo + amount, 0, maxAmmo);
            OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
        }

        /// <summary>
        /// Modifica o tamanho do retículo de mira para modo de precisão cirúrgica (Power-up de Precisão).
        /// </summary>
        public void SetPrecisionMode(bool enabled)
        {
            isPrecisionActive = enabled;
            targetCrosshairBaseScale = enabled ? 0.60f : 1.0f;
        }
    }
}