using UnityEngine;
using System.Collections.Generic;
using TriggerSystem;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public class ObjectiveHighlightRuntime : MonoBehaviour
    {
        private enum HighlightMode
        {
            None,
            Pulsing,
            Solid
        }

        private static readonly int OutlineColorId = Shader.PropertyToID("_Outline_Color");
        private static readonly int OutlineThicknessId = Shader.PropertyToID("_Outline_Thickness");
        private const string DefaultRadiusContactTargetTag = "HandAnchor";
        private static Material _sharedOutlineMaterial;

        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private TSParasite[] targetParasites;
        [SerializeField] private TSObjectiveReturnZone[] targetReturnZones;
        [SerializeField] private float pulsingMinThickness = 0.02f;
        [SerializeField] private float pulsingMaxThickness = 0.05f;
        [SerializeField] private float solidThickness = 0.04f;
        [SerializeField] private string radiusContactTargetTag = DefaultRadiusContactTargetTag;
        [SerializeField] private Color radiusContactHighlightColor = new Color(0.05f, 0.45f, 1f, 0.5f);

        private HighlightMode _mode;
        private Color _color;
        private float _pulseSpeed = 2.5f;
        private float _minAlphaMultiplier = 0.55f;
        private float _maxAlphaMultiplier = 1f;
        private readonly List<RendererOutlineState> _rendererStates = new List<RendererOutlineState>();

        private sealed class RendererOutlineState
        {
            public Renderer Renderer;
            public Material[] OriginalSharedMaterials;
            public int OutlineMaterialIndex;
            public MaterialPropertyBlock PropertyBlock;
        }

        public static void ApplyPulsingHighlight(
            GameObject target,
            Color color,
            float pulseSpeed,
            float minAlphaMultiplier,
            float maxAlphaMultiplier)
        {
            ObjectiveHighlightRuntime runtime = GetOrAddRuntime(target);
            if (runtime == null)
            {
                return;
            }

            runtime.ConfigurePulsing(color, pulseSpeed, minAlphaMultiplier, maxAlphaMultiplier);
        }

        public static void ApplySolidHighlight(GameObject target, Color color)
        {
            ObjectiveHighlightRuntime runtime = GetOrAddRuntime(target);
            if (runtime == null)
            {
                return;
            }

            runtime.ConfigureSolid(color);
        }

        public static void Clear(GameObject target)
        {
            if (target == null || !target.TryGetComponent(out ObjectiveHighlightRuntime runtime))
            {
                return;
            }

            runtime.ClearHighlight();
        }

        private static ObjectiveHighlightRuntime GetOrAddRuntime(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            if (!target.TryGetComponent(out ObjectiveHighlightRuntime runtime))
            {
                runtime = target.AddComponent<ObjectiveHighlightRuntime>();
            }

            runtime.RefreshRenderers();
            return runtime;
        }

        private void Awake()
        {
            RefreshRenderers();
        }

        private void LateUpdate()
        {
            if (_mode == HighlightMode.None)
            {
                return;
            }

            if (_mode == HighlightMode.Pulsing)
            {
                float oscillation = 0.5f + 0.5f * Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f);
                float alphaMultiplier = Mathf.Lerp(_minAlphaMultiplier, _maxAlphaMultiplier, oscillation);
                Color animatedColor = ResolveHighlightColor();
                animatedColor.a *= alphaMultiplier;
                float thickness = Mathf.Lerp(pulsingMinThickness, pulsingMaxThickness, oscillation);
                ApplyOutlineProperties(animatedColor, thickness);
                return;
            }

            ApplyOutlineProperties(ResolveHighlightColor(), solidThickness);
        }

        private void ConfigurePulsing(Color color, float pulseSpeed, float minAlphaMultiplier, float maxAlphaMultiplier)
        {
            _color = color;
            _pulseSpeed = Mathf.Max(0.01f, pulseSpeed);
            _minAlphaMultiplier = Mathf.Clamp01(minAlphaMultiplier);
            _maxAlphaMultiplier = Mathf.Max(_minAlphaMultiplier, maxAlphaMultiplier);
            EnsureOutlineApplied();
            _mode = HighlightMode.Pulsing;
            enabled = true;
        }

        private void ConfigureSolid(Color color)
        {
            _color = color;
            EnsureOutlineApplied();
            _mode = HighlightMode.Solid;
            enabled = true;
            ApplyOutlineProperties(ResolveHighlightColor(), solidThickness);
        }

        private void ClearHighlight()
        {
            _mode = HighlightMode.None;
            enabled = false;

            for (int i = 0; i < _rendererStates.Count; i++)
            {
                RendererOutlineState state = _rendererStates[i];
                if (state == null)
                {
                    continue;
                }

                Renderer renderer = state.Renderer;
                if (renderer == null)
                {
                    continue;
                }

                renderer.SetPropertyBlock(null, state.OutlineMaterialIndex);
                if (state.OriginalSharedMaterials != null)
                {
                    renderer.sharedMaterials = state.OriginalSharedMaterials;
                }
            }

            _rendererStates.Clear();
        }

        private void RefreshRenderers()
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
            RefreshParasites();
            RefreshReturnZones();
        }

        private void RefreshParasites()
        {
            TSParasite[] childParasites = GetComponentsInChildren<TSParasite>(true);
            TSParasite[] parentParasites = GetComponentsInParent<TSParasite>(true);

            if ((childParasites == null || childParasites.Length == 0) &&
                (parentParasites == null || parentParasites.Length == 0))
            {
                targetParasites = null;
                return;
            }

            List<TSParasite> resolvedParasites = new List<TSParasite>();
            AddUniqueParasites(childParasites, resolvedParasites);
            AddUniqueParasites(parentParasites, resolvedParasites);
            targetParasites = resolvedParasites.ToArray();
        }

        private void RefreshReturnZones()
        {
            TSObjectiveReturnZone[] childReturnZones = GetComponentsInChildren<TSObjectiveReturnZone>(true);
            TSObjectiveReturnZone[] parentReturnZones = GetComponentsInParent<TSObjectiveReturnZone>(true);

            if ((childReturnZones == null || childReturnZones.Length == 0) &&
                (parentReturnZones == null || parentReturnZones.Length == 0))
            {
                targetReturnZones = null;
                return;
            }

            List<TSObjectiveReturnZone> resolvedReturnZones = new List<TSObjectiveReturnZone>();
            AddUniqueReturnZones(childReturnZones, resolvedReturnZones);
            AddUniqueReturnZones(parentReturnZones, resolvedReturnZones);
            targetReturnZones = resolvedReturnZones.ToArray();
        }

        private Color ResolveHighlightColor()
        {
            if (HasTargetInsideParasiteRadius())
            {
                return radiusContactHighlightColor;
            }

            return _color;
        }

        private bool HasTargetInsideParasiteRadius()
        {
            if (targetParasites == null || targetParasites.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < targetParasites.Length; i++)
            {
                TSParasite parasite = targetParasites[i];
                if (parasite == null || !parasite.HasTargetInsideRadius)
                {
                    continue;
                }

                IReadOnlyList<GameObject> insideTargets = parasite.TargetsInsideRadius;
                for (int targetIndex = 0; targetIndex < insideTargets.Count; targetIndex++)
                {
                    if (HasRadiusContactTag(insideTargets[targetIndex]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasRadiusContactTag(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            string targetTag = string.IsNullOrWhiteSpace(radiusContactTargetTag)
                ? DefaultRadiusContactTargetTag
                : radiusContactTargetTag;

            for (Transform cursor = target.transform; cursor != null; cursor = cursor.parent)
            {
                if (cursor.gameObject.tag == targetTag)
                {
                    return true;
                }
            }

            return HasReturnZoneAcceptedTag(target);
        }

        private bool HasReturnZoneAcceptedTag(GameObject target)
        {
            if (target == null || targetReturnZones == null || targetReturnZones.Length == 0)
            {
                return false;
            }

            for (int zoneIndex = 0; zoneIndex < targetReturnZones.Length; zoneIndex++)
            {
                TSObjectiveReturnZone returnZone = targetReturnZones[zoneIndex];
                if (returnZone == null || returnZone.AcceptedObjectTags == null)
                {
                    continue;
                }

                IReadOnlyList<string> acceptedTags = returnZone.AcceptedObjectTags;
                for (int tagIndex = 0; tagIndex < acceptedTags.Count; tagIndex++)
                {
                    if (HasTagInHierarchy(target, acceptedTags[tagIndex]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddUniqueParasites(TSParasite[] source, List<TSParasite> destination)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                TSParasite parasite = source[i];
                if (parasite != null && !destination.Contains(parasite))
                {
                    destination.Add(parasite);
                }
            }
        }

        private static void AddUniqueReturnZones(TSObjectiveReturnZone[] source, List<TSObjectiveReturnZone> destination)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                TSObjectiveReturnZone returnZone = source[i];
                if (returnZone != null && !destination.Contains(returnZone))
                {
                    destination.Add(returnZone);
                }
            }
        }

        private static bool HasTagInHierarchy(GameObject target, string tag)
        {
            if (target == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            for (Transform cursor = target.transform; cursor != null; cursor = cursor.parent)
            {
                if (cursor.gameObject.tag == tag)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureOutlineApplied()
        {
            RefreshRenderers();
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                return;
            }

            Material outlineMaterial = GetOrCreateOutlineMaterial();
            if (outlineMaterial == null)
            {
                return;
            }

            if (_rendererStates.Count > 0)
            {
                return;
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] original = renderer.sharedMaterials;
                if (original == null || original.Length == 0)
                {
                    continue;
                }

                Material[] withOutline = new Material[original.Length + 1];
                for (int m = 0; m < original.Length; m++)
                {
                    withOutline[m] = original[m];
                }

                int outlineIndex = withOutline.Length - 1;
                withOutline[outlineIndex] = outlineMaterial;
                renderer.sharedMaterials = withOutline;

                RendererOutlineState state = new RendererOutlineState
                {
                    Renderer = renderer,
                    OriginalSharedMaterials = original,
                    OutlineMaterialIndex = outlineIndex,
                    PropertyBlock = new MaterialPropertyBlock()
                };
                _rendererStates.Add(state);
            }
        }

        private static Material GetOrCreateOutlineMaterial()
        {
            if (_sharedOutlineMaterial != null)
            {
                return _sharedOutlineMaterial;
            }

            Shader shader = Shader.Find("Shader Graphs/OutlineShader");
            if (shader == null)
            {
                shader = Shader.Find("OutlineShader");
            }

            if (shader == null)
            {
                return null;
            }

            _sharedOutlineMaterial = new Material(shader)
            {
                name = "Runtime_ObjectiveOutlineMat"
            };
            return _sharedOutlineMaterial;
        }

        private void ApplyOutlineProperties(Color color, float thickness)
        {
            if (_rendererStates.Count == 0)
            {
                return;
            }

            float clampedThickness = Mathf.Max(0.0001f, thickness);
            for (int i = 0; i < _rendererStates.Count; i++)
            {
                RendererOutlineState state = _rendererStates[i];
                if (state == null || state.Renderer == null)
                {
                    continue;
                }

                state.PropertyBlock.Clear();
                state.PropertyBlock.SetColor(OutlineColorId, color);
                state.PropertyBlock.SetFloat(OutlineThicknessId, clampedThickness);
                state.Renderer.SetPropertyBlock(state.PropertyBlock, state.OutlineMaterialIndex);
            }
        }

        private void OnDestroy()
        {
            ClearHighlight();
        }

    }
}
