using System.Collections.Generic;
using UnityEngine;

// 격자에 놓인 재료들의 상대 배치를 좌상단 기준으로 정규화해 담고, 다른 배치와의 일치를 판정하는 패턴.
// 제작법(레시피)과 작업대에 실제로 배치된 모습을 같은 타입으로 표현한다. 점유된 칸만 저장하므로 격자 크기·모양(3x3, 4x4, 십자 등)과 무관하게 동작한다.
public class CraftingPattern
{
    // 정규화된(좌상단 기준) 좌표 -> 재료. 점유된 칸만 포함한다.
    private readonly Dictionary<Vector2Int, ResourceData> _cells;

    private CraftingPattern(Dictionary<Vector2Int, ResourceData> cells)
    {
        _cells = cells;

        int width = 0;
        int height = 0;
        foreach (var coord in cells.Keys)
        {
            if (coord.x + 1 > width) width = coord.x + 1;
            if (coord.y + 1 > height) height = coord.y + 1;
        }
        Size = new Vector2Int(width, height);
    }

    // 점유된 칸이 하나도 없는지 여부.
    public bool IsEmpty => _cells.Count == 0;

    // 정규화된 좌표 -> 재료. 점유된 칸만 들어 있다. (힌트 UI처럼 배치 자체를 그려야 하는 쪽이 읽는다)
    public IReadOnlyDictionary<Vector2Int, ResourceData> Cells => _cells;

    // 점유된 칸을 모두 감싸는 격자 크기(x=열 수, y=행 수). 빈 패턴이면 (0, 0)이다.
    public Vector2Int Size { get; }

    // 점유된 (좌표, 재료) 목록을 좌상단 기준으로 정규화해 패턴을 생성한다. 재료가 null인 칸은 무시한다.
    public static CraftingPattern FromCells(IEnumerable<(Vector2Int coord, ResourceData item)> cells)
    {
        var occupied = new List<(Vector2Int coord, ResourceData item)>();
        foreach (var cell in cells)
        {
            if (cell.item != null)
                occupied.Add(cell);
        }

        var normalized = new Dictionary<Vector2Int, ResourceData>();
        if (occupied.Count == 0)
            return new CraftingPattern(normalized);

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        foreach (var cell in occupied)
        {
            if (cell.coord.x < minX) minX = cell.coord.x;
            if (cell.coord.y < minY) minY = cell.coord.y;
        }

        var offset = new Vector2Int(minX, minY);
        foreach (var cell in occupied)
            normalized[cell.coord - offset] = cell.item;

        return new CraftingPattern(normalized);
    }

    // 다른 패턴과 정규화된 배치·재료가 정확히 일치하는지 판정한다(대칭·회전 비교 없음).
    public bool Matches(CraftingPattern other)
    {
        if (other == null || _cells.Count != other._cells.Count)
            return false;

        foreach (var pair in _cells)
        {
            if (!other._cells.TryGetValue(pair.Key, out var item) || item != pair.Value)
                return false;
        }
        return true;
    }

    // 이 패턴이 소비하는 재료별 수량을 집계한다.
    public IReadOnlyDictionary<ResourceData, int> CountItems()
    {
        var counts = new Dictionary<ResourceData, int>();
        foreach (var pair in _cells)
        {
            counts.TryGetValue(pair.Value, out var current);
            counts[pair.Value] = current + 1;
        }
        return counts;
    }
}
