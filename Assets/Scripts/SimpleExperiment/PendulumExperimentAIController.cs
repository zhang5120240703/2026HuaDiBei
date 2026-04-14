using UnityEngine;

/// <summary>
/// 单摆实验 - AI 统一数据接口
/// 作用：给AI提供全部实验数据，无需关心内部逻辑

/// </summary>
public class PendulumExperimentAIController : MonoBehaviour
{
    public static PendulumExperimentAIController Instance;

    [Header("绑定实验脚本（自动绑定）")]
    public Pendulum pendulum;
    public PendulumDragControl dragControl;
    public PendulumCounter counter;
    public PendulumDataRecorder recorder;
    public PendulumExperimentJudge judge;
    public PendulumEnergyVisualizer energy;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 【AI 调用】获取一次完整实验数据包
    /// 返回：实验全部参数 + 数据 + 结果 + 能量
    /// </summary>
    public ExperimentPackage GetAllData()
    {
        ExperimentPackage data = new ExperimentPackage();

        // 基础物理参数
        data.pendulumLength = pendulum.GetPendulumLength();
        data.gravity = pendulum.GetGravityValue();

        // 当前摆长与摆角
        data.currentLength = dragControl.GetCurrentLength();
        data.currentAngle = dragControl.GetCurrentAngle();

        // 周期数据
        data.totalCycles = counter.GetTotalCycles();
        data.averageCycle = counter.GetAverageCycle();
        data.allCycles = counter.GetAllCycleRecords();

        // 多组实验数据
        data.allGValues = recorder.GetAllGValues();
        data.finalAverageG = recorder.GetFinalAverageG();
        data.allDataValid = recorder.IsAllExperimentsValid();

        // 用户输入结果
        data.userInputG = judge.GetUserInputFinalG();

        // 能量数据
        data.kinetic = energy.GetCurrentKinetic();
        data.potential = energy.GetCurrentPotential();
        data.totalEnergy = energy.GetTotalEnergy();

        return data;
    }
}

/// <summary>
/// 单摆实验完整数据包
/// 所有字段都带单位、含义、用途，AI可直接解析判定
/// </summary>
[System.Serializable]
public class ExperimentPackage
{
    // 基础配置
    public float pendulumLength;    // 摆长(m)
    public float gravity;           // 理论重力=9.8(m/s²)

    // 当前状态
    public float currentLength;     // 当前摆长(m)
    public float currentAngle;      // 当前摆角(deg)

    // 周期数据
    public int totalCycles;         // 完成周期数
    public float averageCycle;      // 平均周期(s)
    public float[] allCycles;       // 所有周期记录

    // 实验结果数据
    public float[] allGValues;      // 各组g
    public float finalAverageG;     // 最终平均g(m/s²)
    public bool allDataValid;       // 数据是否有效

    // 用户提交结果
    public float userInputG;        // 用户输入的g

    // 能量数据
    public float kinetic;           // 动能(J)
    public float potential;         // 势能(J)
    public float totalEnergy;       // 总机械能(J)
}