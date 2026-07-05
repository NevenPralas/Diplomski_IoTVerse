using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapImageSelectionMarker : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler,
    IPointerDownHandler,
    IPointerClickHandler
{
    [Header("Map Image")]
    [Tooltip("RectTransform slike tlocrta. Ako ostane prazno, koristi se RectTransform ovog objekta.")]
    [SerializeField] private RectTransform mapImageRect;

    [Header("Markers")]
    [Tooltip("Plava kružnica koja prati ray dok korisnik prelazi preko slike.")]
    [SerializeField] private RectTransform blueHoverMarker;

    [Tooltip("Plava kružnica koja ostaje na mjestu nakon pritiska Triggera.")]
    [SerializeField] private RectTransform blueSelectedMarker;

    [Tooltip("Zelena kružnica koja označava stvarnu/točnu lokaciju problema.")]
    [SerializeField] private RectTransform greenCorrectMarker;

    [Header("Behaviour")]
    [Tooltip("Ako je uključeno, nakon prvog odabira korisnik više ne može mijenjati plavu oznaku.")]
    [SerializeField] private bool lockAfterFirstSelection = true;

    [Tooltip("Ako je uključeno, zelena oznaka se prikazuje tek nakon korisnikovog odabira.")]
    [SerializeField] private bool showGreenMarkerAfterSelection = true;

    [Tooltip("Ako je uključeno, svi markeri se sakrivaju na početku scene.")]
    [SerializeField] private bool hideMarkersOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool logSelection = false;

    private bool pointerIsOverMap = false;
    private bool selectionLocked = false;

    private bool hasLastLocalPoint = false;
    private Vector2 lastLocalPoint;

    private int lastSelectionFrame = -1;

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
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerIsOverMap = true;

        if (selectionLocked && lockAfterFirstSelection)
            return;

        UpdateHoverMarker(eventData);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!pointerIsOverMap)
            return;

        if (selectionLocked && lockAfterFirstSelection)
            return;

        UpdateHoverMarker(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerIsOverMap = false;
        hasLastLocalPoint = false;

        if (!selectionLocked)
            SetMarkerVisible(blueHoverMarker, false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TryConfirmSelectionFromPointer(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryConfirmSelectionFromPointer(eventData);
    }

    private void UpdateHoverMarker(PointerEventData eventData)
    {
        Vector2 localPoint;

        if (!TryGetLocalPointOnMap(eventData, out localPoint))
            return;

        lastLocalPoint = localPoint;
        hasLastLocalPoint = true;

        MoveMarkerToLocalPoint(blueHoverMarker, localPoint);
        SetMarkerVisible(blueHoverMarker, true);
    }

    private void TryConfirmSelectionFromPointer(PointerEventData eventData)
    {
        if (selectionLocked && lockAfterFirstSelection)
            return;

        // Sprječava dvostruki odabir u istom frameu ako Unity pošalje i PointerDown i PointerClick.
        if (lastSelectionFrame == Time.frameCount)
            return;

        Vector2 localPoint;

        if (TryGetLocalPointOnMap(eventData, out localPoint))
        {
            ConfirmSelection(localPoint);
            lastSelectionFrame = Time.frameCount;
            return;
        }

        if (hasLastLocalPoint)
        {
            ConfirmSelection(lastLocalPoint);
            lastSelectionFrame = Time.frameCount;
        }
    }

    private void ConfirmSelection(Vector2 localPoint)
    {
        lastLocalPoint = localPoint;
        hasLastLocalPoint = true;

        MoveMarkerToLocalPoint(blueSelectedMarker, localPoint);
        SetMarkerVisible(blueSelectedMarker, true);

        SetMarkerVisible(blueHoverMarker, false);

        if (showGreenMarkerAfterSelection)
            SetMarkerVisible(greenCorrectMarker, true);

        selectionLocked = true;

        if (logSelection)
            Debug.Log($"Korisnik je označio poziciju na slici: {localPoint}");
    }

    private bool TryGetLocalPointOnMap(PointerEventData eventData, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        if (mapImageRect == null)
            return false;

        RaycastResult raycastResult = eventData.pointerCurrentRaycast;

        if (raycastResult.gameObject != null)
        {
            Vector3 worldPoint = raycastResult.worldPosition;

            if (worldPoint != Vector3.zero || raycastResult.distance > 0.0001f)
            {
                Vector3 localPoint3D = mapImageRect.InverseTransformPoint(worldPoint);
                localPoint = new Vector2(localPoint3D.x, localPoint3D.y);

                if (mapImageRect.rect.Contains(localPoint))
                    return true;
            }
        }

        Camera eventCamera = eventData.pressEventCamera;

        if (eventCamera == null)
            eventCamera = eventData.enterEventCamera;

        bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapImageRect,
            eventData.position,
            eventCamera,
            out localPoint
        );

        if (!success)
            return false;

        return mapImageRect.rect.Contains(localPoint);
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

    private void SetMarkerVisible(RectTransform marker, bool visible)
    {
        if (marker != null)
            marker.gameObject.SetActive(visible);
    }

    private void PrepareMarker(RectTransform marker)
    {
        if (marker == null)
            return;

        Image markerImage = marker.GetComponent<Image>();

        if (markerImage != null)
            markerImage.raycastTarget = false;
    }

    [ContextMenu("Reset Selection")]
    public void ResetSelection()
    {
        selectionLocked = false;
        pointerIsOverMap = false;
        hasLastLocalPoint = false;
        lastSelectionFrame = -1;

        SetMarkerVisible(blueHoverMarker, false);
        SetMarkerVisible(blueSelectedMarker, false);
        SetMarkerVisible(greenCorrectMarker, false);
    }
}