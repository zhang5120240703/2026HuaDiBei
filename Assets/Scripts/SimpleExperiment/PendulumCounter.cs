using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// 单摆周期计数器（方向反转计数版 —— 最稳定）
/// 逻辑：摆球运动方向改变 → 计数半周期 → 2次方向变 = 1个完整周期
/// 优化：仅在摆球被拖动释放后开始计数 + 统计10个周期总时间
/// 新增：基于固定g=10的理论周期对比
/// </summary>
public class PendulumCounter : MonoBehaviour
{
    [Header("绑定摆球")]
    public Rigidbody ballRb;

    [Header("摆长（米）和原脚本一致")]
    public float pendulumLength = 2f;

    [Header("固定重力加速度")]
    public float fixedG = 10f; // 固定为10
    [HideInInspector] public float theoreticalPeriod; // 理论周期

    [Header("计数设置")]
    [Tooltip("判定为有效摆动的最小速度")]
    public float minSpeedForCount = 0.1f;

    [Header("当前数据")]
    public int totalCycles = 0;
    public float currentPeriod = 0;
    public float averagePeriod = 0;
    public float calculatedG = 0; // 固定为10
    public float totalTimeFor10Cycles = 0; // 10个周期总时间

    [Header("UI绑定")]
    public Text cycleCountText; // 显示周期数的UI文本
    public Text totalTime10CyclesText; // 显示10周期总时间的UI文本
    public Text theoreticalPeriodText; // 新增：显示理论周期

    // 方向判断核心
    private int lastDir = 0; // -1=左, 1=右, 0=初始
    private int dirChangeCount = 0; // 方向改变次数（2次=1周期）
    private float lastChangeTime;
    private List<float> periodList = new List<float>();

    // 新增：摆动激活标记（仅拖动释放后计数）
    private bool isSwingActive = false;
    // 最低点
    private Vector3 lowestPos;

    void Start()
    {
        if (ballRb == null)
        {
            Debug.LogError("未绑定摆球 Rigidbody");
            return;
        }

        // 初始化固定g和理论周期
        calculatedG = fixedG;
        CalculateTheoreticalPeriod();

        lowestPos = ballRb.position;
        lastChangeTime = Time.time;

        // 优化阻尼，匹配g=10的物理表现
        ballRb.angularDrag = 0.05f;
        ballRb.drag = 0.001f;

        // 初始化UI
        UpdateUIText();
    }

    void FixedUpdate()
    {
        if (ballRb == null || !isSwingActive) return; // 未激活则不计数

        // 速度太小 = 静止，不计数
        if (ballRb.velocity.magnitude < minSpeedForCount)
        {
            lastDir = 0;
            return;
        }

        // 获取摆球相对于最低点的水平方向（单摆左右摆动 = X轴）
        float deltaX = ballRb.position.x - lowestPos.x;

        // 当前方向：右=1，左=-1
        int currentDir = deltaX > 0 ? 1 : -1;

        // 方向发生改变才计数
        if (lastDir != 0 && currentDir != lastDir)
        {
            OnDirectionChanged();
        }

        // 更新方向
        lastDir = currentDir;
    }

    /// <summary>
    /// 摆球运动方向反转时调用
    /// </summary>
    void OnDirectionChanged()
    {
        dirChangeCount++;
        float now = Time.time;

        // 2次方向反转 = 1个完整周期（左→右→左）
        if (dirChangeCount >= 2)
        {
            dirChangeCount = 0;

            // 计算周期
            currentPeriod = now - lastChangeTime;
            lastChangeTime = now;

            // 过滤太短的无效周期
            if (currentPeriod < 0.3f) return;

            periodList.Add(currentPeriod);
            totalCycles = periodList.Count;

            // 平均周期
            averagePeriod = 0f;
            foreach (var t in periodList) averagePeriod += t;
            averagePeriod /= periodList.Count;

            // 强制g=10，不再通过周期反算
            calculatedG = fixedG;

            // 计算10个周期总时间（最多统计前10个）
            Calculate10CyclesTotalTime();

            // 更新UI
            UpdateUIText();

            // 输出（新增理论周期对比）
            Debug.Log($" 第 {totalCycles} 周期 | 实际T={currentPeriod:F2}s | 理论T={theoreticalPeriod:F2}s | 平均T={averagePeriod:F2}s | g={calculatedG:F2}");
        }
    }

    /// <summary>
    /// 基于固定g=10计算理论周期
    /// </summary>
    public void CalculateTheoreticalPeriod()
    {
        theoreticalPeriod = 2 * Mathf.PI * Mathf.Sqrt(pendulumLength / fixedG);
    }

    /// <summary>
    /// 计算前10个周期的总时间
    /// </summary>
    private void Calculate10CyclesTotalTime()
    {
        totalTimeFor10Cycles = 0;
        int count = Mathf.Min(totalCycles, 10); // 最多取前10个
        for (int i = 0; i < count; i++)
        {
            totalTimeFor10Cycles += periodList[i];
        }
    }

    /// <summary>
    /// 激活摆动计数（由Pendulum脚本在释放鼠标时调用）
    /// </summary>
    public void ActivateSwingCount()
    {
        // 激活前重新计算理论周期（防止摆长被修改）
        CalculateTheoreticalPeriod();
        isSwingActive = true;
        lastChangeTime = Time.time; // 重置计时起点
        lastDir = 0;
        dirChangeCount = 0;
        Debug.Log("摆球释放，开始计数周期");
    }

    /// <summary>
    /// 重置计数器（保留原逻辑，新增UI更新）
    /// </summary>
    [ContextMenu("重置计数器")]
    public void ResetAll()
    {
        totalCycles = 0;
        currentPeriod = 0;
        averagePeriod = 0;
        calculatedG = fixedG; // 重置后仍固定为10
        totalTimeFor10Cycles = 0;
        dirChangeCount = 0;
        lastDir = 0;
        periodList.Clear();
        lastChangeTime = Time.time;
        isSwingActive = false; // 重置后关闭计数
        UpdateUIText(); // 更新UI
        Debug.Log("计数器已重置");
    }

    /// <summary>
    /// 更新UI文本显示
    /// </summary>
    private void UpdateUIText()
    {
        if (cycleCountText != null)
        {
            cycleCountText.text = $"周期数：{totalCycles}";
        }

        if (totalTime10CyclesText != null)
        {
            totalTime10CyclesText.text = $"10周期总时间：{totalTimeFor10Cycles:F2}s";
        }

        // 新增：显示理论周期
        if (theoreticalPeriodText != null)
        {
            theoreticalPeriodText.text = $"理论周期：{theoreticalPeriod:F2}s (g=10)";
        }
    }

    // 摆长变更时同步更新理论周期
    private void OnValidate()
    {
        CalculateTheoreticalPeriod();
        calculatedG = fixedG;
    }
}