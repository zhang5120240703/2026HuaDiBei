using UnityEngine;
using UnityEngine.EventSystems;

public class CylinderController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public Transform piston; // 活塞的Transform组件
    public float cylinderHeight = 2.0f; // 气缸高度（对应2.0L体积）
    public float minHeight = 0.0f; // 最小高度（对应0.0L体积）
    public float maxHeight = 2.0f; // 最大高度（对应2.0L体积）
    
    private float initialY; // 初始鼠标Y位置
    private float initialPistonY; // 初始活塞Y位置
    private bool isDragging = false;
    private float lastVolume; // 上一次记录的体积
    private float volumeChangeRate; // 体积变化率
    private float lastChangeTime; // 上一次变化时间
    
    // 事件
    public System.Action<float> OnVolumeChanged;
    public System.Action<bool> OnVolumeRangeExceeded; // 当体积超出范围时触发
    
    private void Start()
    {
        // 初始设置活塞位置（对应1.0L体积）
        SetPistonPosition(1.0f);
        lastVolume = GetCurrentVolume();
        lastChangeTime = Time.time;
    }
    
    private void Update()
    {
        // 计算体积变化率
        if (Time.time - lastChangeTime > 0.1f) // 每0.1秒计算一次
        {
            float currentVolume = GetCurrentVolume();
            volumeChangeRate = Mathf.Abs(currentVolume - lastVolume) / (Time.time - lastChangeTime);
            lastVolume = currentVolume;
            lastChangeTime = Time.time;
        }
    }
    #region 鼠标拖拽
    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        initialY = Input.mousePosition.y;
        initialPistonY = piston.localPosition.y;
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging)
        {
            float deltaY = Input.mousePosition.y - initialY;
            // 将鼠标移动转换为活塞移动（调整灵敏度）
            float pistonDeltaY = deltaY * 0.001f * cylinderHeight;
            
            float newPistonY = initialPistonY + pistonDeltaY;
            // 限制活塞位置在气缸范围内
            float minPistonY = -cylinderHeight / 2;
            float maxPistonY = cylinderHeight/2;
            
            bool isOutOfRange = false;
            if (newPistonY < minPistonY)
            {
                newPistonY = minPistonY;
                isOutOfRange = true;
            }
            else if (newPistonY > maxPistonY)
            {
                newPistonY = maxPistonY;
                isOutOfRange = true;
            }
            
            piston.localPosition = new Vector3(piston.localPosition.x, newPistonY, piston.localPosition.z);
            
            // 计算当前体积
            float currentVolume = GetCurrentVolume();
            OnVolumeChanged?.Invoke(currentVolume);
            OnVolumeRangeExceeded?.Invoke(isOutOfRange);
        }
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    #endregion

    private float GetCurrentVolume()
    {
        // 根据活塞位置计算体积
        float pistonHeight = Mathf.Abs(piston.localPosition.y);
        float volume = (pistonHeight / cylinderHeight) * (maxHeight - minHeight) + minHeight;
        return Mathf.Clamp(volume, minHeight, maxHeight);
    }
    
    public void SetPistonPosition(float volume)
    {
        // 根据体积设置活塞位置
        float normalizedVolume = (volume - minHeight) / (maxHeight - minHeight);
        float pistonHeight = normalizedVolume * cylinderHeight-cylinderHeight/2;
        piston.localPosition = new Vector3(piston.localPosition.x, pistonHeight, piston.localPosition.z);
    }
    
    // 获取当前体积变化率
    public float GetVolumeChangeRate()
    {
        return volumeChangeRate;
    }
    
    // 检查体积是否稳定
    public bool IsVolumeStable()
    {
        return volumeChangeRate < 0.01f; // 变化小于0.01L/秒视为稳定
    }
    
    // 获取体积范围
    public float GetMinVolume()
    {
        return minHeight;
    }
    
    public float GetMaxVolume()
    {
        return maxHeight;
    }
}