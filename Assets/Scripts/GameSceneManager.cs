using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Lives in the Game scene. Shows the current letter's word and picture
/// (from GameManager.CurrentLetterIndex), builds its jigsaw puzzle via
/// JigsawPuzzleController, and on solve reveals the full picture with a
/// confetti burst - then WAITS for the player to press Next before moving
/// on (it no longer auto-advances). Also drives the progress indicator
/// and the Replay/Next buttons.
///
/// Flow: Show letter A's word/picture -> build A's puzzle -> player solves
/// it -> JigsawPuzzleController fires OnPuzzleComplete -> reveal picture +
/// confetti -> player clicks Next -> advance to B -> repeat.
///
/// Wiring (set in Inspector):
///   letterDatabase      -> the LetterDatabase asset
///   wordText            -> TextMeshPro text that displays the current word
///   illustrationImage   -> Image that displays the current picture
///                          (hidden while the puzzle is active, revealed
///                          again on solve as the reward)
///   boardArea           -> the SAME RectTransform wired into
///                          JigsawPuzzleController's Board Area field -
///                          used to align the revealed picture exactly
///                          over the puzzle area
///   puzzleController    -> the JigsawPuzzleController in this scene
///   progressText        -> TextMeshPro text showing the current letter,
///                          e.g. "Letter A" - always the same character
///                          length regardless of which letter, so the
///                          bubble only needs to be sized once
///   replayButton        -> rebuilds the CURRENT letter's puzzle from
///                          scratch (new scramble, same letter)
///   nextButton          -> advances to the next letter - LOCKED
///                          (non-interactable) until the current puzzle
///                          is solved, then becomes the "continue" action
///                          once the reward is showing
///   confettiEffect      -> the ConfettiEffect component that plays on
///                          solve (optional - leave null to skip)
///   audioSource         -> AudioSource for the completion fanfare (needs
///                          Play On Awake OFF - PlayOneShot is used)
///   puzzleCompleteClip  -> sound played the moment a puzzle is solved,
///                          alongside the confetti/reveal (optional)
///   celebrationClip     -> a bigger celebratory sound (e.g. applause)
///                          played alongside puzzleCompleteClip - the two
///                          overlap/mix together via PlayOneShot (optional)
///   promoVideoController -> the PromoVideoController for between-letter
///                          promo videos (optional - leave null to skip
///                          entirely)
///   showPromoEveryNLetters -> shows the promo video after every Nth
///                          letter completed (e.g. 5 = after letters
///                          5, 10, 15...). Set to 0 to disable.
///   gameCompletePanel   -> "Well Done!" panel shown after finishing the
///                          LAST letter (Z) - covers the Next button flow,
///                          since there's no next letter to go to
///   gameCompleteCanvasGroup -> CanvasGroup on gameCompletePanel, used to
///                          fade it in smoothly (optional - leave null
///                          for an instant appearance with no fade)
///   gameCompleteFadeInDuration -> seconds the fade-in takes
///   homeButton          -> button on gameCompletePanel that returns to
///                          the Main Menu when tapped - the player
///                          chooses when to leave, no auto-timer
///   websiteButton       -> button on gameCompletePanel that opens the
///                          website matching the CURRENT language when
///                          tapped
///   websiteUrlEnglish   -> URL to open when the player is in English mode
///   websiteUrlSpanish   -> URL to open when the player is in Spanish mode
/// </summary>
public class GameSceneManager : MonoBehaviour
{
    [SerializeField] private LetterDatabase letterDatabase;
    [SerializeField] private TMP_Text wordText;
    [SerializeField] private Image illustrationImage;
    [SerializeField] private RectTransform boardArea;
    [SerializeField] private JigsawPuzzleController puzzleController;

    [Header("Progress & Controls")]
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Reward")]
    [SerializeField] private ConfettiEffect confettiEffect;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip puzzleCompleteClip;
    [SerializeField] private AudioClip celebrationClip;

    [Header("Promo Video")]
    [SerializeField] private PromoVideoController promoVideoController;
    [SerializeField] private int showPromoEveryNLetters = 5;

