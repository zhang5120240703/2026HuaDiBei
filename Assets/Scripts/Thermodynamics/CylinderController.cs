using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 气缸控制器，负责处理活塞的拖动和体积计算
/// </summary>
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
    //
    private const float sensitivity = 0.001f; // 鼠标移动转换为活塞移动的灵敏度
    private float dragStartThreshold = 5f; // 像素级阈值，防止点击也触发移动
    private bool pointerDown = false; // 标记是否按下但尚未达到拖动阈值
    private bool blockedByPressure = false; // 当压强过大时阻止新的拖动

    // 新增：累计位移，用于判断拖动阈值；限制每帧最大位移，防止快速拖拽一次性跳到顶端
    private float accumulatedDrag = 0f;
    private const float maxStepFractionPerFrame = 0.25f; // 每帧允许移动的最大比例（相对于 cylinderHeight）

    // 防止体积为0导致除零和允许的最小体积（可根据需求调整）
    private const float minVolumeEpsilon = 1e-3f;

    // 事件
    public System.Action<float> OnVolumeChanged;
    public System.Action<bool> OnVolumeRangeExceeded; // 当体积超出范围时触发
    
    private void Start()
    {
        // 初始设置活塞位置（对应1.0L体积）
        SetPistonPosition(1.0f);
        lastVolume = GetCurrentVolume();
        lastChangeTime = Time.time;
        blockedByPressure = GetPressure() >= IdealGasSimulation.Instance.GetMaxPressure();
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
        // 如果之前被压强阻止，检查是否可以解除阻止
        if (blockedByPressure && GetPressure() < IdealGasSimulation.Instance.GetMaxPressure())
        {
            blockedByPressure = false;
        }
    }
    #region 鼠标拖拽
    public void OnPointerDown(PointerEventData eventData)
    {
        // 使用 pressPosition 作为基准，清零累计值，不立即进入拖动状态
        pointerDown = true;
        isDragging = false;
        accumulatedDrag = 0f;
        initialY = eventData.pressPosition.y;
        initialPistonY = piston.localPosition.y;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!pointerDown)
            return;

        // 累计绝对位移用于阈值判断（避免点按触发）
        accumulatedDrag += Mathf.Abs(eventData.delta.y);

        if (!isDragging)
        {
            if (accumulatedDrag < dragStartThreshold)
            {
                return; // 尚未达到拖动阈值
            }
            isDragging = true;
            // initialPistonY 已记录为按下时的活塞位置
        }

        // 使用增量移动，避免将鼠标总位移相对于按下时一次性映射到活塞位置
        float rawDeltaY = eventData.delta.y;
        float pistonDeltaY = rawDeltaY * sensitivity * cylinderHeight;

        // 限制单帧最大移动量，防止快速一帧内跳到边缘
        float maxStep = cylinderHeight * maxStepFractionPerFrame;
        pistonDeltaY = Mathf.Clamp(pistonDeltaY, -maxStep, maxStep);

        // 采用相对于当前活塞位置的增量（而不是 initialPistonY + totalDelta）
        float tentativePistonY = piston.localPosition.y + pistonDeltaY;

        // 限制活塞位置在气缸范围内（先基础范围）
        float minPistonY = -cylinderHeight / 2;
        float maxPistonY = cylinderHeight / 2;

        bool isOutOfRange = false;
        if (tentativePistonY < minPistonY)
        {
            tentativePistonY = minPistonY;
            isOutOfRange = true;
        }
        else if (tentativePistonY > maxPistonY)
        {
            tentativePistonY = maxPistonY;
            isOutOfRange = true;
        }

        // 计算变动前后的体积与当前/预测压强
        float currentVolume = GetCurrentVolume();
        float newVolume = VolumeFromPistonY(tentativePistonY);

        // 强制新体积不小于 allowedMinVolume，避免体积为0
        float allowedMinVolume = Mathf.Max(minHeight + minVolumeEpsilon, minVolumeEpsilon);
        if (newVolume < allowedMinVolume)
        {
            // 将活塞位置限制到对应 allowedMinVolume 的位置（不允许进一步压缩到 0）
            tentativePistonY = PistonYFromVolume(allowedMinVolume);
            newVolume = allowedMinVolume;
            isOutOfRange = true;
        }

        // 预测压强（根据预测体积）
        float predictedPressure = PredictPressureForVolume(newVolume);
        float maxPressure = IdealGasSimulation.Instance.GetMaxPressure();

        // 如果这次移动会进一步减小体积（压缩）且预测压强超过最大压强，则阻止该移动
        if (newVolume < currentVolume && predictedPressure > maxPressure)
        {
            // 阻止这次压缩移动，但允许向下（增大体积）的移动
            Debug.Log("预测压强将超过上限，阻止此次压缩移动。");
            return;
        }

        // 应用移动
        piston.localPosition = new Vector3(piston.localPosition.x, tentativePistonY, piston.localPosition.z);

        // 计算当前体积并通知
        float appliedVolume = GetCurrentVolume();
        OnVolumeChanged?.Invoke(appliedVolume);
        OnVolumeRangeExceeded?.Invoke(isOutOfRange);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        pointerDown = false;
        accumulatedDrag = 0f;
    }

    #endregion

    private float GetCurrentVolume()
    {
        // 将活塞的Y位置归一化为[0, 1]范围
        float normalizedPosition = Mathf.InverseLerp(-cylinderHeight / 2, cylinderHeight / 2, piston.localPosition.y);

        // 将归一化的活塞位置映射到体积范围[minHeight, maxHeight]
        float volume = Mathf.Lerp(maxHeight, minHeight, normalizedPosition);

        return Mathf.Clamp(volume, minHeight + minVolumeEpsilon, maxHeight); // 保证体积在有效范围内，避免为0
    }

    // 根据指定的活塞Y位置计算体积（不修改实际位置）
    private float VolumeFromPistonY(float pistonY)
    {
        float normalizedPosition = Mathf.InverseLerp(-cylinderHeight / 2, cylinderHeight / 2, pistonY);
        float volume = Mathf.Lerp(maxHeight, minHeight, normalizedPosition);
        return Mathf.Clamp(volume, minHeight, maxHeight);
    }

    // 根据给定体积返回对应的活塞Y位置（不修改实际位置）
    private float PistonYFromVolume(float volume)
    {
        float normalizedVolume = Mathf.InverseLerp(minHeight, maxHeight, volume);
        float pistonY = Mathf.Lerp(cylinderHeight / 2, -cylinderHeight / 2, normalizedVolume);
        return pistonY;
    }

    // 预测给定体积下的压强（不改变当前状态）
    private float PredictPressureForVolume(float volume)
    {
        float safeVolume = Mathf.Max(volume, minVolumeEpsilon);
        float pressure = (IdealGasSimulation.Instance.moles * IdealGasSimulation.R * IdealGasSimulation.Instance.GetTemperature()) / safeVolume;
        return pressure;
    }

    private float GetPressure()
    {
        float volume = GetCurrentVolume();
        // 防止除以0
        float safeVolume = Mathf.Max(volume, minVolumeEpsilon);
        float pressure = (IdealGasSimulation.Instance.moles * IdealGasSimulation.R * IdealGasSimulation.Instance.GetTemperature()) / safeVolume;
        return Mathf.Clamp(pressure, IdealGasSimulation.Instance.GetMinPressure(), IdealGasSimulation.Instance.GetMaxPressure()); // 保证压强在合理范围内
    }
    public void SetPistonPosition(float volume)
    {
        // 将体积映射到[0, 1]范围
        float normalizedVolume = Mathf.InverseLerp(minHeight, maxHeight, volume);

        // 将归一化的体积值映射到活塞的Y位置
        float pistonY = Mathf.Lerp(cylinderHeight / 2, -cylinderHeight / 2, normalizedVolume);

        // 设置活塞位置
        piston.localPosition = new Vector3(piston.localPosition.x, pistonY, piston.localPosition.z);
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