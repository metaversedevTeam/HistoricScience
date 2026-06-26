using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ResourceData), true)]
public class ResourceDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var idProp = serializedObject.FindProperty("_id");
        int id = idProp.intValue;

        GUI.enabled = false;
        EditorGUILayout.IntField("ID", id == -1 ? 0 : id);
        GUI.enabled = true;

        if(id == -1)
        {
            EditorGUILayout.HelpBox("ID 미할당 — ItemDataList에서 '목록 갱신'을 실행하세요.", MessageType.Warning);
        }

        GUILayout.Space(4);
        DrawDefaultInspector();
    }
}
