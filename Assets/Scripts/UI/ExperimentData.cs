using UnityEngine;

[CreateAssetMenu(fileName = "NewExperiment", menuName = "Experiment Data")]
public class ExperimentData : ScriptableObject
{
    [Header("基础信息")]
    public string experimentName;    // 实验名称
    public Sprite previewSprite;     // 预览图
    public string sceneName;         // 实验场景名（场景3用）

    [Header("预览面板信息")]
    public string estimatedTime;     // 预计用时
    [TextArea(3, 10)]
    public string experimentIntro;   // 实验简介
    [TextArea(3, 10)]
    public string taskObjective;     // 任务目标
}
