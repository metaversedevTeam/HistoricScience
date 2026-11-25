using UnityEngine;

// 거리 안개 색을 지금 시간의 지평선 하늘 색에 시간에 따른 밝기 보정을 더한 값으로 맞추는 컴포넌트
// 안개 색 = 하늘 색 + (-0.5 * cos(2π * 현재 시간 / 하루 길이)) * (1, 1, 1) * 50 (0~255 단위)
// 자정에 -25/255로 가장 어둡고, 정오에 +25/255로 가장 밝으며, 자정을 지날 때도 끊기지 않고 부드럽게 이어진다
[RequireComponent(typeof(DistanceFogController))]
public class SkyFogColor : MonoBehaviour
{
    // 0~255 단위 색 값 50을 Unity 색(0~1) 단위로 바꾼 밝기 보정 배율
    private const float BrightnessOffsetScale = 50f / 255f;

    [Tooltip("하늘 색과 현재 시간을 읽어 올 컴포넌트. 비워 두면 씬에서 찾는다")]
    [SerializeField] private SkyboxTimeOfDay _skyTime;

    private DistanceFogController _fogController;

    private void Awake()
    {
        _fogController = GetComponent<DistanceFogController>();

        if (_skyTime == null)
        {
            _skyTime = FindFirstObjectByType<SkyboxTimeOfDay>();
        }
    }

    private void LateUpdate()
    {
        // SkyboxTimeOfDay.Update에서 시간이 흐른 뒤에 색을 맞춘다
        HandleFogColor();
    }

    // 지금 시간의 하늘 색과 밝기 보정으로 안개 색을 계산해 적용한다
    private void HandleFogColor()
    {
        if (_skyTime == null || !_skyTime.TryGetHorizonSkyColor(out Color skyColor))
        {
            return;
        }

        // 현재 시간 / 하루 길이 = 하루 진행도(0~1). 코사인이라 진행도가 1에서 0으로 돌아가도 값이 이어진다 (-0.5 ~ +0.5)
        float offset = -0.5f * Mathf.Cos(_skyTime.TimeOfDay * Mathf.PI * 2f) * BrightnessOffsetScale;
        _fogController.FogColor = new Color(
            Mathf.Clamp01(skyColor.r + offset),
            Mathf.Clamp01(skyColor.g + offset),
            Mathf.Clamp01(skyColor.b + offset),
            1f);
    }
}
