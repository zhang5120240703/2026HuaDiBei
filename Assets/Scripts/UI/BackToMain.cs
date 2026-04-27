using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 挂载在实验场景的"返回主菜单"按钮上。
/// 效果：放弃本次实验，清空桥接器数据，回主菜单首页。
/// </summary>
public class BackToMain : MonoBehaviour
{
    public void OnClick()
    {
        // 标记放弃：清空桥接器（计时清零，不存记录）
        ExperimentResultBridge.Instance?.Clear();

        SceneManager.LoadScene("MainMenu");
    }
}