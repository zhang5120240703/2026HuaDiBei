using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GraphRenderer : MonoBehaviour
{
    // 引用
    public DataCollector dataCollector;
    public ExperimentStepController experimentStepController;

    // 图表区域（Inspector 指定）
    public RectTransform pvGraphArea;
    public RectTransform pInverseVGraphArea;
    public RectTransform ptGraphArea;
    public RectTransform pInverseTGraphArea;

    // UI 纹理与 RawImage
    private RawImage pvRawImage;
    private RawImage pInvRawImage;
    private RawImage ptRawImage;
    private RawImage pInvTRawImage;
    private Texture2D pvTexture;
    private Texture2D pInvTexture;
    private Texture2D ptTexture;
    private Texture2D pInvTTexture;

    // 数据点标记（UI Image）
    private List<GameObject> dataPointMarkers = new List<GameObject>();

    // UI 轴对象（用于 reset）
    private List<GameObject> axisObjects = new List<GameObject>();

    // 参数
    private const int defaultTexW = 512;
    private const int defaultTexH = 320;
    private const float pointSize = 12f; // 像素
    private const int lineWidth = 2; // 线宽
    private readonly Color backgroundColor = new Color(0f, 0f, 0f, 0f);
    private readonly Color pvLineColor = Color.red;
    private readonly Color pInvLineColor = Color.red;
    private readonly Color fitLineColor = Color.red;
    private readonly Color pointColor = Color.blue;

    
    // 字体与点图标（Inspector 指定，可选）
    public TMP_FontAsset font;
    public Sprite uiImage;

    private void Start()
    {
        InitializeGraphs(); // 创建轴等 UI 元素
        InitializeTextures(); // 创建 RawImage + Texture2D

        if (dataCollector != null)
        {
            //dataCollector.OnDataCollected += UpdateGraphs;
            dataCollector.OnAnalysisCompleted += UpdateGraphs;
        }
    }

    private void OnDisable()
    {
        if (dataCollector != null)
        {
            //dataCollector.OnDataCollected -= UpdateGraphs;
            dataCollector.OnAnalysisCompleted -= UpdateGraphs;
        }
    }

    // 初始化
    private void InitializeGraphs()
    {
        DrawAxes(pvGraphArea, "体积 (L)", "压力 (kPa)");
        DrawAxes(pInverseVGraphArea, "1/体积 (1/L)", "压力 (kPa)");
        DrawAxes(ptGraphArea, "温度 (K)", "压力 (kPa)");
        DrawAxes(pInverseTGraphArea, "1/温度 (1/K)", "压力 (kPa)");
    }

    private void InitializeTextures()
    {
        // PV 区域
        pvRawImage = GetOrAddRawImage(pvGraphArea);
        CreateOrResizeTexture(ref pvTexture, pvGraphArea, pvRawImage);

        // PInverseV 区域
        pInvRawImage = GetOrAddRawImage(pInverseVGraphArea);
        CreateOrResizeTexture(ref pInvTexture, pInverseVGraphArea, pInvRawImage);

        // PT 区域
        ptRawImage = GetOrAddRawImage(ptGraphArea);
        CreateOrResizeTexture(ref ptTexture, ptGraphArea, ptRawImage);

        // PInverseT 区域
        pInvTRawImage = GetOrAddRawImage(pInverseTGraphArea);
        CreateOrResizeTexture(ref pInvTTexture, pInverseTGraphArea, pInvTRawImage);
    }

    private RawImage GetOrAddRawImage(RectTransform area)
    {
        if (area == null) return null;
        var ri = area.GetComponent<RawImage>();
        if (ri == null) ri = area.gameObject.AddComponent<RawImage>();
        // 保证 RawImage 在最下层（不会遮挡轴文本，若需要可调整 hierarchy）
        ri.raycastTarget = false;
        return ri;
    }

    private void CreateOrResizeTexture(ref Texture2D tex, RectTransform area, RawImage target)
    {
        if (area == null || target == null) return;
        int w = Mathf.Max(defaultTexW, Mathf.RoundToInt(area.rect.width));
        int h = Mathf.Max(defaultTexH, Mathf.RoundToInt(area.rect.height));
        if (tex == null || tex.width != w || tex.height != h)
        {
            if (tex != null) Destroy(tex);
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
        }
        ClearTexture(tex);
        target.texture = tex;
    }

    #region 坐标轴与文本
    private void DrawAxes(RectTransform graphArea, string xLabel, string yLabel)
    {
        if (graphArea == null) return;

        Color axisColor = new Color(1f, 1f, 1f, 1f);
        float axisThickness = 8f;

        // 垂直轴
        GameObject vAxis = CreateUIImage("Axis_V", graphArea, axisColor);
        var vRt = vAxis.GetComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0f, 0f);
        vRt.anchorMax = new Vector2(0f, 1f);
        vRt.pivot = new Vector2(0.5f, 0.5f);
        vRt.anchoredPosition = new Vector2(4f, 0f);
        vRt.sizeDelta = new Vector2(axisThickness, 0f);

        // 水平轴
        GameObject hAxis = CreateUIImage("Axis_H", graphArea, axisColor);
        var hRt = hAxis.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 0f);
        hRt.anchorMax = new Vector2(1f, 0f);
        hRt.pivot = new Vector2(0.5f, 0.5f);
        hRt.anchoredPosition = new Vector2(0f, 4f);
        hRt.sizeDelta = new Vector2(0f, axisThickness);

        // 标签
        GameObject xLabelGO = CreateUIText("XLabel", graphArea, xLabel);
        var xRt = xLabelGO.GetComponent<RectTransform>();
        xRt.anchorMin = new Vector2(0.5f, 0f);
        xRt.anchorMax = new Vector2(0.5f, 0f);
        xRt.pivot = new Vector2(0.5f, 1f);
        xRt.anchoredPosition = new Vector2(0f, 6f);
        xRt.sizeDelta = new Vector2(graphArea.rect.width * 0.6f, 24f);

        GameObject yLabelGO = CreateUIText("YLabel", graphArea, yLabel);
        var yRt = yLabelGO.GetComponent<RectTransform>();
        yRt.anchorMin = new Vector2(0f, 0.5f);
        yRt.anchorMax = new Vector2(0f, 0.5f);
        yRt.pivot = new Vector2(0.5f, 0.5f);
        yRt.anchoredPosition = new Vector2(12f, 0f);
        yRt.sizeDelta = new Vector2(graphArea.rect.height * 0.6f, 24f);
        yRt.localRotation = Quaternion.Euler(0f, 0f, 90f);

        axisObjects.Add(vAxis);
        axisObjects.Add(hAxis);
        axisObjects.Add(xLabelGO);
        axisObjects.Add(yLabelGO);
    }

    private GameObject CreateUIImage(string name, RectTransform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    private GameObject CreateUIText(string name, RectTransform parent, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = 32;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.red;
        txt.raycastTarget = false;
        if (font != null) txt.font = font;
        return go;
    }
    #endregion

    public void UpdateGraphs()
    {
        // 检查是否进入数据分析阶段，只有在数据分析阶段才绘制图像
        //if (experimentStepController != null && 
        //    experimentStepController.GetCurrentStage() != ExperimentStepController.ExperimentStage.DataAnalysis &&
        //    experimentStepController.GetCurrentStage() != ExperimentStepController.ExperimentStage.Conclusion)
        //{
        //    return;
        //}

        if (dataCollector == null) return;
        var points = dataCollector.GetDataPoints();
        if (points == null || points.Count == 0)
        {
            // 清空
            if (pvTexture != null) { ClearTexture(pvTexture); pvRawImage.texture = pvTexture; }
            if (pInvTexture != null) { ClearTexture(pInvTexture); pInvRawImage.texture = pInvTexture; }
            if (ptTexture != null) { ClearTexture(ptTexture); ptRawImage.texture = ptTexture; }
            if (pInvTTexture != null) { ClearTexture(pInvTTexture); pInvTRawImage.texture = pInvTTexture; }
            ClearDataPointMarkers();
            return;
        }

        // 过滤掉压强、温度或体积值为0的异常数据点
        var validPoints = new List<DataCollector.DataPoint>();
        foreach (var p in points)
        {
            if (p.pressure > 0 && p.temperature > 0 && p.volume > 0)
            {
                validPoints.Add(p);
            }
        }

        if (validPoints.Count == 0)
        {
            // 清空
            if (pvTexture != null) { ClearTexture(pvTexture); pvRawImage.texture = pvTexture; }
            if (pInvTexture != null) { ClearTexture(pInvTexture); pInvRawImage.texture = pInvTexture; }
            if (ptTexture != null) { ClearTexture(ptTexture); ptRawImage.texture = ptTexture; }
            if (pInvTTexture != null) { ClearTexture(pInvTTexture); pInvTRawImage.texture = pInvTTexture; }
            ClearDataPointMarkers();
            return;
        }

        // 确保纹理尺寸与 rect 同步（UI 可能 resize）
        CreateOrResizeTexture(ref pvTexture, pvGraphArea, pvRawImage);
        CreateOrResizeTexture(ref pInvTexture, pInverseVGraphArea, pInvRawImage);
        CreateOrResizeTexture(ref ptTexture, ptGraphArea, ptRawImage);
        CreateOrResizeTexture(ref pInvTTexture, pInverseTGraphArea, pInvTRawImage);

        DrawGraphToTexture(pvTexture, validPoints, GraphType.PV);
        DrawGraphToTexture(pInvTexture, validPoints, GraphType.PInverseV);
        DrawGraphToTexture(ptTexture, validPoints, GraphType.PT);
        DrawGraphToTexture(pInvTTexture, validPoints, GraphType.PInverseT);


    }

    private enum GraphType { PV, PInverseV, PT, PInverseT }

    private void DrawGraphToTexture(Texture2D tex, List<DataCollector.DataPoint> pts, GraphType type)
    {
        if (tex == null) return;

        ClearTexture(tex);

        // 计算像素点
        var pixList = new List<Vector2Int>();
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2Int pix;
            switch (type)
            {
                case GraphType.PV:
                    pix = DataPointToPixelPV(tex, pts[i]);
                    break;
                case GraphType.PInverseV:
                    pix = DataPointToPixelPInv(tex, pts[i]);
                    break;
                case GraphType.PT:
                    pix = DataPointToPixelPT(tex, pts[i]);
                    break;
                case GraphType.PInverseT:
                    pix = DataPointToPixelPInvT(tex, pts[i]);
                    break;
                default:
                    pix = Vector2Int.zero;
                    break;
            }
            pixList.Add(pix);
        }

        // 判断是否达到绘制连线的条件
        bool dataEnoughForLines = pts.Count >= dataCollector.GetRequiredPointsForLines();
        // 画折线 - 只在非等温/等容状态下绘制数据点连线
        // 避免等温状态下P-V图像中点的错误连接
        if(dataEnoughForLines)
        {
            bool shouldDrawDataLines = true;
            if (IdealGasSimulation.Instance != null)
            {
                var process = IdealGasSimulation.Instance.GetCurrentProcess();
                if ((type == GraphType.PV && process == IdealGasSimulation.ProcessType.Isothermal) ||
                    (type == GraphType.PInverseT && process == IdealGasSimulation.ProcessType.Isochoric))
                {
                    shouldDrawDataLines = false;
                }
            }

            if (shouldDrawDataLines)
            {
                Color lineCol = pvLineColor; // 使用统一的线颜色
                for (int i = 1; i < pixList.Count; i++)
                    DrawLine(tex, pixList[i - 1].x, pixList[i - 1].y, pixList[i].x, pixList[i].y, lineCol);
            }

                // 拟合线（根据不同图像类型）
                if (IdealGasSimulation.Instance != null)
                {
                    switch (type)
                    {
                        case GraphType.PV:
                            if (IdealGasSimulation.Instance.GetCurrentProcess() == IdealGasSimulation.ProcessType.Isothermal)
                                DrawPVFitOnTexture(tex);
                            break;
                        case GraphType.PInverseV:
                            if (IdealGasSimulation.Instance.GetCurrentProcess() == IdealGasSimulation.ProcessType.Isothermal)
                                DrawPInverseVFitOnTexture(tex);
                            break;
                        case GraphType.PT:
                            if (IdealGasSimulation.Instance.GetCurrentProcess() == IdealGasSimulation.ProcessType.Isobaric)
                                DrawPTFitOnTexture(tex);
                            break;
                        case GraphType.PInverseT:
                            if (IdealGasSimulation.Instance.GetCurrentProcess() == IdealGasSimulation.ProcessType.Isochoric)
                                DrawPInverseTFitOnTexture(tex);
                            break;
                    }
                }
        }

        // 画点
        foreach (var p in pixList)
            DrawFilledCircle(tex, p.x, p.y, Mathf.Max(2, Mathf.RoundToInt(pointSize / 3f)), pointColor);

        tex.Apply();

    }

    // 数据点 -> 像素映射 
    private Vector2Int DataPointToPixelPV(Texture2D tex, DataCollector.DataPoint p)
    {
        float minV = IdealGasSimulation.Instance.GetMinVolume();
        float maxV = IdealGasSimulation.Instance.GetMaxVolume();
        float minP = IdealGasSimulation.Instance.GetMinPressure();
        float maxP = IdealGasSimulation.Instance.GetMaxPressure();

        float nx = Mathf.InverseLerp(minV, maxV, p.volume);
        float ny = Mathf.InverseLerp(minP, maxP, p.pressure);

        int x = Mathf.RoundToInt(Mathf.Lerp(0, tex.width - 1, nx));
        int y = Mathf.RoundToInt(Mathf.Lerp(0, tex.height - 1, ny));
        return new Vector2Int(x, y);
    }

    private Vector2Int DataPointToPixelPInv(Texture2D tex, DataCollector.DataPoint p)
    {
        float minInvV = 1f / Mathf.Max(IdealGasSimulation.Instance.GetMaxVolume(), 1e-6f);
        float maxInvV = 1f / Mathf.Max(IdealGasSimulation.Instance.GetMinVolume(), 1e-6f);
        float minP = IdealGasSimulation.Instance.GetMinPressure();
        float maxP = IdealGasSimulation.Instance.GetMaxPressure();

        float nx = Mathf.InverseLerp(minInvV, maxInvV, p.inverseVolume);
        float ny = Mathf.InverseLerp(minP, maxP, p.pressure);

        int x = Mathf.RoundToInt(Mathf.Lerp(0, tex.width - 1, nx));
        int y = Mathf.RoundToInt(Mathf.Lerp(0, tex.height - 1, ny));
        return new Vector2Int(x, y);
    }

    private Vector2Int DataPointToPixelPT(Texture2D tex, DataCollector.DataPoint p)
    {
        float minT = IdealGasSimulation.Instance.GetMinTemperature();
        float maxT = IdealGasSimulation.Instance.GetMaxTemperature();
        float minP = IdealGasSimulation.Instance.GetMinPressure();
        float maxP = IdealGasSimulation.Instance.GetMaxPressure();

        float nx = Mathf.InverseLerp(minT, maxT, p.temperature);
        float ny = Mathf.InverseLerp(minP, maxP, p.pressure);

        int x = Mathf.RoundToInt(Mathf.Lerp(0, tex.width - 1, nx));
        int y = Mathf.RoundToInt(Mathf.Lerp(0, tex.height - 1, ny));
        return new Vector2Int(x, y);
    }

    private Vector2Int DataPointToPixelPInvT(Texture2D tex, DataCollector.DataPoint p)
    {
        float minInvT = 1f / Mathf.Max(IdealGasSimulation.Instance.GetMaxTemperature(), 1e-6f);
        float maxInvT = 1f / Mathf.Max(IdealGasSimulation.Instance.GetMinTemperature(), 1e-6f);
        float minP = IdealGasSimulation.Instance.GetMinPressure();
        float maxP = IdealGasSimulation.Instance.GetMaxPressure();

        float nx = Mathf.InverseLerp(minInvT, maxInvT, 1f / Mathf.Max(p.temperature, 1e-6f));
        float ny = Mathf.InverseLerp(minP, maxP, p.pressure);

        int x = Mathf.RoundToInt(Mathf.Lerp(0, tex.width - 1, nx));
        int y = Mathf.RoundToInt(Mathf.Lerp(0, tex.height - 1, ny));
        return new Vector2Int(x, y);
    }

    private void DrawPVFitOnTexture(Texture2D tex)
    {
        var sim = IdealGasSimulation.Instance;
        if (sim == null) return;
        int samples = 100;
        Vector2Int prev = Vector2Int.zero;

        // 体积范围，不向x=0方向延伸，只向体积增大的方向延伸
        float minV = sim.GetMinVolume();
        float maxV = sim.GetMaxVolume();
        float extendedMaxV = maxV * 1.2f; // 只向体积增大方向延伸

        for (int i = 0; i < samples; i++)
        {
            float v = Mathf.Lerp(minV, extendedMaxV, i / (float)(samples - 1));
            float p = (sim.moles * IdealGasSimulation.R * sim.GetTemperature()) / Mathf.Max(v, 1e-6f);
            var dp = new DataCollector.DataPoint { volume = v, pressure = p, inverseVolume = 1f / Mathf.Max(v, 1e-6f) };
            var pix = DataPointToPixelPV(tex, dp);
            if (i > 0) DrawLine(tex, prev.x, prev.y, pix.x, pix.y, fitLineColor);
            prev = pix;
        }
    }

    private void DrawPInverseVFitOnTexture(Texture2D tex)
    {
        float k = dataCollector.GetAveragePVProduct();
        var sim = IdealGasSimulation.Instance;
        if (sim == null) return;

        // 1/V的范围，只向1/V增大的方向延伸，不向y轴（x=0）延伸
        float minInvV = 1f / Mathf.Max(sim.GetMaxVolume(), 1e-6f);
        float maxInvV = 1f / Mathf.Max(sim.GetMinVolume(), 1e-6f);
        float extendedMaxInvV = maxInvV * 1.2f; // 只向1/V增大方向延伸

        var p0 = new DataCollector.DataPoint { inverseVolume = minInvV, pressure = k * minInvV, volume = sim.GetMaxVolume() };
        var p1 = new DataCollector.DataPoint { inverseVolume = extendedMaxInvV, pressure = k * extendedMaxInvV, volume = 1f / extendedMaxInvV };
        var pix0 = DataPointToPixelPInv(tex, p0);
        var pix1 = DataPointToPixelPInv(tex, p1);
        DrawLine(tex, pix0.x, pix0.y, pix1.x, pix1.y, fitLineColor);
    }

    private void DrawPTFitOnTexture(Texture2D tex)
    {
        var sim = IdealGasSimulation.Instance;
        if (sim == null) return;
        int samples = 100;
        Vector2Int prev = Vector2Int.zero;

        // 温度范围，不向x=0方向延伸，只向温度增大的方向延伸
        float minT = sim.GetMinTemperature();
        float maxT = sim.GetMaxTemperature();
        float extendedMaxT = maxT * 1.2f; // 只向温度增大方向延伸

        for (int i = 0; i < samples; i++)
        {
            float t = Mathf.Lerp(minT, extendedMaxT, i / (float)(samples - 1));
            // 等压过程：体积与温度成正比
            float v = (sim.GetVolume() / sim.GetTemperature()) * t;
            float p = (sim.moles * IdealGasSimulation.R * t) / Mathf.Max(v, 1e-6f);
            var dp = new DataCollector.DataPoint { volume = v, pressure = p, temperature = t, inverseVolume = 1f / Mathf.Max(v, 1e-6f) };
            var pix = DataPointToPixelPT(tex, dp);
            if (i > 0) DrawLine(tex, prev.x, prev.y, pix.x, pix.y, fitLineColor);
            prev = pix;
        }
    }

    private void DrawPInverseTFitOnTexture(Texture2D tex)
    {
        var sim = IdealGasSimulation.Instance;
        if (sim == null) return;
        int samples = 100;
        Vector2Int prev = Vector2Int.zero;

        // 1/T的范围，只向1/T增大的方向延伸，不向y轴（x=0）延伸
        float minInvT = 1f / Mathf.Max(sim.GetMaxTemperature(), 1e-6f);
        float maxInvT = 1f / Mathf.Max(sim.GetMinTemperature(), 1e-6f);
        float extendedMaxInvT = maxInvT * 1.2f; // 只向1/T增大方向延伸

        for (int i = 0; i < samples; i++)
        {
            float invT = Mathf.Lerp(minInvT, extendedMaxInvT, i / (float)(samples - 1));
            float t = 1f / invT;
            // 等容过程：压力与温度成正比
            float p = (sim.GetPressure() / sim.GetTemperature()) * t;
            var dp = new DataCollector.DataPoint { volume = sim.GetVolume(), pressure = p, temperature = t, inverseVolume = 1f / Mathf.Max(sim.GetVolume(), 1e-6f) };
            var pix = DataPointToPixelPInvT(tex, dp);
            if (i > 0) DrawLine(tex, prev.x, prev.y, pix.x, pix.y, fitLineColor);
            prev = pix;
        }
    }

    // 基础像素绘制（Bresenham + 圆）
    private void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = (dx > dy ? dx : -dy) / 2, e2;

        while (true)
        {
            // 绘制以当前点为中心的矩形区域，宽度为lineWidth
            for (int w = -lineWidth / 2; w <= lineWidth / 2; w++)
            {
                for (int h = -lineWidth / 2; h <= lineWidth / 2; h++)
                {
                    int px = Mathf.Clamp(x0 + w, 0, tex.width - 1);
                    int py = Mathf.Clamp(y0 + h, 0, tex.height - 1);
                    tex.SetPixel(px, py, col);
                }
            }

            if (x0 == x1 && y0 == y1) break;
            e2 = err;
            if (e2 > -dx) { err -= dy; x0 += sx; }
            if (e2 < dy) { err += dx; y0 += sy; }
        }
    }

    private void DrawFilledCircle(Texture2D tex, int cx, int cy, int r, Color col)
    {
        int r2 = r * r;
        int xmin = Mathf.Clamp(cx - r, 0, tex.width - 1);
        int xmax = Mathf.Clamp(cx + r, 0, tex.width - 1);
        int ymin = Mathf.Clamp(cy - r, 0, tex.height - 1);
        int ymax = Mathf.Clamp(cy + r, 0, tex.height - 1);

        for (int y = ymin; y <= ymax; y++)
        {
            for (int x = xmin; x <= xmax; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy <= r2)
                    tex.SetPixel(x, y, col);
            }
        }
    }

    // 绘制数据点标记
    private void DrawDataPointMarkers(List<DataCollector.DataPoint> pts)
    {
        ClearDataPointMarkers();
        Sprite s = uiImage;
        foreach (var p in pts)
        {
            var pvPix = DataPointToPixelPV(pvTexture, p);
            var go = CreateDataPointMarker(pvGraphArea, new Vector2(pvPix.x, pvPix.y), Color.cyan, s);
            dataPointMarkers.Add(go);

            var invPix = DataPointToPixelPInv(pInvTexture, p);
            var go2 = CreateDataPointMarker(pInverseVGraphArea, new Vector2(invPix.x, invPix.y), Color.yellow, s);
            dataPointMarkers.Add(go2);

            var ptPix = DataPointToPixelPT(ptTexture, p);
            var go3 = CreateDataPointMarker(ptGraphArea, new Vector2(ptPix.x, ptPix.y), Color.green, s);
            dataPointMarkers.Add(go3);

            var invTPix = DataPointToPixelPInvT(pInvTTexture, p);
            var go4 = CreateDataPointMarker(pInverseTGraphArea, new Vector2(invTPix.x, invTPix.y), Color.magenta, s);
            dataPointMarkers.Add(go4);
        }
    }

    private GameObject CreateDataPointMarker(RectTransform parent, Vector2 localPos, Color color, Sprite sprite)
    {
        GameObject go = new GameObject("DataPoint", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();


        Rect r = parent.rect;
        float x = Mathf.Lerp(r.xMin, r.xMax, localPos.x / Mathf.Max(1, parent.GetComponent<RawImage>()?.texture?.width ?? defaultTexW));
        float y = Mathf.Lerp(r.yMin, r.yMax, localPos.y / Mathf.Max(1, parent.GetComponent<RawImage>()?.texture?.height ?? defaultTexH));
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(pointSize, pointSize);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    //清除数据点标记
    private void ClearDataPointMarkers()
    {
        foreach (var m in dataPointMarkers) if (m != null) Destroy(m);
        dataPointMarkers.Clear();
    }

    // 清空纹理
    private void ClearTexture(Texture2D tex)
    {
        if (tex == null) return;
        Color[] cols = tex.GetPixels();
        for (int i = 0; i < cols.Length; i++) cols[i] = backgroundColor;
        tex.SetPixels(cols);
        tex.Apply();
    }

    public void ResetGraphs()
    {

        // 清除并销毁 PV 纹理
        if (pvTexture != null)
        {
            ClearTexture(pvTexture);
            if (pvRawImage != null) pvRawImage.texture = null;
            Destroy(pvTexture);
            pvTexture = null;
        }

        // 清除并销毁 PInverseV 纹理
        if (pInvTexture != null)
        {
            ClearTexture(pInvTexture);
            if (pInvRawImage != null) pInvRawImage.texture = null;
            Destroy(pInvTexture);
            pInvTexture = null;
        }

        // 清除并销毁 PT 纹理
        if (ptTexture != null)
        {
            ClearTexture(ptTexture);
            if (ptRawImage != null) ptRawImage.texture = null;
            Destroy(ptTexture);
            ptTexture = null;
        }

        // 清除并销毁 PInverseT 纹理
        if (pInvTTexture != null)
        {
            ClearTexture(pInvTTexture);
            if (pInvTRawImage != null) pInvTRawImage.texture = null;
            Destroy(pInvTTexture);
            pInvTTexture = null;
        }

        //// 清除 RawImage 引用（可选：保留 RawImage 组件）
        //if (pvRawImage != null) pvRawImage.texture = null;
        //if (pInvRawImage != null) pInvRawImage.texture = null;
        //if (ptRawImage != null) ptRawImage.texture = null;
        //if (pInvTRawImage != null) pInvTRawImage.texture = null;

        // 清除所有数据点 UI 标记
        ClearDataPointMarkers();

        // 重新初始化纹理（使界面呈现空白画布）
        InitializeTextures();


    }

    
}