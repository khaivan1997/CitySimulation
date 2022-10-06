using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
[CustomEditor(typeof(IntersectionManager))]
public class IntersectionManagerEditor : Editor 
{
    // Start is called before the first frame update
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        IntersectionManager x = (IntersectionManager)target;
        EditorGUILayout.HelpBox("will stop any automatic cycle" ,MessageType.Info);
        if (GUILayout.Button("change way"))
        {
            x.ChangeWayEditor(-1) ;
        }
        if (GUILayout.Button("all red"))
        {
            x.AllRed();
        }
        if (GUILayout.Button("all green"))
        {
            x.AllGreen();
        }
        EditorGUILayout.HelpBox("start/stop automatic cycle", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("start automatic cycle"))
        {
            x.StartAutoCycle();
        }
        if (GUILayout.Button("stop automatic cycle"))
        {
            x.StopAutoCycle();
        }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("reset intersection light materials"))
        {
            x.ResetAllMaterials();
        }
    }
}

[CustomEditor(typeof(IntersectionEditorUtilities))]
public class IntersectionUtilitiesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("reset all lights materials"))
        {
            IntersectionEditorUtilities x = (IntersectionEditorUtilities)target;
            x.ResetAllMaterials();
        }
    }
}

#endif