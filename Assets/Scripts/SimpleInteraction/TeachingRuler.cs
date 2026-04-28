using UnityEngine;
using TMPro;
using System.Collections;

public class TeachingRuler_Final : MonoBehaviour
{
    [Header("直尺外观")]
    public float rulerLength = 2.2f;
    public float rulerWidth = 0.08f;
    public float rulerThickness = 0.012f;
    public Color rulerColor = new Color(1f, 0.95f, 0.7f);

    [Header("刻度设置（平贴尺面，从右边缘向内）")]
    public float majorTickInterval = 0.5f;
    public float minorTickInterval = 0.1f;
    public float majorTickLength = 0.045f;
    public float minorTickLength = 0.03f;
    public float tickWidth = 0.005f;

    [Header("数字标签（位于刻度线内侧）")]
    public float fontSize = 0.22f;
    public float labelOffsetX = -0.035f;
    public float labelOffsetY = 0.008f;
    public Color labelColor = Color.black;

    [Header("端点标记")]
    public float endPointRadius = 0.08f;
    public Color startColor = Color.red;
    public Color endColor = Color.blue;

    [Header("交互速度（只能移动，不能旋转）")]
    public float moveSensitivity = 0.12f;

    [Header("相机聚焦")]
    public Transform pendulumAnchor;
    public float measureDistance = 3.8f;
    public float measureFOV = 42f;
    public float transitionDuration = 0.6f;   // 过渡动画时长

    private Transform startPoint;
    private Transform endPoint;
    private Camera mainCam;

    // 默认状态
    private Vector3 defaultCamPos;
    private Quaternion defaultCamRot;
    private float defaultFOV;
    private Vector3 defaultRulerPos;
    private Quaternion defaultRulerRot;

    private bool isDraggingMove = false;
    private Vector3 dragStartMousePos;
    private Vector3 dragStartObjectPos;
    private bool clickPotential = false;
    private Vector3 clickStartPos;
    private bool isTransitioning = false;
    private bool hasBeenUpright = false;

    public float RealLength => Vector3.Distance(startPoint.position, endPoint.position);

    void Start()
    {
        mainCam = Camera.main;
        if (mainCam == null) mainCam = FindObjectOfType<Camera>();

        defaultCamPos = mainCam.transform.position;
        defaultCamRot = mainCam.transform.rotation;
        defaultFOV = mainCam.fieldOfView;

        if (pendulumAnchor == null)
        {
            Pendulum pendulum = FindObjectOfType<Pendulum>();
            if (pendulum != null) pendulumAnchor = pendulum.transform;
        }

        BuildRulerVisuals();

        defaultRulerPos = transform.position;
        defaultRulerRot = transform.rotation;
    }

    void Update()
    {
        HandleMouseDragAndClick();
        if (Input.GetKeyDown(KeyCode.F) && !isTransitioning)
            StartCoroutine(ResetCameraAndRuler());
    }

