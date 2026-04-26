/// <summary>
/// 单摆实验完整数据包（给AI的标准接口）
/// 所有字段均可被AI解析、判断、打分
/// </summary>
[System.Serializable]
public class PendulumExperimentData
{
    // 基础实验参数
    public float pendulumLength;       // 摆长
    public float gravityTheoretical;   // 理论g = 9.8
    public float allowableError;       // 允许误差

    // 三组实验原始数据
    public float[] lengthList = new float[3];
    public float[] periodList = new float[3];
    public float[] gValueList = new float[3];

    // 计算结果
    public float averageG;             // 平均g
    public float actualError;          // 实际误差
    public bool isExperimentValid;     // 数据是否有效

    // 用户输入的最终结果
    public float userInputFinalG;

    // AI 判定结果（预留）
    public bool aiJudgedSuccess;
    public string aiFeedback;
}