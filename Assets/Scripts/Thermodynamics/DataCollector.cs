using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ExperimentStepController;
using static IdealGasSimulation;

public class DataCollector : MonoBehaviour
{
    public CylinderController cylinderController;
    public ExperimentStepController experimentStepController;

    public class DataPoint
    {
        public float volume;
        public float pressure;
        public float temperature;
        public float pvProduct;
        public float inverseVolume;
        public float inverseTempreture;
        public float volumeChangeRate;
        public float settleTime;
        public bool sampledBeforeStable;
        public bool tooCloseToExisting;
    }

    private readonly List<DataPoint> dataPoints = new List<DataPoint>();
    private float lastVolumeChangeTime;
    private float lastInteractionEndTime;
    private const float stabilizationTime = 0.5f;

    private const float autoCollectInterval = 0.75f;
    private float autoCollectTimer = 0f;
    private int requiredPointsForLines = 12;
    private const float marginalSettleWindow = 0.35f;
    private const float marginalVolumeRateThreshold = 0.004f;
    private const float postInteractionSettleTime = 0.4f;
    private bool isUserInteracting;

    [Tooltip("Minimum normalized spacing between sampled points.")]
    private float minNormalizedSpacing = 0.01f;

    private float averagePVProduct;
    private float pvStandardDeviation;
    private float PVAverageErrorPercentage;
    private float VTAverageErrorPercentage;
    private float PTAverageErrorPercentage;
    private float currentAverageValue;
    private float currentRelativeStd;
    private float dataCoverageRatio;
    private float unstableSampleRatio;
    private bool lastVerificationPassed;
    private bool isConfirm = false;

    public Action OnDataCollected;
    public Action OnAnalysisCompleted;

    private void Start()
    {
        lastVolumeChangeTime = Time.time;
        lastInteractionEndTime = Time.time;
    }

    private void Update()
    {
        autoCollectTimer += Time.deltaTime;

        bool readyToSample = !isUserInteracting &&
                             Time.time - lastInteractionEndTime >= postInteractionSettleTime &&
                             cylinderController.IsVolumeStable() &&
                             Time.time - lastVolumeChangeTime >= stabilizationTime;

        if (readyToSample &&
            dataPoints.Count < requiredPointsForLines &&
            autoCollectTimer >= autoCollectInterval &&
            experimentStepController.GetCurrentStage() == ExperimentStage.DataCollection)
        {
            if (CollectDataPoint())
            {
                autoCollectTimer = 0f;
            }
        }

        NeedAnalyzData();
    }

    public void ConfirmParameter()
    {
        if (isConfirm)
        {
            return;
        }

        ExperimentStage currentStage = experimentStepController.GetCurrentStage();
        if (currentStage != ExperimentStage.DataCollection)
        {
            return;
        }

        isConfirm = true;
    }

    public void OnVolumeChanged(float newVolume)
    {
        lastVolumeChangeTime = Time.time;
    }

    public void SetUserInteracting(bool interacting)
    {
        isUserInteracting = interacting;
        if (!interacting)
        {
            lastInteractionEndTime = Time.time;
        }
    }

