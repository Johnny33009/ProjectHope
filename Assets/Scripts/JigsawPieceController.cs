using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a jigsaw puzzle at runtime by slicing a single Sprite into a
/// grid of pieces, laying them out in a shuffled horizontal SCROLLABLE
/// tray at a smaller "browse" size, and firing OnPuzzleComplete once
/// every piece has been dragged into place.
///
/// Tray behavior:
///   - Pieces display at traySmallScale while sitting in the tray (easy
///     to see the whole tray at once), and snap to full size the moment
///     they're picked up.
///   - When a piece is placed correctly, remaining tray pieces compact
///     left to fill the gap, keeping them easy to find.
///   - When a piece is dropped somewhere that ISN'T close enough to its
///     home spot, it automatically returns to its place in the tray
///     (not left wherever it was dropped).
///
/// Wiring (set in Inspector):
///   boardArea       -> RectTransform marking where the assembled picture
///                      belongs (defines piece size + home layout)
///   trayScrollRect  -> the ScrollRect component on your tray (Horizontal
///                      only). Its Viewport should be a masked RectTransform
///                      showing only part of the tray; its Content is
///                      trayContent below.
///   trayContent     -> the ScrollRect's Content RectTransform - pieces
///                      spawn here initially, and this gets resized wider
///                      every build so there's room to scroll when there
///                      are more pieces than fit on screen at once
///   piecePrefab     -> a UI prefab with an Image component (a JigsawPiece
///                      component gets added automatically if missing)
///   pieceMaskTexture -> the puzzle-piece silhouette shape (a texture with
///                      alpha: opaque where the piece shape is, transparent
///                      outside it). Each piece's photo gets cut to this
///                      shape at runtime, so pieces look jigsaw-shaped
///                      instead of plain rectangles. Requires BOTH this
///                      texture AND every letter illustration texture to
///                      have "Read/Write Enabled" checked in their Import
///                      Settings (Advanced section) - without that, pixel
///                      reading fails and you'll get an error instead of
///                      a puzzle piece. Leave unassigned for plain
///                      rectangular pieces.
///   pieceParent     -> Transform pieces move to once picked up (detaching
///                      them from the scrollable tray) AND the coordinate
///                      frame home positions are calculated in - must share
///                      the same Canvas as boardArea (a plain RectTransform
///                      directly under Canvas, not nested in boardArea).
///                      This also gets brought to the front of its own
///                      siblings whenever a piece is picked up, so dragged
///                      pieces always render ABOVE the tray, never behind it.
///   traySlotPadding -> extra spacing multiplier around each piece in the
///                      tray so they don't touch (1.15 = 15% padding)
///   traySmallScale  -> display scale for pieces while they sit in the
///                      tray (e.g. 0.7 = 70% of full size). They snap to
///                      full size (1.0) the moment they're picked up.
///   snapThreshold   -> how close (in pixels) a piece must be dropped to
///                      its home position to snap into place
///   audioSource     -> AudioSource for the piece-snap chime (needs Play
///                      On Awake OFF - PlayOneShot is used)
///   pieceSnapClip   -> short sound played each time a piece snaps into
///                      place (optional)
///
/// A visual border for the whole play area (showing kids where the picture
/// goes) doesn't need code - just add a static border/outline Image as a
/// child of boardArea in the Editor, stretched to fill it.
///
/// Call BuildPuzzle(sprite, pieceCount) to start a puzzle. Call
/// ClearPuzzle() before building a new one if pieces from a previous
/// letter might still be around (BuildPuzzle does this automatically).
/// </summary>
public class JigsawPuzzleController : MonoBehaviour
{
    [SerializeField] private RectTransform boardArea;
    [SerializeField] private ScrollRect trayScrollRect;
    [SerializeField] private RectTransform trayContent;
    [SerializeField] private Image piecePrefab;
    [SerializeField] private Texture2D pieceMaskTexture;
    [SerializeField] private Transform pieceParent;
    [SerializeField] private float traySlotPadding = 1.15f;
    [SerializeField] private float traySmallScale = 0.7f;
    [SerializeField] private float snapThreshold = 40f;

