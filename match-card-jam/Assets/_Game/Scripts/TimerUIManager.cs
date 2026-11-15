using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Manages the level countdown bar (TimerBar) and timer text (TimeTxt).
/// Hides the bar (SetActive(false)) if level has no timer, formats as 00:30 if timer enabled.
/// Transitions to warning color and plays a pulse heartbeat effect when time falls below threshold.
/// </summary>
public class TimerUIManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Main root object for timer bar (if unassigned, uses this GameObject).")]
    [SerializeField] private GameObject timerBarRoot;

    [Tooltip("TextMeshProUGUI component displaying remaining time.")]
    [SerializeField] private TextMeshProUGUI timeTxt;

    [Header("Format Settings")]
    [Tooltip("Timer display format (e.g. {0:00}:{1:00} -> 00:30).")]
    [SerializeField] private string timeFormat = "{0:00}:{1:00}";

    [Header("Warning Effect (Under Threshold)")]
    [Tooltip("Remaining seconds threshold to trigger warning effect.")]
    [SerializeField] private float warningThreshold = 10f;

    [Tooltip("Text color when warning is active (Red).")]
    [SerializeField] private Color warningColor = new Color(0.96f, 0.26f, 0.21f, 1f);

    [Tooltip("Whether to trigger a pulse / heartbeat effect on each second tick.")]
    [SerializeField] private bool usePulseEffect = true;

    [Tooltip("Pulse scale intensity.")]
    [SerializeField] private float pulseScale = 0.25f;

    [Tooltip("Pulse duration.")]
    [SerializeField] private float pulseDuration = 0.3f;

    [Header("Frozen Effect")]
    [Tooltip("Text color when the timer is frozen by FreezeTime power-up.")]
    [SerializeField] private Color frozenColor = new Color(0.35f, 0.85f, 1f, 1f);

    private int lastDisplayedSeconds = -1;
    private Color originalColor;
    private Vector3 originalScale;
    private Tween pulseTween;
    private Tween colorTween;
    private bool isWarningActive;
    private bool isFrozen;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        ResetVisuals();
    }

    /// <summary>
    /// Called by LevelManager at level start.
    /// Hides bar if timer disabled, otherwise enables bar and sets initial value.
    /// </summary>
    public void Initialize(bool useTimer, float initialTime)
    {
        CacheComponents();
        ResetVisuals();

        if (!useTimer)
        {
            Hide();
            return;
        }

        Show();
        lastDisplayedSeconds = -1;
        UpdateTimer(initialTime);
    }

    /// <summary>
    /// Updates timer text as mm:ss (00:30).
    /// Turns red and pulses like a heartbeat every second when under threshold.
    /// </summary>
    public void UpdateTimer(float remainingSeconds)
    {
        if (timeTxt == null) return;

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        if (totalSeconds == lastDisplayedSeconds) return;

        lastDisplayedSeconds = totalSeconds;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timeTxt.text = string.Format(timeFormat, minutes, seconds);

        // Warning effect check when below threshold
        if (totalSeconds <= warningThreshold && totalSeconds > 0)
        {
            ApplyWarningTick();
        }
        else if (isWarningActive && totalSeconds > warningThreshold)
        {
            ResetVisuals();
        }
    }

    private void ApplyWarningTick()
    {
        // Switch color to warning color
        if (!isWarningActive)
        {
            isWarningActive = true;
            colorTween?.Kill();
            colorTween = timeTxt.DOColor(warningColor, 0.2f);
        }

        // Heartbeat / pulse punch scale effect every second
        if (usePulseEffect)
        {
            pulseTween?.Kill();
            timeTxt.transform.localScale = originalScale;
            pulseTween = timeTxt.transform.DOPunchScale(originalScale * pulseScale, pulseDuration, 1, 0.5f);
        }
    }

    public void Show()
    {
        if (timerBarRoot != null)
        {
            timerBarRoot.SetActive(true);
        }
    }

    public void Hide()
    {
        if (timerBarRoot != null)
        {
            timerBarRoot.SetActive(false);
        }
    }

    private void CacheComponents()
    {
        if (timerBarRoot == null)
        {
            timerBarRoot = gameObject;
        }

        if (timeTxt == null)
        {
            timeTxt = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (timeTxt != null)
        {
            if (originalScale == Vector3.zero)
            {
                originalScale = timeTxt.transform.localScale;
            }

            if (originalColor == default)
            {
                originalColor = timeTxt.color;
            }
        }
    }

    /// <summary>
    /// Updates visual state when timer is frozen/unfrozen by FreezeTime power-up.
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        if (timeTxt == null) return;

        colorTween?.Kill();

        if (isFrozen)
        {
            pulseTween?.Kill();
            if (originalScale != Vector3.zero)
            {
                timeTxt.transform.localScale = originalScale;
            }
            colorTween = timeTxt.DOColor(frozenColor, 0.2f);
        }
        else
        {
            Color targetColor = isWarningActive ? warningColor : originalColor;
            if (targetColor != default)
            {
                colorTween = timeTxt.DOColor(targetColor, 0.2f);
            }
        }
    }

    public void ResetVisuals()
    {
        pulseTween?.Kill();
        colorTween?.Kill();

        isWarningActive = false;
        isFrozen = false;

        if (timeTxt != null)
        {
            if (originalScale != Vector3.zero)
            {
                timeTxt.transform.localScale = originalScale;
            }

            if (originalColor != default)
            {
                timeTxt.color = originalColor;
            }
        }
    }
}
