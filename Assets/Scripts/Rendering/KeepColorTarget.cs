using System.Collections.Generic;
using UnityEngine;

// 이 오브젝트(와 자식)의 렌더러를 KeepColor 렌더링 레이어에 넣어 흑백 화면에서도 색을 유지시키는 컴포넌트
public class KeepColorTarget : MonoBehaviour
{
    public const string RenderingLayerName = "KeepColor";

    [Tooltip("자식 오브젝트의 렌더러까지 색을 유지할지 여부")]
    [SerializeField] private bool _includeChildren = true;

    // 렌더러별로 KeepColor 레이어를 적용 중인 KeepColorTarget 수. 마지막 하나가 빠질 때만 비트를 지운다
    private static readonly Dictionary<Renderer, int> AppliedCounts = new Dictionary<Renderer, int>();

    private readonly List<Renderer> _renderers = new List<Renderer>();
    private uint _layerBit;

    private void OnEnable()
    {
        int layerIndex = RenderingLayerMask.NameToRenderingLayer(RenderingLayerName);
        if (layerIndex < 0)
        {
            Debug.LogWarning($"렌더링 레이어 '{RenderingLayerName}'가 없습니다. Project Settings > Tags and Layers에서 추가하세요.", this);
            return;
        }

        _layerBit = 1u << layerIndex;
        Refresh();
    }

    private void OnDisable()
    {
        RemoveLayer();
    }

    // 렌더러 목록을 다시 모아 KeepColor 레이어를 적용한다. 자식이 새로 생기면 호출한다
    public void Refresh()
    {
        if (_layerBit == 0)
        {
            return;
        }

        RemoveLayer();
        CollectRenderers();
        ApplyLayer();
    }

    // 모은 렌더러에 KeepColor 레이어를 적용하고 적용 횟수를 기록한다
    private void ApplyLayer()
    {
        for (int i = _renderers.Count - 1; i >= 0; i--)
        {
            Renderer target = _renderers[i];
            if (AppliedCounts.TryGetValue(target, out int count))
            {
                AppliedCounts[target] = count + 1;
                continue;
            }

            // 인스펙터 등에서 이미 레이어가 지정된 렌더러는 건드리지 않고 관리 대상에서 뺀다
            if ((target.renderingLayerMask & _layerBit) != 0)
            {
                _renderers.RemoveAt(i);
                continue;
            }

            target.renderingLayerMask |= _layerBit;
            AppliedCounts[target] = 1;
        }
    }

    // 색을 유지할 렌더러를 모은다
    private void CollectRenderers()
    {
        _renderers.Clear();
        if (_includeChildren)
        {
            GetComponentsInChildren(true, _renderers);
        }
        else if (TryGetComponent(out Renderer own))
        {
            _renderers.Add(own);
        }
    }

    // 적용했던 KeepColor 레이어를 렌더러에서 제거한다
    private void RemoveLayer()
    {
        foreach (Renderer target in _renderers)
        {
            if (!AppliedCounts.TryGetValue(target, out int count))
            {
                continue;
            }

            // 다른 KeepColorTarget이 아직 적용 중이면 횟수만 줄이고 비트는 남긴다
            if (count > 1)
            {
                AppliedCounts[target] = count - 1;
                continue;
            }

            AppliedCounts.Remove(target);
            if (target != null)
            {
                target.renderingLayerMask &= ~_layerBit;
            }
        }

        // 같은 렌더러를 두 번 해제하지 않도록 목록을 비운다
        _renderers.Clear();
    }
}
