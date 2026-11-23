using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Kryvon
{
    [DisallowMultipleComponent]
    public class MinimapController : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField] float cameraHeight = 600f;
        [SerializeField] float orthographicSize = 180f;
        [SerializeField] int textureSize = 512;
        [SerializeField] bool rotateWithPlayer = true;

        [Header("References")]
        public Transform playerTransform;
        public Camera minimapCamera;
        public RectTransform minimapContainer;
        public RawImage minimapImage;
        public RectTransform playerIconRect;
        public RectTransform mapMaskRect;

        public bool RotateWithPlayer => rotateWithPlayer;

        // auto-start off: KryvonMinimap already draws the round map from a baked texture (no second live camera every frame).
        // Add this component by hand to try it.
        static void Boot()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => CreateIfNeeded();
            CreateIfNeeded();
        }

        static void CreateIfNeeded()
        {
            if (FindAnyObjectByType<MinimapController>() != null) return;
            var player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;
            player.gameObject.AddComponent<MinimapController>();
        }

        void Start()
        {
            if (playerTransform == null) playerTransform = transform;
            BuildCamera();
            BuildUI();
        }

        void BuildCamera()
        {
            if (minimapCamera == null)
            {
                var go = new GameObject("MinimapCamera");
                go.transform.SetParent(transform, false);
                minimapCamera = go.AddComponent<Camera>();
                minimapCamera.transform.SetPositionAndRotation(
                    new Vector3(playerTransform.position.x, cameraHeight, playerTransform.position.z),
                    Quaternion.Euler(90f, 0f, 0f));
                minimapCamera.orthographic = true;
                minimapCamera.orthographicSize = orthographicSize;
                minimapCamera.nearClipPlane = 1f;
                minimapCamera.farClipPlane = cameraHeight + 1000f;
                minimapCamera.enabled = true;
            }

            textureSize = Mathf.Clamp(textureSize, 256, 1024);
            var rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
            rt.name = "KRYVON_MinimapRT";
            rt.useMipMap = false;
            rt.autoGenerateMips = false;
            rt.Create();
            minimapCamera.targetTexture = rt;
            minimapCamera.allowMSAA = false;
            minimapCamera.allowHDR = false;
        }

        void BuildUI()
        {
            if (!FindAnyObjectByType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var canvasGo = new GameObject("KRAYVON_MinimapCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var container = Rect("MinimapContainer", canvasGo.transform);
            container.anchorMin = container.anchorMax = new Vector2(0f, 1f);
            container.pivot = new Vector2(0f, 1f);
            container.anchoredPosition = new Vector2(24f, -24f);
            container.sizeDelta = new Vector2(300f, 300f);
            minimapContainer = container;

            var frame = Rect("MapFrame", container);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = Vector2.zero;
            frame.offsetMax = Vector2.zero;
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = new Color(0.04f, 0.06f, 0.07f, 0.92f);

            var mask = Rect("MapMask", frame);
            mask.anchorMin = Vector2.zero;
            mask.anchorMax = Vector2.one;
            mask.offsetMin = new Vector2(8f, 8f);
            mask.offsetMax = new Vector2(-8f, -8f);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var maskImage = mask.gameObject.AddComponent<Image>();
            maskImage.color = new Color(0.12f, 0.32f, 0.43f);
            mapMaskRect = mask;

            var rawGo = new GameObject("MapImage", typeof(RectTransform), typeof(RawImage));
            rawGo.transform.SetParent(mask, false);
            minimapImage = rawGo.GetComponent<RawImage>();
            minimapImage.texture = minimapCamera.targetTexture;
            var rawRect = minimapImage.rectTransform;
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = rawRect.offsetMax = Vector2.zero;

            var icon = Rect("PlayerArrow", frame);
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = Vector2.zero;
            icon.sizeDelta = new Vector2(28f, 28f);
            icon.gameObject.AddComponent<Image>().color = new Color(1f, 0.82f, 0.25f);
            playerIconRect = icon;

            var close = Rect("CloseMap", container);
            close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 1f);
            close.anchoredPosition = new Vector2(-8f, -8f);
            close.sizeDelta = new Vector2(54f, 54f);
            var closeImage = close.gameObject.AddComponent<Image>();
            closeImage.color = new Color(0.62f, 0.10f, 0.08f, 0.96f);
            var closeText = Rect("X", close);
            closeText.anchorMin = Vector2.zero; closeText.anchorMax = Vector2.one;
            closeText.offsetMin = closeText.offsetMax = Vector2.zero;
            var text = closeText.gameObject.AddComponent<Text>();
            text.text = "X"; text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 34; text.color = Color.white;
            close.gameObject.AddComponent<Button>().onClick.AddListener(CloseMinimap);

            var rotate = Rect("ToggleRotation", container);
            rotate.anchorMin = rotate.anchorMax = rotate.pivot = new Vector2(0f, 0f);
            rotate.anchoredPosition = new Vector2(10f, 10f);
            rotate.sizeDelta = new Vector2(118f, 44f);
            var rotateImage = rotate.gameObject.AddComponent<Image>();
            rotateImage.color = new Color(0.12f, 0.14f, 0.16f, 0.96f);
            var rotateText = Rect("Label", rotate);
            rotateText.anchorMin = Vector2.zero; rotateText.anchorMax = Vector2.one;
            rotateText.offsetMin = rotateText.offsetMax = Vector2.zero;
            var rt = rotateText.gameObject.AddComponent<Text>();
            rt.text = "ROTATE: ON"; rt.alignment = TextAnchor.MiddleCenter;
            rt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rt.fontSize = 20; rt.color = Color.white;
            var rb = rotate.gameObject.AddComponent<Button>();
            rb.onClick.AddListener(() => ToggleRotationMode(rt));

            var open = Rect("OpenMinimap", canvasGo.transform);
            open.anchorMin = open.anchorMax = open.pivot = new Vector2(0f, 1f);
            open.anchoredPosition = new Vector2(24f, -24f);
            open.sizeDelta = new Vector2(72f, 72f);
            var openImage = open.gameObject.AddComponent<Image>();
            openImage.color = new Color(0.09f, 0.11f, 0.12f, 0.96f);
            open.gameObject.AddComponent<Button>().onClick.AddListener(() => OpenMinimap(open));
            open.gameObject.SetActive(false);
            openMapButton = open.gameObject;
        }

        GameObject openMapButton;

        public void CloseMinimap()
        {
            if (minimapContainer != null) minimapContainer.gameObject.SetActive(false);
            if (openMapButton != null) openMapButton.SetActive(true);
        }

        public void OpenMinimap(RectTransform openButton)
        {
            if (minimapContainer != null) minimapContainer.gameObject.SetActive(true);
            if (openMapButton != null) openMapButton.SetActive(false);
        }

        public void ToggleMinimap()
        {
            if (minimapContainer == null) return;
            if (minimapContainer.gameObject.activeSelf) CloseMinimap();
            else OpenMinimap(null);
        }

        public void ToggleRotationMode(Text label)
        {
            rotateWithPlayer = !rotateWithPlayer;
            if (label != null) label.text = rotateWithPlayer ? "ROTATE: ON" : "FIXED NORTH";
        }

        void LateUpdate()
        {
            if (playerTransform == null || minimapCamera == null) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
                ToggleMinimap();
            float yaw = playerTransform.eulerAngles.y;
            minimapCamera.transform.position = new Vector3(playerTransform.position.x, cameraHeight, playerTransform.position.z);

            if (rotateWithPlayer)
            {
                minimapCamera.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                if (playerIconRect != null) playerIconRect.localRotation = Quaternion.identity;
            }
            else
            {
                minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                if (playerIconRect != null)
                    playerIconRect.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            }
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}
