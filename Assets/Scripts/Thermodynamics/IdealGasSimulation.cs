using UnityEngine;

public class IdealGasSimulation : MonoBehaviour
{
    public static IdealGasSimulation Instance;

    // 理想气体常数 R (J/(mol·K))
    public const float R = 8.314f;
    
    // 气体摩尔数 (mol)
    public float moles = 0.04f; // 约1升气体在标准状况下的摩尔数
    
    // 当前状态变量
    private float pressure; // 压力 (kPa)
    private float volume; // 体积 (L)
    private float temperature; // 温度 (K)
    
    // 过程类型
    public enum ProcessType
    {
        Isothermal=0, // 等温过程0
        Isobaric=1, // 等压过程1
        Isochoric=2 // 等容过程2
    }
    
    public ProcessType currentProcess = ProcessType.Isothermal;
    
    // 固定值（根据当前过程）
    private float fixedValue;
    
    // 体积范围限制
    private const float minVolume = 0.0f;
    private const float maxVolume = 2.0f;
    
    // 温度范围限制
    private const float minTemperature = 100.0f;
    private const float maxTemperature = 500.0f;
    
    // 压力范围限制
    public const float minPressure = 1.0f;
    public const float maxPressure = 500.0f;
    
    // 事件
    public System.Action<float, float, float> OnStateChanged;

    private void Awake()
    {
        if(Instance ==null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        
    }
    private void Start()
    {
        // 初始设置
        volume = 1.0f;
        temperature = 300.0f;
        UpdatePressure();
        SetProcess(ProcessType.Isothermal);
    }
    
    public void SetProcess(ProcessType process)
    {
        currentProcess = process;
        
        // 记录当前值作为固定值
        switch (process)
        {
            case ProcessType.Isothermal:// 等温过程：温度固定
                fixedValue = temperature;
                break;
            case ProcessType.Isobaric:// 等压过程：压力固定
                fixedValue = pressure;
                break;
            case ProcessType.Isochoric:// 等容过程：体积固定
                fixedValue = volume;
                break;
        }
    }
    
    public void SetVolume(float newVolume)
    {
        // 限制体积范围
        volume = Mathf.Clamp(newVolume, minVolume, maxVolume);
        
        // 根据当前过程更新其他参数
        switch (currentProcess)
        {
            case ProcessType.Isothermal:
                // 等温过程：PV = 常数
                temperature = fixedValue;
                UpdatePressure();
                break;
            case ProcessType.Isobaric:
                // 等压过程：V/T = 常数
                pressure = fixedValue;
                UpdateTemperature();
                break;
            case ProcessType.Isochoric:
                // 等容过程：P/T = 常数
                volume = fixedValue;
                UpdatePressure();
                break;
        }
        
        NotifyStateChanged();
    }
    
    public void SetTemperature(float newTemperature)
    {
        // 限制温度范围
        temperature = Mathf.Clamp(newTemperature, minTemperature, maxTemperature);
        
        // 根据当前过程更新其他参数
        switch (currentProcess)
        {
            case ProcessType.Isothermal:
                // 等温过程：温度固定
                temperature = fixedValue;
                break;
            case ProcessType.Isobaric:
                // 等压过程：V/T = 常数
                pressure = fixedValue;
                UpdateVolume();
                break;
            case ProcessType.Isochoric:
                // 等容过程：P/T = 常数
                volume = fixedValue;
                UpdatePressure();
                break;
        }
        
        NotifyStateChanged();
    }
    
    public void SetPressure(float newPressure)
    {
        // 限制压力范围
        pressure = Mathf.Clamp(newPressure, minPressure, maxPressure);
        
        // 根据当前过程更新其他参数
        switch (currentProcess)
        {
            case ProcessType.Isothermal:
                // 等温过程：PV = 常数
                temperature = fixedValue;
                UpdateVolume();
                break;
            case ProcessType.Isobaric:
                // 等压过程：压力固定
                pressure = fixedValue;
                break;
            case ProcessType.Isochoric:
                // 等容过程：P/T = 常数
                volume = fixedValue;
                UpdateTemperature();
                break;
        }
        
        NotifyStateChanged();
    }
    
    private void UpdatePressure()
    {
        // PV = nRT
        // P (kPa) = (nRT) / V (L) * 1000 (Pa/kPa) / 1000 (L/m³) = nRT / V
        pressure = (moles * R * temperature) / volume;
        pressure = Mathf.Clamp(pressure, minPressure, maxPressure);
    }
    
    private void UpdateVolume()
    {
        // V = nRT / P
        volume = (moles * R * temperature) / pressure;
        // 限制体积范围
        volume = Mathf.Clamp(volume, minVolume, maxVolume);
    }
    
    private void UpdateTemperature()
    {
        // T = PV / (nR)
        temperature = (pressure * volume) / (moles * R);
        // 限制温度范围
        temperature = Mathf.Clamp(temperature, minTemperature, maxTemperature);
    }
    
    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke(pressure, volume, temperature);
    }
    
    // 获取当前状态值
    public float GetPressure() { return pressure; }
    public float GetVolume() { return volume; }
    public float GetTemperature() { return temperature; }
    public float GetPVProduct() { return pressure * volume; }
    public float GetMinVolume() { return minVolume; }
    public float GetMaxVolume() { return maxVolume; }
    public float GetMinTemperature() { return minTemperature; }
    public float GetMaxTemperature() { return maxTemperature; }
    public float GetMinPressure() { return minPressure; }
    public float GetMaxPressure() { return maxPressure; }
    
    // 计算理论值（用于误差分析）
    public float CalculateTheoreticalPressure(float targetVolume, float targetTemperature)
    {
        return (moles * R * targetTemperature) / targetVolume;
    }
    
    public float CalculateTheoreticalVolume(float targetPressure, float targetTemperature)
    {
        return (moles * R * targetTemperature) / targetPressure;
    }
    
    public float CalculateTheoreticalTemperature(float targetPressure, float targetVolume)
    {
        return (targetPressure * targetVolume) / (moles * R);
    }
    
    // 计算误差
    public float CalculateError(float actual, float theoretical)
    {
        if (theoretical == 0) return 0;
        return Mathf.Abs((actual - theoretical) / theoretical) * 100f;
    }
}