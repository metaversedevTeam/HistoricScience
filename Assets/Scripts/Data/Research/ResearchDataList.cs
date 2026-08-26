using System.Collections.Generic;
using UnityEngine;

// 연구 UI가 나열할 연구 목록. 목록에 넣은 순서가 그대로 "No. 001" 번호와 카드 순서가 된다.
// 맵 파일에서 연구 기록을 복원할 때 식별자로 연구를 되찾는 데에도 쓴다.
[CreateAssetMenu(fileName = "연구 목록", menuName = "스크립터블 오브젝트/연구/연구 목록", order = int.MinValue + 1)]
public class ResearchDataList : ScriptableObject
{
    [SerializeField] private List<ResearchData> _researches = new();

    public IReadOnlyList<ResearchData> Researches => _researches;

    // 식별자로 연구를 찾는다. 목록에 없는 식별자면 null을 반환한다.
    public ResearchData Find(string researchId)
    {
        if (string.IsNullOrEmpty(researchId)) return null;

        foreach (ResearchData research in _researches)
        {
            if (research != null && research.ResearchId == researchId)
                return research;
        }

        return null;
    }
}
