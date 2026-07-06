using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class RoomMapXRRaySelector : MonoBehaviour
{
    [Header("Map Image")]
    [Tooltip("RectTransform slike tlocrta. Ako ostane prazno, koristi RectTransform objekta na kojem je skripta.")]
    [SerializeField] private RectTransform mapImageRect;

    [Header("Ray Source")]
    [Tooltip("Najbolje ovdje ubaci isti Transform koji koristi SpaceTimeSliceTooltip kao Right Controller Ray Origin.")]
    [SerializeField] private Transform rightControllerRayOrigin;

    [Tooltip("Opcionalno. Ako ne koristiš Ray Origin transform, možeš ubaciti desni NearFarInteractor.")]
    [SerializeField] private NearFarInteractor rightControllerNearFarInteractor;

    [Tooltip("Duljina provjere raya prema slici.")]
    [SerializeField] private float rayDistance = 50f;

    [Header("Runtime Normal Pointer To Hide")]
    [Tooltip("Ime runtime objekta koji tvoja SpaceTimeSliceTooltip skripta stvara za debeli/stalni ray.")]
    [SerializeField] private string normalPointerRuntimeName = "RightHandAlwaysVisiblePointer";

    [Tooltip("Ako je uključeno, skripta sama traži runtime pointer po imenu.")]
    [SerializeField] private bool autoFindNormalPointer = true;

    [Tooltip("Koliko često se ponovno traži runtime pointer ako još nije pronađen.")]
    [SerializeField] private float refindNormalPointerInterval = 0.5f;

    [Tooltip("Ako je uključeno, skriva se samo LineRenderer, ne cijeli GameObject.")]
    [SerializeField] private bool hideOnlyNormalPointerLineRenderer = true;

    [Header("Precise Image Ray")]
    [Tooltip("Ako je uključeno, skripta sama stvara tanki ray za ciljanje slike.")]
    [SerializeField] private bool autoCreatePreciseRay = true;

    [Tooltip("Ime runtime objekta za tanki precizni ray.")]
    [SerializeField] private string preciseRayObjectName = "RoomMapPreciseRay";

    [Tooltip("Ako želiš ručno zadati LineRenderer tankog raya, ubaci ga ovdje. Inače ostavi prazno.")]
    [SerializeField] private LineRenderer preciseRayLine;

    [SerializeField] private float preciseRayWidth = 0.006f;
    [SerializeField] private Color preciseRayColor = Color.cyan;

    [Tooltip("Mali pomak od površine slike da linija ne treperi u istoj ravnini s canvasom.")]
    [SerializeField] private float surfaceOffset = 0.002f;

    [Header("Markers")]
    [Tooltip("Plava kružnica koja prati ray dok se prelazi preko slike.")]
    [SerializeField] private RectTransform blueHoverMarker;

    [Tooltip("Plava kružnica koja ostaje na mjestu nakon Triggera.")]
    [SerializeField] private RectTransform blueSelectedMarker;

    [Tooltip("Zelena kružnica koja označava točnu lokaciju.")]
    [SerializeField] private RectTransform greenCorrectMarker;

    [Header("Selection Behaviour")]
    [Tooltip("Ako je uključeno, nakon prvog odabira korisnik više ne može promijeniti plavu oznaku.")]
    [SerializeField] private bool lockAfterFirstSelection = true;

    [Tooltip("Ako je uključeno, zelena oznaka se prikazuje tek nakon korisnikova odabira.")]
    [SerializeField] private bool showGreenMarkerAfterSelection = true;

    [Tooltip("Ako je uključeno, svi markeri se sakrivaju na početku scene.")]
    [SerializeField] private bool hideMarkersOnStart = true;

    [Tooltip("Ako je uključeno, plavi hover marker nestaje nakon potvrde odabira.")]
    [SerializeField] private bool hideHoverMarkerAfterSelection = true;

    [Header("Input")]
    [Tooltip("Trigger na desnom Meta Quest kontroleru potvrđuje odabir.")]
    [SerializeField] private bool useRightControllerTrigger = true;

    [Tooltip("Za testiranje u Editoru: lijevi klik miša također potvrđuje odabir.")]
    [SerializeField] private bool allowMouseClickInEditor = true;

    [Header("Debug")]
    [SerializeField] private bool debugDrawRay = false;
    [SerializeField] private bool logStateChanges = false;
    [SerializeField] private bool logPointerSearch = false;
    [SerializeField] private bool logSelection = false;

    private LineRenderer normalPointerLineRenderer;
    private GameObject normalPointerObject;

    private bool isHoveringMap = false;
    private bool selectionLocked = false;

    private bool lastTriggerPressed = false;
    private bool lastMousePressed = false;

    private bool hasCurrentMapPoint = false;
    private Vector2 currentLocalMapPoint;
    private Vector3 currentWorldMapPoint;

    private float nextAllowedPointerFindTime = 0f;

    private void Awake()
    {
        if (mapImageRect == null)
            mapImageRect = GetComponent<RectTransform>();

        PrepareMarker(blueHoverMarker);
        PrepareMarker(blueSelectedMarker);
        PrepareMarker(greenCorrectMarker);

        if (hideMarkersOnStart)
        {
            SetMarkerVisible(blueHoverMarker, false);
            SetMarkerVisible(blueSelectedMarker, false);
            SetMarkerVisible(greenCorrectMarker, false);
        }

        if (autoCreatePreciseRay && preciseRayLine == null)
            CreatePreciseRay();

        ConfigurePreciseRay();

        SetPreciseRayVisible(false);
    }

    private void Start()
    {
        TryFindNormalPointer(force: true);
    }

    private void Update()
    {
        if (autoFindNormalPointer && normalPointerLineRenderer == null)
            TryFindNormalPointer(force: false);

        bool rayValid = TryGetRay(out Vector3 rayOrigin, out Vector3 rayDirection);

        if (!rayValid)
        {
            SetMapHoverState(false);
            UpdateInputStateOnly();
            return;
        }

        if (debugDrawRay)
            Debug.DrawRay(rayOrigin, rayDirection * rayDistance, Color.cyan);

        bool hitMap = TryGetPointOnMap(rayOrigin, rayDirection, out Vector2 localPoint, out Vector3 worldPoint);

        if (hitMap)
        {
            hasCurrentMapPoint = true;
            currentLocalMapPoint = localPoint;
            currentWorldMapPoint = worldPoint;

            SetMapHoverState(true);
            UpdatePreciseRay(rayOrigin, worldPoint);

            if (!selectionLocked || !lockAfterFirstSelection)
            {
                MoveMarkerToLocalPoint(blueHoverMarker, localPoint);
                SetMarkerVisible(blueHoverMarker, true);
            }
        }
        else
        {
            hasCurrentMapPoint = false;
            SetMapHoverState(false);
        }

        HandleSelectionInput();
    }

    private void OnDisable()
    {
        SetPreciseRayVisible(false);
        SetNormalPointerVisible(true);

        isHoveringMap = false;
        hasCurrentMapPoint = false;
        lastTriggerPressed = false;
        lastMousePressed = false;
    }

    private bool TryGetRay(out Vector3 rayOrigin, out Vector3 rayDirection)
    {
        if (rightControllerRayOrigin != null)
        {
            rayOrigin = rightControllerRayOrigin.position;
            rayDirection = rightControllerRayOrigin.forward.normalized;
            return true;
        }

        if (rightControllerNearFarInteractor != null &&
            rightControllerNearFarInteractor.gameObject.activeInHierarchy &&
            rightControllerNearFarInteractor.isActiveAndEnabled)
        {
            rayOrigin = rightControllerNearFarInteractor.transform.position;
            rayDirection = rightControllerNearFarInteractor.transform.forward.normalized;
            return true;
        }

        rayOrigin = Vector3.zero;
        rayDirection = Vector3.forward;
        return false;
    }

    private bool TryGetPointOnMap(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        out Vector2 localPoint,
        out Vector3 worldPoint
    )
    {
        localPoint = Vector2.zero;
        worldPoint = Vector3.zero;

        if (mapImageRect == null)
            return false;

        Plane mapPlane = new Plane(mapImageRect.forward, mapImageRect.position);
        Ray ray = new Ray(rayOrigin, rayDirection);

        if (!mapPlane.Raycast(ray, out float enter))
            return false;

        if (enter < 0f || enter > rayDistance)
            return false;

        Vector3 hitPoint = ray.GetPoint(enter);
        Vector3 localPoint3D = mapImageRect.InverseTransformPoint(hitPoint);

        localPoint = new Vector2(localPoint3D.x, localPoint3D.y);

        if (!mapImageRect.rect.Contains(localPoint))
            return false;

        worldPoint = hitPoint + mapImageRect.forward * surfaceOffset;
        return true;
    }

    private void SetMapHoverState(bool hovering)
    {
        if (isHoveringMap == hovering)
            return;

        isHoveringMap = hovering;

        if (isHoveringMap)
        {
            SetNormalPointerVisible(false);
            SetPreciseRayVisible(true);

            if (logStateChanges)
                Debug.Log("RoomMapXRRaySelector: ray je na slici tlocrta.");
        }
        else
        {
            SetPreciseRayVisible(false);
            SetNormalPointerVisible(true);

            if (!selectionLocked)
                SetMarkerVisible(blueHoverMarker, false);

            if (logStateChanges)
                Debug.Log("RoomMapXRRaySelector: ray je izašao sa slike tlocrta.");
        }
    }

    private void HandleSelectionInput()
    {
        bool pressed = GetSelectionPressedThisFrame();

        if (!pressed)
            return;

        if (!isHoveringMap)
            return;

        if (!hasCurrentMapPoint)
            return;

        if (selectionLocked && lockAfterFirstSelection)
            return;

        ConfirmSelection(currentLocalMapPoint);
    }

    private bool GetSelectionPressedThisFrame()
    {
        bool currentPressed = false;

        if (useRightControllerTrigger)
        {
            InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            if (rightHand.isValid)
            {
                bool triggerButtonPressed = false;
                float triggerValue = 0f;

                rightHand.TryGetFeatureValue(CommonUsages.triggerButton, out triggerButtonPressed);
                rightHand.TryGetFeatureValue(CommonUsages.trigger, out triggerValue);

                currentPressed = triggerButtonPressed || triggerValue > 0.75f;
            }
        }

        bool pressedThisFrame = currentPressed && !lastTriggerPressed;
        lastTriggerPressed = currentPressed;

#if UNITY_EDITOR
        if (allowMouseClickInEditor)
        {
            bool mousePressed = Input.GetMouseButton(0);
            bool mousePressedThisFrame = mousePressed && !lastMousePressed;
            lastMousePressed = mousePressed;

            pressedThisFrame = pressedThisFrame || mousePressedThisFrame;
        }
#endif

        return pressedThisFrame;
    }

    private void UpdateInputStateOnly()
    {
        if (useRightControllerTrigger)
        {
            InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            if (rightHand.isValid)
            {
                bool triggerButtonPressed = false;
                float triggerValue = 0f;

                rightHand.TryGetFeatureValue(CommonUsages.triggerButton, out triggerButtonPressed);
                rightHand.TryGetFeatureValue(CommonUsages.trigger, out triggerValue);

                lastTriggerPressed = triggerButtonPressed || triggerValue > 0.75f;
            }
            else
            {
                lastTriggerPressed = false;
            }
        }

#if UNITY_EDITOR
        if (allowMouseClickInEditor)
            lastMousePressed = Input.GetMouseButton(0);
#endif
    }

    private void ConfirmSelection(Vector2 localPoint)
    {
        MoveMarkerToLocalPoint(blueSelectedMarker, localPoint);
        SetMarkerVisible(blueSelectedMarker, true);

        if (hideHoverMarkerAfterSelection)
            SetMarkerVisible(blueHoverMarker, false);

        if (showGreenMarkerAfterSelection)
            SetMarkerVisible(greenCorrectMarker, true);

        selectionLocked = true;

        if (logSelection)
            Debug.Log($"RoomMapXRRaySelector: korisnik je označio poziciju na karti: {localPoint}");
    }

    private void MoveMarkerToLocalPoint(RectTransform marker, Vector2 localPoint)
    {
        if (marker == null)
            return;

        marker.anchorMin = new Vector2(0.5f, 0.5f);
        marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.anchoredPosition = localPoint;
    }

    private void PrepareMarker(RectTransform marker)
    {
        if (marker == null)
            return;

        Image markerImage = marker.GetComponent<Image>();

        if (markerImage != null)
            markerImage.raycastTarget = false;
    }

    private void SetMarkerVisible(RectTransform marker, bool visible)
    {
        if (marker != null)
            marker.gameObject.SetActive(visible);
    }

    private void TryFindNormalPointer(bool force)
    {
        if (!autoFindNormalPointer)
            return;

        if (normalPointerLineRenderer != null)
            return;

        if (!force && Time.time < nextAllowedPointerFindTime)
            return;

        nextAllowedPointerFindTime = Time.time + refindNormalPointerInterval;

        GameObject foundObject = GameObject.Find(normalPointerRuntimeName);

        if (foundObject != null)
        {
            LineRenderer foundLine = foundObject.GetComponent<LineRenderer>();

            if (foundLine == null)
                foundLine = foundObject.GetComponentInChildren<LineRenderer>(true);

            if (foundLine != null)
            {
                normalPointerObject = foundObject;
                normalPointerLineRenderer = foundLine;

                if (logPointerSearch)
                    Debug.Log($"RoomMapXRRaySelector: pronađen normal pointer: {foundObject.name}");

                return;
            }
        }

        LineRenderer[] allLineRenderers = FindObjectsByType<LineRenderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < allLineRenderers.Length; i++)
        {
            LineRenderer line = allLineRenderers[i];

            if (line == null)
                continue;

            if (line.gameObject.name == normalPointerRuntimeName ||
                line.gameObject.name.Contains(normalPointerRuntimeName))
            {
                normalPointerObject = line.gameObject;
                normalPointerLineRenderer = line;

                if (logPointerSearch)
                    Debug.Log($"RoomMapXRRaySelector: pronađen normal pointer preko LineRenderer pretrage: {line.gameObject.name}");

                return;
            }
        }

        if (logPointerSearch)
            Debug.LogWarning($"RoomMapXRRaySelector: nije pronađen runtime pointer: {normalPointerRuntimeName}");
    }

    private void SetNormalPointerVisible(bool visible)
    {
        TryFindNormalPointer(force: false);

        if (normalPointerLineRenderer == null)
            return;

        if (hideOnlyNormalPointerLineRenderer)
        {
            normalPointerLineRenderer.enabled = visible;
        }
        else if (normalPointerObject != null)
        {
            normalPointerObject.SetActive(visible);
        }
    }

    private void CreatePreciseRay()
    {
        GameObject existing = GameObject.Find(preciseRayObjectName);

        if (existing != null)
        {
            preciseRayLine = existing.GetComponent<LineRenderer>();

            if (preciseRayLine != null)
                return;
        }

        GameObject rayObject = new GameObject(preciseRayObjectName);
        preciseRayLine = rayObject.AddComponent<LineRenderer>();
    }

    private void ConfigurePreciseRay()
    {
        if (preciseRayLine == null)
            return;

        preciseRayLine.positionCount = 2;
        preciseRayLine.useWorldSpace = true;
        preciseRayLine.loop = false;

        preciseRayLine.startWidth = preciseRayWidth;
        preciseRayLine.endWidth = preciseRayWidth;

        preciseRayLine.startColor = preciseRayColor;
        preciseRayLine.endColor = preciseRayColor;

        preciseRayLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        preciseRayLine.receiveShadows = false;

        preciseRayLine.material = CreateUnlitMaterial(preciseRayColor);
    }

    private void UpdatePreciseRay(Vector3 rayOrigin, Vector3 hitWorldPoint)
    {
        if (preciseRayLine == null)
            return;

        preciseRayLine.positionCount = 2;
        preciseRayLine.useWorldSpace = true;

        preciseRayLine.SetPosition(0, rayOrigin);
        preciseRayLine.SetPosition(1, hitWorldPoint);
    }

    private void SetPreciseRayVisible(bool visible)
    {
        if (preciseRayLine != null)
            preciseRayLine.enabled = visible;
    }

    private Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        Material material = new Material(shader);
        material.name = "RoomMapPreciseRayMaterial";
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        return material;
    }

    [ContextMenu("Reset Selection")]
    public void ResetSelection()
    {
        selectionLocked = false;
        hasCurrentMapPoint = false;

        SetMarkerVisible(blueHoverMarker, false);
        SetMarkerVisible(blueSelectedMarker, false);
        SetMarkerVisible(greenCorrectMarker, false);
    }

    [ContextMenu("Force Restore Normal Pointer")]
    public void ForceRestoreNormalPointer()
    {
        SetPreciseRayVisible(false);
        SetNormalPointerVisible(true);
    }
}