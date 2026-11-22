using UnityEngine;

namespace WizardGame.Core
{
    /// <summary>
    /// Ajusta dinamicamente a escala do cenário de fundo para cobrir 100% da visão da câmera ortográfica (Aspect Fill),
    /// sem distorção e sem barras pretas em qualquer resolução ou proporção de aspecto (16:9, 16:10, 21:9).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BackgroundScaler : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        private SpriteRenderer spriteRenderer;
        private float extraZoom = 1.0f;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private float lastOrthoSize;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (targetCamera == null) targetCamera = Camera.main;
            AdjustScale();
        }

        private void Start()
        {
            AdjustScale();
        }

        private void Update()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            float ortho = targetCamera != null ? targetCamera.orthographicSize : 0f;
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight || !Mathf.Approximately(ortho, lastOrthoSize))
            {
                AdjustScale();
            }
        }

        public void SetExtraZoom(float zoom)
        {
            extraZoom = Mathf.Clamp(zoom, 1.0f, 1.5f);
            AdjustScale();
        }

        public void AdjustScale()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null) return;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastOrthoSize = targetCamera.orthographicSize;

            float worldScreenHeight = targetCamera.orthographicSize * 2f;
            float worldScreenWidth = worldScreenHeight / Mathf.Max(1, Screen.height) * Mathf.Max(1, Screen.width);

            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            float scaleX = worldScreenWidth / spriteSize.x;
            float scaleY = worldScreenHeight / spriteSize.y;

            // Aspect Fill: garante cobertura total da tela sem bordas pretas
            float scale = Mathf.Max(scaleX, scaleY) * Mathf.Max(1.0f, extraZoom);
            transform.localScale = new Vector3(scale, scale, 1f);
            transform.position = new Vector3(targetCamera.transform.position.x, targetCamera.transform.position.y, 0f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AdjustScale();
        }
#endif
    }
}
