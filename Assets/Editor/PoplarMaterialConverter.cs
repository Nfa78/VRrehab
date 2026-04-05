using UnityEditor;
using UnityEngine;

public static class PoplarMaterialConverter
{
    private const string PoplarMaterialsPath = "Assets/ALP_Assets/Poplar Tree FREE/Models/Materials";

    [MenuItem("Tools/Poplar/Convert Materials To URP Lit")]
    private static void ConvertToUrpLit()
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("URP Lit shader not found. Make sure URP is installed and active.");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Material", new[] { PoplarMaterialsPath });
        if (guids == null || guids.Length == 0)
        {
            Debug.LogWarning($"No materials found under {PoplarMaterialsPath}.");
            return;
        }

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                continue;
            }

            var baseMap = mat.GetTexture("_MainTex");
            var normalMap = mat.GetTexture("_BumpMap");
            var occlusionMap = mat.GetTexture("_OcclusionMap");

            mat.shader = urpLit;

            if (baseMap != null)
            {
                mat.SetTexture("_BaseMap", baseMap);
            }

            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (occlusionMap != null)
            {
                mat.SetTexture("_OcclusionMap", occlusionMap);
                mat.SetFloat("_OcclusionStrength", 1f);
            }

            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.25f);
            mat.SetFloat("_Surface", 0f); // Opaque

            var isAlphaClip = mat.name.Contains("Branches") || mat.name.Contains("Billboard");
            mat.SetFloat("_AlphaClip", isAlphaClip ? 1f : 0f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetFloat("_Cull", isAlphaClip ? 0f : 2f);

            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Poplar materials converted to URP/Lit. Review for visual tweaks.");
    }
}
