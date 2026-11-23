using UnityEngine;
using UnityEngine.EventSystems;

// 마우스를 올리면 UI를 부드럽게 살짝 키우고, 벗어나면 원래 크기로 되돌리는 연출.
// 게임 일시정지와 무관하게 돌도록 실제 경과 시간을 쓴다.
public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // 마우스를 올렸을 때의 크기(원래 크기 대비 비율)
    [SerializeField, Min(0f)] private float _hoverScale = 1.08f;

    // 목표 크기를 따라가는 속도(클수록 빠르다)
    [SerializeField, Min(0.1f)] private float _response = 14f;

    // 원래 크기
    private Vector3 _baseScale;

    // 마우스가 올라와 있는지 여부
    private bool _isHovered;

    // 지금 커진 정도(0~1)
    private float _amount;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        _isHovered = false;
        _amount = 0f;
        transform.localScale = _baseScale;
    }

    private void Update()
    {
        HandleScale();
    }

    // 마우스가 올라오면 키운다.
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
    }

    // 마우스가 벗어나면 원래 크기로 되돌린다.
    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
    }

    // 목표 크기를 향해 부드럽게 따라간다.
    private void HandleScale()
    {
        float k = 1f - Mathf.Exp(-_response * Time.unscaledDeltaTime);
        _amount = Mathf.Lerp(_amount, _isHovered ? 1f : 0f, k);
        transform.localScale = _baseScale * Mathf.Lerp(1f, _hoverScale, _amount);
    }
}
