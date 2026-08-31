using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drop this on any Button (or other UI object with a RectTransform) to
/// give it a tactile press animation: shrinks slightly on press, springs
/// back with a little overshoot on release. Works with both mouse and
/// touch automatically, since it uses the standard pointer event
/// interfaces (same system JigsawPiece's dragging uses).
///
/// Uses a per-frame Update loop rather than coroutines, specifically so
/// it can't get stuck mid-animation - coroutines can be silently
/// cancelled by things happening elsewhere in the same frame (heavy
/// synchronous work, object reparenting, etc.), which was leaving some
/// buttons stuck shrunk. This approach just checks "what scale should I
/// be at right now" every frame, so it always recovers on its own.
///
/// No wiring needed beyond attaching it - it reads its own RectTransform.
/// Tweak pressedScale / bounceScale / durations in the Inspector to taste.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonBounce : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Press")]
    [SerializeField] private float pressedScale = 0.92f;
    [SerializeField] private float pressDuration = 0.08f;

    [Header("Release (springs back with a little overshoot)")]
    [SerializeField] private float bounceScale = 1.06f;
    [SerializeField] private float bounceDuration = 0.12f;
    [SerializeField] private float settleDuration = 0.08f;

    private enum AnimState { Idle, Pressing, BouncingUp, Settling }

    private RectTransform rectTransform;
    private AnimState state = AnimState.Idle;
    private float stateElapsed;
    private float fromScale = 1f;
    private float toScale = 1f;
    private bool isPressed;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnDisable()
    {
        // Safety net for the case where this GameObject gets deactivated
        // mid-animation (e.g. hiding a panel) - guarantees it's back to
        // normal the next time it's shown, regardless of what interrupted it.
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
        }

        isPressed = false;
        state = AnimState.Idle;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        BeginState(AnimState.Pressing, rectTransform.localScale.x, pressedScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressed) return;
        isPressed = false;
        BeginState(AnimState.BouncingUp, rectTransform.localScale.x, bounceScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // If a finger/cursor drags off the button while pressed (common on
        // touch), treat it the same as releasing - otherwise it could get
        // stuck shrunk.
        if (!isPressed) return;
        isPressed = false;
        BeginState(AnimState.BouncingUp, rectTransform.localScale.x, bounceScale);
    }

    private void BeginState(AnimState newState, float from, float to)
    {
        state = newState;
        stateElapsed = 0f;
        fromScale = from;
        toScale = to;
    }

    private void Update()
    {
        if (state == AnimState.Idle) return;

        float duration = pressDuration;
        if (state == AnimState.BouncingUp) duration = bounceDuration;
        else if (state == AnimState.Settling) duration = settleDuration;

        stateElapsed += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(stateElapsed / duration) : 1f;
        float scale = Mathf.Lerp(fromScale, toScale, t);
        rectTransform.localScale = new Vector3(scale, scale, 1f);

        if (t >= 1f)
        {
            if (state == AnimState.BouncingUp)
            {
                BeginState(AnimState.Settling, bounceScale, 1f);
            }
            else
            {
                state = AnimState.Idle;
            }
        }
    }
}