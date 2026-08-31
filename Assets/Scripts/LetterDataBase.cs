using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds the full set of LetterData assets (26 letters) and provides
/// lookup by letter or index. Build this by dragging all 26 LetterData
/// assets into the Letters list in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "LetterDatabase", menuName = "PuzzleGame/Letter Database")]
public class LetterDatabase : ScriptableObject
{
    [SerializeField] private List<LetterData> letters = new List<LetterData>();

    public int Count => letters.Count;

    public LetterData GetByIndex(int index)
    {
        if (index < 0 || index >= letters.Count)
        {
            Debug.LogWarning($"LetterDatabase: index {index} out of range (0-{letters.Count - 1}).");
            return null;
        }

        return letters[index];
    }

    public LetterData GetByLetter(char letter)
    {
        char target = char.ToUpperInvariant(letter);

        foreach (LetterData data in letters)
        {
            if (data != null && char.ToUpperInvariant(data.letter) == target)
            {
                return data;
            }
        }

        Debug.LogWarning($"LetterDatabase: no LetterData found for letter '{letter}'.");
        return null;
    }

    public IReadOnlyList<LetterData> GetAll()
    {
        return letters;
    }

    /// <summary>
    /// Returns the ordered list of letters for the given language -
    /// letters flagged spanishOnly (like Ñ) are skipped for English,
    /// included for Spanish. The list order preserves whatever order
    /// "letters" is in, so make sure Ñ is positioned right after N in
    /// the Inspector list for correct alphabetical order in Spanish.
    /// </summary>
    public List<LetterData> GetLettersForLanguage(LanguageManager.Language language)
    {
        List<LetterData> result = new List<LetterData>();

        foreach (LetterData data in letters)
        {
            if (data == null) continue;
            if (data.spanishOnly && language == LanguageManager.Language.English) continue;

            result.Add(data);
        }

        return result;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only sanity check: warns about missing entries or duplicate
    /// letters so mistakes get caught while building the database, not at
    /// runtime. Right-click the asset and use this via a custom inspector
    /// later if you want a button - for now it just runs on validate.
    /// </summary>
    private void OnValidate()
    {
        var seen = new HashSet<char>();

        foreach (LetterData data in letters)
        {
            if (data == null)
            {
                Debug.LogWarning($"LetterDatabase '{name}': list contains an empty (null) entry.");
                continue;
            }

            char upper = char.ToUpperInvariant(data.letter);

            if (!seen.Add(upper))
            {
                Debug.LogWarning($"LetterDatabase '{name}': duplicate entry for letter '{upper}'.");
            }
        }

        if (letters.Count > 0 && letters.Count != 26 && letters.Count != 27)
        {
            Debug.LogWarning($"LetterDatabase '{name}': has {letters.Count} entries, expected 26 (or 27 if Ñ is included).");
        }
    }
#endif
}