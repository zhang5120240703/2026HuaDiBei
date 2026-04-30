using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 图鉴面板：展示所有实验的历史记录
/// </summary>
public class UI_ExperimentRecords : MonoBehaviour
{
    [Header("5个实验对应的 TextMeshPro（按实验名匹配）")]
    public TMP_Text txtRecord1;   // 实验1
    public TMP_Text txtRecord2;   // 实验2
    public TMP_Text txtRecord3;   // 实验3
    public TMP_Text txtRecord4;   // 实验4
    public TMP_Text txtRecord5;   // 实验5

    [Header("实验名匹配（填 ExperimentData 里的 experimentName）")]
    public string experimentName1;
    public string experimentName2;
    public string experimentName3;
    public string experimentName4;
    public string experimentName5;

    void OnEnable()
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        var dataMgr = ExperimentDataManager.Instance;
        if (dataMgr == null)
        {
            Debug.LogWarning("[UI_ExperimentRecords] ExperimentDataManager 不存在");
            return;
        }

        ShowRecords(dataMgr, experimentName1, txtRecord1);
        ShowRecords(dataMgr, experimentName2, txtRecord2);
        ShowRecords(dataMgr, experimentName3, txtRecord3);
        ShowRecords(dataMgr, experimentName4, txtRecord4);
        ShowRecords(dataMgr, experimentName5, txtRecord5);
    }

    void ShowRecords(ExperimentDataManager mgr, string expName, TMP_Text textUI)
    {
        if (textUI == null || string.IsNullOrEmpty(expName)) return;

        var records = mgr.GetRecordsByName(expName);

        if (records == null || records.Count == 0)
        {
            textUI.text = $"<b>{expName}</b>\n暂无记录";
            return;
        }

        // 按时间倒序，最新在上
        var sorted = records.OrderByDescending(r => r.timestamp).ToList();

        string result = $"<b>{expName}</b>  (共{sorted.Count}次)\n\n";
        for (int i = 0; i < sorted.Count; i++)
        {
            var r = sorted[i];
            result += $"第{i + 1}次  {r.timestamp}\n" +
                      $"Z位移={r.xDistance:F2}m  Y位移={r.yDistance:F2}m\n" +
                      $"路程={r.totalDistance:F2}m  点数={r.pointCount}\n" +
                      $"v={r.velocity:F1}  θ={r.angle:F1}°  用时={r.duration}\n\n";
        }

        textUI.text = result;
    }
}