    [Header("SFX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pieceSnapClip;

    private readonly List<JigsawPiece> activePieces = new List<JigsawPiece>();

    // Remembered from the most recent BuildPuzzle call, so
    // RecalculateTrayLayout can reposition pieces later (on placement or
    // failed drop) using the same spacing the tray was originally built with.
    private float currentTraySlotWidth;
    private float currentTrayContentWidth;

    public event System.Action OnPuzzleComplete;

    public void BuildPuzzle(Sprite sourceSprite, int pieceCount)
    {
        ClearPuzzle();

        if (sourceSprite == null)
        {
            Debug.LogError("JigsawPuzzleController: no source sprite provided.");
            return;
        }

        (int rows, int cols) = GetGridDimensions(pieceCount);
        int totalPieces = rows * cols;

        Rect spriteRect = sourceSprite.rect; // region of the texture this sprite uses
        Texture2D texture = sourceSprite.texture;

        float pieceWidth = boardArea.rect.width / cols;
        float pieceHeight = boardArea.rect.height / rows;

        float sourcePieceWidth = spriteRect.width / cols;
        float sourcePieceHeight = spriteRect.height / rows;

        // Tray pieces keep this fixed comfortable size regardless of piece
        // count - the tray scrolls instead of squeezing pieces smaller.
        currentTraySlotWidth = pieceWidth * traySlotPadding;
        currentTrayContentWidth = Mathf.Max(trayContent.rect.width, totalPieces * currentTraySlotWidth);
        trayContent.sizeDelta = new Vector2(currentTrayContentWidth, trayContent.sizeDelta.y);

        // Pieces get laid out left-to-right in the tray in a shuffled
        // order (not the same order they're generated in), so the tray
        // position doesn't just match left-to-right solve order.
        List<int> traySlots = ShuffledIndices(totalPieces);
        int pieceCounter = 0;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                // Unity textures are stored bottom-up (row 0 in texture
                // data = bottom of the image), but "row" here counts
                // top-down (row 0 = the piece that belongs at the visual
                // top). Flip which texture row we read so piece content
                // actually matches where each piece's home position is.
                int textureRow = rows - 1 - row;

                Rect pieceTextureRect = new Rect(
                    spriteRect.x + col * sourcePieceWidth,
                    spriteRect.y + textureRow * sourcePieceHeight,
                    sourcePieceWidth,
                    sourcePieceHeight);

                Sprite pieceSprite = BuildMaskedPieceSprite(texture, pieceTextureRect, sourceSprite.pixelsPerUnit);

                // Home position: where this piece sits when the picture is
                // fully assembled, relative to boardArea's center. Rows are
                // sliced top-to-bottom from the sprite, but anchoredPosition
                // Y grows upward, so row order is inverted here.
                float homeX = (col + 0.5f) * pieceWidth - boardArea.rect.width / 2f;
                float homeY = boardArea.rect.height / 2f - (row + 0.5f) * pieceHeight;

                Image pieceImage = Instantiate(piecePrefab, trayContent);
                pieceImage.sprite = pieceSprite;
                pieceImage.gameObject.name = $"Piece_{row}_{col}";

                RectTransform pieceRT = pieceImage.rectTransform;
                pieceRT.sizeDelta = new Vector2(pieceWidth, pieceHeight);

                int slotIndex = traySlots[pieceCounter];
                pieceCounter++;

                JigsawPiece piece = pieceImage.GetComponent<JigsawPiece>();
                if (piece == null) piece = pieceImage.gameObject.AddComponent<JigsawPiece>();

                piece.OriginalTraySlotIndex = slotIndex;
                pieceRT.anchoredPosition = GetTraySlotLocalPosition(slotIndex);
                piece.SetInTrayScale(traySmallScale);

                // Convert the home point through world space rather than
                // assuming boardArea and pieceParent share one coordinate
                // frame - this works correctly no matter how they're
                // positioned/scaled relative to each other.
                Vector3 homeWorldPoint = boardArea.TransformPoint(new Vector3(homeX, homeY, 0f));
                Vector3 homeLocalInParent = pieceParent.InverseTransformPoint(homeWorldPoint);
                piece.HomeAnchoredPosition = new Vector2(homeLocalInParent.x, homeLocalInParent.y);

                piece.SetSnapThreshold(snapThreshold);
                piece.SetDragParent(pieceParent);
                piece.OnPlaced += HandlePiecePlaced;
                piece.OnDragEndedWithoutPlacing += HandlePieceDroppedWithoutPlacing;

                activePieces.Add(piece);
            }
        }

        if (trayScrollRect != null)
        {
            trayScrollRect.horizontalNormalizedPosition = 0f; // reset scroll to the start
        }
    }

