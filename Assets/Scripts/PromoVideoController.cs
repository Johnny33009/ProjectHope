using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Controls the full-screen promo video overlay shown between letters.
/// Rotates through a list of clips (round-robin) so repeated views don't
/// always show the same one.
///
/// Wiring (set in Inspector):
///   videoPanel   -> full-screen GameObject containing the video display
///                  (a RawImage targeting videoPlayer's Render Texture is
///                  the usual setup - this script doesn't care which
///                  display method you use, just that videoPanel shows/
///                  hides the whole thing)
///   videoPlayer  -> the VideoPlayer component playing the clip
///   promoClips   -> list of VideoClip assets to rotate through. Each
///                  Play() call uses the next one in the list, wrapping
///                  back to the start after the last. If empty, falls
///                  back to whatever clip is already assigned directly
///                  on the Video Player component.
///   skipButton   -> button that appears after skipDelaySeconds, lets the
///                  player skip ahead - optional, leave unassigned for a
///                  non-skippable video (not recommended for a kids app,
///                  but supported)
///   skipDelaySeconds -> how long before Skip becomes available
///   aspectRatioFitter -> the AspectRatioFitter on your RawImage (optional
///                  but strongly recommended if your promo clips have
///                  DIFFERENT resolutions/aspect ratios from each other -
///                  without this, a single fixed Aspect Ratio value on
///                  that component only fits ONE of your clips correctly,
///                  and every other clip will look cropped or squished no
///                  matter what value you pick. This sets it dynamically
///                  per-clip instead, based on each clip's actual size.
///
/// Call Play() to start the video. OnVideoFinished fires once, either
/// when the clip ends naturally or the player taps Skip - the caller
/// doesn't need to know which happened.
/// </summary>
public class PromoVideoController : MonoBehaviour
{
    [SerializeField] private GameObject videoPanel;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private List<VideoClip> promoClips = new List<VideoClip>();
    [SerializeField] private Button skipButton;
    [SerializeField] private float skipDelaySeconds = 3f;
    [SerializeField] private AspectRatioFitter aspectRatioFitter;

    public event System.Action OnVideoFinished;

    private Coroutine skipRevealRoutine;
    private int nextClipIndex = 0;

    private void Awake()
    {
        if (videoPanel != null) videoPanel.SetActive(false);

        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(false);
            skipButton.onClick.AddListener(FinishVideo);
        }
    }

    public void Play()
    {
        if (videoPanel == null || videoPlayer == null)
        {
            Debug.LogWarning("PromoVideoController: not fully wired - skipping promo video.");
            OnVideoFinished?.Invoke();
            return;
        }

        VideoClip selectedClip = null;

        if (promoClips.Count > 0)
        {
            selectedClip = promoClips[nextClipIndex];
            videoPlayer.clip = selectedClip;
            nextClipIndex = (nextClipIndex + 1) % promoClips.Count;
        }
        else
        {
            selectedClip = videoPlayer.clip;
        }

        // Set the aspect ratio for THIS specific clip, not a fixed value -
        // different clips can have different native resolutions, and a
        // single static Aspect Ratio Fitter value only fits one of them.
        if (aspectRatioFitter != null && selectedClip != null && selectedClip.height > 0)
        {
            aspectRatioFitter.aspectRatio = (float)selectedClip.width / selectedClip.height;
        }

        videoPanel.SetActive(true);
        videoPlayer.loopPointReached += HandleVideoReachedEnd;
        videoPlayer.Play();

        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(false);
            if (skipRevealRoutine != null) StopCoroutine(skipRevealRoutine);
            skipRevealRoutine = StartCoroutine(RevealSkipAfterDelay());
        }
    }

    private IEnumerator RevealSkipAfterDelay()
    {
        yield return new WaitForSeconds(skipDelaySeconds);
        if (skipButton != null) skipButton.gameObject.SetActive(true);
    }

    private void HandleVideoReachedEnd(VideoPlayer vp)
    {
        FinishVideo();
    }

    private void FinishVideo()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= HandleVideoReachedEnd;
            videoPlayer.Stop();
        }

        if (skipRevealRoutine != null)
        {
            StopCoroutine(skipRevealRoutine);
            skipRevealRoutine = null;
        }

        if (videoPanel != null) videoPanel.SetActive(false);

        OnVideoFinished?.Invoke();
    }
}