    void BuildRulerVisuals()
    {
        while (transform.childCount > 0) DestroyImmediate(transform.GetChild(0).gameObject);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "RulerBody";
        body.transform.SetParent(transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(rulerWidth, rulerThickness, rulerLength);
        body.GetComponent<Renderer>().material.color = rulerColor;
        Destroy(body.GetComponent<Collider>());

        startPoint = CreateSphere("StartPoint", new Vector3(0, 0, -rulerLength / 2), startColor);
        endPoint = CreateSphere("EndPoint", new Vector3(0, 0, rulerLength / 2), endColor);

        float halfLen = rulerLength / 2;
        float surfaceY = rulerThickness / 2 + 0.0005f;
        float rightEdgeX = rulerWidth / 2;

        for (float pos = -halfLen; pos <= halfLen + 0.001f; pos += minorTickInterval)
        {
            bool isMajor = Mathf.Abs(Mathf.Round(pos / majorTickInterval) * majorTickInterval - pos) < 0.01f;
            float tickLen = isMajor ? majorTickLength : minorTickLength;

            GameObject tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tick.transform.SetParent(transform);
            tick.transform.localPosition = new Vector3(rightEdgeX - tickLen / 2, surfaceY, pos);
            tick.transform.localScale = new Vector3(tickLen, 0.001f, tickWidth);
            tick.GetComponent<Renderer>().material.color = Color.black;
            Destroy(tick.GetComponent<Collider>());

            if (isMajor && Mathf.Abs(pos) > 0.001f)
            {
                float lengthValue = pos + halfLen;
                GameObject label = new GameObject("Label_" + lengthValue);
                label.transform.SetParent(transform);
                float labelX = rightEdgeX - tickLen + labelOffsetX;
                label.transform.localPosition = new Vector3(labelX, surfaceY + labelOffsetY, pos);
                TextMeshPro tmp = label.AddComponent<TextMeshPro>();
                tmp.text = lengthValue.ToString("F1") + "m";
                tmp.fontSize = fontSize;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = labelColor;
                tmp.fontStyle = FontStyles.Bold;
                if (tmp.font == null) tmp.font = TMP_Settings.defaultFontAsset;
                label.AddComponent<FaceCamera>();
            }
        }

        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(rulerWidth * 1.2f, rulerThickness * 1.2f, rulerLength);
        collider.center = Vector3.zero;
        foreach (var childCollider in GetComponentsInChildren<Collider>())
            if (childCollider != collider) Destroy(childCollider);
    }

    Transform CreateSphere(string name, Vector3 localPos, Color color)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(transform);
        sphere.transform.localPosition = localPos;
        sphere.transform.localScale = Vector3.one * endPointRadius;
        sphere.GetComponent<Renderer>().material.color = color;
        Destroy(sphere.GetComponent<Collider>());
        return sphere.transform;
    }

