// 마우스를 올렸을 때 아이템 툴팁으로 보여 줄 아이템을 알려 주는 UI 칸 (작업대 보유 칸·조합 칸 등)
public interface IItemHoverSource
{
    // 지금 이 칸이 보여 주고 있는 아이템. 빈 칸이면 null.
    ItemData Item { get; }
}
