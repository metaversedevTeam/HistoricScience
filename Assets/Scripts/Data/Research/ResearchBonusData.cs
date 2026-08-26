using UnityEngine;

// 연구로 얻는 보너스 한 종류(이동 속도, 채집 속도 등)를 정의하는 스크립터블 오브젝트.
// 값을 실제로 쓰는 쪽은 이 에셋을 키로 ResearchManager에서 합계를 조회한다.
[CreateAssetMenu(fileName = "연구 보너스", menuName = "스크립터블 오브젝트/연구/연구 보너스", order = int.MinValue + 2)]
public class ResearchBonusData : ScriptableObject
{
    // 연구 효과 문구에 표시할 보너스 이름 (예: 시민 이동 속도)
    [SerializeField] private string _bonusName;
    // 이 보너스가 무엇을 바꾸는지에 대한 설명. 인스펙터에서 고를 때 참고용이다.
    [SerializeField, TextArea(2, 3)] private string _description;
    // 값을 문구로 표기하는 방식
    [SerializeField] private ResearchBonusValueKind _valueKind = ResearchBonusValueKind.Percent;

    public string BonusName => _bonusName;
    public string Description => _description;
    public ResearchBonusValueKind ValueKind => _valueKind;

    // 합산된 값을 연구 효과 문구로 바꾼다. (예: 0.15 -> "시민 이동 속도 +15%")
    public string Format(float value)
    {
        string amount = _valueKind switch
        {
            ResearchBonusValueKind.Percent => $"{value * 100f:+0.#;-0.#;0}%",
            _ => $"{value:+0.##;-0.##;0}"
        };

        return $"{_bonusName} {amount}";
    }
}

// 연구 보너스 값을 문구로 표기하는 방식
public enum ResearchBonusValueKind
{
    // 비율. 0.15를 넣으면 +15%로 표시되고, 쓰는 쪽은 보통 (1 + 합계)를 곱해서 쓴다.
    Percent,

    // 고정 수치. 넣은 값이 그대로 더해진다.
    Flat,
}

// 연구 하나가 주는 보너스 한 줄(보너스 종류·값)을 인스펙터에서 지정하기 위한 직렬화 항목
[System.Serializable]
public struct ResearchBonusEntry
{
    public ResearchBonusData Bonus;
    public float Value;
}
