using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 单摆周期计数器（方向反转计数版 —— 最稳定）
/// 逻辑：摆球运动方向改变 → 计数半周期 → 2次方向变 = 1个完整周期
/// </summary>
public class PendulumCounter : MonoBehaviour
{
    [Header("绑定摆球")]
    public Rigidbody ballRb;

    [Header("摆长（米）和原脚本一致")]
    public float pendulumLength = 2f;

    [Header("计数设置")]
    [Tooltip("判定为有效摆动的最小速度")]
    public float minSpeedForCount = 0.1f;

    [Header("当前数据")]
    public int totalCycles = 0;
    public float currentPeriod = 0;
    public float averagePeriod = 0;
    public float calculatedG = 0;

    // 方向判断核心
    private int lastDir = 0; // -1=左, 1=右, 0=初始
    private int dirChangeCount = 0; // 方向改变次数（2次=1周期）
    private float lastChangeTime;
    private List<float> periodList = new List<float>();

    // 最低点
    private Vector3 lowestPos;

    void Start()
    {
        if (ballRb == null)
        {
            Debug.LogError("未绑定摆球 Rigidbody");
            return;
        }

        lowestPos = ballRb.position;
        lastChangeTime = Time.time;

        // 衰减
        ballRb.angularDrag = 4.0f;
        ballRb.drag = 0.6f;
    }

    void FixedUpdate()
    {
        if (ballRb == null) return;

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

            // 计算g
            calculatedG = 4 * Mathf.PI * Mathf.PI * pendulumLength / (averagePeriod * averagePeriod);

            // 输出
            Debug.Log($" 第 {totalCycles} 周期 | T={currentPeriod:F2}s | 平均T={averagePeriod:F2}s | g={calculatedG:F2}");
        }
    }

    [ContextMenu("重置计数器")]
    public void ResetAll()
    {
        totalCycles = 0;
        currentPeriod = 0;
        averagePeriod = 0;
        calculatedG = 0;
        dirChangeCount = 0;
        lastDir = 0;
        periodList.Clear();
        lastChangeTime = Time.time;
        Debug.Log("计数器已重置");
    }
}