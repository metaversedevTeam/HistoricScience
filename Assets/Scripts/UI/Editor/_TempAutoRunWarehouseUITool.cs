using UnityEditor;
using UnityEngine;

// 임시 부트스트랩: 에디터가 이미 실행 중인 상태에서 WarehouseUIPrefabTool을 1회 자동 실행하기 위한 스크립트. 완료 후 삭제 예정.
[InitializeOnLoad]
public static class _TempAutoRunWarehouseUITool
{
    static _TempAutoRunWarehouseUITool()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(WarehouseUIPrefabTool.PanelPrefabPath) != null)
            return;

        EditorApplication.delayCall += () =>
        {
            Debug.Log("[_TempAutoRunWarehouseUITool] WarehouseUIPrefabTool.Generate() 자동 실행 시작");
            WarehouseUIPrefabTool.Generate();
            Debug.Log("[_TempAutoRunWarehouseUITool] WarehouseUIPrefabTool.Generate() 자동 실행 완료");
        };
    }
}
