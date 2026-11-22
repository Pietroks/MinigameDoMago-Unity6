using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WizardGame.UI
{
    /// <summary>
    /// Adiciona resposta tátil e visual suave (hover/press spring) para botões de menu e cartas de UI,
    /// funcionando mesmo com Time.timeScale = 0 (Menus e Pausa).
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.045f;
        [SerializeField] private float pressScale = 0.96f;
        [SerializeField] private float smoothSpeed = 16f;
        [SerializeField] private Color hoverTint = new Color(1.12f, 1.08f, 1.0f, 1.0f);

        private Vector3 baseScale = Vector3.one;
        private Vector3 targetScale = Vector3.one;
        private Color baseColor = Color.white;
        private Color targetColor = Color.white;
        private Graphic targetGraphic;
        private bool isHovered;
        private bool isPressed;

        private void Awake()
        {
            baseScale = transform.localScale;
            targetScale = baseScale;
            targetGraphic = GetComponent<Graphic>();
            if (targetGraphic != null)
            {
                baseColor = targetGraphic.color;
                targetColor = baseColor;
            }
        }

        private void OnEnable()
        {
            isHovered = false;
            isPressed = false;
            targetScale = baseScale;
            transform.localScale = baseScale;
            if (targetGraphic != null)
            {
                targetGraphic.color = baseColor;
                targetColor = baseColor;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime * smoothSpeed;
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, dt);
            if (targetGraphic != null)
            {
                targetGraphic.color = Color.Lerp(targetGraphic.color, targetColor, dt);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            RefreshTargets();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
            RefreshTargets();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
            RefreshTargets();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            RefreshTargets();
        }

        private void RefreshTargets()
        {
            if (isPressed)
            {
                targetScale = baseScale * pressScale;
                targetColor = baseColor * 0.92f;
                targetColor.a = baseColor.a;
            }
            else if (isHovered)
            {
                targetScale = baseScale * hoverScale;
                targetColor = new Color(
                    Mathf.Clamp01(baseColor.r * hoverTint.r),
                    Mathf.Clamp01(baseColor.g * hoverTint.g),
                    Mathf.Clamp01(baseColor.b * hoverTint.b),
                    baseColor.a
                );
            }
            else
            {
                targetScale = baseScale;
                targetColor = baseColor;
            }
        }
    }
}
