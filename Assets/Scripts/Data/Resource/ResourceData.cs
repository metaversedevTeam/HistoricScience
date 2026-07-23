using System.Collections.Generic;
using UnityEngine;

public abstract class ResourceData : ScriptableObject
{
    public string Nmae => _name;
    [SerializeField] private string _name;

    public Sprite IconSprite => _iconSprite;
    [SerializeField] private Sprite _iconSprite;

    public int Id => _id;
    [SerializeField, HideInInspector] private int _id = -1;

    // 조합 레시피의 점유 칸 목록(좌표+재료). 비어 있으면 조합 대상이 아니다.
    public IReadOnlyList<RecipeCell> Recipe => _recipe;
    [SerializeField] private List<RecipeCell> _recipe = new();

    // 격자 저작 편의를 위한 에디터 전용 힌트(그릴 격자 크기). 런타임 로직에는 사용하지 않는다.
    [SerializeField, HideInInspector] private Vector2Int _editorGridSize = new(3, 3);

    // 조합 가능한(점유 칸이 있는) 레시피를 가졌는지 여부를 반환한다.
    public bool HasRecipe => _recipe != null && _recipe.Count > 0;

    // 레시피의 점유 칸들을 정규화된 조합 패턴으로 변환한다.
    public CraftingPattern ToPattern()
    {
        var cells = new List<(Vector2Int coord, ResourceData item)>();
        if (_recipe != null)
        {
            foreach (var cell in _recipe)
            {
                if (cell != null && cell.Item != null)
                    cells.Add((cell.Coord, cell.Item));
            }
        }
        return CraftingPattern.FromCells(cells);
    }
}

// 레시피 한 칸: 격자 좌표(x=열, y=행)와 그 칸에 놓이는 재료.
[System.Serializable]
public class RecipeCell
{
    public Vector2Int Coord;
    public ResourceData Item;
}