    private DataPoint CreatDataPoint()
    {
        float pressure = IdealGasSimulation.Instance.GetPressure();
        float volume = IdealGasSimulation.Instance.GetVolume();
        float temperature = IdealGasSimulation.Instance.GetTemperature();
        float volumeRate = cylinderController.GetVolumeChangeRate();
        float settleTime = Time.time - lastVolumeChangeTime;

        float speedFactor = Mathf.Clamp01(volumeRate / 0.25f);
        float settlePenalty = Mathf.Clamp01((stabilizationTime - settleTime) / stabilizationTime);
        float operationFactor = Mathf.Clamp01(Mathf.Max(speedFactor, settlePenalty));
        float noisePercent = Mathf.Lerp(0.003f, 0.06f, operationFactor);

        ApplyMeasurementNoise(ref pressure, ref volume, ref temperature, noisePercent);

        DataPoint point = new DataPoint
        {
            volume = volume,
            pressure = pressure,
            temperature = temperature,
            pvProduct = pressure * volume,
            inverseVolume = 1.0f / Mathf.Max(volume, 1e-6f),
            inverseTempreture = 1.0f / Mathf.Max(temperature, 1e-6f),
            volumeChangeRate = volumeRate,
            settleTime = settleTime,
            sampledBeforeStable = IsMarginalSample(settleTime, volumeRate)
        };

        bool exists = dataPoints.Any(p =>
            Mathf.Approximately(p.volume, point.volume) &&
            Mathf.Approximately(p.pressure, point.pressure) &&
            Mathf.Approximately(p.temperature, point.temperature));

        if (exists)
        {
            return null;
        }

        bool tooClose = IsTooClose(point);
        point.tooCloseToExisting = tooClose;
        if (tooClose)
        {
            return null;
        }

        return point;
    }

    private void ApplyMeasurementNoise(ref float pressure, ref float volume, ref float temperature, float noisePercent)
    {
        ProcessType process = IdealGasSimulation.Instance.GetCurrentProcess();

        switch (process)
        {
            case ProcessType.Isothermal:
                pressure += pressure * UnityEngine.Random.Range(-noisePercent, noisePercent);
                volume += volume * UnityEngine.Random.Range(-noisePercent, noisePercent);
                break;

            case ProcessType.Isobaric:
                volume += volume * UnityEngine.Random.Range(-noisePercent, noisePercent);
                temperature += temperature * UnityEngine.Random.Range(-noisePercent, noisePercent);
                pressure += pressure * UnityEngine.Random.Range(-noisePercent * 0.2f, noisePercent * 0.2f);
                break;

            case ProcessType.Isochoric:
                pressure += pressure * UnityEngine.Random.Range(-noisePercent, noisePercent);
                temperature += temperature * UnityEngine.Random.Range(-noisePercent, noisePercent);
                volume += volume * UnityEngine.Random.Range(-noisePercent * 0.2f, noisePercent * 0.2f);
                break;
        }
    }

    private bool IsTooClose(DataPoint candidate)
    {
        if (dataPoints.Count == 0)
        {
            return false;
        }

        var sim = IdealGasSimulation.Instance;
        float minV = sim.GetMinVolume();
        float maxV = sim.GetMaxVolume();
        float minP = sim.GetMinPressure();
        float maxP = sim.GetMaxPressure();

        float vRange = Mathf.Max(1e-6f, maxV - minV);
        float pRange = Mathf.Max(1e-6f, maxP - minP);
        ProcessType type = sim.GetCurrentProcess();

        foreach (var p in dataPoints)
        {
            float dist = 0f;
            switch (type)
            {
                case ProcessType.Isochoric:
                    dist = Mathf.Abs(candidate.pressure - p.pressure) / pRange;
                    break;

                case ProcessType.Isothermal:
                    float nx = Mathf.Abs(candidate.volume - p.volume) / vRange;
                    float ny = Mathf.Abs(candidate.pressure - p.pressure) / pRange;
                    dist = Mathf.Sqrt(nx * nx + ny * ny);
                    break;

                case ProcessType.Isobaric:
                    dist = Mathf.Abs(candidate.volume - p.volume) / vRange;
                    break;
            }

            if (dist < minNormalizedSpacing)
            {
                return true;
            }
        }

        return false;
    }

    public bool CollectDataPoint()
    {
        DataPoint point = CreatDataPoint();
        if (point == null)
        {
            return false;
        }

        dataPoints.Add(point);
        OnDataCollected?.Invoke();
        return true;
    }

    private void NeedAnalyzData()
    {
        if (experimentStepController.GetCurrentStage() != ExperimentStage.DataAnalysis)
        {
            return;
        }

        if (dataPoints.Count >= requiredPointsForLines && isConfirm)
        {
            AnalyzeData();
            isConfirm = false;
        }
    }

