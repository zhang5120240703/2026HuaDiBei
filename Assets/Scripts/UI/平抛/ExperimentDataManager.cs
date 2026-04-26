using UnityEngine;
using System.Collections.Generic;

// 实验数据记录
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
    public string duration;         // 实验用时
    public string timestamp;        // 完成时间

    public ExperimentRecord(string name, float x, float y, float total, int points, float v, float a, string duration)
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
}

// 数据管理器（单例，跨场景保留）
public class ExperimentDataManager : MonoBehaviour
{
    public static ExperimentDataManager Instance { get; private set; }

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

    public void AddRecord(string name, float x, float y, float total, int points, float v, float a, string duration)
    {
        ExperimentRecord record = new ExperimentRecord(name, x, y, total, points, v, a, duration);
        records.Add(record);
        Debug.Log($"添加记录: {name} | X={x:F2}m Y={y:F2}m 用时={duration}");
    }

    public void ClearRecords()
    {
        records.Clear();
        Debug.Log("清空所有记录");
    }
}