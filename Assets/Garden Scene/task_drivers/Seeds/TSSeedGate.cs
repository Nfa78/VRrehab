using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Distance-based seed gate that treats a seed as inside when its center fits within the gate radius.
/// </summary>
[DisallowMultipleComponent]
public class TSSeedGate : MonoBehaviour
{
    [Header("Gate")]
    [SerializeField] private string gateId = "Gate";
    [SerializeField] private Transform gateCenter;
    [SerializeField] private Transform radiusSource;
    [SerializeField] private bool useExplicitRadius;
    [SerializeField][Min(0.01f)] private float explicitRadius = 0.2f;
    [SerializeField][Min(0.01f)] private float minimumRadius = 0.02f;
    [SerializeField][Min(0.01f)] private float radiusScaleMultiplier = 0.5f;
    [SerializeField][Min(0f)] private float seedRadiusPadding;

    [Header("Feedback")]
    [SerializeField] private List<Renderer> flashRenderers = new List<Renderer>();
    [SerializeField] private bool autoFindFlashRenderers = true;
    [SerializeField] private Color seedEnterFlashColor = new Color(0.2f, 1f, 0.35f, 1f);
    [SerializeField][Min(0.01f)] private float seedEnterFlashDurationSeconds = 0.12f;
    [SerializeField] private bool logDebug;

    [Header("Runtime")]
    [SerializeField] private int seedEntryCount;

    private readonly HashSet<int> seedsInside = new HashSet<int>();
    private readonly List<MaterialColorState> materialColorStates = new List<MaterialColorState>();
    private Coroutine flashRoutine;

    public event Action<TSSeedGate, SeedProjectileMarker> SeedPassed;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Transform GateCenter => gateCenter != null ? gateCenter : transform;
    private Transform RadiusSource => radiusSource != null ? radiusSource : transform;

    private void Reset()
    {
        CacheFlashRenderers();
    }

    private void Awake()
    {
        CacheFlashRenderers();
        CacheMaterialColors();
    }

    private void OnDisable()
    {
        seedsInside.Clear();
        StopFlashRoutine();
        RestoreBaseColors();
    }

    private void Update()
    {
        IReadOnlyList<SeedProjectileMarker> markers = SeedProjectileMarker.ActiveMarkers;
        if (markers.Count == 0)
        {
            seedsInside.Clear();
            return;
        }

        HashSet<int> stillInside = new HashSet<int>();
        for (int i = 0; i < markers.Count; i++)
        {
            SeedProjectileMarker marker = markers[i];
            if (marker == null || !marker.isActiveAndEnabled)
            {
                continue;
            }

            int markerId = marker.GetInstanceID();
            if (!IsSeedInsideGate(marker, out float centerDistance, out float gateRadius, out float seedRadius))
            {
                continue;
            }

            stillInside.Add(markerId);
            if (seedsInside.Contains(markerId))
            {
                continue;
            }

            seedsInside.Add(markerId);
            seedEntryCount++;
            StartFlash();

            if (logDebug)
            {
                Debug.Log(
                    $"[TSSeedGate] {gateId} accepted seed '{marker.name}'. " +
                    $"Count={seedEntryCount}, Distance={centerDistance:0.###}, GateRadius={gateRadius:0.###}, SeedRadius={seedRadius:0.###}, ThrowId={marker.ThrowId}.",
                    this);
            }

            SeedPassed?.Invoke(this, marker);
        }

        seedsInside.RemoveWhere(markerId => !stillInside.Contains(markerId));
    }

    public float GetGateRadiusWorld()
    {
        if (useExplicitRadius)
        {
            return Mathf.Max(minimumRadius, explicitRadius);
        }

        Vector3 scale = RadiusSource.lossyScale;
        float largestAxis = Mathf.Max(scale.x, scale.y, scale.z);
        return Mathf.Max(minimumRadius, largestAxis * radiusScaleMultiplier);
    }

    private bool IsSeedInsideGate(
        SeedProjectileMarker marker,
        out float centerDistance,
        out float gateRadius,
        out float seedRadius)
    {
        gateRadius = GetGateRadiusWorld();
        seedRadius = marker.ApproximateRadiusWorld + seedRadiusPadding;
        centerDistance = Vector3.Distance(GateCenter.position, marker.transform.position);

        float allowedDistance = Mathf.Max(0f, gateRadius - seedRadius);
        return centerDistance <= allowedDistance;
    }

    private void CacheFlashRenderers()
    {
        if (!autoFindFlashRenderers || flashRenderers.Count > 0)
        {
            RemoveNullFlashRenderers();
            return;
        }

        flashRenderers.Clear();
        flashRenderers.AddRange(GetComponentsInChildren<Renderer>(true));
        RemoveNullFlashRenderers();
    }

    private void RemoveNullFlashRenderers()
    {
        for (int i = flashRenderers.Count - 1; i >= 0; i--)
        {
            if (flashRenderers[i] == null)
            {
                flashRenderers.RemoveAt(i);
            }
        }
    }

    private void CacheMaterialColors()
    {
        materialColorStates.Clear();

        for (int i = 0; i < flashRenderers.Count; i++)
        {
            Renderer renderer = flashRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                bool hasBaseColor = material.HasProperty(BaseColorId);
                bool hasColor = material.HasProperty(ColorId);
                if (!hasBaseColor && !hasColor)
                {
                    continue;
                }

                materialColorStates.Add(
                    new MaterialColorState(
                        material,
                        hasBaseColor ? material.GetColor(BaseColorId) : default,
                        hasBaseColor,
                        hasColor ? material.GetColor(ColorId) : default,
                        hasColor));
            }
        }
    }

    private void StartFlash()
    {
        if (flashRenderers.Count == 0)
        {
            return;
        }

        if (materialColorStates.Count == 0)
        {
            CacheMaterialColors();
        }

        StopFlashRoutine();
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        ApplyFlashColors();
        yield return new WaitForSeconds(seedEnterFlashDurationSeconds);
        RestoreBaseColors();
        flashRoutine = null;
    }

    private void StopFlashRoutine()
    {
        if (flashRoutine == null)
        {
            return;
        }

        StopCoroutine(flashRoutine);
        flashRoutine = null;
    }

    private void ApplyFlashColors()
    {
        for (int i = 0; i < materialColorStates.Count; i++)
        {
            MaterialColorState state = materialColorStates[i];
            if (state.Material == null)
            {
                continue;
            }

            if (state.HasBaseColor)
            {
                state.Material.SetColor(BaseColorId, seedEnterFlashColor);
            }

            if (state.HasColor)
            {
                state.Material.SetColor(ColorId, seedEnterFlashColor);
            }
        }
    }

    private void RestoreBaseColors()
    {
        for (int i = 0; i < materialColorStates.Count; i++)
        {
            MaterialColorState state = materialColorStates[i];
            if (state.Material == null)
            {
                continue;
            }

            if (state.HasBaseColor)
            {
                state.Material.SetColor(BaseColorId, state.BaseColor);
            }

            if (state.HasColor)
            {
                state.Material.SetColor(ColorId, state.LegacyColor);
            }
        }
    }

    private readonly struct MaterialColorState
    {
        public MaterialColorState(
            Material material,
            Color baseColor,
            bool hasBaseColor,
            Color legacyColor,
            bool hasColor)
        {
            Material = material;
            BaseColor = baseColor;
            HasBaseColor = hasBaseColor;
            LegacyColor = legacyColor;
            HasColor = hasColor;
        }

        public Material Material { get; }
        public Color BaseColor { get; }
        public bool HasBaseColor { get; }
        public Color LegacyColor { get; }
        public bool HasColor { get; }
    }
}
