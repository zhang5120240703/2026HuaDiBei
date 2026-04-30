using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controls piston dragging, volume mapping, and piston synchronization with gas state changes.
/// </summary>
public class CylinderController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("References")]
    public Transform piston;
    [SerializeField] private Transform syringe;
    [SerializeField] private Transform pistonTopColumn;

    [Header("Volume Range (L)")]
    public float cylinderHeight = 2.0f;
    public float minHeight = 0.2f;
    public float maxHeight = 2.0f;
    [SerializeField, Min(minVolumeEpsilon)] private float minimumRemainingVolume = 0.2f;
    public const float minVolumeEpsilon = 1e-3f;

    [Header("Travel Bounds")]
    [SerializeField] private Vector3 syringeLocalTravelAxis = Vector3.zero;
    [SerializeField, Min(0f)] private float syringeTravelPadding = 0f;
    [SerializeField, Min(0f)] private float syringeBottomReserve = 0f;
    [SerializeField] private bool invertSyringeBottomDirection = false;

    [Header("Drag")]
    [SerializeField] private bool configurePistonInteractionAtRuntime = true;
    [SerializeField] private bool addMissingPistonDragCollider = true;
    [SerializeField] private bool enableDirectMouseDragFallback = true;
    [SerializeField] private bool driveSimulationWhenNoVolumeListener = true;
    [SerializeField] private float dragStartThreshold = 5f;
    [SerializeField] private float screenDragSensitivity = 0.001f;
    [SerializeField] private float maxStepFractionPerFrame = 0.25f;

    [Header("Motion")]
    public float smoothTime = 0.1f;

    public System.Action<float> OnVolumeChanged;
    public System.Action<bool> OnVolumeRangeExceeded;
    public System.Action<bool> OnInteractionStateChanged;
    public System.Action<float, float> OnVolumeLimitsChanged;

    private bool isDragging;
    private bool pointerDown;
    private bool canDrag;
    private float lastVolume;
    private float volumeChangeRate;
    private float lastChangeTime;
    private float accumulatedDrag;
    private float targetPistonAxisPosition;
    private float smoothVelocity;
    private float dragPointerAxisOffset;
    private float fallbackDragStartAxisPosition;
    private Vector2 previousDragScreenPosition;
    private Vector2 previousDirectMousePosition;
    private Camera dragCamera;
    private Camera directMouseCamera;
    private bool directMouseDragging;
    private IdealGasSimulation.ProcessType currentProcess;
    private Transform resolvedSyringe;
    private Transform resolvedPistonTopColumn;
    private Vector3 initialPistonLocalPosition;
    private Vector3 initialPistonWorldPosition;
    private Vector3 pistonWorldMoveAxis = Vector3.up;
    private float pistonWorldUnitsPerAxisUnit = 1f;
    private float pistonAxisAtMaxVolume;
    private float pistonAxisAtMinVolume;
    private float pistonAxisAtMinimumRemainingVolume;
    private float referenceVolume = 1.0f;
    private bool subscribedToSimulation;
    private bool travelSettingsSnapshotReady;
    private float lastSyringeBottomReserve;
    private float lastSyringeTravelPadding;
    private float lastMinimumRemainingVolume;
    private Vector3 lastSyringeLocalTravelAxis;
    private bool lastInvertSyringeBottomDirection;

    private void Start()
    {
        if (piston == null)
        {
            Debug.LogError($"{nameof(CylinderController)} requires a piston Transform.", this);
            enabled = false;
            return;
        }

        SynchronizeVolumeLimitsWithSimulation();

        if (IdealGasSimulation.Instance != null)
        {
            currentProcess = IdealGasSimulation.Instance.GetCurrentProcess();
            IdealGasSimulation.Instance.OnStateChanged += OnGasStateChanged;
            subscribedToSimulation = true;
        }

        initialPistonLocalPosition = piston.localPosition;
        initialPistonWorldPosition = piston.position;
        resolvedSyringe = ResolveSyringeTravelSource();
        resolvedPistonTopColumn = ResolvePistonTopColumn();
        pistonWorldMoveAxis = ResolvePistonWorldMoveAxis(resolvedSyringe);
        pistonWorldUnitsPerAxisUnit = ResolvePistonWorldUnitsPerAxisUnit();
        referenceVolume = ResolveReferenceVolume();
        ResolvePistonTravelRange();
        StoreTravelSettingsSnapshot();

        targetPistonAxisPosition = PistonAxisPositionFromVolume(GetSimulationVolumeOrReference());
        SetPistonAxisPosition(targetPistonAxisPosition);
        lastVolume = GetCurrentVolume();
        lastChangeTime = Time.time;
        SetPistonDragged(currentProcess);

        if (configurePistonInteractionAtRuntime)
        {
            ConfigurePistonInteraction();
        }

        NotifyVolumeLimitsChanged();
    }

    private void OnDestroy()
    {
        if (subscribedToSimulation && IdealGasSimulation.Instance != null)
        {
            IdealGasSimulation.Instance.OnStateChanged -= OnGasStateChanged;
        }
    }

    private void Update()
    {
        if (piston == null)
        {
            return;
        }

        if (Time.time - lastChangeTime > 0.1f)
        {
            float currentVolume = GetCurrentVolume();
            volumeChangeRate = Mathf.Abs(currentVolume - lastVolume) / (Time.time - lastChangeTime);
            lastVolume = currentVolume;
            lastChangeTime = Time.time;
        }

        HandleDirectMouseDragFallback();
        RefreshTravelRangeIfNeeded();
        SmoothChangePiston();
    }

    #region Drag

    public void OnPointerDown(PointerEventData eventData)
    {
        BeginPistonDrag(eventData.position, eventData.pressEventCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        DragPiston(eventData.position, eventData.delta, eventData.pressEventCamera);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        EndPistonDrag();
    }

    public void BeginPistonDrag(Vector2 screenPosition, Camera eventCamera)
    {
        if (!canDrag)
        {
            return;
        }

        pointerDown = true;
        isDragging = false;
        accumulatedDrag = 0f;
        dragCamera = ResolveDragCamera(eventCamera);
        previousDragScreenPosition = screenPosition;
        fallbackDragStartAxisPosition = targetPistonAxisPosition;

        if (TryGetPointerAxisPosition(screenPosition, dragCamera, out float pointerAxisPosition))
        {
            dragPointerAxisOffset = targetPistonAxisPosition - pointerAxisPosition;
        }
        else
        {
            dragPointerAxisOffset = 0f;
        }

        OnInteractionStateChanged?.Invoke(true);
    }

    public void DragPiston(Vector2 screenPosition, Vector2 screenDelta, Camera eventCamera)
    {
        if (!canDrag || !pointerDown)
        {
            return;
        }

        dragCamera = ResolveDragCamera(eventCamera != null ? eventCamera : dragCamera);
        accumulatedDrag += screenDelta.magnitude;
        if (!isDragging)
        {
            if (accumulatedDrag < dragStartThreshold)
            {
                previousDragScreenPosition = screenPosition;
                return;
            }

            isDragging = true;
        }

        float targetAxisPosition;
        if (TryGetPointerAxisPosition(screenPosition, dragCamera, out float pointerAxisPosition))
        {
            targetAxisPosition = pointerAxisPosition + dragPointerAxisOffset;
        }
        else
        {
            Vector2 delta = screenDelta;
            if (delta.sqrMagnitude <= Mathf.Epsilon)
            {
                delta = screenPosition - previousDragScreenPosition;
            }

            float screenAxisDelta = Vector2.Dot(delta, GetScreenAxisDirection(dragCamera));
            float maxStep = Mathf.Max(cylinderHeight, 0.0001f) * maxStepFractionPerFrame;
            float axisDelta = Mathf.Clamp(screenAxisDelta * screenDragSensitivity * cylinderHeight, -maxStep, maxStep);
            fallbackDragStartAxisPosition = ClampPistonAxisPosition(fallbackDragStartAxisPosition + axisDelta);
            targetAxisPosition = fallbackDragStartAxisPosition;
        }

        previousDragScreenPosition = screenPosition;
        ApplyVolumeFromAxisPosition(targetAxisPosition, true);
    }

    public void EndPistonDrag()
    {
        if (!pointerDown && !isDragging)
        {
            return;
        }

        isDragging = false;
        pointerDown = false;
        accumulatedDrag = 0f;
        smoothVelocity = 0f;
        OnInteractionStateChanged?.Invoke(false);
    }

    private void HandleDirectMouseDragFallback()
    {
        if (!enableDirectMouseDragFallback || !canDrag)
        {
            if (directMouseDragging)
            {
                directMouseDragging = false;
                EndPistonDrag();
            }

            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePosition = Input.mousePosition;
            if (!IsPointerOverBlockingUiControl(mousePosition) && TryGetPistonUnderPointer(mousePosition, out Camera hitCamera))
            {
                directMouseDragging = true;
                directMouseCamera = hitCamera;
                previousDirectMousePosition = mousePosition;
                BeginPistonDrag(mousePosition, directMouseCamera);
            }
        }

        if (directMouseDragging && Input.GetMouseButton(0))
        {
            Vector2 mousePosition = Input.mousePosition;
            DragPiston(mousePosition, mousePosition - previousDirectMousePosition, directMouseCamera);
            previousDirectMousePosition = mousePosition;
        }

        if (directMouseDragging && Input.GetMouseButtonUp(0))
        {
            directMouseDragging = false;
            EndPistonDrag();
        }
    }

    private void ApplyVolumeFromAxisPosition(float axisPosition, bool notifySimulation)
    {
        float clampedAxisPosition = ClampPistonAxisPosition(axisPosition);
        float newVolume = VolumeFromPistonAxisPosition(clampedAxisPosition);
        bool exceeded = Mathf.Abs(axisPosition - clampedAxisPosition) > 0.0001f;

        targetPistonAxisPosition = PistonAxisPositionFromVolume(newVolume);
        SetPistonAxisPosition(targetPistonAxisPosition);
        smoothVelocity = 0f;
        OnVolumeRangeExceeded?.Invoke(exceeded);

        if (notifySimulation)
        {
            NotifyVolumeChanged(newVolume);
        }
    }

    private void NotifyVolumeChanged(float newVolume)
    {
        if (OnVolumeChanged == null && driveSimulationWhenNoVolumeListener && IdealGasSimulation.Instance != null)
        {
            IdealGasSimulation.Instance.SetVolume(newVolume);
            return;
        }

        OnVolumeChanged?.Invoke(newVolume);
    }

    #endregion

    #region Piston Motion

    private void SmoothChangePiston()
    {
        if (isDragging)
        {
            return;
        }

        if (smoothTime <= 0f)
        {
            SetPistonAxisPosition(targetPistonAxisPosition);
            return;
        }

        float newAxisPosition = Mathf.SmoothDamp(
            GetCurrentPistonAxisPosition(),
            targetPistonAxisPosition,
            ref smoothVelocity,
            smoothTime
        );

        SetPistonAxisPosition(newAxisPosition);
    }

    private void OnGasStateChanged(float pressure, float volume, float temperature)
    {
        if (isDragging)
        {
            return;
        }

        float clampedVolume = ClampRuntimeVolume(volume);
        targetPistonAxisPosition = PistonAxisPositionFromVolume(clampedVolume);
        OnVolumeRangeExceeded?.Invoke(!Mathf.Approximately(clampedVolume, volume));

        if (!Mathf.Approximately(clampedVolume, volume) && CanDriveSimulationVolume())
        {
            IdealGasSimulation.Instance.SetVolume(clampedVolume);
        }
    }

    public void SetPistonPosition(float volume)
    {
        float clampedVolume = ClampRuntimeVolume(volume);
        targetPistonAxisPosition = PistonAxisPositionFromVolume(clampedVolume);
        SetPistonAxisPosition(targetPistonAxisPosition);
        smoothVelocity = 0f;
    }

    #endregion

    #region Volume Mapping

    private float GetCurrentVolume()
    {
        return VolumeFromPistonAxisPosition(GetCurrentPistonAxisPosition());
    }

    private float VolumeFromPistonAxisPosition(float pistonAxisPosition)
    {
        pistonAxisPosition = ClampPistonAxisPosition(pistonAxisPosition);
        float normalizedPosition = Mathf.InverseLerp(pistonAxisAtMaxVolume, pistonAxisAtMinVolume, pistonAxisPosition);
        float volume = Mathf.Lerp(GetMaximumRuntimeVolume(), GetMinimumRuntimeVolume(), normalizedPosition);
        return ClampRuntimeVolume(volume);
    }

    private float PistonAxisPositionFromVolume(float volume)
    {
        float clampedVolume = ClampRuntimeVolume(volume);
        float normalizedVolume = Mathf.InverseLerp(GetPhysicalMinimumVolume(), GetMaximumRuntimeVolume(), clampedVolume);
        float pistonAxisPosition = Mathf.Lerp(pistonAxisAtMinVolume, pistonAxisAtMaxVolume, normalizedVolume);
        return ClampPistonAxisPosition(pistonAxisPosition);
    }

    private float ClampRuntimeVolume(float volume)
    {
        return Mathf.Clamp(volume, GetMinimumRuntimeVolume(), GetMaximumRuntimeVolume());
    }

    private float GetMinimumRuntimeVolume()
    {
        return Mathf.Clamp(
            Mathf.Max(GetPhysicalMinimumVolume(), minimumRemainingVolume, minVolumeEpsilon),
            GetPhysicalMinimumVolume(),
            GetMaximumRuntimeVolume()
        );
    }

    private float GetPhysicalMinimumVolume()
    {
        return Mathf.Max(minHeight, minVolumeEpsilon);
    }

    private float GetMaximumRuntimeVolume()
    {
        return Mathf.Max(maxHeight, GetPhysicalMinimumVolume() + minVolumeEpsilon);
    }

    private float GetSimulationVolumeOrReference()
    {
        if (IdealGasSimulation.Instance == null)
        {
            return referenceVolume;
        }

        return IdealGasSimulation.Instance.GetVolume();
    }

    private void SynchronizeVolumeLimitsWithSimulation()
    {
        if (IdealGasSimulation.Instance == null)
        {
            minHeight = Mathf.Max(minHeight, minVolumeEpsilon);
            maxHeight = Mathf.Max(maxHeight, minHeight + minVolumeEpsilon);
            SanitizeVolumeLimits();
            return;
        }

        minHeight = Mathf.Max(IdealGasSimulation.Instance.GetMinVolume(), minVolumeEpsilon);
        maxHeight = Mathf.Max(IdealGasSimulation.Instance.GetMaxVolume(), minHeight + minVolumeEpsilon);
        SanitizeVolumeLimits();
    }

    private void SanitizeVolumeLimits()
    {
        minHeight = Mathf.Max(minHeight, minVolumeEpsilon);
        maxHeight = Mathf.Max(maxHeight, minHeight + minVolumeEpsilon);
        minimumRemainingVolume = Mathf.Clamp(minimumRemainingVolume, minHeight, maxHeight);
    }

    #endregion

    #region Travel Resolution

    private Vector3 GetPistonWorldMoveAxis()
    {
        if (pistonWorldMoveAxis.sqrMagnitude <= Mathf.Epsilon)
        {
            return Vector3.up;
        }

        return pistonWorldMoveAxis.normalized;
    }

    private float ResolvePistonWorldUnitsPerAxisUnit()
    {
        if (piston.parent == null)
        {
            return 1f;
        }

        float scale = piston.parent.TransformVector(Vector3.up).magnitude;
        return Mathf.Max(scale, 0.0001f);
    }

    private float GetCurrentPistonAxisPosition()
    {
        float worldOffset = Vector3.Dot(piston.position - initialPistonWorldPosition, GetPistonWorldMoveAxis());
        return initialPistonLocalPosition.y + worldOffset / pistonWorldUnitsPerAxisUnit;
    }

    private void SetPistonAxisPosition(float axisPosition)
    {
        axisPosition = ClampPistonAxisPosition(axisPosition);
        float axisOffset = axisPosition - initialPistonLocalPosition.y;
        piston.position = initialPistonWorldPosition + GetPistonWorldMoveAxis() * axisOffset * pistonWorldUnitsPerAxisUnit;
    }

    private float ClampPistonAxisPosition(float axisPosition)
    {
        float minAxis = Mathf.Min(pistonAxisAtMaxVolume, pistonAxisAtMinimumRemainingVolume);
        float maxAxis = Mathf.Max(pistonAxisAtMaxVolume, pistonAxisAtMinimumRemainingVolume);
        return Mathf.Clamp(axisPosition, minAxis, maxAxis);
    }

    private float ResolveReferenceVolume()
    {
        float initialVolume = GetSimulationVolumeOrReference();
        if (initialVolume <= 0f)
        {
            initialVolume = referenceVolume;
        }

        return ClampRuntimeVolume(initialVolume);
    }

    private float GetReferenceMappedAxisPosition()
    {
        float normalizedReferenceVolume = Mathf.InverseLerp(GetPhysicalMinimumVolume(), GetMaximumRuntimeVolume(), referenceVolume);
        return Mathf.Lerp(cylinderHeight / 2f, -cylinderHeight / 2f, normalizedReferenceVolume);
    }

    private void ResolvePistonTravelRange()
    {
        Transform travelSource = resolvedSyringe != null ? resolvedSyringe : ResolveSyringeTravelSource();
        if (TryGetTravelProjectionRange(travelSource, GetPistonWorldMoveAxis(), out float minProjection, out float maxProjection))
        {
            float padding = Mathf.Max(0f, syringeTravelPadding);
            if (maxProjection - minProjection > padding * 2f)
            {
                minProjection += padding;
                maxProjection -= padding;
            }

            if (TryGetPistonTopColumnProjectionOffsets(GetPistonWorldMoveAxis(), out float minColumnOffset, out float maxColumnOffset))
            {
                minProjection -= minColumnOffset;
                maxProjection -= maxColumnOffset;
            }

            if (maxProjection < minProjection)
            {
                float collapsedProjection = (minProjection + maxProjection) * 0.5f;
                minProjection = collapsedProjection;
                maxProjection = collapsedProjection;
            }

            float minAxisPosition = AxisPositionFromWorldProjection(minProjection);
            float maxAxisPosition = AxisPositionFromWorldProjection(maxProjection);
            ResolveVolumeAxisEndpoints(Mathf.Min(minAxisPosition, maxAxisPosition), Mathf.Max(minAxisPosition, maxAxisPosition));
            cylinderHeight = Mathf.Max(Mathf.Abs(pistonAxisAtMinVolume - pistonAxisAtMaxVolume), 0.0001f);
            return;
        }

        float referenceMappedAxisPosition = GetReferenceMappedAxisPosition();
        float fallbackMinAxis = initialPistonLocalPosition.y - cylinderHeight / 2f - referenceMappedAxisPosition;
        float fallbackMaxAxis = initialPistonLocalPosition.y + cylinderHeight / 2f - referenceMappedAxisPosition;
        ResolveVolumeAxisEndpoints(fallbackMinAxis, fallbackMaxAxis);
    }

    private void ResolveVolumeAxisEndpoints(float travelMinAxis, float travelMaxAxis)
    {
        float reserveAxisDistance = Mathf.Max(0f, syringeBottomReserve) / Mathf.Max(pistonWorldUnitsPerAxisUnit, 0.0001f);
        float directionToBottom = ResolveBottomDirectionSign();
        float travelSpan = Mathf.Max(travelMaxAxis - travelMinAxis, 0.0001f);
        reserveAxisDistance = Mathf.Min(reserveAxisDistance, travelSpan - 0.0001f);

        if (directionToBottom > 0f)
        {
            pistonAxisAtMinVolume = travelMaxAxis - reserveAxisDistance;
            pistonAxisAtMaxVolume = travelMinAxis;
        }
        else
        {
            pistonAxisAtMinVolume = travelMinAxis + reserveAxisDistance;
            pistonAxisAtMaxVolume = travelMaxAxis;
        }

        cylinderHeight = Mathf.Max(Mathf.Abs(pistonAxisAtMaxVolume - pistonAxisAtMinVolume), 0.0001f);
        UpdateMinimumRemainingAxisPosition();
    }

    private void UpdateMinimumRemainingAxisPosition()
    {
        float normalizedVolume = Mathf.InverseLerp(
            GetPhysicalMinimumVolume(),
            GetMaximumRuntimeVolume(),
            ClampRuntimeVolume(GetMinimumRuntimeVolume())
        );

        pistonAxisAtMinimumRemainingVolume = Mathf.Lerp(
            pistonAxisAtMinVolume,
            pistonAxisAtMaxVolume,
            normalizedVolume
        );
    }

    private void RefreshTravelRangeIfNeeded()
    {
        if (!travelSettingsSnapshotReady || piston == null)
        {
            return;
        }

        bool settingsChanged =
            !Mathf.Approximately(lastSyringeBottomReserve, syringeBottomReserve) ||
            !Mathf.Approximately(lastSyringeTravelPadding, syringeTravelPadding) ||
            !Mathf.Approximately(lastMinimumRemainingVolume, minimumRemainingVolume) ||
            lastSyringeLocalTravelAxis != syringeLocalTravelAxis ||
            lastInvertSyringeBottomDirection != invertSyringeBottomDirection;

        if (!settingsChanged)
        {
            return;
        }

        float preservedVolume = IdealGasSimulation.Instance != null
            ? IdealGasSimulation.Instance.GetVolume()
            : GetCurrentVolume();

        SanitizeVolumeLimits();
        ResolvePistonTravelRange();
        StoreTravelSettingsSnapshot();
        NotifyVolumeLimitsChanged();

        float clampedPreservedVolume = ClampRuntimeVolume(preservedVolume);
        targetPistonAxisPosition = PistonAxisPositionFromVolume(clampedPreservedVolume);
        if (!Mathf.Approximately(clampedPreservedVolume, preservedVolume) && CanDriveSimulationVolume())
        {
            IdealGasSimulation.Instance.SetVolume(clampedPreservedVolume);
        }

        if (isDragging)
        {
            SetPistonAxisPosition(targetPistonAxisPosition);
            smoothVelocity = 0f;
        }
    }

    private void StoreTravelSettingsSnapshot()
    {
        lastSyringeBottomReserve = syringeBottomReserve;
        lastSyringeTravelPadding = syringeTravelPadding;
        lastMinimumRemainingVolume = minimumRemainingVolume;
        lastSyringeLocalTravelAxis = syringeLocalTravelAxis;
        lastInvertSyringeBottomDirection = invertSyringeBottomDirection;
        travelSettingsSnapshotReady = true;
    }

    private bool CanDriveSimulationVolume()
    {
        return IdealGasSimulation.Instance != null
            && IdealGasSimulation.Instance.GetCurrentProcess() != IdealGasSimulation.ProcessType.Isochoric;
    }

    public float GetMinimumAllowedVolume()
    {
        return GetMinimumRuntimeVolume();
    }

    public float GetMaximumAllowedVolume()
    {
        return GetMaximumRuntimeVolume();
    }

    private void NotifyVolumeLimitsChanged()
    {
        OnVolumeLimitsChanged?.Invoke(GetMinimumRuntimeVolume(), GetMaximumRuntimeVolume());
    }

    private float ResolveBottomDirectionSign()
    {
        Vector3 axis = GetPistonWorldMoveAxis();
        float direction = 1f;

        if (TryGetPistonTopColumnProjectionOffsets(axis, out float minColumnOffset, out float maxColumnOffset))
        {
            float farColumnOffset = Mathf.Abs(maxColumnOffset) >= Mathf.Abs(minColumnOffset)
                ? maxColumnOffset
                : minColumnOffset;

            if (Mathf.Abs(farColumnOffset) > 0.0001f)
            {
                direction = Mathf.Sign(farColumnOffset);
            }
        }
        else if (piston != null && piston.parent != null)
        {
            float localYDirection = Vector3.Dot(piston.parent.TransformVector(Vector3.up), axis);
            if (Mathf.Abs(localYDirection) > 0.0001f)
            {
                direction = Mathf.Sign(localYDirection);
            }
        }

        return invertSyringeBottomDirection ? -direction : direction;
    }

    private Transform ResolveSyringeTravelSource()
    {
        if (syringe != null)
        {
            return syringe;
        }

        if (transform.name.Equals("syringe", System.StringComparison.OrdinalIgnoreCase))
        {
            return transform;
        }

        Transform syringeChild = FindChildByName(transform, "syringe");
        if (syringeChild != null)
        {
            return syringeChild;
        }

        Transform rootSyringe = FindChildByName(transform.root, "syringe");
        return rootSyringe != null ? rootSyringe : transform;
    }

    private Transform ResolvePistonTopColumn()
    {
        if (pistonTopColumn != null)
        {
            return pistonTopColumn;
        }

        if (piston == null)
        {
            return null;
        }

        Transform column = FindChildByName(piston, "column");
        return column != null ? column : piston;
    }

    private Transform FindChildByName(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform match = FindChildByName(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private bool TryGetPistonTopColumnProjectionOffsets(Vector3 axis, out float minOffset, out float maxOffset)
    {
        minOffset = float.PositiveInfinity;
        maxOffset = float.NegativeInfinity;

        if (resolvedPistonTopColumn == null || axis.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        axis.Normalize();
        bool hasBounds = false;
        Renderer[] renderers = resolvedPistonTopColumn.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            EncapsulateProjectedOffsetBounds(renderer.bounds, axis, ref minOffset, ref maxOffset);
            hasBounds = true;
        }

        if (hasBounds)
        {
            return maxOffset >= minOffset;
        }

        Collider[] colliders = resolvedPistonTopColumn.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            EncapsulateProjectedOffsetBounds(collider.bounds, axis, ref minOffset, ref maxOffset);
            hasBounds = true;
        }

        if (hasBounds)
        {
            return maxOffset >= minOffset;
        }

        float pointOffset = Vector3.Dot(resolvedPistonTopColumn.position - initialPistonWorldPosition, axis);
        minOffset = pointOffset;
        maxOffset = pointOffset;
        return true;
    }

    private Vector3 ResolvePistonWorldMoveAxis(Transform travelSource)
    {
        if (travelSource != null && syringeLocalTravelAxis.sqrMagnitude > Mathf.Epsilon)
        {
            return travelSource.TransformDirection(syringeLocalTravelAxis).normalized;
        }

        if (travelSource == null)
        {
            return Vector3.up;
        }

        Vector3[] candidateAxes =
        {
            travelSource.right,
            travelSource.up,
            travelSource.forward
        };

        Vector3 bestAxis = candidateAxes[0].normalized;
        float bestLength = -1f;
        foreach (Vector3 candidateAxis in candidateAxes)
        {
            Vector3 axis = candidateAxis.normalized;
            if (TryGetTravelProjectionRange(travelSource, axis, out float minProjection, out float maxProjection))
            {
                float length = maxProjection - minProjection;
                if (length > bestLength)
                {
                    bestLength = length;
                    bestAxis = axis;
                }
            }
        }

        return bestAxis.sqrMagnitude > Mathf.Epsilon ? bestAxis.normalized : Vector3.up;
    }

    private bool TryGetTravelProjectionRange(Transform source, Vector3 axis, out float minProjection, out float maxProjection)
    {
        minProjection = float.PositiveInfinity;
        maxProjection = float.NegativeInfinity;

        if (source == null || axis.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        axis.Normalize();
        bool hasBounds = false;
        Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (ShouldIgnoreTravelBounds(renderer.transform))
            {
                continue;
            }

            EncapsulateProjectedBounds(renderer.bounds, axis, ref minProjection, ref maxProjection);
            hasBounds = true;
        }

        if (hasBounds)
        {
            return maxProjection > minProjection;
        }

        Collider[] colliders = source.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (ShouldIgnoreTravelBounds(collider.transform))
            {
                continue;
            }

            EncapsulateProjectedBounds(collider.bounds, axis, ref minProjection, ref maxProjection);
            hasBounds = true;
        }

        return hasBounds && maxProjection > minProjection;
    }

    private bool ShouldIgnoreTravelBounds(Transform candidate)
    {
        return candidate == null || (piston != null && (candidate == piston || candidate.IsChildOf(piston)));
    }

    private void EncapsulateProjectedBounds(Bounds bounds, Vector3 axis, ref float minProjection, ref float maxProjection)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    float projection = Vector3.Dot(corner, axis);
                    minProjection = Mathf.Min(minProjection, projection);
                    maxProjection = Mathf.Max(maxProjection, projection);
                }
            }
        }
    }

    private void EncapsulateProjectedOffsetBounds(Bounds bounds, Vector3 axis, ref float minOffset, ref float maxOffset)
    {
        float pistonInitialProjection = Vector3.Dot(initialPistonWorldPosition, axis);
        float minProjection = float.PositiveInfinity;
        float maxProjection = float.NegativeInfinity;

        EncapsulateProjectedBounds(bounds, axis, ref minProjection, ref maxProjection);

        minOffset = Mathf.Min(minOffset, minProjection - pistonInitialProjection);
        maxOffset = Mathf.Max(maxOffset, maxProjection - pistonInitialProjection);
    }

    private float AxisPositionFromWorldProjection(float projection)
    {
        float initialProjection = Vector3.Dot(initialPistonWorldPosition, GetPistonWorldMoveAxis());
        return initialPistonLocalPosition.y + (projection - initialProjection) / pistonWorldUnitsPerAxisUnit;
    }

    #endregion

    #region Pointer Projection

    private Camera ResolveDragCamera(Camera eventCamera)
    {
        if (eventCamera != null)
        {
            return eventCamera;
        }

        return Camera.main;
    }

    private bool TryGetPointerAxisPosition(Vector2 screenPosition, Camera eventCamera, out float axisPosition)
    {
        axisPosition = targetPistonAxisPosition;
        if (eventCamera == null)
        {
            return false;
        }

        Ray ray = eventCamera.ScreenPointToRay(screenPosition);
        Vector3 axis = GetPistonWorldMoveAxis();
        Vector3 rayDirection = ray.direction.normalized;
        float denominator = 1f - Vector3.Dot(axis, rayDirection) * Vector3.Dot(axis, rayDirection);

        if (Mathf.Abs(denominator) < 0.0001f)
        {
            return false;
        }

        Vector3 lineOffset = initialPistonWorldPosition - ray.origin;
        float axisProjection = Vector3.Dot(axis, lineOffset);
        float rayProjection = Vector3.Dot(rayDirection, lineOffset);
        float closestAxisDistance = (Vector3.Dot(axis, rayDirection) * rayProjection - axisProjection) / denominator;
        axisPosition = initialPistonLocalPosition.y + closestAxisDistance / pistonWorldUnitsPerAxisUnit;
        return true;
    }

    private Vector2 GetScreenAxisDirection(Camera eventCamera)
    {
        if (eventCamera == null)
        {
            return Vector2.up;
        }

        Vector3 axis = GetPistonWorldMoveAxis();
        Vector3 screenA = eventCamera.WorldToScreenPoint(initialPistonWorldPosition);
        Vector3 screenB = eventCamera.WorldToScreenPoint(initialPistonWorldPosition + axis);
        Vector2 direction = (Vector2)(screenB - screenA);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
    }

    #endregion

    #region Direct Mouse Hit Testing

    private bool TryGetPistonUnderPointer(Vector2 screenPosition, out Camera hitCamera)
    {
        hitCamera = null;
        Camera[] cameras = Camera.allCameras;
        if (cameras == null || cameras.Length == 0)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return false;
            }

            cameras = new[] { mainCamera };
        }

        foreach (Camera camera in cameras)
        {
            if (camera == null || !camera.isActiveAndEnabled)
            {
                continue;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, camera.eventMask, QueryTriggerInteraction.Collide);
            if (hits.Length == 0)
            {
                continue;
            }

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null && IsPistonTransform(hit.collider.transform))
                {
                    hitCamera = camera;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsPistonTransform(Transform candidate)
    {
        return candidate != null && piston != null && (candidate == piston || candidate.IsChildOf(piston));
    }

    private bool IsPointerOverBlockingUiControl(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        foreach (RaycastResult result in results)
        {
            GameObject hitObject = result.gameObject;
            if (hitObject == null)
            {
                continue;
            }

            if (hitObject.GetComponentInParent<Selectable>() != null)
            {
                return true;
            }

            if (hitObject.GetComponentInParent<Scrollbar>() != null)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Interaction Components

    private void ConfigurePistonInteraction()
    {
        if (addMissingPistonDragCollider && piston.GetComponent<Collider>() == null)
        {
            BoxCollider boxCollider = piston.gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = false;
            ApplyRendererBoundsToBoxCollider(boxCollider);
        }

        Collider[] colliders = piston.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            BoxCollider boxCollider = piston.gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = false;
            ApplyRendererBoundsToBoxCollider(boxCollider);
            colliders = piston.GetComponentsInChildren<Collider>(true);
        }

        AttachDragReceiver(piston.gameObject);
        foreach (Collider collider in colliders)
        {
            AttachDragReceiver(collider.gameObject);
        }

        Camera[] cameras = FindObjectsOfType<Camera>();
        foreach (Camera camera in cameras)
        {
            if (camera.GetComponent<PhysicsRaycaster>() == null)
            {
                camera.gameObject.AddComponent<PhysicsRaycaster>();
            }
        }
    }

    private void AttachDragReceiver(GameObject target)
    {
        if (target == null || target == gameObject)
        {
            return;
        }

        CylinderPistonDragReceiver receiver = target.GetComponent<CylinderPistonDragReceiver>();
        if (receiver == null)
        {
            receiver = target.AddComponent<CylinderPistonDragReceiver>();
        }

        receiver.Initialize(this);
    }

    private void ApplyRendererBoundsToBoxCollider(BoxCollider boxCollider)
    {
        Renderer[] renderers = piston.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            boxCollider.center = Vector3.zero;
            boxCollider.size = Vector3.one * 0.2f;
            return;
        }

        Bounds localBounds = new Bounds(piston.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
        foreach (Renderer renderer in renderers)
        {
            EncapsulateWorldBoundsAsLocalBounds(renderer.bounds, ref localBounds);
        }

        boxCollider.center = localBounds.center;
        boxCollider.size = Vector3.Max(localBounds.size, Vector3.one * 0.01f);
    }

    private void EncapsulateWorldBoundsAsLocalBounds(Bounds worldBounds, ref Bounds localBounds)
    {
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    localBounds.Encapsulate(piston.InverseTransformPoint(worldCorner));
                }
            }
        }
    }

    #endregion

    public void SetCurrentProcess(IdealGasSimulation.ProcessType process)
    {
        currentProcess = process;
        SetPistonDragged(process);
    }

    private void SetPistonDragged(IdealGasSimulation.ProcessType process)
    {
        canDrag = process switch
        {
            IdealGasSimulation.ProcessType.Isothermal => true,
            IdealGasSimulation.ProcessType.Isobaric => true,
            IdealGasSimulation.ProcessType.Isochoric => false,
            IdealGasSimulation.ProcessType.Null => true,
            _ => false
        };

        if (!canDrag)
        {
            EndPistonDrag();
        }
    }

    public bool IsVolumeStable()
    {
        return volumeChangeRate < 0.01f;
    }

    public float GetVolumeChangeRate()
    {
        return volumeChangeRate;
    }

    public bool GetPistonDragged()
    {
        return canDrag;
    }
}

public class CylinderPistonDragReceiver : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private CylinderController controller;

    public void Initialize(CylinderController cylinderController)
    {
        controller = cylinderController;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        controller?.BeginPistonDrag(eventData.position, eventData.pressEventCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        controller?.DragPiston(eventData.position, eventData.delta, eventData.pressEventCamera);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        controller?.EndPistonDrag();
    }

    private void OnDisable()
    {
        controller?.EndPistonDrag();
    }
}
