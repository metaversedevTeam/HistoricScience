using HistoricScience.Test;
using UnityEngine;

// 자신이 놓인 위치의 바이옴 데이터에서 바닥 머티리얼(BottomMaterial)을 받아와 연결된 MeshRenderer에 적용하는 컴포넌트.
// 청크 터레인이 여러 개라 Terrain.activeTerrain은 위치에 따라 다른 청크를 가리킬 수 있으므로, GroundSnapper와 같이 아래로 레이캐스트해 자신이 선 청크를 찾는다.
public class BottomMaterialController : MonoBehaviour
{
    // 바이옴의 바닥 머티리얼을 적용할 대상 렌더러
    [SerializeField] private MeshRenderer _bottomRenderer;
    // 레이캐스트로 감지할 지면 레이어. 비워두면 Awake에서 "Ground" 레이어를 자동으로 찾는다.
    [SerializeField] private LayerMask _groundLayer;
    // 활성화될 때(오브젝트 배치 시점) 자동으로 바닥 머티리얼을 적용할지 여부
    [SerializeField] private bool _applyOnEnable = true;
    // 자신의 위치 기준 이 높이(m)만큼 위에서부터 아래로 레이를 쏜다. 터레인 최대 높이보다 충분히 커야 한다.
    [SerializeField, Min(0f)] private float _raycastUpDistance = 500f;
    // 레이캐스트 총 길이(m). 위 시작 높이를 지나 터레인 아래까지 닿을 만큼 충분히 커야 한다.
    [SerializeField, Min(0f)] private float _raycastLength = 1000f;

    // Ground 레이어가 미설정된 경우 자동으로 찾아 할당
    private void Awake()
    {
        if (_groundLayer.value == 0)
        {
            int idx = LayerMask.NameToLayer("Ground");
            if (idx >= 0)
                _groundLayer = 1 << idx;
            else
                Debug.LogWarning("[BottomMaterialController] 'Ground' 레이어를 찾을 수 없습니다. Inspector에서 직접 설정해주세요.");
        }
    }

    // 활성화 시(오브젝트가 배치되는 시점) 자동으로 바닥 머티리얼을 적용한다
    private void OnEnable()
    {
        if (_applyOnEnable)
            ApplyBottomMaterial();
    }

    // 자신의 위치에 해당하는 바이옴의 BottomMaterial을 연결된 렌더러에 적용한다. 원하는 임의의 타이밍에 호출해 갱신할 수 있다.
    // 지면 청크를 찾지 못했거나 그 바이옴에 바닥 머티리얼이 지정되지 않았으면 머티리얼을 바꾸지 않고 false를 반환한다.
    public bool ApplyBottomMaterial()
    {
        if (_bottomRenderer == null)
        {
            Debug.LogWarning("[BottomMaterialController] 바닥 MeshRenderer가 연결되지 않았습니다. Inspector에서 직접 설정해주세요.");
            return false;
        }

        MapBiome biome = HandleFindBiome();
        if (biome == null || biome.BottomMaterial == null)
            return false;

        // 머티리얼 에셋을 그대로 참조시켜, 렌더러마다 불필요한 머티리얼 인스턴스가 생기지 않도록 한다.
        _bottomRenderer.sharedMaterial = biome.BottomMaterial;
        return true;
    }

    // 자신의 XZ 위치 아래로 레이캐스트해, 부딪힌 지점을 관리하는 청크의 맵 데이터에서 그 자리의 바이옴을 찾는다. 찾지 못하면 null을 반환한다.
    private MapBiome HandleFindBiome()
    {
        Vector3 origin = transform.position + Vector3.up * _raycastUpDistance;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _raycastLength, _groundLayer))
            return null;

        // 콜라이더가 자식에 달린 경우가 있어 부모까지 거슬러 올라가 청크의 TerrainPainter를 찾는다.
        TerrainPainter terrainPainter = hit.collider.GetComponentInParent<TerrainPainter>();
        if (terrainPainter == null || terrainPainter.CurrentMapData == null)
            return null;

        return terrainPainter.CurrentMapData.GetBiome(terrainPainter.WorldToMapPosition(hit.point));
    }
}
