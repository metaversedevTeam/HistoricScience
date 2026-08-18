using UnityEngine;

// 자신이 놓인 위치의 바이옴 데이터에서 바닥 머티리얼(BottomMaterial)을 받아와 연결된 MeshRenderer에 적용하는 컴포넌트.
// 맵 데이터와 월드 → 맵 좌표 변환은 MapSaveUtil이 단독으로 관리하므로, 발밑 청크를 찾을 필요 없이 자신의 XZ 위치만으로 바이옴을 판정한다.
// 자동 적용은 Start에서 이뤄지므로, 이 오브젝트를 소환하는 쪽은 소환한 프레임 안에서 최종 XZ 위치까지 잡아 주어야 한다.
// (Instantiate 직후 위치를 넣지 않고 다음 프레임으로 미루면, 프리팹 원본 자리의 바이옴을 읽어 엉뚱한 머티리얼이 적용된다.)
public class BottomMaterialController : MonoBehaviour
{
    // 바이옴의 바닥 머티리얼을 적용할 대상 렌더러
    [SerializeField] private MeshRenderer _bottomRenderer;
    // 첫 프레임에 자동으로 바닥 머티리얼을 적용할지 여부
    [SerializeField] private bool _applyOnStart = true;

    // 소환한 쪽이 위치를 잡아 준 뒤에 자동으로 바닥 머티리얼을 적용한다.
    // OnEnable은 Instantiate 안에서 곧바로 실행되어 저장 파일에서 복원되는 오브젝트가 아직 프리팹 원본 자리에 있으므로, 위치 지정이 끝난 뒤 도는 Start를 쓴다.
    private void Start()
    {
        if (_applyOnStart)
            ApplyBottomMaterial();
    }

    // 자신의 위치에 해당하는 바이옴의 BottomMaterial을 연결된 렌더러에 적용한다. 원하는 임의의 타이밍에 호출해 갱신할 수 있다.
    // 바이옴을 찾지 못했거나 그 바이옴에 바닥 머티리얼이 지정되지 않았으면 머티리얼을 바꾸지 않고 false를 반환한다.
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

    // 자신의 XZ 위치를 맵 좌표로 바꿔 그 자리의 바이옴을 찾는다. 맵 데이터가 아직 만들어지지 않았으면 null을 반환한다.
    private MapBiome HandleFindBiome()
    {
        MapSaveUtil mapSaveUtil = MapSaveUtil.Instance;
        if (mapSaveUtil == null)
        {
            Debug.LogWarning("[BottomMaterialController] 씬에 MapSaveUtil이 없어 바이옴을 찾을 수 없습니다.", this);
            return null;
        }

        MapData mapData = mapSaveUtil.CurrentMapData;
        if (mapData == null)
            return null;

        return mapData.GetBiome(mapSaveUtil.WorldToMapPosition(transform.position));
    }
}
