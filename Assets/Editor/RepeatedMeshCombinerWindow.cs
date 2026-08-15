using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class RepeatedMeshCombinerWindow : EditorWindow
{
    private const string DefaultOutputFolder = "Assets/Generated/CombinedMeshes";
    private const string GeneratedContainerName = "__CombinedMeshes";

    private enum GroupingMode
    {
        ExactSharedMeshAndMaterials,
        MeshNameAndMaterials,
        MaterialLayout
    }

    private enum SourceMode
    {
        RootHierarchy,
        CurrentSelection
    }

    [SerializeField] private Transform root;
    [SerializeField] private string outputFolder = DefaultOutputFolder;
    [SerializeField] private SourceMode sourceMode = SourceMode.RootHierarchy;
    [SerializeField] private GroupingMode groupingMode = GroupingMode.MaterialLayout;
    [SerializeField] private bool includeInactive;
    [SerializeField] private bool includeDisabledRenderers;
    [SerializeField] private bool disableSourceRenderers = true;
    [SerializeField] private bool replaceExistingGeneratedContainer = true;
    [SerializeField] private int minimumInstances = 2;
    [SerializeField] private string lastAnalysisSummary = "Select a root and run the tool.";

    [MenuItem("Tools/VR Stroke Rehab/Optimization/Repeated Mesh Combiner")]
    public static void OpenWindow()
    {
        var window = GetWindow<RepeatedMeshCombinerWindow>("Repeated Mesh Combiner");
        window.minSize = new Vector2(420f, 320f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Repeated Mesh Combiner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Combines repeated child meshes under a selected root using a configurable grouping mode. " +
            "Useful for dense repeated props like fence bars, leaves, or grass planes.",
            MessageType.Info);

        root = (Transform)EditorGUILayout.ObjectField("Root", root, typeof(Transform), true);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        sourceMode = (SourceMode)EditorGUILayout.EnumPopup("Source Mode", sourceMode);
        groupingMode = (GroupingMode)EditorGUILayout.EnumPopup("Grouping Mode", groupingMode);
        minimumInstances = EditorGUILayout.IntSlider("Minimum Instances", minimumInstances, 2, 100);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
        includeDisabledRenderers = EditorGUILayout.Toggle("Include Disabled Renderers", includeDisabledRenderers);
        disableSourceRenderers = EditorGUILayout.Toggle("Disable Source Renderers", disableSourceRenderers);
        replaceExistingGeneratedContainer = EditorGUILayout.Toggle("Replace Existing Output", replaceExistingGeneratedContainer);

        EditorGUILayout.HelpBox(GetSourceModeDescription(sourceMode), MessageType.None);
        EditorGUILayout.HelpBox(GetGroupingModeDescription(groupingMode), MessageType.None);

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Active Selection"))
            {
                root = Selection.activeTransform;
            }

            using (new EditorGUI.DisabledScope(sourceMode == SourceMode.RootHierarchy && root == null))
            {
                if (GUILayout.Button("Analyze", GUILayout.Height(28f)))
                {
                    AnalyzeCurrentSource();
                }

                if (GUILayout.Button("Combine", GUILayout.Height(28f)))
                {
                    CombineCurrentSource();
                }
            }
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Combine Each Selected Root", GUILayout.Height(28f)))
        {
            CombineSelectedRoots();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Last Analysis", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(lastAnalysisSummary, MessageType.None);
    }

    private void CombineSelectedRoots()
    {
        var selectedRoots = Selection.transforms
            .Where(selected => selected != null)
            .Distinct()
            .ToArray();

        if (selectedRoots.Length == 0)
        {
            EditorUtility.DisplayDialog("Repeated Mesh Combiner", "Select at least one root object.", "OK");
            return;
        }

        for (var i = 0; i < selectedRoots.Length; i++)
        {
            CombineFromRoot(selectedRoots[i]);
        }
    }

    private void AnalyzeCurrentSource()
    {
        if (sourceMode == SourceMode.CurrentSelection)
        {
            AnalyzeSelection();
            return;
        }

        AnalyzeRoot(root);
    }

    private void CombineCurrentSource()
    {
        if (sourceMode == SourceMode.CurrentSelection)
        {
            CombineSelection();
            return;
        }

        CombineFromRoot(root);
    }

    private void CombineFromRoot(Transform selectedRoot)
    {
        if (selectedRoot == null)
        {
            EditorUtility.DisplayDialog("Repeated Mesh Combiner", "A root object is required.", "OK");
            return;
        }

        try
        {
            EnsureFolderPath(outputFolder);
        }
        catch (Exception exception)
        {
            EditorUtility.DisplayDialog("Repeated Mesh Combiner", exception.Message, "OK");
            return;
        }

        var filters = selectedRoot.GetComponentsInChildren<MeshFilter>(includeInactive);
        var analysis = AnalyzeSources(filters, selectedRoot, selectedRoot.name);
        lastAnalysisSummary = BuildAnalysisSummary(selectedRoot.name, analysis);
        var groups = analysis.Groups;

        if (groups.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Repeated Mesh Combiner",
                "No matching groups were found.\n\n" + lastAnalysisSummary,
                "OK");
            Debug.Log("Repeated Mesh Combiner analysis:\n" + lastAnalysisSummary, selectedRoot);
            return;
        }

        Undo.IncrementCurrentGroup();
        var undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Combine Repeated Meshes");

        try
        {
            var container = PrepareGeneratedContainer(selectedRoot);
            var combinedObjectsCreated = 0;
            var sourceRenderersDisabled = 0;

            foreach (var group in groups.Values.OrderByDescending(entry => entry.Count))
            {
                var combinedObject = CreateCombinedObjectForGroup(group, selectedRoot, container.transform, selectedRoot.name);
                if (combinedObject == null)
                {
                    continue;
                }

                combinedObjectsCreated++;

                if (!disableSourceRenderers)
                {
                    continue;
                }

                for (var i = 0; i < group.Count; i++)
                {
                    var renderer = group[i].Renderer;
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }

                    Undo.RecordObject(renderer, "Disable source renderer");
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                    sourceRenderersDisabled++;
                }
            }

            Debug.Log(
                $"Repeated Mesh Combiner: created {combinedObjectsCreated} combined object(s) under '{selectedRoot.name}' " +
                $"and disabled {sourceRenderersDisabled} source renderer(s). Assets were saved to {outputFolder}.\n" +
                lastAnalysisSummary,
                selectedRoot);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
        }
    }

    private void CombineSelection()
    {
        var selection = Selection.transforms
            .Where(selected => selected != null)
            .Distinct()
            .ToArray();

        if (selection.Length == 0)
        {
            EditorUtility.DisplayDialog("Repeated Mesh Combiner", "Select at least two repeated objects or their parent groups.", "OK");
            return;
        }

        try
        {
            EnsureFolderPath(outputFolder);
        }
        catch (Exception exception)
        {
            EditorUtility.DisplayDialog("Repeated Mesh Combiner", exception.Message, "OK");
            return;
        }

        var filters = GetMeshFiltersFromSelection(selection);
        var contextName = BuildSelectionContextName(selection);
        var anchor = FindCommonAncestor(selection);
        var analysis = AnalyzeSources(filters, anchor, contextName);
        lastAnalysisSummary = BuildAnalysisSummary(contextName, analysis);
        var groups = analysis.Groups;

        if (groups.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Repeated Mesh Combiner",
                "No matching groups were found.\n\n" + lastAnalysisSummary,
                "OK");
            Debug.Log("Repeated Mesh Combiner analysis:\n" + lastAnalysisSummary);
            return;
        }

        Undo.IncrementCurrentGroup();
        var undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Combine Selected Meshes");

        try
        {
            var container = PrepareSelectionContainer(selection, anchor);
            var combinedObjectsCreated = 0;
            var sourceRenderersDisabled = 0;

            foreach (var group in groups.Values.OrderByDescending(entry => entry.Count))
            {
                var combinedObject = CreateCombinedObjectForGroup(group, anchor, container.transform, contextName);
                if (combinedObject == null)
                {
                    continue;
                }

                combinedObjectsCreated++;

                if (!disableSourceRenderers)
                {
                    continue;
                }

                for (var i = 0; i < group.Count; i++)
                {
                    var renderer = group[i].Renderer;
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }

                    Undo.RecordObject(renderer, "Disable source renderer");
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                    sourceRenderersDisabled++;
                }
            }

            Debug.Log(
                $"Repeated Mesh Combiner: created {combinedObjectsCreated} combined object(s) from current selection " +
                $"and disabled {sourceRenderersDisabled} source renderer(s). Assets were saved to {outputFolder}.\n" +
                lastAnalysisSummary,
                container);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
        }
    }

    private void AnalyzeRoot(Transform selectedRoot)
    {
        if (selectedRoot == null)
        {
            EditorUtility.DisplayDialog("Repeated Mesh Combiner", "A root object is required.", "OK");
            return;
        }

        var filters = selectedRoot.GetComponentsInChildren<MeshFilter>(includeInactive);
        var analysis = AnalyzeSources(filters, selectedRoot, selectedRoot.name);
        lastAnalysisSummary = BuildAnalysisSummary(selectedRoot.name, analysis);
        Debug.Log("Repeated Mesh Combiner analysis:\n" + lastAnalysisSummary, selectedRoot);
    }

    private void AnalyzeSelection()
    {
        var selection = Selection.transforms
            .Where(selected => selected != null)
            .Distinct()
            .ToArray();

        if (selection.Length == 0)
        {
            EditorUtility.DisplayDialog("Repeated Mesh Combiner", "Select at least two repeated objects or their parent groups.", "OK");
            return;
        }

        var filters = GetMeshFiltersFromSelection(selection);
        var contextName = BuildSelectionContextName(selection);
        var analysis = AnalyzeSources(filters, FindCommonAncestor(selection), contextName);
        lastAnalysisSummary = BuildAnalysisSummary(contextName, analysis);
        Debug.Log("Repeated Mesh Combiner analysis:\n" + lastAnalysisSummary);
    }

    private AnalysisResult AnalyzeSources(IEnumerable<MeshFilter> filters, Transform selectedRoot, string contextName)
    {
        var result = new AnalysisResult();
        var candidateGroups = new Dictionary<string, List<MeshSource>>();
        var generatedContainer = selectedRoot != null ? selectedRoot.Find(GeneratedContainerName) : null;

        foreach (var filter in filters)
        {
            result.ScannedMeshFilters++;

            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            if (generatedContainer != null &&
                (filter.transform == generatedContainer || filter.transform.IsChildOf(generatedContainer)))
            {
                continue;
            }

            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                continue;
            }

            if (!includeDisabledRenderers && !renderer.enabled)
            {
                continue;
            }

            if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0)
            {
                continue;
            }

            var subMeshCount = Mathf.Min(filter.sharedMesh.subMeshCount, renderer.sharedMaterials.Length);
            if (subMeshCount <= 0)
            {
                continue;
            }

            result.EligibleRenderers++;

            var key = BuildGroupKey(filter.sharedMesh, renderer.sharedMaterials, subMeshCount, groupingMode);
            if (!candidateGroups.TryGetValue(key, out var list))
            {
                list = new List<MeshSource>();
                candidateGroups.Add(key, list);
            }

            list.Add(new MeshSource(filter, renderer));
        }

        result.CandidateGroupCount = candidateGroups.Count;
        result.Groups = candidateGroups
            .Where(pair => pair.Value.Count >= minimumInstances)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        result.TopCandidateDescriptions = candidateGroups.Values
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group[0].Filter.sharedMesh != null ? group[0].Filter.sharedMesh.name : string.Empty)
            .Take(5)
            .Select(DescribeGroup)
            .ToArray();

        result.ContextName = contextName;

        return result;
    }

    private GameObject PrepareGeneratedContainer(Transform selectedRoot)
    {
        var existing = selectedRoot.Find(GeneratedContainerName);
        if (existing != null && replaceExistingGeneratedContainer)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        if (existing != null && !replaceExistingGeneratedContainer)
        {
            return existing.gameObject;
        }

        var container = new GameObject(GeneratedContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Create combined mesh container");
        container.transform.SetParent(selectedRoot, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        container.transform.localScale = Vector3.one;
        return container;
    }

    private GameObject PrepareSelectionContainer(Transform[] selection, Transform anchor)
    {
        if (anchor != null)
        {
            return PrepareGeneratedContainer(anchor);
        }

        var containerName = GeneratedContainerName + "_Selection";
        var existingRootContainers = Resources.FindObjectsOfTypeAll<Transform>()
            .Where(item => item != null &&
                           item.name == containerName &&
                           item.parent == null &&
                           item.gameObject.scene.IsValid())
            .ToArray();

        if (replaceExistingGeneratedContainer)
        {
            for (var i = 0; i < existingRootContainers.Length; i++)
            {
                Undo.DestroyObjectImmediate(existingRootContainers[i].gameObject);
            }
        }
        else if (existingRootContainers.Length > 0)
        {
            return existingRootContainers[0].gameObject;
        }

        var container = new GameObject(containerName);
        Undo.RegisterCreatedObjectUndo(container, "Create combined mesh container");
        return container;
    }

    private GameObject CreateCombinedObjectForGroup(List<MeshSource> group, Transform anchor, Transform container, string contextName)
    {
        if (group == null || group.Count == 0)
        {
            return null;
        }

        var first = group[0];
        if (first.Filter == null || first.Renderer == null || first.Filter.sharedMesh == null)
        {
            return null;
        }

        var sourceMesh = first.Filter.sharedMesh;
        var materials = first.Renderer.sharedMaterials;
        var subMeshCount = Mathf.Min(sourceMesh.subMeshCount, materials.Length);
        if (subMeshCount == 0)
        {
            return null;
        }

        var subMeshCombiners = new List<CombineInstance>[subMeshCount];
        for (var subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
        {
            subMeshCombiners[subMeshIndex] = new List<CombineInstance>(group.Count);
        }

        for (var i = 0; i < group.Count; i++)
        {
            var source = group[i];
            if (source.Filter == null || source.Renderer == null || source.Filter.sharedMesh == null)
            {
                continue;
            }

            for (var subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                subMeshCombiners[subMeshIndex].Add(new CombineInstance
                {
                    mesh = source.Filter.sharedMesh,
                    subMeshIndex = subMeshIndex,
                    transform = GetAnchorMatrix(anchor) * source.Filter.transform.localToWorldMatrix
                });
            }
        }

        var validSubMeshes = new List<CombineInstance>();
        var validMaterials = new List<Material>();

        for (var subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
        {
            var subMeshInstances = subMeshCombiners[subMeshIndex];
            if (subMeshInstances.Count == 0)
            {
                continue;
            }

            var subMesh = new Mesh
            {
                name = $"{sourceMesh.name}_Submesh{subMeshIndex}_Combined",
                indexFormat = IndexFormat.UInt32
            };
            subMesh.CombineMeshes(subMeshInstances.ToArray(), true, true);

            validSubMeshes.Add(new CombineInstance
            {
                mesh = subMesh,
                subMeshIndex = 0,
                transform = Matrix4x4.identity
            });
            validMaterials.Add(materials[subMeshIndex]);
        }

        if (validSubMeshes.Count == 0)
        {
            return null;
        }

        var combinedMesh = new Mesh
        {
            name = $"{sourceMesh.name}_Combined",
            indexFormat = IndexFormat.UInt32
        };
        combinedMesh.CombineMeshes(validSubMeshes.ToArray(), false, false);
        combinedMesh.RecalculateBounds();
        MeshUtility.Optimize(combinedMesh);

        var assetPath = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(outputFolder, $"{SanitizeName(contextName)}_{sourceMesh.name}_Combined.asset").Replace("\\", "/"));
        AssetDatabase.CreateAsset(combinedMesh, assetPath);

        for (var i = 0; i < validSubMeshes.Count; i++)
        {
            if (validSubMeshes[i].mesh != null)
            {
                DestroyImmediate(validSubMeshes[i].mesh);
            }
        }

        var combinedObject = new GameObject($"{sourceMesh.name}_Combined_{group.Count}");
        Undo.RegisterCreatedObjectUndo(combinedObject, "Create combined mesh object");
        combinedObject.transform.SetParent(container, false);

        var filter = Undo.AddComponent<MeshFilter>(combinedObject);
        filter.sharedMesh = combinedMesh;

        var renderer = Undo.AddComponent<MeshRenderer>(combinedObject);
        renderer.sharedMaterials = validMaterials.ToArray();
        CopyRendererSettings(first.Renderer, renderer);

        var staticFlags = GameObjectUtility.GetStaticEditorFlags(first.Renderer.gameObject);
        GameObjectUtility.SetStaticEditorFlags(combinedObject, staticFlags);

        EditorUtility.SetDirty(filter);
        EditorUtility.SetDirty(renderer);
        return combinedObject;
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Combined";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
        return sanitized.Replace(' ', '_');
    }

    private static Matrix4x4 GetAnchorMatrix(Transform anchor)
    {
        return anchor != null ? anchor.worldToLocalMatrix : Matrix4x4.identity;
    }

    private static void CopyRendererSettings(MeshRenderer source, MeshRenderer destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.shadowCastingMode = source.shadowCastingMode;
        destination.receiveShadows = source.receiveShadows;
        destination.lightProbeUsage = source.lightProbeUsage;
        destination.reflectionProbeUsage = source.reflectionProbeUsage;
        destination.motionVectorGenerationMode = source.motionVectorGenerationMode;
        destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
        destination.renderingLayerMask = source.renderingLayerMask;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
        destination.probeAnchor = source.probeAnchor;
    }

    private static string BuildGroupKey(Mesh mesh, Material[] materials, int subMeshCount, GroupingMode mode)
    {
        var materialIds = string.Join("_",
            materials.Take(subMeshCount).Select(material => material != null ? material.GetInstanceID().ToString() : "null"));
        var meshId = mesh != null ? mesh.GetInstanceID().ToString() : "null";
        var meshName = mesh != null ? mesh.name : "null";

        switch (mode)
        {
            case GroupingMode.ExactSharedMeshAndMaterials:
                return "exact::" + subMeshCount + "::" + meshId + "::" + materialIds;
            case GroupingMode.MeshNameAndMaterials:
                return "meshname::" + subMeshCount + "::" + meshName + "::" + materialIds;
            case GroupingMode.MaterialLayout:
                return "materials::" + subMeshCount + "::" + materialIds;
            default:
                return "exact::" + subMeshCount + "::" + meshId + "::" + materialIds;
        }
    }

    private static string GetGroupingModeDescription(GroupingMode mode)
    {
        switch (mode)
        {
            case GroupingMode.ExactSharedMeshAndMaterials:
                return "Exact Shared Mesh + Materials: safest, but strict. Only matches identical shared mesh assets with identical material lists.";
            case GroupingMode.MeshNameAndMaterials:
                return "Mesh Name + Materials: good when imported copies are separate mesh assets but still represent the same repeated object.";
            case GroupingMode.MaterialLayout:
                return "Material Layout: broadest match. Combines any child meshes that use the same material list and submesh layout under the selected root.";
            default:
                return string.Empty;
        }
    }

    private static string GetSourceModeDescription(SourceMode mode)
    {
        switch (mode)
        {
            case SourceMode.RootHierarchy:
                return "Root Hierarchy: scans every child mesh under the selected Root object.";
            case SourceMode.CurrentSelection:
                return "Current Selection: scans the currently selected objects and their descendants together, even if they are not under one dedicated parent.";
            default:
                return string.Empty;
        }
    }

    private string BuildAnalysisSummary(string contextName, AnalysisResult analysis)
    {
        var lines = new List<string>
        {
            $"Context: {contextName}",
            $"Source mode: {sourceMode}",
            $"Grouping mode: {groupingMode}",
            $"Scanned mesh filters: {analysis.ScannedMeshFilters}",
            $"Eligible mesh renderers: {analysis.EligibleRenderers}",
            $"Candidate groups: {analysis.CandidateGroupCount}",
            $"Groups meeting minimum {minimumInstances}: {analysis.Groups.Count}"
        };

        if (analysis.EligibleRenderers < 2)
        {
            lines.Add("Hint: fewer than two eligible renderers were found. Select a parent that contains the repeated objects or switch Source Mode to Current Selection.");
        }

        if (analysis.TopCandidateDescriptions != null && analysis.TopCandidateDescriptions.Length > 0)
        {
            lines.Add("Top candidates:");
            for (var i = 0; i < analysis.TopCandidateDescriptions.Length; i++)
            {
                lines.Add("- " + analysis.TopCandidateDescriptions[i]);
            }
        }
        else
        {
            lines.Add("Top candidates: none");
        }

        return string.Join("\n", lines);
    }

    private static string DescribeGroup(List<MeshSource> group)
    {
        if (group == null || group.Count == 0)
        {
            return "empty";
        }

        var first = group[0];
        var mesh = first.Filter != null ? first.Filter.sharedMesh : null;
        var materials = first.Renderer != null ? first.Renderer.sharedMaterials : null;
        var materialNames = materials == null || materials.Length == 0
            ? "no materials"
            : string.Join(", ", materials.Select(material => material != null ? material.name : "null"));
        var meshName = mesh != null ? mesh.name : "null mesh";
        return $"{group.Count}x {meshName} [{materialNames}]";
    }

    private MeshFilter[] GetMeshFiltersFromSelection(Transform[] selection)
    {
        var filters = new List<MeshFilter>();
        var seen = new HashSet<int>();

        for (var i = 0; i < selection.Length; i++)
        {
            var transform = selection[i];
            if (transform == null)
            {
                continue;
            }

            var localFilters = transform.GetComponentsInChildren<MeshFilter>(includeInactive);
            for (var j = 0; j < localFilters.Length; j++)
            {
                var filter = localFilters[j];
                if (filter == null)
                {
                    continue;
                }

                var id = filter.GetInstanceID();
                if (seen.Add(id))
                {
                    filters.Add(filter);
                }
            }
        }

        return filters.ToArray();
    }

    private static string BuildSelectionContextName(Transform[] selection)
    {
        if (selection == null || selection.Length == 0)
        {
            return "Selection";
        }

        if (selection.Length == 1)
        {
            return "Selection: " + selection[0].name;
        }

        return $"Selection ({selection.Length} roots)";
    }

    private static Transform FindCommonAncestor(Transform[] transforms)
    {
        if (transforms == null || transforms.Length == 0)
        {
            return null;
        }

        var first = transforms[0];
        if (first == null)
        {
            return null;
        }

        var ancestors = new List<Transform>();
        var current = first;
        while (current != null)
        {
            ancestors.Add(current);
            current = current.parent;
        }

        for (var i = 0; i < ancestors.Count; i++)
        {
            var candidate = ancestors[i];
            var sharedByAll = true;

            for (var j = 1; j < transforms.Length; j++)
            {
                var probe = transforms[j];
                var found = false;
                while (probe != null)
                {
                    if (probe == candidate)
                    {
                        found = true;
                        break;
                    }

                    probe = probe.parent;
                }

                if (!found)
                {
                    sharedByAll = false;
                    break;
                }
            }

            if (sharedByAll)
            {
                return candidate;
            }
        }

        return null;
    }

    private static void EnsureFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new InvalidOperationException("Output folder cannot be empty.");
        }

        var normalized = folderPath.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        var parts = normalized.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
        {
            throw new InvalidOperationException("Output folder must stay inside the Assets folder.");
        }

        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private struct MeshSource
    {
        public MeshSource(MeshFilter filter, MeshRenderer renderer)
        {
            Filter = filter;
            Renderer = renderer;
        }

        public MeshFilter Filter { get; }

        public MeshRenderer Renderer { get; }
    }

    private sealed class AnalysisResult
    {
        public string ContextName = string.Empty;
        public int ScannedMeshFilters;
        public int EligibleRenderers;
        public int CandidateGroupCount;
        public Dictionary<string, List<MeshSource>> Groups = new Dictionary<string, List<MeshSource>>();
        public string[] TopCandidateDescriptions = Array.Empty<string>();
    }
}