    /// <summary>
    /// Builds a new sprite by combining a piece's photo region with
    /// pieceMaskTexture's alpha shape - the result is a jigsaw-piece-shaped
    /// cutout of the photo instead of a plain rectangle. Falls back to a
    /// plain rectangular sprite (with a warning) if no mask is assigned.
    ///
    /// Requires sourceTexture and pieceMaskTexture to both have
    /// "Read/Write Enabled" checked in their Import Settings.
    /// </summary>
    private Sprite BuildMaskedPieceSprite(Texture2D sourceTexture, Rect pieceTextureRect, float pixelsPerUnit)
    {
        if (pieceMaskTexture == null)
        {
            return Sprite.Create(sourceTexture, pieceTextureRect, new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        int width = Mathf.RoundToInt(pieceTextureRect.width);
        int height = Mathf.RoundToInt(pieceTextureRect.height);
        int startX = Mathf.RoundToInt(pieceTextureRect.x);
        int startY = Mathf.RoundToInt(pieceTextureRect.y);

        Color[] photoPixels = sourceTexture.GetPixels(startX, startY, width, height);
        Color32[] outputPixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;

            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;

                Color maskColor = pieceMaskTexture.GetPixelBilinear(u, v);
                Color photo = photoPixels[y * width + x];

                byte combinedAlpha = (byte)(photo.a * maskColor.a * 255f);
                outputPixels[y * width + x] = new Color32(
                    (byte)(photo.r * 255f),
                    (byte)(photo.g * 255f),
                    (byte)(photo.b * 255f),
                    combinedAlpha);
            }
        }

        Texture2D pieceTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        pieceTexture.SetPixels32(outputPixels);
        pieceTexture.Apply();

        return Sprite.Create(
            pieceTexture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);
    }

    public void ClearPuzzle()
    {
        foreach (JigsawPiece piece in activePieces)
        {
            if (piece != null) Destroy(piece.gameObject);
        }

        activePieces.Clear();
    }

    private void HandlePiecePlaced(JigsawPiece piece)
    {
        if (audioSource != null && pieceSnapClip != null)
        {
            audioSource.PlayOneShot(pieceSnapClip);
        }

        // A piece just left the tray for good - shift the remaining
        // pieces left to fill the gap and keep them easy to find.
        RecalculateTrayLayout();

        foreach (JigsawPiece p in activePieces)
        {
            if (!p.IsPlaced) return; // at least one piece still unplaced
        }

        OnPuzzleComplete?.Invoke();
    }

    /// <summary>
    /// A piece was dropped somewhere that wasn't close enough to home -
    /// return it (and re-compact everyone else) to their tray spots.
    /// </summary>
    private void HandlePieceDroppedWithoutPlacing(JigsawPiece piece)
    {
        RecalculateTrayLayout();
    }

    /// <summary>
    /// Repositions every currently-unplaced, not-being-dragged piece into
    /// its compacted tray slot (ordered by OriginalTraySlotIndex, with no
    /// gaps for pieces that have already been placed) - reparenting it
    /// back into trayContent and restoring the small tray display scale
    /// if it isn't already there (e.g. a piece returning after a failed
    /// drop). A piece currently being dragged is left alone.
    /// </summary>
    private void RecalculateTrayLayout()
    {
        List<JigsawPiece> unplaced = activePieces
            .Where(p => p != null && !p.IsPlaced && !p.IsDragging)
            .OrderBy(p => p.OriginalTraySlotIndex)
            .ToList();

        for (int i = 0; i < unplaced.Count; i++)
        {
            JigsawPiece piece = unplaced[i];
            RectTransform pieceRT = piece.GetComponent<RectTransform>();

            if (pieceRT.parent != trayContent)
            {
                pieceRT.SetParent(trayContent, worldPositionStays: false);
            }

            pieceRT.anchoredPosition = GetTraySlotLocalPosition(i);
            piece.SetInTrayScale(traySmallScale);
        }
    }

    /// <summary>
    /// Local position (within trayContent) for the given slot index, using
    /// the spacing established for the current puzzle.
    /// </summary>
    private Vector2 GetTraySlotLocalPosition(int slotIndex)
    {
        float x = -currentTrayContentWidth / 2f + (slotIndex + 0.5f) * currentTraySlotWidth;
        return new Vector2(x, 0f); // vertically centered in the tray content
    }

    /// <summary>
    /// Fisher-Yates shuffle of [0, count) - used to randomize which tray
    /// slot each piece lands in, so the tray order doesn't match
    /// left-to-right solve order.
    /// </summary>
    private List<int> ShuffledIndices(int count)
    {
        List<int> indices = new List<int>(count);
        for (int i = 0; i < count; i++) indices.Add(i);

        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        return indices;
    }

    /// <summary>
    /// Finds a rows x cols grid closest to square for the requested piece
    /// count, e.g. 4 -> 2x2, 6 -> 2x3, 9 -> 3x3, 10 -> 3x4. The actual
    /// piece count produced is rows * cols, which may be slightly more
    /// than requested if pieceCount isn't a clean rectangle.
    /// </summary>
    private (int rows, int cols) GetGridDimensions(int pieceCount)
    {
        int rows = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(pieceCount)));
        int cols = Mathf.CeilToInt((float)pieceCount / rows);
        return (rows, cols);
    }
}