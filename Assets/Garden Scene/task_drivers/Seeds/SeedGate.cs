using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger-based ring gate. Emits pass-through events when a seed projectile enters.
/// </summary>
[DisallowMultipleComponent]
public class SeedGate : MonoBehaviour
{
    [SerializeField] private string gateId = "Gate";
    [SerializeField] private Collider triggerCollider;

    [Header("Feedback")]
    [SerializeField] private List<Renderer> flashRenderers = new List<Renderer>();
    [SerializeField] private bool autoFindFlashRenderers = true;
    [SerializeField] private Color seedEnterFlashColor = new Color(0.2f, 1f, 0.35f, 1f);
    [SerializeField][Min(0.01f)] private float seedEnterFlashDurationSeconds = 0.12f;
    [SerializeField] private bool logDebug;

    [Header("Runtime")]
    [SerializeField] private int seedEntryCount;

    public event Action<SeedGate, SeedProjectileMarker> SeedPassed;

    private readonly List<MaterialColorState> materialColorStates = new List<MaterialColorState>();
    private Coroutine flashRoutine;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Reset()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        CacheFlashRenderers();
    }

    private void Awake()
    {
        CacheFlashRenderers();
        CacheMaterialColors();
    }

    private void OnDisable()
    {
        StopFlashRoutine();
        RestoreBaseColors();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        SeedProjectileMarker marker = other.GetComponentInParent<SeedProjectileMarker>();
        if (marker == null)
        {
            return;
        }

        seedEntryCount++;
        StartFlash();

        if (logDebug)
        {
            Debug.Log(
                $"[SeedGate] {gateId} detected seed entry. Count={seedEntryCount}, ThrowId={marker.ThrowId}.",
                this);
        }

        SeedPassed?.Invoke(this, marker);
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
