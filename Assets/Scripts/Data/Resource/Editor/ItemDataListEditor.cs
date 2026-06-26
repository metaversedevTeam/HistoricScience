using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemDataList))]
public class ItemDataListEditor : Editor
{
    private const string ItemFolderPath = "Assets/Data/ScriptableObjects/자원/아이템";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);
        if (GUILayout.Button("목록 갱신"))
            AssignNewIds((ItemDataList)target);

        GUILayout.Space(4);
        GUI.color = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("ID 초기화 (전체 리셋)"))
        {
            if (EditorUtility.DisplayDialog("ID 초기화", "모든 아이템의 ID가 초기화됩니다. 계속하시겠습니까?", "초기화", "취소"))
                ResetAllIds((ItemDataList)target);
        }
        GUI.color = Color.white;
    }

    // 폴더를 스캔해 _items 목록만 갱신 (ID는 건드리지 않음)
    public static void RefreshList(ItemDataList list)
    {
        var soList = new SerializedObject(list);
        var itemsProp = soList.FindProperty("_items");
        var items = FindAllItemData();

        itemsProp.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
            itemsProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];

        soList.ApplyModifiedProperties();
        EditorUtility.SetDirty(list);
        AssetDatabase.SaveAssets();

        Debug.Log($"[ItemDataList] 목록 갱신 완료: {items.Count}개");
    }

    // 목록을 갱신하고 ID가 없는(-1) 아이템에만 새 ID를 부여
    public static void AssignNewIds(ItemDataList list)
    {
        RefreshList(list);

        var soList = new SerializedObject(list);
        var nextIdProp = soList.FindProperty("_nextId");
        int assignCount = 0;

        foreach (var item in FindAllItemData())
        {
            var soItem = new SerializedObject(item);
            var idProp = soItem.FindProperty("_id");
            if (idProp.intValue != -1) continue;

            idProp.intValue = nextIdProp.intValue++;
            soItem.ApplyModifiedProperties();
            EditorUtility.SetDirty(item);
            assignCount++;
        }

        soList.ApplyModifiedProperties();
        EditorUtility.SetDirty(list);
        AssetDatabase.SaveAssets();

        Debug.Log($"[ItemDataList] {assignCount}개 아이템에 ID 부여 완료 (다음 ID: {nextIdProp.intValue})");
    }

    // 모든 아이템 ID를 -1로 초기화하고 _nextId를 1로 리셋
    public static void ResetAllIds(ItemDataList list)
    {
        foreach (var item in FindAllItemData())
        {
            var soItem = new SerializedObject(item);
            soItem.FindProperty("_id").intValue = -1;
            soItem.ApplyModifiedProperties();
            EditorUtility.SetDirty(item);
        }

        var soList = new SerializedObject(list);
        soList.FindProperty("_nextId").intValue = 1;
        soList.ApplyModifiedProperties();
        EditorUtility.SetDirty(list);
        AssetDatabase.SaveAssets();

        Debug.Log("[ItemDataList] 모든 ID 초기화 완료");
    }

    private static List<ItemData> FindAllItemData()
    {
        var items = new List<ItemData>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData", new[] { ItemFolderPath }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null) items.Add(item);
        }
        return items;
    }
}
