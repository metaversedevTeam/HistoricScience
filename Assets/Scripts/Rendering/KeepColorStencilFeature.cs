using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// 흑백 화면에서 색을 유지할 오브젝트의 픽셀을 스텐실 버퍼에 표시하는 렌더러 기능
public class KeepColorStencilFeature : ScriptableRendererFeature
{
    [Tooltip("스텐실만 기록하는 머티리얼 (Hidden/HistoricScience/KeepColorStencil)")]
    [SerializeField] private Material _stencilMaterial;

    [Tooltip("색을 유지할 오브젝트를 구분하는 렌더링 레이어 이름")]
    [SerializeField] private string _renderingLayerName = KeepColorTarget.RenderingLayerName;

    [Tooltip("흑백 패스보다 먼저 실행되어야 한다")]
    [SerializeField] private RenderPassEvent _passEvent = RenderPassEvent.AfterRenderingTransparents;

    [Tooltip("색을 유지할 오브젝트 앞을 가린 투명 오브젝트를 흑백으로 처리할지 여부")]
    [SerializeField] private bool _occludeByTransparents = true;

    [Tooltip("이 알파 이상인 투명 픽셀만 앞을 가린 것으로 본다")]
    [SerializeField, Range(0f, 1f)] private float _occluderAlphaThreshold = 0.5f;

    private KeepColorStencilPass _pass;

    // 렌더 패스를 생성한다
    public override void Create()
    {
        _pass = new KeepColorStencilPass();
    }

    // 머티리얼과 렌더링 레이어가 준비되고 흑백 효과가 켜진 경우에만 패스를 등록한다
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_stencilMaterial == null || renderingData.cameraData.cameraType == CameraType.Preview)
        {
            return;
        }

        // 흑백 효과가 꺼져 있으면 스텐실을 표시할 필요가 없다
        if (!MonochromeEffect.IsActive)
        {
            return;
        }

        int layerIndex = RenderingLayerMask.NameToRenderingLayer(_renderingLayerName);
        if (layerIndex < 0)
        {
            return;
        }

        _pass.renderPassEvent = _passEvent;
        _pass.Setup(_stencilMaterial.shader, 1u << layerIndex, _occludeByTransparents, _occluderAlphaThreshold);
        renderer.EnqueuePass(_pass);
    }

    // 지정한 렌더링 레이어의 오브젝트를 스텐실 전용 셰이더로 다시 그리는 패스
    private class KeepColorStencilPass : ScriptableRenderPass
    {
        private const int MarkPassIndex = 0;
        private const int ClearPassIndex = 1;

        private static readonly int OccluderAlphaId = Shader.PropertyToID("_KeepColorOccluderAlpha");

        private static readonly List<ShaderTagId> ShaderTags = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        private Shader _shader;
        private uint _renderingLayerMask;
        private bool _occludeByTransparents;
        private float _occluderAlphaThreshold;

        // 렌더 함수에 넘길 데이터
        private class PassData
        {
            public RendererListHandle MarkList;
            public RendererListHandle OccluderClearList;
            public RendererListHandle TransparentMarkList;
            public bool OccludeByTransparents;
            public float OccluderAlphaThreshold;
        }

        // 이번 프레임에 사용할 셰이더, 렌더링 레이어 마스크, 투명체 처리 설정을 넣는다
        public void Setup(Shader shader, uint renderingLayerMask, bool occludeByTransparents, float occluderAlphaThreshold)
        {
            _shader = shader;
            _renderingLayerMask = renderingLayerMask;
            _occludeByTransparents = occludeByTransparents;
            _occluderAlphaThreshold = occluderAlphaThreshold;
        }

        // 렌더 그래프에 스텐실 기록 패스를 추가한다
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            // 덮어쓰기 셰이더는 원래 머티리얼의 프로퍼티와 키워드를 유지하므로 알파 클립이 그대로 적용된다
            DrawingSettings keepMark = CreateStencilDrawingSettings(renderingData, cameraData, lightData, SortingCriteria.CommonOpaque, MarkPassIndex);
            FilteringSettings keepAll = new FilteringSettings(RenderQueueRange.all, -1, _renderingLayerMask);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Keep Color Stencil", out PassData passData))
            {
                passData.OccludeByTransparents = _occludeByTransparents;
                passData.OccluderAlphaThreshold = _occluderAlphaThreshold;

                passData.MarkList = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, keepMark, keepAll));
                builder.UseRendererList(passData.MarkList);

                if (_occludeByTransparents)
                {
                    DrawingSettings occluderClear = CreateStencilDrawingSettings(renderingData, cameraData, lightData, SortingCriteria.CommonTransparent, ClearPassIndex);
                    DrawingSettings transparentMark = CreateStencilDrawingSettings(renderingData, cameraData, lightData, SortingCriteria.CommonTransparent, MarkPassIndex);
                    FilteringSettings anyTransparent = new FilteringSettings(RenderQueueRange.transparent);
                    FilteringSettings keepTransparent = new FilteringSettings(RenderQueueRange.transparent, -1, _renderingLayerMask);

                    passData.OccluderClearList = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, occluderClear, anyTransparent));
                    passData.TransparentMarkList = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, transparentMark, keepTransparent));
                    builder.UseRendererList(passData.OccluderClearList);
                    builder.UseRendererList(passData.TransparentMarkList);
                    builder.AllowGlobalStateModification(true);
                }

                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }
        }

        // 스텐실 셰이더의 지정한 패스로 덮어써서 그리는 드로잉 설정을 만든다
        private DrawingSettings CreateStencilDrawingSettings(UniversalRenderingData renderingData, UniversalCameraData cameraData, UniversalLightData lightData, SortingCriteria sortingCriteria, int shaderPassIndex)
        {
            DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(ShaderTags, renderingData, cameraData, lightData, sortingCriteria);
            drawingSettings.overrideShader = _shader;
            drawingSettings.overrideShaderPassIndex = shaderPassIndex;
            return drawingSettings;
        }

        // 색 유지 표시 → 앞을 가린 투명체 지우기 → 색 유지 투명체 다시 표시 순서로 그린다
        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            context.cmd.DrawRendererList(data.MarkList);

            if (!data.OccludeByTransparents)
            {
                return;
            }

            context.cmd.SetGlobalFloat(OccluderAlphaId, data.OccluderAlphaThreshold);
            context.cmd.DrawRendererList(data.OccluderClearList);
            context.cmd.DrawRendererList(data.TransparentMarkList);
        }
    }
}
