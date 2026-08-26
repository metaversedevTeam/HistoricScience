// 완료한 연구들에서 같은 종류끼리 합산된 보너스 하나. ResearchManager가 목록으로 제공한다.
public readonly struct ResearchBonusTotal
{
    // 어떤 종류의 보너스인지
    public readonly ResearchBonusData Bonus;

    // 그 종류로 합산된 값
    public readonly float Value;

    public ResearchBonusTotal(ResearchBonusData bonus, float value)
    {
        Bonus = bonus;
        Value = value;
    }
}
