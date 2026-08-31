using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI-based confetti burst - spawns small pieces that fall, spin,
/// and fade out. Built with plain Image objects (not a Particle System),
/// so it renders correctly inside a Screen Space - Overlay Canvas like
/// the rest of this game's UI.
///
/// Wiring (set in Inspector):
///   confettiSprites -> your confetti art assets - mix of colors/shapes
///                      looks best with more than one
///   spawnArea       -> RectTransform confetti spawns across (usually
///                      stretched near the top of the screen)
///   pieceParent     -> Transform pieces get created under (any RectTransform
///                      under the same Canvas works)
///   pieceCount      -> how many pieces per burst
///   fallDuration    -> roughly how long each piece takes to fall + fade
/// </summary>
public class ConfettiEffect : MonoBehaviour
{
    [SerializeField] private List<Sprite> confettiSprites = new List<Sprite>();
    [SerializeField] private RectTransform spawnArea;
    [SerializeField] private Transform pieceParent;
    [SerializeField] private int pieceCount = 24;
    [SerializeField] private float fallDuration = 1.6f;
    [SerializeField] private float fallDistance = 500f;
    [SerializeField] private Vector2 pieceSize = new Vector2(24f, 24f);

    public void Burst()
    {
        if (confettiSprites.Count == 0 || spawnArea == null)
        {
            Debug.LogWarning("ConfettiEffect: no sprites or spawn area assigned.");
            return;
        }

        for (int i = 0; i < pieceCount; i++)
        {
            SpawnPiece();
        }
    }

    private void SpawnPiece()
    {
        GameObject pieceObj = new GameObject("ConfettiPiece", typeof(RectTransform), typeof(Image));
        pieceObj.transform.SetParent(pieceParent != null ? pieceParent : spawnArea, false);

        Image image = pieceObj.GetComponent<Image>();
        image.sprite = confettiSprites[Random.Range(0, confettiSprites.Count)];
        image.raycastTarget = false; // never blocks clicks on the Next button underneath

        RectTransform rt = pieceObj.GetComponent<RectTransform>();
        rt.sizeDelta = pieceSize;

        float halfWidth = spawnArea.rect.width / 2f;
        float startX = Random.Range(-halfWidth, halfWidth);
        float startY = spawnArea.rect.height / 2f;
        rt.anchoredPosition = spawnArea.anchoredPosition + new Vector2(startX, startY);
        rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        StartCoroutine(AnimatePiece(rt, image));
    }

    private IEnumerator AnimatePiece(RectTransform rt, Image image)
    {
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(Random.Range(-100f, 100f), -fallDistance);
        float spinSpeed = Random.Range(-360f, 360f);
        float duration = fallDuration * Random.Range(0.8f, 1.2f);

        Color startColor = image.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            rt.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            // Fade out over the last 30% of the fall.
            if (t > 0.7f)
            {
                float fadeT = (t - 0.7f) / 0.3f;
                image.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, fadeT));
            }

            yield return null;
        }

        Destroy(rt.gameObject);
    }
}