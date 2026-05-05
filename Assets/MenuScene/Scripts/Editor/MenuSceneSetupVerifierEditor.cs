using UnityEditor;
using UnityEngine;
using VRStrokeRehab.MenuScene;

[CustomEditor(typeof(MenuSceneSetupVerifier))]
public class MenuSceneSetupVerifierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Validate Menu Scene Setup"))
        {
            var verifier = (MenuSceneSetupVerifier)target;
            verifier.ValidateAndLog();
        }

        if (GUILayout.Button("Open Auto Setup Tool"))
        {
            MenuSceneAutoSetupWindow.OpenAndFocus();
        }
    }
}
