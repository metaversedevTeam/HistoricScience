using System.Collections.Generic;
using UnityEngine;

// 연구 한 건의 데이터(이름, 썸네일, 연구 보너스, 시대 제한, 선행 연구, 비용)를 담는 스크립터블 오브젝트
[CreateAssetMenu(fileName = "연구", menuName = "스크립터블 오브젝트/연구/연구", order = int.MinValue)]
public class ResearchData : ScriptableObject
{
    // 맵 파일에 연구 완료 여부를 기록할 때 쓰는 고정 식별자. 비워 두면 에셋 이름을 쓰지만,
    // 에셋 이름을 바꾸면 저장된 기록과 어긋나므로 되도록 직접 지정한다.
    [SerializeField] private string _researchId;
    // 목록과 상세 패널에 표시할 연구 이름
    [SerializeField] private string _researchName;
    // 카드와 상세 패널의 썸네일로 쓸 이미지
    [SerializeField] private Sprite _thumbnail;
    // 상세 패널 효과 상자 아래에 표시할 배경 설명
    [SerializeField, TextArea(2, 4)] private string _description;

    [Header("연구 보너스")]
    // 이 연구를 끝냈을 때 얻는 보너스 목록. 인스펙터에서 종류와 값을 짝지어 여러 개 넣을 수 있고,
    // 완료된 연구들의 같은 종류끼리는 ResearchManager가 합산한다.
    [SerializeField] private List<ResearchBonusEntry> _bonuses = new();

    [Header("시대 제한")]
    // 이 연구를 시작할 수 있게 되는 최소 시대. 현재 시대가 이보다 앞서면 잠긴 카드로 표시된다.
    [SerializeField] private Age _requiredAge = Age.Paleolithic;

    [Header("조건")]
    // 이 연구보다 먼저 끝내야 하는 연구 목록. 비워 두면 시대 제한만 적용된다.
    [SerializeField] private List<ResearchData> _prerequisites = new();
    // 연구할 때 한 번 소모하는 자원 목록
    [SerializeField] private List<ResearchCostEntry> _costs = new();

    // 저장 파일에 쓰는 고정 식별자. 따로 지정하지 않았으면 에셋 이름을 그대로 쓴다.
    public string ResearchId => string.IsNullOrEmpty(_researchId) ? name : _researchId;

    public string ResearchName => _researchName;
    public Sprite Thumbnail => _thumbnail;
    public string Description => _description;
    public IReadOnlyList<ResearchBonusEntry> Bonuses => _bonuses;
    public Age RequiredAge => _requiredAge;
    public IReadOnlyList<ResearchData> Prerequisites => _prerequisites;
    public IReadOnlyList<ResearchCostEntry> Costs => _costs;
}

// 연구 비용 하나(자원 종류·수량)를 인스펙터에서 지정하기 위한 직렬화 항목
[System.Serializable]
public struct ResearchCostEntry
{
    public ResourceData Resource;
    public int Count;
}
