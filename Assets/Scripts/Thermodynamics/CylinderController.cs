using UnityEngine;
using UnityEngine.EventSystems;
using static UnityEngine.GraphicsBuffer;

/// <summary>
/// 气缸控制器，负责处理活塞的拖动和体积计算
/// </summary>
public class CylinderController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public Transform piston; // 活塞的Transform组件

    public float cylinderHeight = 2.0f; // 气缸高度（对应2.0L体积）
    public float minHeight = 0.2f; // 最小高度（对应0.0L体积）
    public float maxHeight = 2.0f; // 最大高度（对应2.0L体积）
    
    private float initialY; // 初始鼠标Y位置
    private float initialPistonY; // 初始活塞Y位置
    private bool isDragging = false;
    private bool canDrag = false; // 是否允许拖动（根据过程类型限制）
    private float lastVolume; // 上一次记录的体积
    private float volumeChangeRate; // 体积变化率
    private float lastChangeTime; // 上一次变化时间
    
    private const float sensitivity = 0.001f; // 鼠标移动转换为活塞移动的灵敏度
    private float dragStartThreshold = 5f; // 像素级阈值，防止点击也触发移动
    private bool pointerDown = false; // 标记是否按下但尚未达到拖动阈值
    private bool blockedByPressure = false; // 当压强过大时阻止新的拖动

    // 累计位移，用于判断拖动阈值；限制每帧最大位移，防止快速拖拽一次性跳到顶端
    private float accumulatedDrag = 0f;
    private const float maxStepFractionPerFrame = 0.25f; // 每帧允许移动的最大比例（相对于 cylinderHeight）

    // 防止体积为0导致除零和允许的最小体积（可根据需求调整）
    public const float minVolumeEpsilon = 1e-3f;

    // 事件
    public System.Action<float> OnVolumeChanged;
    public System.Action<bool> OnVolumeRangeExceeded; // 当体积超出范围时触发

    //平滑移动参数
    private float targetPistonY;// 目标活塞位置
    private float smoothVelocity; // 平滑移动的速度
    public float smoothTime = 0.1f; // 平滑移动的时间常数

    private IdealGasSimulation.ProcessType currentProcess;

    private void Start()
    {
        currentProcess = IdealGasSimulation.Instance.GetCurrentProcess();
        //监听气体变化
        IdealGasSimulation.Instance.OnStateChanged += OnGasStateChanged;
        //初始化目标位置为当前活塞位置
        targetPistonY = piston.localPosition.y;



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
        SmoothChangePiston();
    }


    public bool CanChangeState()
    {
        var sim= IdealGasSimulation.Instance;
        float T=sim.GetTemperature();
        float maxT=sim.GetMaxTemperature();
        float P=sim.GetPressure();
        float maxP=sim.GetMaxPressure();

        //温度过高时禁止改变状态
        bool tempBlocked = T >= maxT;

        //压强过大时禁止改变状态
        bool pressureBlocked = P >= maxP;

        return (tempBlocked || pressureBlocked);
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
        //如果为等容过程，不允许拖动
        if (!canDrag)
            return;

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

        // 采用相对于当前活塞位置的增量
        float tentativePistonY = piston.localPosition.y + pistonDeltaY;

        // 限制活塞位置在气缸范围内（基础范围）
        float minPistonY = -cylinderHeight / 2;
        float maxPistonY = cylinderHeight / 2;

        // 计算当前体积和新体积
        float currentVolume = GetCurrentVolume();
        float newVolume = VolumeFromPistonY(tentativePistonY);

        // 检查体积边界
        if (newVolume <= minHeight)
        {
            tentativePistonY = PistonYFromVolume(minHeight);
            // 触发体积超出范围事件
            OnVolumeRangeExceeded?.Invoke(true);
        }
        else if (newVolume >= maxHeight)
        {
            tentativePistonY = PistonYFromVolume(maxHeight);
            // 触发体积超出范围事件
            OnVolumeRangeExceeded?.Invoke(true);
        }
        else
        {
            // 体积在有效范围内
            OnVolumeRangeExceeded?.Invoke(false);
        }

        if (newVolume < IdealGasSimulation.Instance.GetMinVolume())
        {
            // 触发体积超出范围事件
            OnVolumeRangeExceeded?.Invoke(true);
            return;
        }

        // 改为设置目标位置（不要直接瞬移）
        targetPistonY = tentativePistonY;

        // 计算当前体积并通知
        float appliedVolume = GetCurrentVolume();


        OnVolumeChanged?.Invoke(appliedVolume);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        pointerDown = false;
        accumulatedDrag = 0f;
    }

    #endregion


    #region 平滑移动活塞
    private void SmoothChangePiston()
    {
        float newY = Mathf.SmoothDamp(
            piston.localPosition.y,
            targetPistonY,
            ref smoothVelocity,
            smoothTime
        );

        piston.localPosition = new Vector3(
            piston.localPosition.x,
            newY,
            piston.localPosition.z
        );
    }
    #endregion

    private void OnGasStateChanged(float pressure, float volume, float temperature)
    {
        // 正在拖拽时，不要覆盖玩家操作
        if (isDragging) return;

        // 限制体积在有效范围内
        float clampedVolume = Mathf.Clamp(volume, minHeight, maxHeight);
        
        // 根据体积计算目标位置
        targetPistonY = PistonYFromVolume(clampedVolume);
    } 


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
        return Mathf.Clamp(volume, minHeight + minVolumeEpsilon, maxHeight);
    }

    // 根据给定体积返回对应的活塞Y位置（不修改实际位置）
    private float PistonYFromVolume(float volume)
    {
        float normalizedVolume = Mathf.InverseLerp(minHeight, maxHeight, volume);
        float pistonY = Mathf.Lerp(cylinderHeight / 2, -cylinderHeight / 2, normalizedVolume);
        return pistonY;
    }

    private float GetPressure()
    {
        float volume = GetCurrentVolume();
        // 防止除以0
        float safeVolume = Mathf.Max(volume, minVolumeEpsilon);
        float pressure = (IdealGasSimulation.Instance.moles * IdealGasSimulation.R * IdealGasSimulation.Instance.GetTemperature()) / safeVolume;
        return Mathf.Clamp(pressure, IdealGasSimulation.Instance.GetMinPressure(), IdealGasSimulation.Instance.GetMaxPressure()); // 保证压强在合理范围内
    }

    // 设置活塞位置（对应体积），会平滑移动到该位置
    public void SetPistonPosition(float volume)
    {
        // 将体积映射到[0, 1]范围
        float normalizedVolume = Mathf.InverseLerp(minHeight, maxHeight, volume);

        // 将归一化的体积值映射到活塞的Y位置
        float pistonY = Mathf.Lerp(cylinderHeight / 2, -cylinderHeight / 2, normalizedVolume);


        targetPistonY=pistonY; // 设置目标位置，平滑移动到该位置
        // 设置活塞位置
        piston.localPosition = new Vector3(piston.localPosition.x, pistonY, piston.localPosition.z);
    }
    
    public void SetCurrentProcess(IdealGasSimulation.ProcessType process)
    {
        currentProcess = process;
        SetPistonDragged(process); 

    }

    private void SetPistonDragged(IdealGasSimulation.ProcessType process)
    {
        canDrag= process switch
        {
            IdealGasSimulation.ProcessType.Isothermal => true, // 等温过程允许拖动
            IdealGasSimulation.ProcessType.Isobaric => true, // 等压过程允许拖动
            IdealGasSimulation.ProcessType.Isochoric => false, // 等容过程不允许拖动
            IdealGasSimulation.ProcessType.Null => false, // 未选择状态不允许拖动
            _ => false
        };

    }

    
    // 检查体积是否稳定
    public bool IsVolumeStable()
    {
        return volumeChangeRate < 0.01f; // 变化小于0.01L/秒视为稳定
    }
    

    public bool GetPistonDragged()
    {
        return canDrag;
    }

}