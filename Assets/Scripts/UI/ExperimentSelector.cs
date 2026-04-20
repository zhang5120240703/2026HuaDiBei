using UnityEngine;
using UnityEngine.UI;
using TMPro; // 引入TMP命名空间

public class ExperimentSelector : MonoBehaviour
{
    [Header("实验数据配置")]
    public ExperimentData[] experimentDatas;

    [Header("UI引用")]
    public Image[] thumbnailButtons;
    public TMP_Text nameText; // 替换为TMP_Text
    public Image bigPreviewImage;

    private ExperimentData currentSelectedData;

    void Start()
    {
        for (int i = 0; i < thumbnailButtons.Length; i++)
        {
            int index = i;
            Button btn = thumbnailButtons[i].GetComponent<Button>();
            btn.onClick.AddListener(() => SelectExperiment(index));
        }

        SelectExperiment(0);
    }

    public void SelectExperiment(int index)
    {
        if (index < 0 || index >= experimentDatas.Length) return;

        currentSelectedData = experimentDatas[index];
        nameText.text = currentSelectedData.experimentName;
        bigPreviewImage.sprite = currentSelectedData.previewSprite;

        UIManager.instance.SelectExperiment(currentSelectedData.sceneName);
    }

    public void OnPreviewClick()
    {
        if (currentSelectedData == null) return;
        UIManager.instance.ShowPage(2);
    }
}
