using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the main menu screen:
///   - Play/Resume + New Game buttons (grouped under mainPanel)
///   - If the player has existing progress (past letter A), the primary
///     button reads "Resume" and continues from their saved letter, and
///     a "New Game" button appears to let them start over from A.
///   - If there's no progress yet, only "Play" shows (New Game is hidden -
///     there's nothing to reset from a fresh start).
///   - Language (EN/SP) buttons are always visible on the main screen -
///     no Settings popup. The currently active language button is shown
///     at full brightness; the inactive one is dimmed, so it's clear
///     which language is selected at a glance.
///
/// Wiring (set in Inspector):
///   mainPanel                                -> GameObject containing Play/Resume,
///                                                New Game, and language buttons
///   playButton                                -> Button ref
///   playButtonLabel                           -> TMP_Text on playButton, so its
///                                                label can switch between "Play"
///                                                and "Resume"
///   newGameButton                             -> Button ref (only shown when
///                                                there's existing progress)
///   languageEnglishButton, languageSpanishButton -> Button refs, always visible
///   nextSceneName                             -> scene Play/Resume/New Game should load
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Main Menu")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private Button playButton;
    [SerializeField] private TMP_Text playButtonLabel;
    [SerializeField] private Button newGameButton;
    [SerializeField] private string nextSceneName = "LetterSelect";

    [Header("Language (always visible)")]
    [SerializeField] private Button languageEnglishButton;
    [SerializeField] private Button languageSpanishButton;
    [SerializeField] private float inactiveLanguageAlpha = 0.55f;

    private void Start()
    {
        playButton.onClick.AddListener(OnPlayClicked);
        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);

        languageEnglishButton.onClick.AddListener(() => SetLanguage(LanguageManager.Language.English));
        languageSpanishButton.onClick.AddListener(() => SetLanguage(LanguageManager.Language.Spanish));

        RefreshResumeState();
        RefreshLanguageSelectionVisual();
    }

    /// <summary>
    /// Shows "Resume" + New Game if the player has existing progress,
    /// or just "Play" (New Game hidden) on a fresh start.
    /// </summary>
    private void RefreshResumeState()
    {
        bool hasProgress = GameManager.Instance.CurrentLetterIndex > 0;

        if (playButtonLabel != null)
        {
            playButtonLabel.text = hasProgress ? "Resume" : "Play";
        }

        if (newGameButton != null)
        {
            newGameButton.gameObject.SetActive(hasProgress);
        }
    }

    private void SetLanguage(LanguageManager.Language language)
    {
        LanguageManager.Instance.SetLanguage(language);
        RefreshLanguageSelectionVisual();
    }

    /// <summary>
    /// Dims whichever language button ISN'T currently selected, so the
    /// active language is obvious at a glance without needing a popup.
    /// </summary>
    private void RefreshLanguageSelectionVisual()
    {
        bool isEnglish = LanguageManager.Instance.CurrentLanguage == LanguageManager.Language.English;
        SetButtonDimmed(languageEnglishButton, !isEnglish);
        SetButtonDimmed(languageSpanishButton, isEnglish);
    }

    private void SetButtonDimmed(Button button, bool dimmed)
    {
        if (button == null) return;

        Image image = button.GetComponent<Image>();
        if (image == null) return;

        Color color = image.color;
        color.a = dimmed ? inactiveLanguageAlpha : 1f;
        image.color = color;
    }

    private void OnPlayClicked()
    {
        // Continues from whatever GameManager.CurrentLetterIndex already
        // is (loaded from PlayerPrefs on startup) - this is "Resume"
        // behavior when progress exists, and just starts at A otherwise.
        GameManager.Instance.SetState(GameManager.GameState.LetterSelect);
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
    }

    private void OnNewGameClicked()
    {
        GameManager.Instance.ResetProgress();
        GameManager.Instance.SetState(GameManager.GameState.LetterSelect);
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
    }
}