using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles async scene loads so the game doesn't freeze on transitions.
/// A loading screen isn't built yet, so OnLoadProgress is exposed as an
/// event you can hook a progress bar into once that UI exists - for now
/// it's safe to leave unused.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    public event System.Action<float> OnLoadProgress; // 0-1
    public event System.Action<string> OnSceneLoaded;  // scene name

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = true;

        while (!op.isDone)
        {
            OnLoadProgress?.Invoke(op.progress);
            yield return null;
        }

        OnSceneLoaded?.Invoke(sceneName);
    }
}