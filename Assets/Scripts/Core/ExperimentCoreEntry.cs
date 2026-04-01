using UnityEngine;

/// <summary>
/// 实验核心逻辑入口

/// </summary>
public class ExperimentCoreEntry : MonoBehaviour
{
    private UserActionManager _userActionManager;

    private void Start()
    {
       
        _userActionManager = new UserActionManager();
        Debug.Log("实验核心流程/交互/状态框架 初始化完成");
    }

    /// <summary>
    /// 外部调用：触发用户操作
    /// 比如UI按钮点击 → 调用这里
    /// </summary>
    public void TriggerUserAction(int actionType)
    {
        _userActionManager.CaptureUserAction((UserActionType)actionType);
    }
}