    [Header("Game Complete (after Z)")]
    [SerializeField] private GameObject gameCompletePanel;
    [SerializeField] private CanvasGroup gameCompleteCanvasGroup;
    [SerializeField] private float gameCompleteFadeInDuration = 0.5f;
    [SerializeField] private Button homeButton;
    [SerializeField] private Button websiteButton;
    [SerializeField] private string websiteUrlEnglish = "https://example.com";
    [SerializeField] private string websiteUrlSpanish = "https://example.com/es";

    private void Awake()
    {
        puzzleController.OnPuzzleComplete += HandlePuzzleComplete;
    }

    private void OnDestroy()
    {
        puzzleController.OnPuzzleComplete -= HandlePuzzleComplete;

        if (promoVideoController != null) promoVideoController.OnVideoFinished -= AdvanceAndShowNext;
    }

    private void Start()
    {
        GameManager.Instance.SetState(GameManager.GameState.Puzzle);

        if (replayButton != null) replayButton.onClick.AddListener(ReplayCurrentLetter);
        if (nextButton != null) nextButton.onClick.AddListener(RequestAdvance);
        if (menuButton != null) menuButton.onClick.AddListener(OnMenuClicked);
        if (homeButton != null) homeButton.onClick.AddListener(OnMenuClicked);
        if (websiteButton != null) websiteButton.onClick.AddListener(OnWebsiteClicked);

        if (promoVideoController != null) promoVideoController.OnVideoFinished += AdvanceAndShowNext;

        if (gameCompletePanel != null) gameCompletePanel.SetActive(false);

        ShowCurrentLetter();
    }

    private void ShowCurrentLetter()
    {
        if (letterDatabase == null)
        {
            Debug.LogError("GameSceneManager: no LetterDatabase assigned.");
            return;
        }

        LanguageManager.Language currentLanguage = LanguageManager.Instance.CurrentLanguage;
        List<LetterData> activeLetters = letterDatabase.GetLettersForLanguage(currentLanguage);

        if (activeLetters.Count == 0)
        {
            Debug.LogError($"GameSceneManager: no letters available for {currentLanguage}.");
            return;
        }

        // The saved progress index doesn't know which language's letter
        // list it was earned in - Spanish has one more letter than English
        // (Ñ), so a position valid in one language can be out of range in
        // the other after switching. Clamp rather than error/blank out:
        // worst case, switching languages lands you on that language's
        // last letter instead of exactly where you left off.
        int index = Mathf.Clamp(GameManager.Instance.CurrentLetterIndex, 0, activeLetters.Count - 1);

        LetterData data = activeLetters[index];

        string currentWord = data.GetWord(currentLanguage);
        Sprite currentIllustration = data.GetIllustration(currentLanguage);

        if (currentIllustration == null)
        {
            Debug.LogError($"GameSceneManager: letter '{data.letter}' has no illustration for {currentLanguage}. " +
                $"If this is Ñ, check that 'Spanish Only' is actually checked on its LetterData asset - " +
                $"otherwise English tries to use its (intentionally blank) English illustration and crashes.");
            return; // bail out safely instead of crashing on a null sprite
        }

        if (wordText != null) wordText.text = currentWord;

        if (progressText != null)
        {
            progressText.text = $"Letter {data.letter}";
        }

        // Hide the picture while the puzzle is being solved - it gets
        // revealed again (aligned to boardArea) once the puzzle is solved,
        // as the reward.
        if (illustrationImage != null)
        {
            illustrationImage.sprite = currentIllustration;
            illustrationImage.gameObject.SetActive(false);
        }

        puzzleController.BuildPuzzle(currentIllustration, data.jigsawPieceCount);

        // Next stays locked until THIS puzzle is actually solved - no
        // skipping ahead mid-solve.
        if (nextButton != null) nextButton.interactable = false;

        // Audio narration hookup comes later, once an AudioSource exists
        // in the scene and LanguageManager is confirmed present here.
    }

    private void HandlePuzzleComplete()
    {
        RevealPicture();

        // Puzzle is actually solved now - Next becomes usable.
        if (nextButton != null) nextButton.interactable = true;

        if (confettiEffect != null)
        {
            confettiEffect.Burst();
        }

        if (audioSource != null && puzzleCompleteClip != null)
        {
            audioSource.PlayOneShot(puzzleCompleteClip);
        }

        if (audioSource != null && celebrationClip != null)
        {
            audioSource.PlayOneShot(celebrationClip);
        }

        // Deliberately NOT auto-advancing anymore - the player now clicks
        // Next to continue, so they get a moment with the completed picture.
    }

