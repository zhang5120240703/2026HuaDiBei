using UnityEngine;

/// <summary>
/// 跨场景数据桥：传递实验名称、用时、轨迹数据、参数
/// DontDestroyOnLoad 单例，生命周期贯穿 MainMenu ↔ 实验场景
/// </summary>
public class ExperimentResultBridge : MonoBehaviour
{
    public static ExperimentResultBridge Instance;

    // ── 从主菜单传入 ──
    public string experimentName;       // 实验名称
    public float startTime;             // 开始时间（Time.time）

    // ── 从实验场景写入 ──
    public float returnTime;            // 返回时间（Time.time）
    public float xDistance;
    public float yDistance;
    public float totalDistance;
    public int trajectoryPointCount;

    // ★ 新增：发射参数
    public float velocity;              // 初速度 (m/s)
    public float launchAngle;           // 仰角 (°)

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

    /// <summary>计算总用时（秒）</summary>
    public float ElapsedTime => returnTime - startTime;

    /// <summary>
    /// 清空数据，准备下一次实验。
    /// 调用时机：BackToMain（放弃实验） 或 UI4 确认关闭后。
    /// </summary>
    public void Clear()
    {
        experimentName = "";
        startTime = 0f;
        returnTime = 0f;
        xDistance = 0f;
        yDistance = 0f;
        totalDistance = 0f;
        trajectoryPointCount = 0;
        velocity = 0f;
        launchAngle = 0f;
    }

    /// <summary>
    /// 格式化秒数为可读时长字符串。
    /// 例：3667.3f → "1小时1分7秒";  85.2f → "1分25秒";  3.5f → "3秒"
    /// </summary>
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