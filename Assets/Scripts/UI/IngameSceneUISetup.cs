using UnityEngine;

public class IngameSceneUISetup : MonoBehaviour
{
    [Header("Bind")]
    [SerializeField] private PlayerManager _playerManager;

    [Header("Prefab")]
    [SerializeField] private TempCommandPanelUI _commandPanelUI;

    private void Start()
    {
        UIManager.Instance.OpenUI(_commandPanelUI, _playerManager);
    }
}
