// Assets/Scripts/ExperimentResultBridge.cs

using UnityEngine;

/// <summary>
/// 跨场景数据桥：传递实验名称、用时、轨迹数据
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

    /// <summary>清空数据，准备下一次实验</summary>
    public void Clear()
    {
        experimentName = "";
        startTime = 0f;
        returnTime = 0f;
        xDistance = 0f;
        yDistance = 0f;
        totalDistance = 0f;
        trajectoryPointCount = 0;
    }
}