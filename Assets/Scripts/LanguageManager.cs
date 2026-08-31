using UnityEngine;

/// <summary>
/// Tracks the active language (English/Spanish), persists the choice,
/// and notifies any listeners (UI text, audio clip selection, etc.)
/// when it changes. Actual string tables / localized text swapping
/// will hook into OnLanguageChanged later - this is just the source
/// of truth for "which language are we in."
/// </summary>
public class LanguageManager : MonoBehaviour
{
    public static LanguageManager Instance { get; private set; }

    public enum Language { English, Spanish }

    private const string PrefsKey = "SelectedLanguage";

    public Language CurrentLanguage { get; private set; } = Language.English;

    public event System.Action<Language> OnLanguageChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSavedLanguage();
    }

    public void SetLanguage(Language newLanguage)
    {
        if (CurrentLanguage == newLanguage) return;

        CurrentLanguage = newLanguage;
        PlayerPrefs.SetInt(PrefsKey, (int)newLanguage);
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke(CurrentLanguage);
    }

    public void ToggleLanguage()
    {
        SetLanguage(CurrentLanguage == Language.English ? Language.Spanish : Language.English);
    }

    private void LoadSavedLanguage()
    {
        if (PlayerPrefs.HasKey(PrefsKey))
        {
            CurrentLanguage = (Language)PlayerPrefs.GetInt(PrefsKey);
        }
        // else: default stays English, no need to write a pref yet.
    }
}