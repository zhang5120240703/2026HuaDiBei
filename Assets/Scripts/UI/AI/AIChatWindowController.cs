using UnityEngine;
using UnityEngine.UI;

public class AIChatWindowController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject smallRobotIcon;  // 小机器人图标
    public GameObject bigChatWindow;   // 大聊天窗口
    public Button smallRobotButton;    // 小机器人的Button组件
    public Button bigRobotButton;      // 大窗口里的机器人Button组件

    void Start()
    {
        // 绑定按钮点击事件
        smallRobotButton.onClick.AddListener(OpenChatWindow);
        bigRobotButton.onClick.AddListener(CloseChatWindow);

        // 初始状态：只显示小图标
        smallRobotIcon.SetActive(true);
        bigChatWindow.SetActive(false);
    }

    // 打开大窗口：隐藏小图标，显示大窗口
    void OpenChatWindow()
    {
        smallRobotIcon.SetActive(false);
        bigChatWindow.SetActive(true);
    }

    // 关闭大窗口：隐藏大窗口，显示小图标
    void CloseChatWindow()
    {
        smallRobotIcon.SetActive(true);
        bigChatWindow.SetActive(false);
    }
}
