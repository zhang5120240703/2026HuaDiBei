using UnityEngine;

public class ExperimentSelect : MonoBehaviour
{
    public string sceneName; // 自己填场景名

    public void OnClick()
    {
        UIManager.instance.SelectExperiment(sceneName);
    }
}
