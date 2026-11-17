using System;
using System.Reflection;
using TaskSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Runs against real Unity Renderers without modifying the open scene's objects.
public static class ObjectiveHighlightRegressionChecks
{
    private static readonly MethodInfo Tick = typeof(ObjectiveHighlightRuntime).GetMethod(
        "LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

    [MenuItem("Tools/Task System/Verify Objective Highlight")]
    public static void Run()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/OutlineShader.shadergraph");
        Require(shader != null && shader.isSupported, "Outline shader must load and be supported in this editor.");
        var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        SerializedProperty included = graphics.FindProperty("m_AlwaysIncludedShaders");
        bool found = false;
        for (int i = 0; i < included.arraySize; i++)
            found |= included.GetArrayElementAtIndex(i).objectReferenceValue == shader;
        Require(found, "Always Included Shaders must reference the imported outline shader.");

        var parent = new GameObject("Highlight regression (temporary)") { hideFlags = HideFlags.HideAndDontSave };
        var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
        child.hideFlags = HideFlags.HideAndDontSave;
        child.transform.SetParent(parent.transform);
        Renderer renderer = child.GetComponent<Renderer>();
        Material original = renderer.sharedMaterial;
        var replacement = new Material(original);
        var block = new MaterialPropertyBlock();
        int marker = Shader.PropertyToID("_HighlightRegressionMarker");
        int colorId = Shader.PropertyToID("_Outline_Color");
        string loggedError = null;
        Application.LogCallback captureErrors = (message, stack, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                loggedError = message;
        };
        Application.logMessageReceived += captureErrors;
        try
        {
            ObjectiveHighlightRuntime.ApplySolidHighlight(parent, Color.blue);
            ObjectiveHighlightRuntime.ApplySolidHighlight(child, Color.red);
            Require(renderer.sharedMaterials.Length == 2, "Overlapping owners must share one outline slot.");
            ObjectiveHighlightRuntime.Clear(parent);
            Tick.Invoke(child.GetComponent<ObjectiveHighlightRuntime>(), null);
            Require(renderer.sharedMaterials.Length == 2, "Clearing the parent must preserve the child's outline.");
            ObjectiveHighlightRuntime.Clear(child);
            Require(renderer.sharedMaterials.Length == 1 && renderer.sharedMaterial == original,
                "The last owner must remove only the outline.");

            ObjectiveHighlightRuntime.ApplySolidHighlight(parent, Color.blue);
            ObjectiveHighlightRuntime.ApplySolidHighlight(child, Color.red);
            ObjectiveHighlightRuntime.Clear(child);
            var runtime = parent.GetComponent<ObjectiveHighlightRuntime>();
            Tick.Invoke(runtime, null);
            renderer.GetPropertyBlock(block, 1);
            Require(block.GetColor(colorId) == Color.blue, "Clearing the child must restore the parent's appearance.");
            ObjectiveHighlightRuntime.ApplySolidHighlight(parent, Color.blue);
            Require(renderer.sharedMaterials.Length == 2, "Repeated configuration must not add another outline.");

            renderer.sharedMaterials = new[] { replacement };
            Tick.Invoke(runtime, null);
            Require(renderer.sharedMaterials.Length == 2 && renderer.sharedMaterial == replacement,
                "A removed outline slot must be recreated without reverting changed base materials.");
            Material outline = renderer.sharedMaterials[1];
            renderer.sharedMaterials = new[] { outline, replacement, original };
            Tick.Invoke(runtime, null);
            renderer.GetPropertyBlock(block, 0);
            Require(block.GetColor(colorId) == Color.blue, "Updating must follow a reordered outline slot.");
            block.Clear();
            block.SetFloat(marker, 17f);
            renderer.SetPropertyBlock(block, 1);
            ObjectiveHighlightRuntime.Clear(parent);
            Require(renderer.sharedMaterials.Length == 2 && renderer.sharedMaterials[0] == replacement &&
                renderer.sharedMaterials[1] == original, "Cleanup must preserve external material order.");
            block.Clear();
            renderer.GetPropertyBlock(block, 0);
            Require(block.GetFloat(marker) == 17f, "Cleanup must preserve shifted base-material property blocks.");

            ObjectiveHighlightRuntime.ApplySolidHighlight(parent, Color.blue);
            renderer.sharedMaterials = Array.Empty<Material>();
            Tick.Invoke(runtime, null);
            Require(renderer.sharedMaterials.Length == 0, "An empty renderer must not receive indexed property writes.");
            renderer.sharedMaterials = new[] { original };
            Tick.Invoke(runtime, null);
            Require(renderer.sharedMaterials.Length == 2, "Outlines must recover when base materials return.");
            renderer.sharedMaterials = new[] { replacement };
            ObjectiveHighlightRuntime.Clear(parent);
            Require(renderer.sharedMaterials.Length == 1 && renderer.sharedMaterial == replacement,
                "Clearing an already removed slot must preserve the replacement material.");

            ObjectiveHighlightRuntime.ApplySolidHighlight(parent, Color.blue);
            runtime.enabled = false;
            InvokeEditModeLifecycle(runtime, "OnDisable");
            Require(renderer.sharedMaterials.Length == 1, "Disabling a highlight must release its slot.");
            runtime.enabled = true;
            InvokeEditModeLifecycle(runtime, "OnEnable");
            Require(renderer.sharedMaterials.Length == 2, "Re-enabling an active highlight must restore its slot.");
            InvokeEditModeLifecycle(runtime, "OnDestroy");
            Object.DestroyImmediate(runtime);
            Require(renderer.sharedMaterials.Length == 1, "Destroying a highlight must release its slot.");
            Require(loggedError == null, "Unity logged an error during the checks: " + loggedError);
            Debug.Log("Objective highlight regression checks passed: shader inclusion, overlap in both release orders, " +
                "reconfiguration, replacement, reordering, property preservation, empty slots, disable/enable and destruction.");
        }
        finally
        {
            ObjectiveHighlightRuntime.Clear(parent);
            ObjectiveHighlightRuntime.Clear(child);
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(replacement);
            Application.logMessageReceived -= captureErrors;
        }
    }

    private static void InvokeEditModeLifecycle(ObjectiveHighlightRuntime runtime, string method)
    {
        // This runtime component is not ExecuteAlways. In Edit Mode Unity does not
        // dispatch its play-mode lifecycle messages, so exercise those handlers explicitly.
        if (!EditorApplication.isPlaying)
            typeof(ObjectiveHighlightRuntime).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(runtime, null);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
