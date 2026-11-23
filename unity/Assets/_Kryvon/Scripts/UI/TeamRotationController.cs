using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kryvon
{
    public class TeamRotationController : MonoBehaviour, IPointerClickHandler
    {
        public enum MarkerType
        {
            DropPoint,
            MoveTo,
            HoldArea,
            Danger
        }

        [Serializable]
        public struct RotationWaypoint
        {
            public Vector2 normalizedMapPos;
            public Vector3 worldPosition;
            public MarkerType markerType;
        }

        [Header("Map")]
        [SerializeField] RectTransform minimapRect;
        [SerializeField] Camera minimapCamera;
        [SerializeField] float terrainWidth = 2560f;
        [SerializeField] float terrainLength = 1396f;
        [SerializeField] Vector2 terrainOrigin;

        [Header("UI")]
        [SerializeField] Button togglePlanningButton;
        [SerializeField] Button clearPathButton;
        [SerializeField] Button closeMinimapButton;
        [SerializeField] Button openMinimapButton;
        [SerializeField] GameObject minimapContainer;
        [SerializeField] GameObject waypointPinPrefab;
        [SerializeField] Transform pinsParent;
        [SerializeField] LineRenderer worldPathLine;

        [SerializeField] MarkerType currentMarkerType = MarkerType.MoveTo;
        readonly List<RotationWaypoint> waypoints = new();
        readonly List<GameObject> spawnedPins = new();
        bool isPlanningMode;

        // auto-start off: KryvonMinimap already draws the round map from a baked texture (no second live camera every frame).
        // Add this component by hand to try it.
        static void Boot()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) =>
            {
                if (FindAnyObjectByType<TeamRotationController>() == null)
                {
                    var player = FindAnyObjectByType<PlayerController>();
                    if (player != null) player.gameObject.AddComponent<TeamRotationController>();
                }
            };
        }

        void Start()
        {
            var minimap = FindAnyObjectByType<MinimapController>();
            if (minimap != null)
            {
                minimapCamera = minimap.minimapCamera;
                minimapRect = minimap.mapMaskRect;
                minimapContainer = minimap.minimapContainer != null ? minimap.minimapContainer.gameObject : null;
            }
            if (worldPathLine == null) BuildWorldPathLine();
            if (togglePlanningButton == null) togglePlanningButton = BuildActionButton("PLAN ROTATION", new Vector2(24f, 326f), 190f, TogglePlanningMode);
            if (clearPathButton == null) clearPathButton = BuildActionButton("CLEAR PATH", new Vector2(224f, 326f), 190f, ClearWaypoints);
            if (minimapContainer != null)
            {
                if (closeMinimapButton == null) closeMinimapButton = BuildActionButton("X", new Vector2(0f, 0f), 54f, CloseMinimap, true);
                if (openMinimapButton == null) openMinimapButton = BuildActionButton("MAP", new Vector2(24f, -24f), 72f, OpenMinimap);
                if (openMinimapButton != null) openMinimapButton.gameObject.SetActive(false);
            }
            SyncTerrainBounds();
        }

        void SyncTerrainBounds()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return;
            var data = terrain.terrainData;
            terrainWidth = data.size.x;
            terrainLength = data.size.z;
            terrainOrigin = new Vector2(terrain.transform.position.x, terrain.transform.position.z);
        }

        void BuildWorldPathLine()
        {
            var lineGo = new GameObject("TeamRotationPath");
            worldPathLine = lineGo.AddComponent<LineRenderer>();
            worldPathLine.positionCount = 0;
            worldPathLine.loop = false;
            worldPathLine.widthMultiplier = 2f;
            worldPathLine.material = new Material(Shader.Find("Sprites/Default"));
            worldPathLine.startColor = worldPathLine.endColor = new Color(1f, 0.82f, 0.28f, 0.95f);
            worldPathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            worldPathLine.receiveShadows = false;
            worldPathLine.useWorldSpace = true;
        }

        Button BuildActionButton(string label, Vector2 anchoredPosition, float width, UnityEngine.Events.UnityAction action, bool right = false)
        {
            var canvas = minimapContainer != null
                ? minimapContainer.GetComponentInParent<Canvas>()?.transform as RectTransform
                : null;
            if (canvas == null)
            {
                var cg = new GameObject("TeamRotationUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var cv = cg.GetComponent<Canvas>();
                cv.renderMode = RenderMode.ScreenSpaceOverlay;
                cv.sortingOrder = 71;
                canvas = cg.transform as RectTransform;
            }
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(canvas, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = right ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rt.pivot = rt.anchorMin;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(width, 44f);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.09f, 0.11f, 0.12f, 0.92f);
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            return button;
        }

        public void TogglePlanningMode()
        {
            isPlanningMode = !isPlanningMode;
            if (togglePlanningButton != null)
                togglePlanningButton.GetComponentInChildren<Text>().text = isPlanningMode ? "PLAN: ON" : "PLAN ROTATION";
        }

        public void SetMarkerType(int typeIndex)
        {
            currentMarkerType = (MarkerType)Mathf.Clamp(typeIndex, 0, 3);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isPlanningMode || minimapRect == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    minimapRect, eventData.position, eventData.pressEventCamera, out var localPoint))
                return;

            var worldPosition = ConvertMinimapPointToWorld(localPoint);
            float halfW = minimapRect.rect.width * 0.5f;
            float halfH = minimapRect.rect.height * 0.5f;
            var normalized = new Vector2(
                Mathf.Clamp01((localPoint.x + halfW) / Mathf.Max(1f, minimapRect.rect.width)),
                Mathf.Clamp01((localPoint.y + halfH) / Mathf.Max(1f, minimapRect.rect.height)));

            var waypoint = new RotationWaypoint
            {
                normalizedMapPos = normalized,
                worldPosition = worldPosition,
                markerType = currentMarkerType
            };
            waypoints.Add(waypoint);
            SpawnPinUI(localPoint);
            UpdatePathLine();
            SyncWaypointWithSquad(waypoint);
        }

        public Vector3 ConvertMinimapPointToWorld(Vector2 localPoint)
        {
            if (minimapCamera != null)
            {
                if (minimapCamera.orthographic)
                {
                    float halfW = minimapRect != null ? minimapRect.rect.width * 0.5f : 1f;
                    float halfH = minimapRect != null ? minimapRect.rect.height * 0.5f : 1f;
                    float offsetX = (localPoint.x / Mathf.Max(1f, halfW)) * minimapCamera.orthographicSize;
                    float offsetZ = (localPoint.y / Mathf.Max(1f, halfH)) * minimapCamera.orthographicSize;
                    var cameraPos = minimapCamera.transform.position;
                    var world = cameraPos + minimapCamera.transform.TransformDirection(new Vector3(offsetX, 0f, offsetZ));
                    world.y = Terrain.activeTerrain != null
                        ? Terrain.activeTerrain.SampleHeight(world) + Terrain.activeTerrain.transform.position.y
                        : 0f;
                    return world;
                }
            }

            var normalized = new Vector2(
                (localPoint.x + (minimapRect != null ? minimapRect.rect.width : 0f) * 0.5f) / Mathf.Max(1f, minimapRect != null ? minimapRect.rect.width : 1f),
                (localPoint.y + (minimapRect != null ? minimapRect.rect.height : 0f) * 0.5f) / Mathf.Max(1f, minimapRect != null ? minimapRect.rect.height : 1f));
            return new Vector3(
                terrainOrigin.x + normalized.x * terrainWidth - terrainWidth * 0.5f,
                0f,
                terrainOrigin.y + normalized.y * terrainLength - terrainLength * 0.5f);
        }

        Camera eventCamera
        {
            get
            {
                var canvas = minimapRect != null ? minimapRect.GetComponentInParent<Canvas>() : null;
                return canvas != null ? canvas.worldCamera : null;
            }
        }

        void SpawnPinUI(Vector2 localPosition)
        {
            if (waypointPinPrefab == null)
            {
                if (pinsParent == null) pinsParent = minimapRect != null ? minimapRect : null;
                if (pinsParent == null) return;
                var pin = new GameObject("RotationWaypoint", typeof(RectTransform), typeof(Image));
                pin.transform.SetParent(pinsParent, false);
                var rt = pin.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = localPosition;
                rt.sizeDelta = new Vector2(28f, 28f);
                pin.GetComponent<Image>().color = MarkerColor(currentMarkerType);
                spawnedPins.Add(pin);
                return;
            }
            var prefabPin = Instantiate(waypointPinPrefab, pinsParent);
            if (prefabPin.TryGetComponent(out RectTransform pinRect))
                pinRect.anchoredPosition = localPosition;
            spawnedPins.Add(prefabPin);
        }

        static Color MarkerColor(MarkerType type)
        {
            return type switch
            {
                MarkerType.DropPoint => new Color(0.25f, 0.72f, 1f),
                MarkerType.HoldArea => new Color(0.35f, 0.95f, 0.45f),
                MarkerType.Danger => new Color(1f, 0.23f, 0.17f),
                _ => new Color(1f, 0.82f, 0.28f)
            };
        }

        void UpdatePathLine()
        {
            if (worldPathLine == null) return;
            worldPathLine.positionCount = waypoints.Count;
            for (int i = 0; i < waypoints.Count; i++)
            {
                var p = waypoints[i].worldPosition;
                p.y += 2f;
                worldPathLine.SetPosition(i, p);
            }
        }

        public void ClearWaypoints()
        {
            waypoints.Clear();
            foreach (var pin in spawnedPins)
                if (pin != null) Destroy(pin);
            spawnedPins.Clear();
            if (worldPathLine != null) worldPathLine.positionCount = 0;
        }

        public void CloseMinimap()
        {
            if (minimapContainer != null) minimapContainer.SetActive(false);
            if (openMinimapButton != null) openMinimapButton.gameObject.SetActive(true);
        }

        public void OpenMinimap()
        {
            if (minimapContainer != null) minimapContainer.gameObject.SetActive(true);
            if (openMinimapButton != null) openMinimapButton.gameObject.SetActive(false);
        }

        void SyncWaypointWithSquad(RotationWaypoint waypoint)
        {
            // Network sync hook: send (markerType, normalizedMapPos, worldPosition) to the squad server.
        }
    }
}
