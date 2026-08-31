using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A single draggable jigsaw piece. Spawned and configured by
/// JigsawPuzzleController - HomeAnchoredPosition and snap threshold get
/// set right after this component is created, so don't rely on defaults.
///
/// If the piece starts out inside a scrollable tray (Content of a
/// ScrollRect), SetDragParent tells it which stable, non-scrolling parent
/// to move to the moment it's picked up - so once you start dragging it,
/// scrolling the tray afterward doesn't drag the piece along with it.
///
/// Pieces display smaller while sitting in the tray (SetInTrayScale) and
/// snap to full size the moment they're picked up, so kids can see the
/// whole tray clearly but still get a correctly-sized piece once dragged
/// onto the board. If dropped somewhere that isn't close enough to home,
/// OnDragEndedWithoutPlacing fires so the controller can return it to its
/// spot in the tray (this piece doesn't manage tray layout itself - the
/// controller owns that, since it needs to know about ALL pieces to
/// compact the remaining ones).
/// </summary>
[RequireComponent(typeof(Image))]
public class JigsawPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Vector2 HomeAnchoredPosition { get; set; }
    public bool IsPlaced { get; private set; }
    public bool IsDragging { get; private set; }

    // Which shuffled tray slot this piece started in - used by the
    // controller to keep remaining pieces in their original relative
    // order when the tray compacts after a piece is placed or returned.
    public int OriginalTraySlotIndex { get; set; }

    // Fired once, the moment this piece snaps into its home position.
    public event System.Action<JigsawPiece> OnPlaced;

    // Fired when a drag ends WITHOUT placing the piece (dropped too far
    // from home) - the controller listens for this to return the piece
    // to its spot in the tray.
    public event System.Action<JigsawPiece> OnDragEndedWithoutPlacing;

    private RectTransform rectTransform;
    private Canvas canvas;
    private float snapThreshold = 40f;
    private Transform dragParent;
    private float trayScale = 1f;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void SetSnapThreshold(float thresholdInPixels)
    {
        snapThreshold = thresholdInPixels;
    }

    /// <summary>
    /// The stable parent this piece should move to once picked up, if it
    /// doesn't already live there (e.g. moving out of a scrollable tray's
    /// Content into a fixed container). Pass null to skip reparenting.
    /// </summary>
    public void SetDragParent(Transform parent)
    {
        dragParent = parent;
    }

    /// <summary>
    /// Sets the display scale used while this piece sits in the tray
    /// (smaller than full size, so the whole tray is easy to scan) and
    /// immediately applies it. Remembered so the piece can be scaled back
    /// down if it's ever returned to the tray after a failed drop.
    /// </summary>
    public void SetInTrayScale(float scale)
    {
        trayScale = scale;
        rectTransform.localScale = new Vector3(scale, scale, 1f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsPlaced) return;

        IsDragging = true;

        // Detach from the scrollable tray (if that's where it started) so
        // scrolling the tray afterward doesn't move this piece too.
        // worldPositionStays keeps it visually exactly where it was.
        if (dragParent != null && rectTransform.parent != dragParent)
        {
            rectTransform.SetParent(dragParent, worldPositionStays: true);
        }

        // Guarantee dragParent (and everything in it) renders ABOVE the
        // tray, regardless of how they're ordered in the Hierarchy -
        // otherwise a picked-up piece could appear to slide BEHIND the
        // tray bar instead of in front of it.
        if (dragParent != null)
        {
            dragParent.SetAsLastSibling();
        }

        // Full size the moment it's picked up - easier to see/manipulate,
        // and matches the size needed to correctly fill a board slot.
        rectTransform.localScale = Vector3.one;

        // Bring to front while dragging so it's never hidden behind other pieces.
        rectTransform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (IsPlaced) return;

        // Canvas scale factor matters on mobile where the canvas is scaled
        // to fit different screen sizes - without dividing by it, pieces
        // would drift faster/slower than the finger depending on device.
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        rectTransform.anchoredPosition += eventData.delta / scale;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (IsPlaced) return;

        IsDragging = false;

        float distance = Vector2.Distance(rectTransform.anchoredPosition, HomeAnchoredPosition);
        if (distance <= snapThreshold)
        {
            rectTransform.anchoredPosition = HomeAnchoredPosition;
            IsPlaced = true;
            OnPlaced?.Invoke(this);
        }
        else
        {
            // Not close enough - let the controller return it to the tray
            // rather than leaving it wherever it was dropped.
            OnDragEndedWithoutPlacing?.Invoke(this);
        }
    }
}