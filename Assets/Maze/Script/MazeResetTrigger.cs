using System.Collections;
using UnityEngine;
using UnityEngine.UI;          // for Image
using UnityEngine.SceneManagement; // only if you ever need scene reloads

[RequireComponent(typeof(Collider))]
public class MazeResetTrigger : MonoBehaviour
{
    [Header("References")]
    public MazeGenerator mazeGenerator;
    public string playerTag = "Player";
    public string spawnTag = "Start";

    [Header("Reset Settings")]
    [Tooltip("Delay before regenerating maze (seconds)")]
    public float resetDelay = 1.0f;

    [Tooltip("Play a reset effect or animation before regeneration")]
    public bool useFadeEffect = true;

    [Header("Fade Settings")]
    [Range(0.1f, 3f)]
    public float fadeDuration = 0.75f;
    public Color fadeColor = Color.black;

    private bool isResetting = false;

    // UI elements created at runtime
    private Canvas fadeCanvas;
    private Image fadeImage;

    private void Awake()
    {
        // Make sure this GameObject persists through maze regeneration
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        
        // Auto-find MazeGenerator if not assigned
        if (mazeGenerator == null)
        {
            mazeGenerator = FindAnyObjectByType<MazeGenerator>();
            if (mazeGenerator == null)
            {
                Debug.LogError("MazeResetTrigger: Could not find MazeGenerator in scene!");
            }
            else
            {
                Debug.Log("MazeResetTrigger: Automatically found MazeGenerator on " + mazeGenerator.gameObject.name);
            }
        }

        if (useFadeEffect) CreateFadeCanvas();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isResetting) return;
        if (other.CompareTag(playerTag))
        {
            StartCoroutine(ResetMazeRoutine(other.gameObject));
        }
    }

    private IEnumerator ResetMazeRoutine(GameObject player)
    {
        isResetting = true;

        // 1. Fade-out
        if (useFadeEffect && fadeImage != null)
            yield return StartCoroutine(Fade(1f));

        // 2. Small extra delay (optional)
        yield return new WaitForSeconds(resetDelay);

        // 3. Move player
        GameObject spawn = GameObject.FindGameObjectWithTag(spawnTag);
        if (spawn != null)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            Rigidbody rb = player.GetComponent<Rigidbody>();

            if (controller) controller.enabled = false;
            if (rb) rb.isKinematic = true;

            player.transform.position = spawn.transform.position;
            player.transform.rotation = spawn.transform.rotation;

            if (controller) controller.enabled = true;
            if (rb) rb.isKinematic = false;
        }
        else
        {
            Debug.LogWarning("No object tagged 'Start' found to respawn player!");
        }

        // 4. Regenerate maze
        if (mazeGenerator)
            mazeGenerator.GenerateMaze();
        else
            Debug.LogError("MazeGenerator not assigned to MazeResetTrigger!");

        // 5. Fade-in
        if (useFadeEffect && fadeImage != null)
            yield return StartCoroutine(Fade(0f));

        isResetting = false;
    }

    #region Fade helpers
    private void CreateFadeCanvas()
    {
        // Build once at runtime
        GameObject canvasGO = new GameObject("FadeCanvas");
        DontDestroyOnLoad(canvasGO); // Persist across scene changes and maze regeneration
        
        fadeCanvas = canvasGO.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 100; // on top of everything

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject imageGO = new GameObject("FadeImage");
        RectTransform rt = imageGO.AddComponent<RectTransform>();
        rt.SetParent(canvasGO.transform, false);
        rt.anchorMin = Vector2.zero;          // bottom-left
        rt.anchorMax = Vector2.one;           // top-right
        rt.sizeDelta = Vector2.zero;          // zero offset → exact screen size
        fadeImage = imageGO.AddComponent<Image>();
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        fadeImage.raycastTarget = true; // block input while fading
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("FadeImage is null, skipping fade effect");
            yield break;
        }

        float startAlpha = fadeImage.color.a;
        float t = 0f;

        while (t < 1f)
        {
            if (fadeImage == null) yield break; // Safety check during fade
            
            t += Time.deltaTime / fadeDuration;
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, newAlpha);
            yield return null;
        }
    }

    private void OnDestroy()
    {
        // Clean up fade canvas when this script is destroyed
        if (fadeCanvas != null)
        {
            Destroy(fadeCanvas.gameObject);
        }
    }
    #endregion
}