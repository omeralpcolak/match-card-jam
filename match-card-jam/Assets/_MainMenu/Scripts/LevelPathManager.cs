using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Controls the vertical level road / path display above the Play button in the Main Menu.
/// Displays the current active level (with a gentle pulse animation) and the upcoming two levels.
/// </summary>
public class LevelPathManager : MonoBehaviour
{
    [Header("Current Level Node (Active)")]
    [Tooltip("Root RectTransform of the current level badge.")]
    [SerializeField] private RectTransform currentNodeRect;

    [Tooltip("Text displaying the current level number.")]
    [SerializeField] private TextMeshProUGUI currentLevelText;

    [Tooltip("Optional text displaying difficulty (e.g., Hard, Normal).")]
    [SerializeField] private TextMeshProUGUI currentDifficultyText;

    [Tooltip("Optional glow or aura effect around the current level node.")]
    [SerializeField] private GameObject currentGlowEffect;

    [Header("Upcoming Level Node (+1)")]
    [Tooltip("Root RectTransform of the upcoming level badge.")]
    [SerializeField] private RectTransform upcomingNodeRect;

    [Tooltip("Text displaying the upcoming level number.")]
    [SerializeField] private TextMeshProUGUI upcomingLevelText;

    [Header("Future Level Node (+2)")]
    [Tooltip("Root RectTransform of the future level badge.")]
    [SerializeField] private RectTransform futureNodeRect;

    [Tooltip("Text displaying the future level number.")]
    [SerializeField] private TextMeshProUGUI futureLevelText;

    [Header("Display Settings")]
    [Tooltip("Prefix for the level label (e.g. 'Level ' resulting in 'Level 1').")]
    [SerializeField] private string levelPrefix = "Level ";

    [Header("Animation Settings")]
    [Tooltip("Whether the current level node gently pulses/breathes.")]
    [SerializeField] private bool usePulseAnimation = true;

    [Tooltip("Pulse scale target.")]
    [SerializeField] private float pulseScale = 1.05f;

    [Tooltip("Duration of each pulse half-cycle.")]
    [SerializeField] private float pulseDuration = 0.8f;

    private Tween pulseTween;

    private void Awake()
    {
        AutoResolveReferences();
    }

    /// <summary>
    /// Updates the 3 level badges with the player's progression numbers and initiates the idle pulse.
    /// </summary>
    public void UpdateLevelPath(int currentLevel)
    {
        // 1. Current Level
        if (currentLevelText != null)
        {
            currentLevelText.text = $"{levelPrefix}{currentLevel}";
        }

        // Optional difficulty label logic (e.g. every 5th level is Hard)
        if (currentDifficultyText != null)
        {
            if (currentLevel % 10 == 0)
            {
                currentDifficultyText.text = "VERY HARD";
                currentDifficultyText.gameObject.SetActive(true);
            }
            else if (currentLevel % 5 == 0)
            {
                currentDifficultyText.text = "HARD";
                currentDifficultyText.gameObject.SetActive(true);
            }
            else
            {
                currentDifficultyText.gameObject.SetActive(false);
            }
        }

        if (currentGlowEffect != null)
        {
            currentGlowEffect.SetActive(true);
        }

        // 2. Upcoming Levels (+1 and +2)
        if (upcomingLevelText != null)
        {
            upcomingLevelText.text = $"{levelPrefix}{currentLevel + 1}";
        }

        if (futureLevelText != null)
        {
            futureLevelText.text = $"{levelPrefix}{currentLevel + 2}";
        }

        // 3. Pulse Animation on Current Node
        StartPulseAnimation();
    }

    private void StartPulseAnimation()
    {
        if (!usePulseAnimation || currentNodeRect == null) return;

        pulseTween?.Kill();
        currentNodeRect.localScale = Vector3.one;

        pulseTween = currentNodeRect
            .DOScale(Vector3.one * pulseScale, pulseDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void AutoResolveReferences()
    {
        if (currentNodeRect == null)
        {
            Transform t = transform.Find("LevelNode_Current") ?? transform.Find("CurrentNode");
            if (t != null)
            {
                currentNodeRect = t as RectTransform;
                if (currentLevelText == null) currentLevelText = t.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (upcomingNodeRect == null)
        {
            Transform t = transform.Find("LevelNode_Upcoming") ?? transform.Find("UpcomingNode");
            if (t != null)
            {
                upcomingNodeRect = t as RectTransform;
                if (upcomingLevelText == null) upcomingLevelText = t.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (futureNodeRect == null)
        {
            Transform t = transform.Find("LevelNode_Future") ?? transform.Find("FutureNode");
            if (t != null)
            {
                futureNodeRect = t as RectTransform;
                if (futureLevelText == null) futureLevelText = t.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }
    }

    private void OnDestroy()
    {
        pulseTween?.Kill();
    }
}