    /// <summary>
    /// Shows the full picture, aligned exactly over boardArea so it lines
    /// up with where the pieces were just assembled.
    /// </summary>
    private void RevealPicture()
    {
        if (illustrationImage == null || boardArea == null) return;

        illustrationImage.rectTransform.anchoredPosition = boardArea.anchoredPosition;
        illustrationImage.rectTransform.sizeDelta = boardArea.sizeDelta;
        illustrationImage.gameObject.SetActive(true);
    }

    /// <summary>
    /// Returns to the Main Menu. Uses Unity's built-in scene loader
    /// directly (not GameManager/SceneLoader) so it works regardless of
    /// whether those singletons exist yet - going to the menu doesn't
    /// need any state from them.
    /// </summary>
    private void OnMenuClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Opens the website matching whichever language is currently active.
    /// </summary>
    private void OnWebsiteClicked()
    {
        string url = LanguageManager.Instance.CurrentLanguage == LanguageManager.Language.Spanish
            ? websiteUrlSpanish
            : websiteUrlEnglish;

        Application.OpenURL(url);
    }

    /// <summary>
    /// Rebuilds the SAME letter's puzzle with a fresh scramble, without
    /// advancing progress. Wired to the Replay button.
    /// </summary>
    public void ReplayCurrentLetter()
    {
        ShowCurrentLetter();
    }

    /// <summary>
    /// Called when Next is clicked. Checks whether the letter just
    /// completed hits the promo video milestone (every Nth letter) - if
    /// so, shows the promo first and advances once it finishes; otherwise
    /// advances immediately.
    /// </summary>
    private void RequestAdvance()
    {
        int activeCount = letterDatabase.GetLettersForLanguage(LanguageManager.Instance.CurrentLanguage).Count;
        bool isLastLetter = GameManager.Instance.CurrentLetterIndex >= activeCount - 1;

        if (isLastLetter)
        {
            ShowGameCompleteScreen();
            return;
        }

        int justCompletedLetterNumber = GameManager.Instance.CurrentLetterIndex + 1; // 1-based

        bool shouldShowPromo =
            promoVideoController != null &&
            showPromoEveryNLetters > 0 &&
            justCompletedLetterNumber % showPromoEveryNLetters == 0;

        if (shouldShowPromo)
        {
            promoVideoController.Play(); // AdvanceAndShowNext runs via OnVideoFinished
        }
        else
        {
            AdvanceAndShowNext();
        }
    }

    /// <summary>
    /// Shows the "Well Done!" panel after the last letter (Z) is
    /// finished. Stays up until the player taps homeButton - no
    /// auto-timer, they choose when to leave.
    /// </summary>
    private void ShowGameCompleteScreen()
    {
        // Clean up the just-finished puzzle's visuals - otherwise the
        // assembled picture and all its placed pieces just sit there
        // forever, since nothing else ever calls ClearPuzzle() or hides
        // the reveal once there's no next letter to build.
        puzzleController.ClearPuzzle();
        if (illustrationImage != null) illustrationImage.gameObject.SetActive(false);

        if (gameCompletePanel != null)
        {
            gameCompletePanel.SetActive(true);
            gameCompletePanel.transform.SetAsLastSibling(); // guarantee it renders on top, regardless of Hierarchy order
        }

        if (gameCompleteCanvasGroup != null)
        {
            gameCompleteCanvasGroup.alpha = 0f;
            StartCoroutine(FadeCanvasGroup(gameCompleteCanvasGroup, 0f, 1f, gameCompleteFadeInDuration));
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = to;
    }

    /// <summary>
    /// Moves to the next letter. Wired to the Next button - used both as
    /// a manual skip during solving, and as the "continue" action after
    /// a puzzle is completed and the reward is showing.
    /// </summary>
    public void AdvanceAndShowNext()
    {
        int activeCount = letterDatabase.GetLettersForLanguage(LanguageManager.Instance.CurrentLanguage).Count;
        GameManager.Instance.AdvanceLetter(activeCount);
        ShowCurrentLetter();
    }
}