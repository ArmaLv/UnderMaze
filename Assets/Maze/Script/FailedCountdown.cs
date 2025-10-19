using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FailedCountdown : MonoBehaviour
{
    [Header("Countdown Time")]
    public int hours = 0;
    public int minutes = 0;
    public int seconds = 10;

    [Header("End Effects")]
    public AudioClip finalSound;        // Sound to play at 0
    public Sprite fadeInSprite;         // Sprite for the fullscreen image
    public float fadeInDuration = 2f;   // Duration for image fade-in
    public float disableSoundAt = 0.1f; // Time left when all other sounds are stopped
    public float quitDelay = 3f;        // Seconds to wait before quitting after end

    private AudioSource audioSource;
    private float countdownTime;
    private Image fadeInImage;

    void Start()
    {
        countdownTime = hours * 3600 + minutes * 60 + seconds;

        if (finalSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = finalSound;
        }

        if (fadeInSprite != null)
        {
            CreateFadeInImage();
        }

        StartCoroutine(CountdownCoroutine());
    }

    void CreateFadeInImage()
    {
        // Find or create Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Create fullscreen Image
        GameObject imageGO = new GameObject("FadeInImage");
        imageGO.transform.SetParent(canvas.transform, false);
        fadeInImage = imageGO.AddComponent<Image>();
        fadeInImage.sprite = fadeInSprite;
        fadeInImage.color = new Color(1, 1, 1, 0);

        // Stretch to fullscreen
        RectTransform rt = fadeInImage.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    IEnumerator CountdownCoroutine()
    {
        while (countdownTime > 0)
        {
            countdownTime -= Time.deltaTime;

            // Disable all other sounds when reaching disableSoundAt
            if (countdownTime <= disableSoundAt)
            {
                StopAllOtherAudio();
            }

            yield return null;
        }

        // Play only final audio
        if (audioSource != null)
        {
            audioSource.Play();
        }

        // Fade in fullscreen image
        if (fadeInImage != null)
        {
            yield return StartCoroutine(FadeInImage());
        }

        // Wait before quitting
        yield return new WaitForSeconds(quitDelay);

        Application.Quit();
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #endif
    }

    void StopAllOtherAudio()
    {
        AudioSource[] sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (AudioSource src in sources)
        {
            if (src != audioSource)
            {
                src.Stop();
            }
        }
    }

    IEnumerator FadeInImage()
    {
        float elapsed = 0f;
        Color c = fadeInImage.color;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Clamp01(elapsed / fadeInDuration);
            fadeInImage.color = c;
            yield return null;
        }
    }
}
