using System;
using UnityEngine;

// 바이옴 하나에 출력할 풀 종류와 그 바이옴에서의 출력 밀도를 묶은 항목. MapBiome의 풀 목록에 등록해 쓴다.
[Serializable]
public class BiomeGrassEntry
{
    // 이 바이옴에 출력할 풀 종류
    [SerializeField] private GrassType m_GrassType;
    // 이 바이옴에서의 출력 밀도(㎡당 개수). 군락이 덮는 자리에서의 최대 밀도이며, 바이옴 경계에서는 알파맵 가중치만큼 줄어든다.
    [SerializeField, Min(0f)] private float m_Density = 1f;

    public GrassType GrassType => m_GrassType;
    public float Density => m_Density;
}
