using UnityEngine;
using TMPro;

public class Timer_TMP : MonoBehaviour
{
    public TMP_Text timerText;
    private float elapsedTime = 0f;
    private bool isTiming = false;

    void Update()
    {
        if (isTiming)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimerDisplay();
        }
    }

    // 进入lift：重置时间并开始计时
    public void StartTimer()
    {
        elapsedTime = 0f;
        isTiming = true;
        UpdateTimerDisplay();
    }

    // 暂停计时
    public void PauseTimer()
    {
        isTiming = false;
    }

    // 恢复计时（接着之前的时间走）
    public void ResumeTimer()
    {
        isTiming = true;
    }

    // 更新时间显示
    public void UpdateTimerDisplay()
    {
        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);
        int milliseconds = Mathf.FloorToInt((elapsedTime * 100f) % 100f);
        timerText.text = $"{minutes:00}:{seconds:00}.{milliseconds:00}";
    }
}
