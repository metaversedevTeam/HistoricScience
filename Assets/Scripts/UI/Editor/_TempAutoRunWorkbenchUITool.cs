using UnityEditor;
using UnityEngine;

// 임시 부트스트랩: 에디터가 이미 실행 중인 상태에서 WorkbenchUIPrefabTool을 1회 자동 실행하기 위한 스크립트. 완료 후 삭제 예정.
[InitializeOnLoad]
public static class _TempAutoRunWorkbenchUITool
{
    static _TempAutoRunWorkbenchUITool()
    {
        // 대화상자 프리팹은 재디자인 전에도 있었으므로, 이번에 새로 생기는 조합 슬롯 프리팹으로 실행 여부를 판단한다.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(WorkbenchUIPrefabTool.CraftingSlotPrefabPath) != null)
            return;

        EditorApplication.delayCall += () =>
        {
            Debug.Log("[_TempAutoRunWorkbenchUITool] WorkbenchUIPrefabTool.Generate() 자동 실행 시작");
            WorkbenchUIPrefabTool.Generate();
            Debug.Log("[_TempAutoRunWorkbenchUITool] WorkbenchUIPrefabTool.Generate() 자동 실행 완료");
        };
    }
}
