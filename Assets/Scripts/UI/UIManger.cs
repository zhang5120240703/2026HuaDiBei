using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject[] uiPanels;
    public Timer_TMP liftTimer;

    private int currentIndex = 0;
    private bool isFrozen = false;

    void Start()
    {
        ShowOnlyCurrent();
    }

    public void GoNext()
    {
        if (isFrozen) return;

        currentIndex++;
        if (currentIndex >= uiPanels.Length)
            currentIndex = 0;

        ShowOnlyCurrent();
        //CheckTimerState();
    }

    public void GoBack()
    {
        if (isFrozen) return;

        currentIndex--;
        if (currentIndex < 0) currentIndex = 0;

        ShowOnlyCurrent();
        //CheckTimerState();
    }

    //public void OnStopButtonClick()
    //{
    //    isFrozen = !isFrozen;

    //    if (isFrozen)
    //    {
    //        liftTimer.PauseTimer();
    //    }
    //    else
    //    {
    //        liftTimer.ResumeTimer();
    //    }
    //}

    void ShowOnlyCurrent()
    {
        for (int i = 0; i < uiPanels.Length; i++)
        {
            uiPanels[i].SetActive(i == currentIndex);
        }
    }

    //void CheckTimerState()
    //{
    //    if (currentIndex == 3) // lift界面（索引3）
    //    {
    //        // 进入 {
    //        // 进入lift：重置时间并开始计时
    //        if (!isFrozen)
    //        {
    //            liftTimer.StartTimer();
    //        }
    //    }
    //    else
    //    {
    //        // 离开lift：暂停计时
    //        liftTimer.PauseTimer();
    //        liftTimer.UpdateTimerDisplay();
    //    }
    //}
}
