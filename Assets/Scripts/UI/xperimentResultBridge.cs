using UnityEngine;

public class ExperimentResultBridge : MonoBehaviour
{
    public static ExperimentResultBridge Instance;

    public string experimentName;         // 实验场景名
    public string experimentDisplayName;  // 实验显示名称（从 SO 读取）
    public float startTime;

    public float returnTime;
    public float xDistance;
    public float yDistance;
    public float totalDistance;
    public int trajectoryPointCount;
    public float velocity;
    public float launchAngle;

    void Awake()
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

    public float ElapsedTime => returnTime - startTime;

    public void Clear()
    {
        experimentName = "";
        experimentDisplayName = "";
        startTime = 0f;
        returnTime = 0f;
        xDistance = 0f;
        yDistance = 0f;
        totalDistance = 0f;
        trajectoryPointCount = 0;
        velocity = 0f;
        launchAngle = 0f;
    }

    public static string FormatDuration(float seconds)
    {
        if (seconds <= 0f) return "0秒";

        int totalSec = Mathf.RoundToInt(seconds);
        int h = totalSec / 3600;
        int m = (totalSec % 3600) / 60;
        int s = totalSec % 60;

        string result = "";
        if (h > 0) result += $"{h}小时";
        if (m > 0) result += $"{m}分";
        if (s > 0 || result == "") result += $"{s}秒";

        return result;
    }
}