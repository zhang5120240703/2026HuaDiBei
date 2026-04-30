using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class ExperimentRecord
{
    public string experimentName;
    public float xDistance;
    public float yDistance;
    public float totalDistance;
    public int pointCount;
    public float velocity;
    public float angle;
    public string duration;
    public string timestamp;

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

    public string ToDisplayString()
    {
        return $"X位移={xDistance:F2}m  Y位移={yDistance:F2}m  路程={totalDistance:F2}m  点数={pointCount}  v={velocity:F1}m/s  θ={angle:F1}°  用时={duration}";
    }
}

public class ExperimentDataManager : MonoBehaviour
{
    public static ExperimentDataManager Instance { get; private set; }

    [SerializeField]
    private List<ExperimentRecord> records = new List<ExperimentRecord>();
    public List<ExperimentRecord> Records => records;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInstance()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("[ExperimentDataManager]");
            Instance = go.AddComponent<ExperimentDataManager>();
            DontDestroyOnLoad(go);
            Debug.Log("[ExperimentDataManager] 自动创建全局单例（BeforeSceneLoad）");
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[ExperimentDataManager] Awake 新实例，记录数={records.Count}");
        }
        else if (Instance != this)
        {
            Debug.Log($"[ExperimentDataManager] Awake 销毁重复对象，旧实例记录数={Instance.Records.Count}");
            Destroy(gameObject);
        }
    }

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
                  $"X={xDistance:F2}m Y={yDistance:F2}m 用时={duration} | 总记录数={records.Count}");
    }

    public List<ExperimentRecord> GetRecordsByName(string experimentName)
    {
        return records
            .Where(r => r.experimentName == experimentName)
            .ToList();
    }

    public ExperimentRecord GetLastRecord()
    {
        return records.Count > 0 ? records[records.Count - 1] : null;
    }

    public void ClearRecords()
    {
        records.Clear();
        Debug.Log("[ExperimentDataManager] 清空所有记录");
    }
}