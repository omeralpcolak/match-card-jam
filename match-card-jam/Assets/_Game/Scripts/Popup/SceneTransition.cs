using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent singleton manager handling smooth fade-to-black scene transitions.
/// Survives scene changes via DontDestroyOnLoad and self-initializes if not present in the scene.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [Header("Transition Settings")]
    [Tooltip("Default duration for fading in and fading out.")]
    [SerializeField] private float defaultFadeDuration = 0.25f;

    [Tooltip("Color of the transition overlay screen.")]
    [SerializeField] private Color fadeColor = Color.black;

    [Header("UI References (Optional)")]
    [Tooltip("Target CanvasGroup controlling the overlay alpha.")]
    [SerializeField] private CanvasGroup overlayCanvasGroup;

    [Tooltip("Image component providing the solid color overlay.")]
    [SerializeField] private Image overlayImage;

    private bool isTransitioning;

    public bool IsTransitioning => isTransitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance == null)
        {
            CreateDefaultInstance();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureComponents();
    }

    /// <summary>
    /// Loads a scene by name with a smooth fade-to-black transition.
    /// E.g.: SceneTransition.LoadScene("MainMenu");
    /// </summary>
    public static void LoadScene(string sceneName, float fadeDuration = -1f, Action onComplete = null)
    {
        EnsureInstanceExists();
        Instance.StartTransition(sceneName, -1, fadeDuration, onComplete);
    }

    /// <summary>
    /// Loads a scene by build index with a smooth fade-to-black transition.
    /// </summary>
    public static void LoadScene(int sceneBuildIndex, float fadeDuration = -1f, Action onComplete = null)
    {
        EnsureInstanceExists();
        Instance.StartTransition(null, sceneBuildIndex, fadeDuration, onComplete);
    }

    /// <summary>
    /// Reloads the current active scene with a smooth fade transition.
    /// </summary>
    public static void ReloadCurrentScene(float fadeDuration = -1f, Action onComplete = null)
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        LoadScene(currentIndex, fadeDuration, onComplete);
    }

    private static void EnsureInstanceExists()
    {
        if (Instance == null)
        {
            CreateDefaultInstance();
        }
    }

    private static void CreateDefaultInstance()
    {
        GameObject transitionObj = new GameObject("SceneTransition");
        Instance = transitionObj.AddComponent<SceneTransition>();
        DontDestroyOnLoad(transitionObj);
    }

    private void EnsureComponents()
    {
        // 1. Setup Canvas
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 999; // Render above all popups and UI elements

        // 2. Setup CanvasScaler
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f; // Portrait standard: Match Width

        // 3. Setup GraphicRaycaster
        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        // 4. Setup Overlay Child GameObject
        if (overlayCanvasGroup == null)
        {
            Transform overlayChild = transform.Find("Overlay");
            GameObject overlayGo;

            if (overlayChild == null)
            {
                overlayGo = new GameObject("Overlay");
                overlayGo.transform.SetParent(transform, false);
            }
            else
            {
                overlayGo = overlayChild.gameObject;
            }

            RectTransform rect = overlayGo.GetComponent<RectTransform>();
            if (rect == null) rect = overlayGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            overlayImage = overlayGo.GetComponent<Image>();
            if (overlayImage == null) overlayImage = overlayGo.AddComponent<Image>();
            overlayImage.color = fadeColor;

            overlayCanvasGroup = overlayGo.GetComponent<CanvasGroup>();
            if (overlayCanvasGroup == null) overlayCanvasGroup = overlayGo.AddComponent<CanvasGroup>();
        }

        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.blocksRaycasts = false;
    }

    private void StartTransition(string sceneName, int sceneIndex, float duration, Action onComplete)
    {
        if (isTransitioning) return;

        float fadeDuration = duration > 0f ? duration : defaultFadeDuration;
        StartCoroutine(TransitionRoutine(sceneName, sceneIndex, fadeDuration, onComplete));
    }

    private IEnumerator TransitionRoutine(string sceneName, int sceneIndex, float duration, Action onComplete)
    {
        isTransitioning = true;
        overlayCanvasGroup.blocksRaycasts = true;

        // 1. Fade to Black
        overlayCanvasGroup.DOKill();
        overlayCanvasGroup.DOFade(1f, duration).SetUpdate(true);
        yield return new WaitForSecondsRealtime(duration);

        // 2. Load Scene Asynchronously
        AsyncOperation asyncOp = !string.IsNullOrEmpty(sceneName)
            ? SceneManager.LoadSceneAsync(sceneName)
            : SceneManager.LoadSceneAsync(sceneIndex);

        if (asyncOp != null)
        {
            while (!asyncOp.isDone)
            {
                yield return null;
            }
        }

        // Wait a frame for newly spawned objects to initialize
        yield return null;

        // 3. Fade Back In
        overlayCanvasGroup.DOKill();
        overlayCanvasGroup.DOFade(0f, duration).SetUpdate(true);
        yield return new WaitForSecondsRealtime(duration);

        overlayCanvasGroup.blocksRaycasts = false;
        isTransitioning = false;

        onComplete?.Invoke();
    }
}
