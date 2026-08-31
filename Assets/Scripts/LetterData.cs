using UnityEngine;

/// <summary>
/// Data for a single letter of the alphabet. One asset per letter (26 total).
///
/// English word/illustration are the original required fields ("word" and
/// "illustration" below). Spanish word/illustration are separate fields -
/// if left blank/unset, they fall back to the English version automatically,
/// so partially-filled-in letters still work correctly instead of showing
/// blank/broken content while you're still adding Spanish data.
/// </summary>
[CreateAssetMenu(fileName = "LetterData_", menuName = "PuzzleGame/Letter Data")]
public class LetterData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("The letter this asset represents, e.g. 'A'.")]
    public char letter;

    [Tooltip("Check this ONLY for letters that exist in the Spanish alphabet but not English (i.e. \u00d1). English mode skips this letter entirely; Spanish mode includes it.")]
    public bool spanishOnly = false;

    [Header("English")]
    [Tooltip("The English word for this letter, e.g. 'Apple'.")]
    public string word;

    [Tooltip("The English illustration for this letter/word.")]
    public Sprite illustration;

    [Header("Spanish (optional - falls back to English if left blank)")]
    [Tooltip("The Spanish word for this letter, e.g. 'Avi\u00f3n'. Leave blank to fall back to the English word.")]
    public string wordSpanish;

    [Tooltip("The Spanish illustration for this letter/word. Leave unset to fall back to the English illustration.")]
    public Sprite illustrationSpanish;

    [Header("Audio Narration")]
    public AudioClip audioClipEnglish;
    public AudioClip audioClipSpanish;

    [Header("Puzzle Settings")]
    [Tooltip("How many jigsaw pieces this letter's puzzle is cut into.")]
    [Min(2)]
    public int jigsawPieceCount = 4;

    /// <summary>
    /// Returns the correct word for the given language, falling back to
    /// English if the Spanish word hasn't been filled in yet.
    /// </summary>
    public string GetWord(LanguageManager.Language language)
    {
        if (language == LanguageManager.Language.Spanish && !string.IsNullOrEmpty(wordSpanish))
        {
            return wordSpanish;
        }

        return word;
    }

    /// <summary>
    /// Returns the correct illustration for the given language, falling
    /// back to English if the Spanish illustration hasn't been assigned yet.
    /// </summary>
    public Sprite GetIllustration(LanguageManager.Language language)
    {
        if (language == LanguageManager.Language.Spanish && illustrationSpanish != null)
        {
            return illustrationSpanish;
        }

        return illustration;
    }

    /// <summary>
    /// Returns the correct narration clip for the given language.
    /// </summary>
    public AudioClip GetAudioClip(LanguageManager.Language language)
    {
        return language == LanguageManager.Language.English ? audioClipEnglish : audioClipSpanish;
    }
}