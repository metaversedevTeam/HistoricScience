using UnityEngine;

// 바다 평면 오브젝트의 높이(월드 Y)를 지형 설정의 해수면 높이에 맞추는 컴포넌트. 걷기 판정에 쓰는 해수면과 화면에 보이는 수면이 어긋나지 않게 한다.
public class SeaLevelPlane : MonoBehaviour
{
    // 해수면 높이를 읽어 올 지형 설정
    [SerializeField] private MapTerrainSettings m_TerrainSettings;

    // 런타임 시작 시 수면 높이를 설정값에 맞춘다.
    private void Awake()
    {
        HandleApplySeaLevel();
    }

#if UNITY_EDITOR
    // 에디터에서 설정을 연결하거나 바꾸면 다음 에디터 갱신 때 수면 높이를 맞춘다. OnValidate 안에서 바로 트랜스폼을 바꾸면 경고가 나므로 한 프레임 미룬다.
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += HandleApplySeaLevelDelayed;
    }

    // 미뤄 둔 수면 높이 적용을 실행한다. 그사이 오브젝트가 파괴되었으면 건너뛴다.
    private void HandleApplySeaLevelDelayed()
    {
        if (this == null)
            return;

        HandleApplySeaLevel();
    }
#endif

    // 오브젝트의 X/Z는 그대로 두고 월드 Y만 해수면 높이로 옮긴다.
    private void HandleApplySeaLevel()
    {
        if (m_TerrainSettings == null)
        {
            Debug.LogError("SeaLevelPlane: TerrainSettings가 지정되지 않아 수면 높이를 맞출 수 없습니다.", this);
            return;
        }

        Vector3 position = transform.position;
        if (Mathf.Approximately(position.y, m_TerrainSettings.SeaLevel))
            return;

        position.y = m_TerrainSettings.SeaLevel;
        transform.position = position;
    }
}
