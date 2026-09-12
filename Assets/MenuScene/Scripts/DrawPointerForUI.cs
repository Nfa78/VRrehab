using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class DrawPointerForUI : MonoBehaviour
{
    private enum RayVisualizationMode
    {
        AuthoritativeInputModule,
        ResolvedPointerSource,
    }

    private enum InputRayKind
    {
        None,
        CanvasPointer,
        TrackedInput,
        FallbackRayTransform,
        ResolvedPointerSource,
    }

    private struct InputRayState
    {
        public InputRayKind Kind;
        public Transform Source;
        public Ray Ray;
        public bool HasHit;
        public bool HasPointerData;
        public bool IsInteractionActive;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public string SourceName;
        public string TargetName;
    }

    private const int MouseLeftPointerId = -1;
    private static readonly FieldInfo VrRayPointerDataField = typeof(OVRInputModule).GetField("m_VRRayPointerData", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo PointerDataField = typeof(PointerInputModule).GetField("m_PointerData", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo TrackedInputSourcesField = typeof(OVRInputModule).GetField("_trackedInputSources", BindingFlags.Instance | BindingFlags.NonPublic);

    [Header("Source")]
    [SerializeField] private Transform sourceOverride;
    [SerializeField] private bool preferLeftHand;
    [SerializeField] private bool syncEventSystemRay = true;
    [SerializeField] private RayVisualizationMode rayVisualizationMode = RayVisualizationMode.AuthoritativeInputModule;
    [SerializeField] private bool logAuthoritativeRayChanges = true;

    [Header("Ray")]
    [SerializeField] private float maxDistance = 6f;
    [SerializeField] private bool usePhysicsFallback = true;
    [SerializeField] private LayerMask physicsMask = ~0;
    [SerializeField] private float surfaceOffset = 0.003f;

    [Header("Smoothing")]
    [SerializeField] private float followSharpness = 18f;
    [SerializeField] private int lineSegments = 12;

    [Header("Visuals")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float startWidth = 0.008f;
    [SerializeField] private float endWidth = 0.0025f;
    [SerializeField] private Color idleColor = new Color(0.18f, 0.8f, 1f, 0.85f);
    [SerializeField] private Color hitColor = Color.white;
    [SerializeField] private bool createHitMarkerIfMissing = true;
    [SerializeField] private Transform hitMarker;
    [SerializeField] private float hitMarkerScale = 0.015f;
    [SerializeField] private Color hitMarkerColor = Color.white;

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);
    private Vector3[] linePoints;
    private OVRPointerEventData pointerEventData;
    private EventSystem pointerEventOwner;
    private OVRInputModule inputModule;
    private OVRGazePointer gazePointer;
    private OVRCameraRig cameraRig;
    private OVRHand[] hands;
    private OVRControllerHelper[] controllerHelpers;
    private Camera fallbackCamera;
    private Renderer hitMarkerRenderer;
    private Transform activeSource;
    private Vector3 smoothedStart;
    private Vector3 smoothedEnd;
    private Vector3 smoothedNormal = Vector3.forward;
    private bool hasSmoothedPose;
    private string lastRaySummary;

    public Transform CurrentSource => activeSource;
    public bool SyncsUiRay => syncEventSystemRay && rayVisualizationMode == RayVisualizationMode.ResolvedPointerSource;

    private void Reset()
    {
        lineRenderer = GetComponent<LineRenderer>();
        ApplyLineRendererSettings();
    }

    private void Awake()
    {
        CacheSceneReferences();
        ApplyLineRendererSettings();
    }

    private void OnEnable()
    {
        CacheSceneReferences();
        ApplyLineRendererSettings();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            SetLineVisible(false);
            SetHitMarkerVisible(false);
        }

        activeSource = null;
        hasSmoothedPose = false;
    }

    private void OnValidate()
    {
        maxDistance = Mathf.Max(0.25f, maxDistance);
        followSharpness = Mathf.Max(1f, followSharpness);
        lineSegments = Mathf.Max(2, lineSegments);
        startWidth = Mathf.Max(0.0005f, startWidth);
        endWidth = Mathf.Max(0.0005f, endWidth);
        hitMarkerScale = Mathf.Max(0.002f, hitMarkerScale);
        surfaceOffset = Mathf.Max(0f, surfaceOffset);

        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer != null)
        {
            ApplyLineRendererSettings();
        }
    }

    private void LateUpdate()
    {
        CacheSceneReferences();
        ApplyLineRendererSettings();

        if (!TryResolveVisualizationState(out var rayState))
        {
            SetLineVisible(false);
            SetHitMarkerVisible(false);
            activeSource = null;
            hasSmoothedPose = false;
            return;
        }

        if (rayVisualizationMode == RayVisualizationMode.ResolvedPointerSource
            && syncEventSystemRay
            && rayState.Source != null)
        {
            SyncUiRay(rayState.Source);
        }

        if (rayState.Kind != InputRayKind.ResolvedPointerSource)
        {
            MaybeLogAuthoritativeRay(rayState);
        }

        var source = rayState.Source != null ? rayState.Source : transform;
        var ray = rayState.Ray;
        var hasHit = rayState.HasHit;
        var targetEnd = hasHit ? rayState.HitPoint : ray.origin + ray.direction * maxDistance;
        var targetNormal = hasHit ? rayState.HitNormal : -ray.direction;

        if (rayVisualizationMode == RayVisualizationMode.AuthoritativeInputModule
            || activeSource != source || !hasSmoothedPose)
        {
            activeSource = source;
            smoothedStart = ray.origin;
            smoothedEnd = targetEnd;
            smoothedNormal = targetNormal;
            hasSmoothedPose = true;
        }
        else
        {
            var lerpFactor = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            smoothedStart = Vector3.Lerp(smoothedStart, ray.origin, lerpFactor);
            smoothedEnd = Vector3.Lerp(smoothedEnd, targetEnd, lerpFactor);
            smoothedNormal = Vector3.Slerp(smoothedNormal, targetNormal, lerpFactor).normalized;
        }

        UpdateLaser(hasHit);
        UpdateHitMarker(hasHit);
    }

    private void CacheSceneReferences()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        inputModule = EventSystem.current != null
            ? EventSystem.current.currentInputModule as OVRInputModule : null;
        gazePointer = inputModule != null ? inputModule.m_Cursor as OVRGazePointer : null;

        if (cameraRig == null)
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
        }

        if (hands == null || hands.Length == 0 || AreHandsMissing())
        {
            hands = FindObjectsByType<OVRHand>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        if (controllerHelpers == null || controllerHelpers.Length == 0 || AreControllerHelpersMissing())
        {
            controllerHelpers = FindObjectsByType<OVRControllerHelper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        if (fallbackCamera == null)
        {
            fallbackCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
        }

        if (hitMarker != null && hitMarkerRenderer == null)
        {
            hitMarkerRenderer = hitMarker.GetComponentInChildren<Renderer>(true);
        }
    }

    private bool AreHandsMissing()
    {
        for (var i = 0; i < hands.Length; i++)
        {
            if (hands[i] != null)
            {
                return false;
            }
        }

        return true;
    }

    private bool AreControllerHelpersMissing()
    {
        for (var i = 0; i < controllerHelpers.Length; i++)
        {
            if (controllerHelpers[i] != null)
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyLineRendererSettings()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.positionCount = Mathf.Max(2, lineSegments);
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.numCapVertices = 8;
        lineRenderer.numCornerVertices = 8;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.startColor = idleColor;
        lineRenderer.endColor = new Color(idleColor.r, idleColor.g, idleColor.b, 0.2f);
    }

    private Transform ResolveSource()
    {
        if (TryResolveTrackedSource(out var trackedSource))
        {
            return trackedSource;
        }

        if (TryResolveConfiguredSource(false, out var configuredSource))
        {
            return configuredSource;
        }

        if (fallbackCamera == null)
        {
            fallbackCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
        }

        if (fallbackCamera != null)
        {
            return fallbackCamera.transform;
        }

        if (TryResolveControllerSource(out var controllerSource))
        {
            return controllerSource;
        }

        if (TryResolveConfiguredSource(true, out configuredSource))
        {
            return configuredSource;
        }

        return transform;
    }

    private bool TryResolveVisualizationState(out InputRayState rayState)
    {
        if (rayVisualizationMode == RayVisualizationMode.AuthoritativeInputModule)
        {
            // An inactive module or a guessed pose cannot describe the actual click.
            return TryResolveAuthoritativeInputModuleRay(out rayState);
        }

        if (TryResolveResolvedPointerSourceRay(out rayState))
        {
            return true;
        }

        rayState = default;
        return false;
    }

    private bool TryResolveAuthoritativeInputModuleRay(out InputRayState rayState)
    {
        rayState = default;
        if (inputModule == null || !inputModule.isActiveAndEnabled)
        {
            return false;
        }

        if (inputModule.IsEditorMouseActive)
        {
            if (!TryGetVrPointerEventData(MouseLeftPointerId, out var mouseData))
                return false;

            var camera = inputModule.editorMouseCamera;
            return TryBuildRayState(InputRayKind.CanvasPointer, camera.transform,
                "EditorMouse:" + camera.name, mouseData.worldSpaceRay, mouseData, out rayState);
        }

        var hasCanvasPointer = TryGetCanvasPointerRayState(out var canvasPointerState);
        if (hasCanvasPointer && canvasPointerState.IsInteractionActive)
        {
            rayState = canvasPointerState;
            return true;
        }

        if (TryGetTrackedInputRayState(out var trackedInputState))
        {
            rayState = trackedInputState;
            return true;
        }

        if (TryGetFallbackRayTransformState(out var fallbackRayState))
        {
            rayState = fallbackRayState;
            return true;
        }

        if (hasCanvasPointer)
        {
            rayState = canvasPointerState;
            return true;
        }

        return false;
    }

    private bool TryResolveResolvedPointerSourceRay(out InputRayState rayState)
    {
        var source = ResolveSource();
        if (source == null)
        {
            rayState = default;
            return false;
        }

        var ray = new Ray(source.position, source.forward);
        var hasHit = TryResolveHit(ray, out var hitPoint, out var hitNormal);
        rayState = new InputRayState
        {
            Kind = InputRayKind.ResolvedPointerSource,
            Source = source,
            Ray = ray,
            HasHit = hasHit,
            HitPoint = hasHit ? hitPoint : ray.origin + ray.direction * maxDistance,
            HitNormal = hasHit ? hitNormal : -ray.direction,
            SourceName = source.name,
            TargetName = hasHit ? "ResolvedHit" : string.Empty,
        };
        return true;
    }

    private bool TryGetTrackedInputRayState(out InputRayState rayState)
    {
        rayState = default;
        if (!TryGetTrackedInputSources(out var trackedInputSources))
        {
            return false;
        }

        var fallbackState = default(InputRayState);
        var hasFallbackState = false;

        for (var i = trackedInputSources.Count - 1; i >= 0; i--)
        {
            if (!(trackedInputSources[i] is OVRInputModule.InputSource inputSource)
                || !inputSource.IsValid()
                || !inputSource.IsActive())
            {
                continue;
            }

            var source = inputSource.GetPointerRayTransform();
            if (source == null)
            {
                continue;
            }

            var ray = new Ray(source.position, source.forward);
            var sourceName = inputSource.GetType().Name + ":" + source.name;
            if (TryGetVrPointerEventData(i, out var pointerData))
            {
                if (TryBuildRayState(InputRayKind.TrackedInput, source, sourceName, ray, pointerData, out var trackedState))
                {
                    if (trackedState.IsInteractionActive)
                    {
                        rayState = trackedState;
                        return true;
                    }

                    if (!hasFallbackState || trackedState.HasHit)
                    {
                        fallbackState = trackedState;
                        hasFallbackState = true;
                    }
                }

                continue;
            }

            var hasHit = TryResolveHit(ray, out var hitPoint, out var hitNormal);
            var candidateState = new InputRayState
            {
                Kind = InputRayKind.TrackedInput,
                Source = source,
                Ray = ray,
                HasHit = hasHit,
                HitPoint = hasHit ? hitPoint : ray.origin + ray.direction * maxDistance,
                HitNormal = hasHit ? hitNormal : -ray.direction,
                SourceName = sourceName,
            };

            if (!hasFallbackState || candidateState.HasHit)
            {
                fallbackState = candidateState;
                hasFallbackState = true;
            }
        }

        if (hasFallbackState)
        {
            rayState = fallbackState;
            return true;
        }

        return false;
    }

    private bool TryGetFallbackRayTransformState(out InputRayState rayState)
    {
        rayState = default;
        if (inputModule == null || inputModule.rayTransform == null)
        {
            return false;
        }

        var source = inputModule.rayTransform;
        var ray = new Ray(source.position, source.forward);
        if (TryGetVrPointerEventData(MouseLeftPointerId, out var pointerData)
            && TryBuildRayState(InputRayKind.FallbackRayTransform, source, source.name, ray, pointerData, out rayState))
        {
            return true;
        }

        var hasHit = TryResolveHit(ray, out var hitPoint, out var hitNormal);
        rayState = new InputRayState
        {
            Kind = InputRayKind.FallbackRayTransform,
            Source = source,
            Ray = ray,
            HasHit = hasHit,
            HitPoint = hasHit ? hitPoint : ray.origin + ray.direction * maxDistance,
            HitNormal = hasHit ? hitNormal : -ray.direction,
            SourceName = source.name,
        };
        return true;
    }

    private bool TryGetCanvasPointerRayState(out InputRayState rayState)
    {
        rayState = default;
        if (inputModule == null
            || inputModule.activeGraphicRaycaster == null
            || inputModule.activeGraphicRaycaster.pointer == null
            || inputModule.activeGraphicRaycaster.eventCamera == null)
        {
            return false;
        }

        var source = inputModule.activeGraphicRaycaster.eventCamera.transform;
        var direction = inputModule.activeGraphicRaycaster.pointer.transform.position - source.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        var ray = new Ray(source.position, direction.normalized);
        var sourceName = "CanvasPointer:" + inputModule.activeGraphicRaycaster.pointer.name;
        if (TryGetCanvasPointerEventData(MouseLeftPointerId, out var pointerData)
            && TryBuildRayState(InputRayKind.CanvasPointer, source, sourceName, ray, pointerData, out rayState))
        {
            rayState.IsInteractionActive |= HasMouseButtonActivity();
            return true;
        }

        var hasHit = TryResolveHit(ray, out var hitPoint, out var hitNormal);
        rayState = new InputRayState
        {
            Kind = InputRayKind.CanvasPointer,
            Source = source,
            Ray = ray,
            HasHit = hasHit,
            HasPointerData = false,
            IsInteractionActive = HasMouseButtonActivity(),
            HitPoint = hasHit ? hitPoint : ray.origin + ray.direction * maxDistance,
            HitNormal = hasHit ? hitNormal : -ray.direction,
            SourceName = sourceName,
        };
        return true;
    }

    private bool TryBuildRayState(InputRayKind rayKind, Transform source, string sourceName, Ray ray, PointerEventData pointerData, out InputRayState rayState)
    {
        if (pointerData is OVRPointerEventData vrPointerData)
            ray = vrPointerData.worldSpaceRay;

        var hasHit = TryExtractPointerEventHit(ray, pointerData, out var hitPoint, out var hitNormal, out var targetName);
        rayState = new InputRayState
        {
            Kind = rayKind,
            Source = source,
            Ray = ray,
            HasHit = hasHit,
            HasPointerData = pointerData != null,
            IsInteractionActive = pointerData != null
                && (pointerData.pointerPress != null || pointerData.dragging || pointerData.eligibleForClick),
            HitPoint = hasHit ? hitPoint : ray.origin + ray.direction * maxDistance,
            HitNormal = hasHit ? hitNormal : -ray.direction,
            SourceName = sourceName,
            TargetName = hasHit ? targetName : string.Empty,
        };
        return true;
    }

    private bool TryExtractPointerEventHit(Ray ray, PointerEventData pointerData, out Vector3 hitPoint, out Vector3 hitNormal, out string targetName)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.forward;
        targetName = string.Empty;

        if (pointerData == null)
        {
            return false;
        }

        var hitResult = pointerData.pointerCurrentRaycast;
        if ((!hitResult.isValid || hitResult.gameObject == null)
            && pointerData.pointerPress != null
            && pointerData.pointerPressRaycast.isValid)
        {
            hitResult = pointerData.pointerPressRaycast;
        }

        if (!hitResult.isValid || hitResult.gameObject == null || IsInternalVisual(hitResult.gameObject.transform))
        {
            return false;
        }

        targetName = hitResult.gameObject.name;
        return TryExtractHitPose(ray, hitResult, out hitPoint, out hitNormal);
    }

    private bool TryGetTrackedInputSources(out IList trackedInputSources)
    {
        trackedInputSources = null;
        if (inputModule == null || TrackedInputSourcesField == null)
        {
            return false;
        }

        trackedInputSources = TrackedInputSourcesField.GetValue(inputModule) as IList;
        return trackedInputSources != null;
    }

    private bool TryGetVrPointerEventData(int pointerId, out OVRPointerEventData pointerData)
    {
        pointerData = null;
        if (inputModule == null || VrRayPointerDataField == null)
        {
            return false;
        }

        if (!(VrRayPointerDataField.GetValue(inputModule) is IDictionary pointerDictionary)
            || !pointerDictionary.Contains(pointerId))
        {
            return false;
        }

        pointerData = pointerDictionary[pointerId] as OVRPointerEventData;
        return pointerData != null;
    }

    private bool TryGetCanvasPointerEventData(int pointerId, out PointerEventData pointerData)
    {
        pointerData = null;
        if (inputModule == null || PointerDataField == null)
        {
            return false;
        }

        if (!(PointerDataField.GetValue(inputModule) is IDictionary pointerDictionary)
            || !pointerDictionary.Contains(pointerId))
        {
            return false;
        }

        pointerData = pointerDictionary[pointerId] as PointerEventData;
        return pointerData != null;
    }

    private bool HasMouseButtonActivity()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        return mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame
            || mouse.rightButton.isPressed || mouse.rightButton.wasReleasedThisFrame
            || mouse.middleButton.isPressed || mouse.middleButton.wasReleasedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(0)
            || Input.GetMouseButtonDown(0)
            || Input.GetMouseButtonUp(0)
            || Input.GetMouseButton(1)
            || Input.GetMouseButtonDown(1)
            || Input.GetMouseButtonUp(1)
            || Input.GetMouseButton(2)
            || Input.GetMouseButtonDown(2)
            || Input.GetMouseButtonUp(2);
#else
        return false;
#endif
    }

    private void MaybeLogAuthoritativeRay(InputRayState rayState)
    {
        if (!logAuthoritativeRayChanges)
        {
            return;
        }

        var summary = rayState.Kind + " | source=" + rayState.SourceName + " | target=" + (string.IsNullOrEmpty(rayState.TargetName) ? "<none>" : rayState.TargetName);
        if (summary == lastRaySummary)
        {
            return;
        }

        lastRaySummary = summary;
        Debug.Log("DrawPointerForUI authoritative ray: " + summary, this);
    }

    private bool TryResolveConfiguredSource(bool allowRawRigAnchors, out Transform source)
    {
        if (TryGetConfiguredSource(sourceOverride, allowRawRigAnchors, out source))
        {
            return true;
        }

        if (gazePointer != null && TryGetConfiguredSource(gazePointer.rayTransform, allowRawRigAnchors, out source))
        {
            return true;
        }

        if (inputModule != null && TryGetConfiguredSource(inputModule.rayTransform, allowRawRigAnchors, out source))
        {
            return true;
        }

        source = null;
        return false;
    }

    private bool TryGetConfiguredSource(Transform candidate, bool allowRawRigAnchors, out Transform source)
    {
        source = null;
        if (candidate == null)
        {
            return false;
        }

        if (!allowRawRigAnchors && IsRawRigAnchor(candidate))
        {
            return false;
        }

        source = candidate;
        return true;
    }

    private bool TryResolveTrackedSource(out Transform source)
    {
        var primaryHand = preferLeftHand ? OVRInput.Handedness.LeftHanded : OVRInput.Handedness.RightHanded;
        if (TryResolveTrackedSource(primaryHand, out source))
        {
            return true;
        }

        var secondaryHand = preferLeftHand ? OVRInput.Handedness.RightHanded : OVRInput.Handedness.LeftHanded;
        return TryResolveTrackedSource(secondaryHand, out source);
    }

    private bool TryResolveTrackedSource(OVRInput.Handedness handedness, out Transform source)
    {
        if (handedness == OVRInput.Handedness.LeftHanded)
        {
            return TryGetHandPointer(OVRPlugin.Hand.HandLeft, out source);
        }

        return TryGetHandPointer(OVRPlugin.Hand.HandRight, out source);
    }

    private bool TryGetHandPointer(OVRPlugin.Hand handType, out Transform source)
    {
        source = null;

        if (hands == null)
        {
            return false;
        }

        for (var i = 0; i < hands.Length; i++)
        {
            var hand = hands[i];
            if (hand == null || hand.GetHand() != handType)
            {
                continue;
            }

            if (!hand.IsActive() || !hand.IsPointerPoseValid)
            {
                continue;
            }

            source = hand.PointerPose;
            return source != null;
        }

        return false;
    }

    private bool TryResolveControllerSource(out Transform source)
    {
        var primaryHand = preferLeftHand ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
        if (TryGetControllerHelper(primaryHand, out source))
        {
            return true;
        }

        var secondaryHand = preferLeftHand ? OVRPlugin.Hand.HandRight : OVRPlugin.Hand.HandLeft;
        if (TryGetControllerHelper(secondaryHand, out source))
        {
            return true;
        }

        if (TryGetRigControllerAnchor(preferLeftHand, out source))
        {
            return true;
        }

        return TryGetRigControllerAnchor(!preferLeftHand, out source);
    }

    private bool TryGetControllerHelper(OVRPlugin.Hand hand, out Transform source)
    {
        source = null;

        if (controllerHelpers == null)
        {
            return false;
        }

        for (var i = 0; i < controllerHelpers.Length; i++)
        {
            var helper = controllerHelpers[i];
            if (helper == null || !helper.IsActive() || helper.GetHand() != hand)
            {
                continue;
            }

            source = helper.GetPointerRayTransform();
            return source != null;
        }

        return false;
    }

    private bool TryGetRigHandAnchor(bool leftHand, out Transform source)
    {
        source = null;
        if (cameraRig == null)
        {
            return false;
        }

        var controller = leftHand ? OVRInput.Controller.LHand : OVRInput.Controller.RHand;
        if (!OVRInput.GetControllerPositionValid(controller))
        {
            return false;
        }

        source = leftHand ? cameraRig.leftHandAnchor : cameraRig.rightHandAnchor;
        return source != null;
    }

    private bool TryGetRigControllerAnchor(bool leftController, out Transform source)
    {
        source = null;
        if (cameraRig == null)
        {
            return false;
        }

        var controller = leftController ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        if (!OVRInput.GetControllerPositionValid(controller))
        {
            return false;
        }

        source = leftController ? cameraRig.leftControllerAnchor : cameraRig.rightControllerAnchor;
        return source != null;
    }

    private bool IsRawRigAnchor(Transform candidate)
    {
        if (candidate == null || cameraRig == null)
        {
            return false;
        }

        return candidate == cameraRig.leftHandAnchor
            || candidate == cameraRig.rightHandAnchor
            || candidate == cameraRig.leftControllerAnchor
            || candidate == cameraRig.rightControllerAnchor;
    }

    private void SyncUiRay(Transform source)
    {
        if (inputModule != null && inputModule.rayTransform != source)
        {
            inputModule.rayTransform = source;
        }

        if (gazePointer != null && gazePointer.rayTransform != source)
        {
            gazePointer.rayTransform = source;
        }
    }

    private bool TryResolveHit(Ray ray, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        if (TryResolveUiHit(ray, out hitPoint, out hitNormal))
        {
            return true;
        }

        if (usePhysicsFallback && TryResolvePhysicsHit(ray, out hitPoint, out hitNormal))
        {
            return true;
        }

        hitPoint = ray.origin + ray.direction * maxDistance;
        hitNormal = -ray.direction;
        return false;
    }

    private bool TryResolveUiHit(Ray ray, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.forward;

        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        if (pointerEventData == null || pointerEventOwner != eventSystem)
        {
            pointerEventData = new OVRPointerEventData(eventSystem);
            pointerEventOwner = eventSystem;
        }

        pointerEventData.Reset();
        pointerEventData.button = PointerEventData.InputButton.Left;
        pointerEventData.position = Vector2.zero;
        pointerEventData.scrollDelta = Vector2.zero;
        pointerEventData.worldSpaceRay = ray;

        raycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, raycastResults);

        for (var i = 0; i < raycastResults.Count; i++)
        {
            var result = raycastResults[i];
            if (result.gameObject == null || IsInternalVisual(result.gameObject.transform))
            {
                continue;
            }

            if (TryExtractHitPose(ray, result, out hitPoint, out hitNormal))
            {
                raycastResults.Clear();
                return true;
            }
        }

        raycastResults.Clear();
        return false;
    }

    private bool TryResolvePhysicsHit(Ray ray, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.forward;

        var hits = Physics.RaycastAll(ray, maxDistance, physicsMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (var i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null || IsInternalVisual(hits[i].collider.transform))
            {
                continue;
            }

            hitPoint = hits[i].point;
            hitNormal = FaceBackTowardSource(ray.direction, hits[i].normal);
            return true;
        }

        return false;
    }

    private bool TryExtractHitPose(Ray ray, RaycastResult result, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.forward;

        if (result.gameObject.TryGetComponent<RectTransform>(out var rectTransform))
        {
            hitPoint = result.worldPosition;
            hitNormal = GetRectTransformNormal(rectTransform);

            var overlayCanvas = rectTransform.GetComponentInParent<OVROverlayCanvas>();
            if (overlayCanvas != null)
            {
                overlayCanvas.GetWorldIntersectionFromCanvas(result.worldPosition, out hitPoint, out hitNormal);
            }

            hitNormal = FaceBackTowardSource(ray.direction, hitNormal);
            return true;
        }

        if (result.distance <= 0f && result.worldPosition == Vector3.zero)
        {
            return false;
        }

        hitPoint = result.worldPosition != Vector3.zero ? result.worldPosition : ray.GetPoint(result.distance);
        hitNormal = result.worldNormal.sqrMagnitude > 0.0001f
            ? FaceBackTowardSource(ray.direction, result.worldNormal)
            : -ray.direction;
        return true;
    }

    private void UpdateLaser(bool hasHit)
    {
        if (lineRenderer == null)
        {
            return;
        }

        SetLineVisible(true);

        var pointCount = Mathf.Max(2, lineSegments);
        if (linePoints == null || linePoints.Length != pointCount)
        {
            linePoints = new Vector3[pointCount];
        }

        lineRenderer.positionCount = pointCount;
        for (var i = 0; i < pointCount; i++)
        {
            var t = pointCount == 1 ? 0f : (float)i / (pointCount - 1);
            linePoints[i] = Vector3.Lerp(smoothedStart, smoothedEnd, t);
        }

        lineRenderer.SetPositions(linePoints);
        lineRenderer.startColor = hasHit ? hitColor : idleColor;
        lineRenderer.endColor = hasHit
            ? hitColor
            : new Color(idleColor.r, idleColor.g, idleColor.b, 0.2f);
    }

    private void UpdateHitMarker(bool hasHit)
    {
        if (gazePointer != null && gazePointer.isActiveAndEnabled && !gazePointer.hidden)
        {
            SetHitMarkerVisible(false);
            return;
        }

        if (!hasHit)
        {
            SetHitMarkerVisible(false);
            return;
        }

        if (hitMarker == null)
        {
            if (!createHitMarkerIfMissing || !Application.isPlaying)
            {
                return;
            }

            CreateRuntimeHitMarker();
        }

        if (hitMarker == null)
        {
            return;
        }

        hitMarker.position = smoothedEnd + smoothedNormal * surfaceOffset;
        hitMarker.rotation = Quaternion.LookRotation(smoothedNormal, activeSource != null ? activeSource.up : Vector3.up);
        hitMarker.localScale = Vector3.one * hitMarkerScale;

        if (hitMarkerRenderer != null)
        {
            hitMarkerRenderer.material.color = hitMarkerColor;
        }

        SetHitMarkerVisible(true);
    }

    private void CreateRuntimeHitMarker()
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "UIRayHitMarker";
        marker.transform.SetParent(transform, false);

        var collider = marker.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        hitMarker = marker.transform;
        hitMarker.TryGetComponent(out hitMarkerRenderer);
        if (hitMarkerRenderer != null)
        {
            hitMarkerRenderer.material.color = hitMarkerColor;
        }

        SetHitMarkerVisible(false);
    }

    private void SetLineVisible(bool isVisible)
    {
        if (lineRenderer != null && lineRenderer.enabled != isVisible)
        {
            lineRenderer.enabled = isVisible;
        }
    }

    private void SetHitMarkerVisible(bool isVisible)
    {
        if (hitMarker != null)
        {
            hitMarker.gameObject.SetActive(isVisible);
        }
    }

    private bool IsInternalVisual(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        return hitMarker != null && (target == hitMarker || target.IsChildOf(hitMarker));
    }

    private static Vector3 GetRectTransformNormal(RectTransform rectTransform)
    {
        var corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        var bottomEdge = corners[3] - corners[0];
        var leftEdge = corners[1] - corners[0];
        return Vector3.Cross(bottomEdge, leftEdge).normalized;
    }

    private static Vector3 FaceBackTowardSource(Vector3 rayDirection, Vector3 normal)
    {
        if (normal.sqrMagnitude <= 0.0001f)
        {
            return -rayDirection.normalized;
        }

        normal.Normalize();
        return Vector3.Dot(normal, rayDirection) > 0f ? -normal : normal;
    }
}