    private void AnalyzeData()
    {
        if (dataPoints.Count == 0)
        {
            return;
        }

        float sumPV = 0f;
        float sumPVSquared = 0f;
        foreach (var point in dataPoints)
        {
            sumPV += point.pvProduct;
            sumPVSquared += point.pvProduct * point.pvProduct;
        }

        averagePVProduct = sumPV / dataPoints.Count;
        float variance = Mathf.Max(0f, (sumPVSquared / dataPoints.Count) - (averagePVProduct * averagePVProduct));
        pvStandardDeviation = Mathf.Sqrt(variance);

        unstableSampleRatio = CalculateUnstableSampleRatio();
        AnalyzeCurrentProcess();
        OnAnalysisCompleted?.Invoke();
    }

    private void AnalyzeCurrentProcess()
    {
        ProcessType process = IdealGasSimulation.Instance.GetCurrentProcess();

        switch (process)
        {
            case ProcessType.Isothermal:
                AnalyzeMetric(point => point.pvProduct, out currentAverageValue, out PVAverageErrorPercentage, out currentRelativeStd);
                dataCoverageRatio = CalculateRangeCoverage(point => point.volume, IdealGasSimulation.Instance.GetMinVolume(), IdealGasSimulation.Instance.GetMaxVolume());
                lastVerificationPassed = PVAverageErrorPercentage < 3.0f &&
                                         currentRelativeStd < 0.05f &&
                                         dataCoverageRatio > 0.28f &&
                                         unstableSampleRatio <= 0.35f;
                break;

            case ProcessType.Isobaric:
                AnalyzeMetric(point => point.volume / Mathf.Max(point.temperature, 1e-6f), out currentAverageValue, out VTAverageErrorPercentage, out currentRelativeStd);
                dataCoverageRatio = CalculateRangeCoverage(point => point.temperature, IdealGasSimulation.Instance.GetMinTemperature(), IdealGasSimulation.Instance.GetMaxTemperature());
                lastVerificationPassed = VTAverageErrorPercentage < 3.0f &&
                                         currentRelativeStd < 0.05f &&
                                         dataCoverageRatio > 0.22f &&
                                         unstableSampleRatio <= 0.35f;
                break;

            case ProcessType.Isochoric:
                AnalyzeMetric(point => point.pressure / Mathf.Max(point.temperature, 1e-6f), out currentAverageValue, out PTAverageErrorPercentage, out currentRelativeStd);
                dataCoverageRatio = CalculateRangeCoverage(point => point.temperature, IdealGasSimulation.Instance.GetMinTemperature(), IdealGasSimulation.Instance.GetMaxTemperature());
                lastVerificationPassed = PTAverageErrorPercentage < 3.0f &&
                                         currentRelativeStd < 0.05f &&
                                         dataCoverageRatio > 0.22f &&
                                         unstableSampleRatio <= 0.35f;
                break;

            default:
                currentAverageValue = 0f;
                currentRelativeStd = 0f;
                dataCoverageRatio = 0f;
                lastVerificationPassed = false;
                break;
        }
    }

    private void AnalyzeMetric(Func<DataPoint, float> selector, out float average, out float averageError, out float relativeStd)
    {
        average = 0f;
        averageError = 0f;
        relativeStd = 0f;

        if (dataPoints.Count == 0)
        {
            return;
        }

        float sum = 0f;
        float sumSquares = 0f;
        foreach (var point in dataPoints)
        {
            float value = selector(point);
            sum += value;
            sumSquares += value * value;
        }

        average = sum / dataPoints.Count;
        float variance = Mathf.Max(0f, (sumSquares / dataPoints.Count) - (average * average));
        float std = Mathf.Sqrt(variance);
        relativeStd = average > 1e-6f ? std / average : 0f;

        float totalError = 0f;
        foreach (var point in dataPoints)
        {
            float value = selector(point);
            totalError += Mathf.Abs(value - average) / Mathf.Max(Mathf.Abs(average), 1e-6f) * 100f;
        }

        averageError = totalError / dataPoints.Count;
    }

