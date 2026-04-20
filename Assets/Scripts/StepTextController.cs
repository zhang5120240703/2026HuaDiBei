using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class StepTextController : MonoBehaviour
{
    //按钮组件
     public Button startButton;
     public Button pauseButton;
     public Button resetButton;
     public Button confirmButton;
     public Button nextStpButton;
     public Button preStpButton;

    //步骤文本
     public TMP_Text stepText;

    //核心组件引用
    private ExperimentCoreEntry coreEntry;
    private ExperimentStateManager stateManager;
    private ExperimentFlowController flowController;
    private UserActionManager userActionManager;
    private void Start()
    {
        coreEntry = FindObjectOfType<ExperimentCoreEntry>();
        stateManager = ExperimentStateManager.Instance;
        userActionManager= UserActionManager.Instance;
        flowController= userActionManager.GetFlowController();

        // 监听状态变化，更新 UI
        flowController.OnStepChanged += OnStepChanged;
        stateManager.OnRunStateChanged += OnRunStateChanged;
        UserActionManager.Instance.OnUserActionPerformed += OnUserAction;
        // 初始显示
        UpdateUI();
    }
    void OnDestroy()
    {
        //取消监听
        if (flowController != null)
            flowController.OnStepChanged -= OnStepChanged;
        if (stateManager != null)
            stateManager.OnRunStateChanged -= OnRunStateChanged;
    }
    private void OnStepChanged(ExperimentStep step)
    {
        UpdateUI();
    }

    private void OnRunStateChanged(ExperimentRunState state)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (stepText != null)
        {
            stepText.text = $"当前步骤: {flowController.CurrentStep}\n运行状态: {stateManager.CurrentRunState}\n参数合法: {stateManager.IsParamValid}";
        }
    }

    void OnUserAction(UserActionType actionType)
    {
        Debug.Log($"用户执行操作：{actionType}"); 
    }
}
