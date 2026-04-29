using UnityEngine;
using System.Collections;

public class TeachingRuler_Final : MonoBehaviour
{
    [Header("直尺模型引用")]
    public Transform startPoint;         // 测量起点（空物体或子物体，位于直尺一端）
    public Transform endPoint;           // 测量终点（空物体或子物体，位于直尺另一端）
    public Collider rulerCollider;       // 直尺的碰撞体（用于拖拽检测，若为空则自动添加）

    [Header("交互速度（只能移动，不能旋转）")]
    public float moveSensitivity = 0.12f;

    [Header("相机聚焦")]
    public Transform pendulumAnchor;     // 悬挂点（自动查找或手动指定）
    public float measureDistance = 3.8f;
    public float measureFOV = 42f;
    public float transitionDuration = 0.6f;   // 过渡动画时长

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

        // 确保必填引用已赋值
        if (startPoint == null || endPoint == null)
        {
            Debug.LogError("TeachingRuler_Final: 请指定 startPoint 和 endPoint！");
            enabled = false;
            return;
        }

        // 如果没有指定碰撞体，尝试从当前物体获取，若没有则添加一个 BoxCollider
        if (rulerCollider == null)
        {
            rulerCollider = GetComponent<Collider>();
            if (rulerCollider == null)
                rulerCollider = gameObject.AddComponent<BoxCollider>();
        }

        // 记录初始状态（位置和旋转）
        defaultRulerPos = transform.position;
        defaultRulerRot = transform.rotation;
    }

    void Update()
    {
        HandleMouseDragAndClick();
        if (Input.GetKeyDown(KeyCode.F) && !isTransitioning)
            StartCoroutine(ResetCameraAndRuler());
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

        hasBeenUpright = false;
        isTransitioning = false;
    }

    Quaternion GetUprightRotation()
    {
        // 基础旋转：使直尺竖直。假设直尺平放时，长度方向为局部 Z 轴，宽度方向（刻度侧）为局部 X 轴。
        return Quaternion.Euler(0f, -90f, 90f);
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