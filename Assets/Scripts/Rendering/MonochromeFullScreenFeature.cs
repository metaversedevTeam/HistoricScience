using UnityEngine.Rendering.Universal;

// 흑백 강도가 0일 때는 풀스크린 패스를 등록하지 않는 흑백 화면용 렌더러 기능
public class MonochromeFullScreenFeature : FullScreenPassRendererFeature
{
    // 흑백 효과가 켜져 있을 때만 풀스크린 패스를 등록한다
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!MonochromeEffect.IsActive)
        {
            return;
        }

        base.AddRenderPasses(renderer, ref renderingData);
    }
}
