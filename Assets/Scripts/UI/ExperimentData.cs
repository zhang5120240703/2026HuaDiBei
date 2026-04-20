using UnityEngine;

[CreateAssetMenu(fileName = "NewExperiment", menuName = "Experiment Data")]
public class ExperimentData : ScriptableObject
{
    public string experimentName; // 实验名称
    public Sprite previewSprite;   // 右侧大图
    public string sceneName;       // 对应实验场景名
}
