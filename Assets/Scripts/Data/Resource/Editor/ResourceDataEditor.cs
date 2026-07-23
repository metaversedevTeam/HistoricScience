using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ResourceData), true)]
public class ResourceDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var idProp = serializedObject.FindProperty("_id");
        int id = idProp.intValue;

        GUI.enabled = false;
        EditorGUILayout.IntField("ID", id == -1 ? 0 : id);
        GUI.enabled = true;

        if (id == -1)
        {
            EditorGUILayout.HelpBox("ID 미할당 — ItemDataList에서 '목록 갱신'을 실행하세요.", MessageType.Warning);
        }

        GUILayout.Space(4);
        // _recipe는 아래에서 격자 형태로 직접 그리므로 기본 인스펙터에서는 제외한다.
        DrawPropertiesExcluding(serializedObject, "_recipe", "_id", "_editorGridSize");

        DrawRecipeGrid();
        DrawRecipePreview();

        serializedObject.ApplyModifiedProperties();
    }

    // 레시피를 크기 조절 가능한 좌표 격자(ObjectField)로 그린다. 빈 칸은 비워 두면 되고 4x4·십자 등 임의 모양을 저작할 수 있다.
    private void DrawRecipeGrid()
    {
        var gridSizeProp = serializedObject.FindProperty("_editorGridSize");
        var recipeProp = serializedObject.FindProperty("_recipe");
        if (gridSizeProp == null || recipeProp == null) return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("조합 레시피 (배치 위치)", EditorStyles.boldLabel);

        var size = gridSizeProp.vector2IntValue;
        EditorGUI.BeginChangeCheck();
        int cols = Mathf.Clamp(EditorGUILayout.IntField("열(가로)", Mathf.Max(1, size.x)), 1, 12);
        int rows = Mathf.Clamp(EditorGUILayout.IntField("행(세로)", Mathf.Max(1, size.y)), 1, 12);
        if (EditorGUI.EndChangeCheck())
            gridSizeProp.vector2IntValue = new Vector2Int(cols, rows);

        var indexByCoord = BuildCoordIndex(recipeProp);

        // 이번 프레임에 변경된 칸을 모아 두었다가 그리기가 끝난 뒤 한 번만 반영한다(순회 중 배열 변형 방지).
        bool hasChange = false;
        var changedCoord = Vector2Int.zero;
        ResourceData changedItem = null;

        EditorGUILayout.Space(2);
        for (int y = 0; y < rows; y++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < cols; x++)
            {
                var coord = new Vector2Int(x, y);
                ResourceData current = null;
                if (indexByCoord.TryGetValue(coord, out var idx))
                    current = recipeProp.GetArrayElementAtIndex(idx).FindPropertyRelative("Item").objectReferenceValue as ResourceData;

                var next = (ResourceData)EditorGUILayout.ObjectField(
                    current, typeof(ResourceData), false, GUILayout.Width(72), GUILayout.Height(24));

                if (next != current)
                {
                    hasChange = true;
                    changedCoord = coord;
                    changedItem = next;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (hasChange)
            SetCell(recipeProp, indexByCoord, changedCoord, changedItem);
    }

    // 레시피 배열을 좌표->인덱스 맵으로 변환한다.
    private Dictionary<Vector2Int, int> BuildCoordIndex(SerializedProperty recipeProp)
    {
        var map = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < recipeProp.arraySize; i++)
        {
            var coord = recipeProp.GetArrayElementAtIndex(i).FindPropertyRelative("Coord").vector2IntValue;
            map[coord] = i;
        }
        return map;
    }

    // 지정 좌표의 칸을 갱신한다. 재료가 null이면 해당 칸을 제거하고, 새 좌표면 항목을 추가한다.
    private void SetCell(SerializedProperty recipeProp, Dictionary<Vector2Int, int> indexByCoord, Vector2Int coord, ResourceData item)
    {
        if (indexByCoord.TryGetValue(coord, out var idx))
        {
            if (item == null)
                recipeProp.DeleteArrayElementAtIndex(idx);
            else
                recipeProp.GetArrayElementAtIndex(idx).FindPropertyRelative("Item").objectReferenceValue = item;
            return;
        }

        if (item == null) return;

        int newIdx = recipeProp.arraySize;
        recipeProp.InsertArrayElementAtIndex(newIdx);
        var element = recipeProp.GetArrayElementAtIndex(newIdx);
        element.FindPropertyRelative("Coord").vector2IntValue = coord;
        element.FindPropertyRelative("Item").objectReferenceValue = item;
    }

    // 레시피가 있으면 재료 아이콘을 조합법 모양(상대 좌표)대로 미리보기로 그린다.
    private void DrawRecipePreview()
    {
        var recipeProp = serializedObject.FindProperty("_recipe");
        if (recipeProp == null || recipeProp.arraySize == 0) return;

        var cells = new List<(Vector2Int coord, ResourceData item)>();
        for (int i = 0; i < recipeProp.arraySize; i++)
        {
            var element = recipeProp.GetArrayElementAtIndex(i);
            var item = element.FindPropertyRelative("Item").objectReferenceValue as ResourceData;
            if (item == null) continue;
            cells.Add((element.FindPropertyRelative("Coord").vector2IntValue, item));
        }
        if (cells.Count == 0) return;

        // 점유 칸의 바운딩 박스를 구해 상대 좌표로 정규화한다.
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in cells)
        {
            minX = Mathf.Min(minX, c.coord.x);
            minY = Mathf.Min(minY, c.coord.y);
            maxX = Mathf.Max(maxX, c.coord.x);
            maxY = Mathf.Max(maxY, c.coord.y);
        }
        int cols = maxX - minX + 1;
        int rows = maxY - minY + 1;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("조합법 미리보기", EditorStyles.boldLabel);

        const float cell = 42f;
        const float gap = 3f;
        float width = cols * cell + (cols - 1) * gap;
        float height = rows * cell + (rows - 1) * gap;
        var area = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));

        // 바운딩 박스 전체를 옅은 배경으로 그려 빈 칸(모양의 틈)이 드러나게 한다.
        var emptyColor = new Color(0f, 0f, 0f, 0.18f);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                var r = new Rect(area.x + x * (cell + gap), area.y + y * (cell + gap), cell, cell);
                EditorGUI.DrawRect(r, emptyColor);
            }
        }

        // 점유 칸에 재료 아이콘을 그린다(아이콘이 없으면 이름으로 대체).
        var filledColor = new Color(1f, 1f, 1f, 0.08f);
        var nameStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        foreach (var c in cells)
        {
            int gx = c.coord.x - minX;
            int gy = c.coord.y - minY;
            var r = new Rect(area.x + gx * (cell + gap), area.y + gy * (cell + gap), cell, cell);
            EditorGUI.DrawRect(r, filledColor);
            if (c.item.IconSprite != null)
                DrawSprite(r, c.item.IconSprite);
            else
                GUI.Label(r, c.item.Nmae, nameStyle);
        }
    }

    // 스프라이트(아틀라스 하위 스프라이트 포함)를 지정한 사각형에 그린다.
    private void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return;
        var tex = sprite.texture;
        var tr = sprite.rect;
        var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        GUI.DrawTextureWithTexCoords(rect, tex, uv);
    }
}
