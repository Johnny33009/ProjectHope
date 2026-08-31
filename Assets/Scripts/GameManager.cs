using UnityEngine;

/// <summary>
/// Top-level game state singleton. Persists across scenes.
/// Other systems (UI, puzzle logic, etc.) read/react to CurrentState
/// rather than talking to each other directly.
///
/// Also saves/loads which letter the player was on, so closing and
/// reopening the game resumes where they left off instead of always
/// restarting at A.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        MainMenu,
        Settings,
        LetterSelect,
        Puzzle,
        Paused
    }

    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    // Tracks progress through the alphabet - 0 = A, 1 = B, etc.
    // The Game scene reads this to know which letter to show.
    public int CurrentLetterIndex { get; private set; } = 0;

    private const string LetterProgressKey = "CurrentLetterIndex";

    // Fired whenever the state changes, so UI/other systems can react
    // without GameManager needing to know about them directly.
    public event System.Action<GameState> OnStateChanged;

    private void Awake()
    {
        // Standard persistent singleton pattern.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadProgress();
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        OnStateChanged?.Invoke(CurrentState);
    }

    /// <summary>
    /// Moves to the next letter in sequence. Clamps at the last letter -
    /// call with the database's Count so it knows where the alphabet ends.
    /// </summary>
    public void AdvanceLetter(int totalLetterCount)
    {
        CurrentLetterIndex = Mathf.Min(CurrentLetterIndex + 1, totalLetterCount - 1);
        SaveProgress();
    }

    public void ResetProgress()
    {
        CurrentLetterIndex = 0;
        SaveProgress();
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetInt(LetterProgressKey, CurrentLetterIndex);
        PlayerPrefs.Save();
    }

    private void LoadProgress()
    {
        if (PlayerPrefs.HasKey(LetterProgressKey))
        {
            CurrentLetterIndex = PlayerPrefs.GetInt(LetterProgressKey);
        }
        // else: default stays 0 (letter A), no need to write a pref yet.
    }
}