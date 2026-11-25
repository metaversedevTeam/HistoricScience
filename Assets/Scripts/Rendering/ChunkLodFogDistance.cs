using UnityEngine;

// 청크 LOD 로딩 범위에 맞춰 거리 안개의 시작·끝 거리를 정하는 컴포넌트
// 시작 거리 = LOD0 최소 거리, 끝 거리 = LOD1 최소 거리. 최소 거리는 추적 대상이 자기 청크 안 어디에 있든 그 LOD 이상으로 로딩된 청크가 보장되는 가장 짧은 거리다
// 설정 화면에서 로딩 범위를 바꾸면 다음 프레임에 안개 거리도 함께 바뀐다
[RequireComponent(typeof(DistanceFogController))]
public class ChunkLodFogDistance : MonoBehaviour
{
    [Tooltip("청크 한 변의 월드 크기를 읽어 올 지형 설정")]
    [SerializeField] private MapTerrainSettings _terrainSettings;

    private DistanceFogController _fogController;

    // 마지막으로 안개 거리에 반영한 값. 바뀌었을 때만 다시 적용한다
    private int _appliedLod0Radius = -1;
    private int _appliedLod1Radius = -1;
    private float _appliedChunkSize = -1f;

    private void Awake()
    {
        _fogController = GetComponent<DistanceFogController>();
    }

    private void OnEnable()
    {
        // 꺼져 있는 동안 바뀐 값이 있어도 켜질 때 반드시 다시 적용한다
        _appliedLod0Radius = -1;
        HandleLodRadiusChange();
    }

    private void Update()
    {
        HandleLodRadiusChange();
    }

    // 로딩 범위나 청크 크기가 바뀌었으면 안개 시작·끝 거리를 다시 계산해 적용한다
    private void HandleLodRadiusChange()
    {
        if (_terrainSettings == null)
        {
            return;
        }

        int lod0Radius = GameSettings.ChunkLod0LoadRadius;
        int lod1Radius = GameSettings.ChunkLod1LoadRadius;
        float chunkSize = _terrainSettings.ChunkWorldSize;

        if (lod0Radius == _appliedLod0Radius && lod1Radius == _appliedLod1Radius && Mathf.Approximately(chunkSize, _appliedChunkSize))
        {
            return;
        }

        _appliedLod0Radius = lod0Radius;
        _appliedLod1Radius = lod1Radius;
        _appliedChunkSize = chunkSize;

        float startDistance = GetGuaranteedRadius(lod0Radius) * chunkSize;
        float endDistance = GetGuaranteedRadius(lod1Radius) * chunkSize;
        _fogController.SetDistances(startDistance, endDistance);
    }

    // 로딩 반경(청크 단위) 안의 청크만 로딩될 때, 추적 대상이 자기 청크 안 어디에 있든 로딩 범위 밖 청크까지 남는 가장 짧은 거리(청크 단위)를 구한다
    // MapChunkLoader처럼 청크 좌표 차이의 길이가 반경 이하인 청크를 로딩한다고 보고, 대상 청크와 로딩되지 않는 청크 사이의 가장 가까운 간격을 찾는다
    public static float GetGuaranteedRadius(int loadRadius)
    {
        int radiusSqr = loadRadius * loadRadius;
        float closestGap = float.MaxValue;

        // 대칭이므로 한 사분면만 본다. (반경 + 1, 0) 청크는 항상 범위 밖이라 그보다 먼 청크는 간격이 더 짧을 수 없다
        int limit = loadRadius + 1;
        for (int y = 0; y <= limit; y++)
        {
            for (int x = 0; x <= limit; x++)
            {
                if (x * x + y * y <= radiusSqr)
                {
                    continue;
                }

                // 대상 청크(한 변 1)와 (x, y) 청크 사이의 축별 빈 간격
                int gapX = Mathf.Max(x - 1, 0);
                int gapY = Mathf.Max(y - 1, 0);
                closestGap = Mathf.Min(closestGap, Mathf.Sqrt(gapX * gapX + gapY * gapY));
            }
        }

        return closestGap;
    }
}
