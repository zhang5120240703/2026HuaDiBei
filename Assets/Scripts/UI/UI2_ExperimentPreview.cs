using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI2_ExperimentPreview : MonoBehaviour
{
    [Header("UI控件（必须全部赋值！）")]
    public TMP_Text Txt_Exp_Name;
    public Image Img_Preview;
    public TMP_Text Txt_Time;
    public TMP_Text Txt_Intro;
    public TMP_Text Txt_Task;

    [Header("返回面板")]
    public GameObject UI1_ListPanel;

    private ExperimentData currentData;

    // 外部调用这个方法接收数据
    public void SetExperimentData(ExperimentData data)
    {
        currentData = data;
        RefreshPreviewInfo();
    }

    void OnEnable()
    {
        // 打开面板时，如果有数据就刷新
        if (currentData != null)
        {
            RefreshPreviewInfo();
        }
    }

    // 核心刷新方法，你之前漏掉了
    void RefreshPreviewInfo()
    {
        if (currentData == null) return;

        if (Txt_Exp_Name != null)
            Txt_Exp_Name.text = currentData.experimentName;

        if (Img_Preview != null)
            Img_Preview.sprite = currentData.previewSprite;

        if (Txt_Time != null)
            Txt_Time.text = "预计用时：" + currentData.estimatedTime;

        if (Txt_Intro != null)
            Txt_Intro.text = currentData.experimentIntro;

        if (Txt_Task != null)
            Txt_Task.text = currentData.taskObjective;
    }

    // 返回按钮事件
    public void Btn_BackToList()
    {
        gameObject.SetActive(false);
        UI1_ListPanel.SetActive(true);
        currentData = null;
    }
}
