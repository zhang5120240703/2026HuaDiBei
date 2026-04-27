using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 实验记录条目
/// </summary>
[System.Serializable]
public class ExperimentRecord
{
    public string experimentName;   // 实验名称
    public float xDistance;         // X位移
    public float yDistance;         // Y位移
    public float totalDistance;     // 总路程
    public int pointCount;          // 轨迹点数
    public float velocity;          // 初速度
    public float angle;             // 仰角
    public string duration;         // 实验用时（已格式化）
    public string timestamp;        // 完成时间

    public ExperimentRecord(
        string name, float x, float y, float total,
        int points, float v, float a, string duration)
    {
        experimentName = name;
        xDistance = x;
        yDistance = y;
        totalDistance = total;
        pointCount = points;
        velocity = v;
        angle = a;
        this.duration = duration;
        timestamp = System.DateTime.Now.ToString("HH:mm:ss");
    }

    /// <summary>格式化单行记录（供 UI 列表显示）</summary>
    public string ToDisplayString()
    {
        return $"X位移={xDistance:F2}m  Y位移={yDistance:F2}m  路程={totalDistance:F2}m  点数={pointCount}  v={velocity:F1}m/s  θ={angle:F1}°  用时={duration}";
    }
}

/// <summary>
/// 数据管理器（单例，跨场景保留）
/// 存储所有实验的历史记录，支持按实验名称查询
/// </summary>
public class ExperimentDataManager : MonoBehaviour
{
    public static ExperimentDataManager Instance { get; private set; }

    [SerializeField]
    private List<ExperimentRecord> records = new List<ExperimentRecord>();
    public List<ExperimentRecord> Records => records;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>添加一条实验记录</summary>
    public void AddRecord(
        string experimentName, float xDistance, float yDistance,
        float totalDistance, int pointCount, float velocity, float angle,
        string duration)
    {
        ExperimentRecord record = new ExperimentRecord(
            experimentName, xDistance, yDistance, totalDistance,
            pointCount, velocity, angle, duration);
        records.Add(record);
        Debug.Log($"[ExperimentDataManager] 添加记录: {experimentName} | " +
                  $"X={xDistance:F2}m Y={yDistance:F2}m 用时={duration}");
    }

    /// <summary>按实验名称筛选记录（用于 UI4 按实验分类展示）</summary>
    public List<ExperimentRecord> GetRecordsByName(string experimentName)
    {
        return records
            .Where(r => r.experimentName == experimentName)
            .ToList();
    }

    /// <summary>获取最后一次实验记录（如果刚完成）</summary>
    public ExperimentRecord GetLastRecord()
    {
        return records.Count > 0 ? records[records.Count - 1] : null;
    }

    /// <summary>清空所有记录</summary>
    public void ClearRecords()
    {
        records.Clear();
        Debug.Log("[ExperimentDataManager] 清空所有记录");
    }
}