    void HandleMouseDragAndClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                clickPotential = true;
                clickStartPos = Input.mousePosition;
                isDraggingMove = true;
                dragStartMousePos = Input.mousePosition;
                dragStartObjectPos = transform.position;
            }
        }

        if (isDraggingMove && Input.GetMouseButton(0))
        {
            if (Vector3.Distance(Input.mousePosition, clickStartPos) > 5f)
                clickPotential = false;
            Vector3 worldDelta = GetMouseWorldDelta();
            transform.position = dragStartObjectPos + worldDelta * moveSensitivity;
        }
        else
        {
            isDraggingMove = false;
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (clickPotential && !isDraggingMove && !isTransitioning)
            {
                StartCoroutine(FocusCameraAndUprightRuler());
            }
            clickPotential = false;
            isDraggingMove = false;
        }
    }

    IEnumerator FocusCameraAndUprightRuler()
    {
        if (hasBeenUpright) yield break;
        hasBeenUpright = true;
        isTransitioning = true;

        // 目标相机状态
        CameraState targetCam = GetMeasureCameraTransform();
        float targetFOV = measureFOV;
        // 目标直尺状态（竖立并指向悬挂点）
        Quaternion targetRulerRot = GetUprightRotation();
        Vector3 targetRulerPos = pendulumAnchor != null ? pendulumAnchor.position + new Vector3(1.2f, -1f, 0f) : defaultRulerPos;

        // 起始状态
        Vector3 startCamPos = mainCam.transform.position;
        Quaternion startCamRot = mainCam.transform.rotation;
        float startFOV = mainCam.fieldOfView;
        Vector3 startRulerPos = transform.position;
        Quaternion startRulerRot = transform.rotation;

        float elapsed = 0;
        while (elapsed < transitionDuration)
        {
            float t = elapsed / transitionDuration;
            mainCam.transform.position = Vector3.Lerp(startCamPos, targetCam.position, t);
            mainCam.transform.rotation = Quaternion.Slerp(startCamRot, targetCam.rotation, t);
            mainCam.fieldOfView = Mathf.Lerp(startFOV, targetFOV, t);
            transform.position = Vector3.Lerp(startRulerPos, targetRulerPos, t);
            transform.rotation = Quaternion.Slerp(startRulerRot, targetRulerRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        // 确保最终精确
        mainCam.transform.position = targetCam.position;
        mainCam.transform.rotation = targetCam.rotation;
        mainCam.fieldOfView = targetFOV;
        transform.position = targetRulerPos;
        transform.rotation = targetRulerRot;
        isTransitioning = false;
    }

    IEnumerator ResetCameraAndRuler()
    {
        isTransitioning = true;

        Vector3 startCamPos = mainCam.transform.position;
        Quaternion startCamRot = mainCam.transform.rotation;
        float startFOV = mainCam.fieldOfView;
        Vector3 startRulerPos = transform.position;
        Quaternion startRulerRot = transform.rotation;

        float elapsed = 0;
        while (elapsed < transitionDuration)
        {
            float t = elapsed / transitionDuration;
            mainCam.transform.position = Vector3.Lerp(startCamPos, defaultCamPos, t);
            mainCam.transform.rotation = Quaternion.Slerp(startCamRot, defaultCamRot, t);
            mainCam.fieldOfView = Mathf.Lerp(startFOV, defaultFOV, t);
            transform.position = Vector3.Lerp(startRulerPos, defaultRulerPos, t);
            transform.rotation = Quaternion.Slerp(startRulerRot, defaultRulerRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        mainCam.transform.position = defaultCamPos;
        mainCam.transform.rotation = defaultCamRot;
        mainCam.fieldOfView = defaultFOV;
        transform.position = defaultRulerPos;
        transform.rotation = defaultRulerRot;

        hasBeenUpright = false;   // 允许下次单击再次竖立
        isTransitioning = false;
    }

    Quaternion GetUprightRotation()
    {
        Quaternion baseRot = Quaternion.Euler(90f, 0f, 0f);
        if (pendulumAnchor == null) return baseRot;
        Vector3 toAnchor = (pendulumAnchor.position - transform.position).normalized;
        Vector3 horizontalDir = new Vector3(toAnchor.x, 0, toAnchor.z).normalized;
        if (horizontalDir == Vector3.zero) return baseRot;
        Vector3 defaultRight = baseRot * Vector3.right;
        float angle = Vector3.SignedAngle(defaultRight, horizontalDir, Vector3.up);
        return Quaternion.AngleAxis(angle, Vector3.up) * baseRot;
    }

    CameraState GetMeasureCameraTransform()
    {
        if (pendulumAnchor == null) return new CameraState(defaultCamPos, defaultCamRot);
        Vector3 anchorPos = pendulumAnchor.position;
        Vector3 camPos = anchorPos + new Vector3(0, 0.9f, -measureDistance);
        Quaternion camRot = Quaternion.LookRotation(anchorPos - camPos);
        return new CameraState(camPos, camRot);
    }

    Vector3 GetMouseWorldDelta()
    {
        Vector3 mouseDelta = Input.mousePosition - dragStartMousePos;
        Vector3 worldDelta = mainCam.transform.right * (mouseDelta.x * 0.01f) +
                             mainCam.transform.up * (mouseDelta.y * 0.01f);
        return worldDelta;
    }

    void OnDrawGizmosSelected()
    {
        if (startPoint != null && endPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(startPoint.position, endPoint.position);
        }
    }

    struct CameraState
    {
        public Vector3 position;
        public Quaternion rotation;
        public CameraState(Vector3 pos, Quaternion rot) { position = pos; rotation = rot; }
    }
}

/// <summary>
/// 始终面向摄像机（用于文字标签）
/// </summary>
public class FaceCamera : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null)
            transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward,
                             cam.transform.rotation * Vector3.up);
    }
}