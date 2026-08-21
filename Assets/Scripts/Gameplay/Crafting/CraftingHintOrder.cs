using System.Collections.Generic;
using UnityEngine;

// 조합법 힌트가 어떤 칸부터 공개될지를 아이템 ID만으로 결정하는 유틸리티.
// 같은 아이템이면 언제·어디서 계산해도 같은 순서가 나오므로 공개된 칸 목록을 저장할 필요 없이 "몇 번 받았는지"만 저장하면 된다.
public static class CraftingHintOrder
{
    // 아이템의 조합법에서 힌트로 공개될 칸들을 공개 순서대로 반환한다. 조합법이 없으면 빈 목록이다.
    public static IReadOnlyList<Vector2Int> GetRevealOrder(ItemData item)
    {
        var order = new List<Vector2Int>();
        if (item == null || !item.HasRecipe) return order;

        foreach (var coord in item.ToPattern().Cells.Keys)
            order.Add(coord);

        // 인스펙터의 레시피 저작 순서가 바뀌어도 힌트 순서가 흔들리지 않도록, 셔플 전에 행 → 열 순으로 정렬해 기준을 고정한다.
        order.Sort(CompareCoord);
        Shuffle(order, item.Id);
        return order;
    }

    // 아이템 ID를 씨앗으로 한 피셔-예이츠 셔플. 플랫폼·런타임 버전과 무관하게 같은 결과를 내도록 난수도 직접 굴린다.
    private static void Shuffle(List<Vector2Int> order, int id)
    {
        uint state = MakeSeed(id);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = (int)(NextRandom(ref state) % (uint)(i + 1));
            (order[i], order[j]) = (order[j], order[i]);
        }
    }

    // 아이템 ID를 잘 흩어진 0이 아닌 난수 씨앗으로 바꾼다. (xorshift는 상태가 0이면 계속 0만 내놓는다)
    private static uint MakeSeed(int id)
    {
        uint seed = (uint)id * 2654435761u + 2166136261u;
        return seed == 0u ? 1u : seed;
    }

    // xorshift32 난수 한 개를 뽑고 상태를 갱신한다.
    private static uint NextRandom(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }

    // 두 격자 좌표를 행(y) → 열(x) 순으로 비교한다.
    private static int CompareCoord(Vector2Int a, Vector2Int b)
    {
        if (a.y != b.y) return a.y.CompareTo(b.y);
        return a.x.CompareTo(b.x);
    }
}
