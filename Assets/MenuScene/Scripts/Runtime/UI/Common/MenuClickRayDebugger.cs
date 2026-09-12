using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VRStrokeRehab.MenuScene
{
    /// <summary>
    /// Draws the exact world-space rays stored by OVRInputModule after it processes XR input.
    /// This is intended for Meta XR Simulator point-and-click diagnosis.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class MenuClickRayDebugger : MonoBehaviour
    {
        [SerializeField] private OVRInputModule inputModule;
        [SerializeField] private Transform menuRoot;
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private bool drawTrackedSourceFallback = true;
        [SerializeField] private bool logRayState = true;
        [SerializeField] private bool logEveryFrame = true;
        [SerializeField] private bool logAllRaycastResults = true;
        [SerializeField] private int maxLoggedResults = 32;
        [SerializeField] private bool drawOnlyActiveSources;
        [SerializeField] private Color rayColor = new Color(1f, 0.1f, 0.1f, 1f);
        [SerializeField] private Color hitColor = new Color(1f, 0.85f, 0.05f, 1f);
        [SerializeField] private float lineWidth = 0.012f;

        private static readonly FieldInfo PointerDataField = typeof(OVRInputModule)
            .GetField("m_VRRayPointerData", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo TrackedSourcesField = typeof(OVRInputModule)
            .GetField("_trackedInputSources", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<RayLine> lines = new List<RayLine>();
        private IDictionary pointerData;
        private IList trackedSources;
        private string lastSummary;
        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(64);
        private int frameCounter;

        private sealed class RayLine
        {
            public GameObject gameObject;
            public LineRenderer renderer;
            public Transform marker;
            public Renderer markerRenderer;
        }

        private void Awake()
        {
            if (inputModule == null)
                inputModule = GetComponent<OVRInputModule>();
            EnsureLine(0);
        }

        private void LateUpdate()
        {
            if (inputModule == null)
                inputModule = GetComponent<OVRInputModule>();
            if (inputModule == null || !inputModule.isActiveAndEnabled)
                return;

            pointerData = PointerDataField?.GetValue(inputModule) as IDictionary;
            trackedSources = TrackedSourcesField?.GetValue(inputModule) as IList;
            var lineIndex = 0;
            var summaryParts = new List<string>();
            frameCounter++;

            if (logEveryFrame)
            {
                Debug.Log("[MenuClickRay][FRAME " + frameCounter + "] eventSystem="
                    + (EventSystem.current != null ? EventSystem.current.currentInputModule?.GetType().Name : "<none>")
                    + " ovrEnabled=" + inputModule.isActiveAndEnabled
                    + " menuRoot=" + (menuRoot != null ? menuRoot.gameObject.activeInHierarchy.ToString() : "<unset>")
                    + " pointerDataCount=" + (pointerData != null ? pointerData.Count.ToString() : "<null>")
                    + " trackedSourceCount=" + (trackedSources != null ? trackedSources.Count.ToString() : "<null>"), this);
            }

            if (pointerData != null)
            {
                foreach (DictionaryEntry entry in pointerData)
                {
                    if (!(entry.Value is OVRPointerEventData data))
                        continue;
                    var ray = data.worldSpaceRay;
                    if (ray.direction.sqrMagnitude < 0.0001f)
                        continue;
                    var sourceName = "pointerId=" + entry.Key + ":" + DescribeSource(entry.Key);
                    DrawRay(lineIndex++, ray, data.pointerCurrentRaycast, sourceName, summaryParts);
                    LogPointerData(entry.Key, data, ray);
                    if (logRayState && (data.pointerPress != null || data.eligibleForClick || data.dragging))
                    {
                        summaryParts.Add("state press=" + (data.pointerPress != null ? data.pointerPress.name : "<none>")
                            + " eligible=" + data.eligibleForClick + " dragging=" + data.dragging);
                    }
                }
            }

            if (drawTrackedSourceFallback && trackedSources != null)
            {
                for (var i = 0; i < trackedSources.Count; i++)
                {
                    if (!(trackedSources[i] is OVRInputModule.InputSource source)
                        || !source.IsValid()
                        || (drawOnlyActiveSources && !source.IsActive()))
                        continue;
                    var transform = source.GetPointerRayTransform();
                    if (transform == null)
                        continue;
                    var hasData = pointerData != null && pointerData.Contains(i);
                    if (hasData)
                        continue;
                    var ray = new Ray(transform.position, transform.forward);
                    DrawRay(lineIndex++, ray, default, "sourceId=" + i + ":" + transform.name, summaryParts);
                }
            }

            for (var i = lineIndex; i < lines.Count; i++)
                SetVisible(lines[i], false);

            var summary = summaryParts.Count == 0 ? "<none>" : string.Join(" | ", summaryParts);
            if (logRayState && summary != lastSummary)
            {
                lastSummary = summary;
                Debug.Log("[MenuClickRay] " + summary, this);
            }
        }

        private void LogPointerData(object pointerId, OVRPointerEventData data, Ray ray)
        {
            if (!logEveryFrame && !logAllRaycastResults)
                return;

            raycastResults.Clear();
            var eventSystem = EventSystem.current;
            if (eventSystem != null && logAllRaycastResults)
                eventSystem.RaycastAll(data, raycastResults);

            var max = Mathf.Min(maxLoggedResults, raycastResults.Count);
            var results = new List<string>(max);
            var menuResultCount = 0;
            for (var i = 0; i < max; i++)
            {
                var result = raycastResults[i];
                var objectName = result.gameObject != null ? result.gameObject.name : "<none>";
                var path = result.gameObject != null ? GetPath(result.gameObject.transform) : "<none>";
                var underMenu = menuRoot != null && result.gameObject != null
                    && result.gameObject.transform.IsChildOf(menuRoot);
                if (underMenu)
                    menuResultCount++;
                var selectable = result.gameObject != null
                    ? result.gameObject.GetComponentInParent<Selectable>(true) : null;
                var button = result.gameObject != null
                    ? result.gameObject.GetComponentInParent<Button>(true) : null;
                results.Add(i + ":" + objectName + " path=" + path + " underMenu=" + underMenu
                    + " module=" + (result.module != null ? result.module.GetType().Name : "<none>")
                    + " dist=" + result.distance.ToString("F3")
                    + " world=" + Format(result.worldPosition)
                    + " selectable=" + (selectable != null ? selectable.GetType().Name + ":" + selectable.interactable : "<none>")
                    + " button=" + (button != null ? button.interactable.ToString() : "<none>"));
            }

            var omitted = raycastResults.Count - max;
            Debug.Log("[MenuClickRay][RAYCAST pointerId=" + pointerId + "] rayOrigin=" + Format(ray.origin)
                + " rayDirection=" + Format(ray.direction)
                + " storedHit=" + DescribeHit(data.pointerCurrentRaycast)
                + " pointerEnter=" + (data.pointerEnter != null ? GetPath(data.pointerEnter.transform) : "<none>")
                + " pointerPress=" + (data.pointerPress != null ? GetPath(data.pointerPress.transform) : "<none>")
                + " eligible=" + data.eligibleForClick + " dragging=" + data.dragging
                + " allResults=" + raycastResults.Count + " menuResults=" + menuResultCount
                + (omitted > 0 ? " omitted=" + omitted : string.Empty)
                + (results.Count > 0 ? "\n  " + string.Join("\n  ", results) : "\n  <no raycast results>"), this);
        }

        private string DescribeHit(RaycastResult result)
        {
            if (!result.isValid || result.gameObject == null)
                return "<none>";
            var underMenu = menuRoot != null && result.gameObject.transform.IsChildOf(menuRoot);
            return GetPath(result.gameObject.transform) + " underMenu=" + underMenu
                + " world=" + Format(result.worldPosition);
        }

        private string GetPath(Transform target)
        {
            if (target == null)
                return "<none>";
            var names = new List<string>();
            var current = target;
            while (current != null)
            {
                names.Add(current.name);
                if (menuRoot != null && current == menuRoot)
                    break;
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private void DrawRay(int index, Ray ray, RaycastResult hit, string sourceName, List<string> summaryParts)
        {
            EnsureLine(index);
            var hasHit = hit.isValid && hit.worldPosition != Vector3.zero;
            var end = hasHit ? hit.worldPosition : ray.origin + ray.direction.normalized * maxDistance;
            var line = lines[index];
            line.renderer.SetPosition(0, ray.origin);
            line.renderer.SetPosition(1, end);
            line.renderer.startColor = rayColor;
            line.renderer.endColor = hasHit ? hitColor : rayColor;
            SetVisible(line, true);

            if (line.marker != null)
            {
                line.marker.position = end;
                line.marker.gameObject.SetActive(hasHit);
            }

            var target = hasHit && hit.gameObject != null ? hit.gameObject.name : "<none>";
            summaryParts.Add(sourceName + " origin=" + Format(ray.origin) + " dir=" + Format(ray.direction)
                + " target=" + target + " mode=" + (inputModule != null && inputModule.IsEditorMouseActive ? "editor" : "xr"));
        }

        private string DescribeSource(object id)
        {
            if (!(id is int index) || trackedSources == null || index < 0 || index >= trackedSources.Count)
                return "<unmapped>";
            var source = trackedSources[index] as OVRInputModule.InputSource;
            if (source == null)
                return "<invalid>";
            var transform = source.GetPointerRayTransform();
            return source.GetType().Name + ":" + (transform != null ? transform.name : "<no-transform>");
        }

        private void EnsureLine(int index)
        {
            while (lines.Count <= index)
            {
                var go = new GameObject("MenuClickRayDebug_" + lines.Count);
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<LineRenderer>();
                renderer.useWorldSpace = true;
                renderer.positionCount = 2;
                renderer.startWidth = lineWidth;
                renderer.endWidth = lineWidth * 0.65f;
                renderer.numCapVertices = 6;
                renderer.material = CreateMaterial();
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                marker.name = go.name + "_Hit";
                marker.SetParent(transform, false);
                var collider = marker.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);
                var markerRenderer = marker.GetComponent<Renderer>();
                markerRenderer.material = CreateMaterial();
                markerRenderer.material.color = hitColor;
                lines.Add(new RayLine { gameObject = go, renderer = renderer, marker = marker, markerRenderer = markerRenderer });
                SetVisible(lines[lines.Count - 1], false);
            }
        }

        private Material CreateMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
            return new Material(shader) { color = rayColor };
        }

        private static void SetVisible(RayLine line, bool visible)
        {
            line.gameObject.SetActive(visible);
            if (line.marker != null)
                line.marker.gameObject.SetActive(visible && line.marker.gameObject.activeSelf);
        }

        private static string Format(Vector3 value)
        {
            return "(" + value.x.ToString("F3") + "," + value.y.ToString("F3") + "," + value.z.ToString("F3") + ")";
        }
    }
}