    private float CalculateRangeCoverage(Func<DataPoint, float> selector, float minValue, float maxValue)
    {
        if (dataPoints.Count == 0)
        {
            return 0f;
        }

        float observedMin = float.MaxValue;
        float observedMax = float.MinValue;
        foreach (var point in dataPoints)
        {
            float value = selector(point);
            observedMin = Mathf.Min(observedMin, value);
            observedMax = Mathf.Max(observedMax, value);
        }

        float fullRange = Mathf.Max(maxValue - minValue, 1e-6f);
        return Mathf.Clamp01((observedMax - observedMin) / fullRange);
    }

    private float CalculateUnstableSampleRatio()
    {
        if (dataPoints.Count == 0)
        {
            return 0f;
        }

        int unstableCount = 0;
        foreach (var point in dataPoints)
        {
            if (point.sampledBeforeStable || point.tooCloseToExisting)
            {
                unstableCount++;
            }
        }

        return unstableCount / (float)dataPoints.Count;
    }

    private bool IsMarginalSample(float settleTime, float volumeRate)
    {
        bool justBarelySettled = settleTime < stabilizationTime + marginalSettleWindow;
        bool stillMovingNoticeably = volumeRate > marginalVolumeRateThreshold;
        return justBarelySettled || stillMovingNoticeably;
    }

    public void ResetData()
    {
        dataPoints.Clear();
        lastVolumeChangeTime = Time.time;
        autoCollectTimer = 0f;
        averagePVProduct = 0f;
        pvStandardDeviation = 0f;
        PVAverageErrorPercentage = 0f;
        VTAverageErrorPercentage = 0f;
        PTAverageErrorPercentage = 0f;
        currentAverageValue = 0f;
        currentRelativeStd = 0f;
        dataCoverageRatio = 0f;
        unstableSampleRatio = 0f;
        lastVerificationPassed = false;
        isConfirm = false;
        isUserInteracting = false;
        lastInteractionEndTime = Time.time;
    }

    public bool IsBoyleLawVerified()
    {
        return IdealGasSimulation.Instance.GetCurrentProcess() == ProcessType.Isothermal && lastVerificationPassed;
    }

    public bool IsCharlesLawVerified()
    {
        return IdealGasSimulation.Instance.GetCurrentProcess() == ProcessType.Isobaric && lastVerificationPassed;
    }

    public bool IsGayLussacLawVerified()
    {
        return IdealGasSimulation.Instance.GetCurrentProcess() == ProcessType.Isochoric && lastVerificationPassed;
    }

    public List<DataPoint> GetDataPoints()
    {
        return dataPoints;
    }

    public float GetAveragePVProduct()
    {
        return averagePVProduct;
    }

    public float GetPVStandardDeviation()
    {
        return pvStandardDeviation;
    }

    public float GetPVAverageErrorPercentage()
    {
        return PVAverageErrorPercentage;
    }

    public float GetVTAverageErrorPercentage()
    {
        return VTAverageErrorPercentage;
    }

    public float GetPTAverageErrorPercentage()
    {
        return PTAverageErrorPercentage;
    }

    public float GetCurrentAverageValue()
    {
        return currentAverageValue;
    }

    public float GetCurrentRelativeStd()
    {
        return currentRelativeStd;
    }

    public float GetDataCoverageRatio()
    {
        return dataCoverageRatio;
    }

    public float GetUnstableSampleRatio()
    {
        return unstableSampleRatio;
    }


    public int GetDataPointCount()
    {
        return dataPoints.Count;
    }

    public int GetRequiredPointsForLines()
    {
        return requiredPointsForLines;
    }

    public bool GetIsConfirm()
    {
        return isConfirm;
    }
}
