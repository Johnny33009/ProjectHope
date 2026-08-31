using UnityEngine;

/// <summary>
/// Safety net so the Game scene works correctly whether you reach it
/// normally (Play -> Game, managers already exist from Main Menu) OR by
/// pressing Play directly on this scene for testing (managers don't exist
/// yet otherwise, causing null reference errors on GameManager.Instance,
/// SceneLoader.Instance, etc.)
///
/// Attach this to an empty GameObject in the Game scene, and wire
/// managersPrefab to a prefab containing your GameManager, LanguageManager,
/// and SceneLoader (whatever GameObject(s) currently hold those scripts in
/// Main Menu - drag that into your Project window to turn it into a
/// prefab, then reference it here).
///
/// This only creates the managers if they're missing - if you reached this
/// scene normally, GameManager.Instance already exists and this does
/// nothing.
/// </summary>
public class EnsureManagers : MonoBehaviour
{
    [SerializeField] private GameObject managersPrefab;

    private void Awake()
    {
        if (GameManager.Instance == null && managersPrefab != null)
        {
            Instantiate(managersPrefab);
        }
    }
}