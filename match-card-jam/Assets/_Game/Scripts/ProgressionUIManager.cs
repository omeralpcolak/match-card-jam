using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visualizes the board clearing progress via a UI Image (Fill Amount),
/// including a pooled fly-to-bar orb effect, arrival punch/VFX, and arrival SFX.
/// Controlled directly by LevelManager (zero static event coupling).
/// </summary>
public class ProgressionUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("UI Image component to fill (Image Type must be Filled).")]
    [SerializeField] private Image fillImage;

    [Tooltip("TextMeshProUGUI component displaying the level number.")]
    [SerializeField] private TextMeshProUGUI levelNoText;

    [Header("Text Format")]
    [Tooltip("Prefix for the level text (e.g. 'Level ' -> 'Level 1').")]
    [SerializeField] private string levelPrefix = "Level ";

    [Header("Animation Settings")]
    [Tooltip("Duration for the bar to animate to the new fill value.")]
    [SerializeField] private float fillDuration = 0.25f;

    [Tooltip("Easing curve for the fill animation.")]
    [SerializeField] private Ease fillEase = Ease.OutQuad;

    [Header("Juice / Punch Effect")]
    [Tooltip("Whether the bar punches/bounces slightly when a match arrives.")]
    [SerializeField] private bool usePunchEffect = true;

    [Tooltip("Punch scale intensity.")]
    [SerializeField] private float punchStrength = 0.08f;

    [Tooltip("Punch duration.")]
    [SerializeField] private float punchDuration = 0.2f;

    [Header("Fly-to-Bar Effect")]
    [Tooltip("Optional UI RectTransform prefab (e.g. star/orb Image) that flies from match position to the bar.")]
    [SerializeField] private RectTransform _flyOrbPrefab;

    [Tooltip("Target RectTransform on the bar where flying orbs land. Defaults to FillImage.")]
    [SerializeField] private RectTransform _flyTargetPoint;

    [Tooltip("Optional ParticleSystem played on the bar when an orb arrives.")]
    [SerializeField] private ParticleSystem _barHitVfx;

    [Tooltip("Flight duration in seconds from dock match position to progression bar.")]
    [SerializeField] private float _flyDuration = 0.42f;

    [Tooltip("Easing curve for the orb flight toward the progression bar.")]
    [SerializeField] private Ease _flyEase = Ease.InBack;

    [Tooltip("Curved arc offset in UI units for the Bezier flight path.")]
    [SerializeField] private float _flyArcOffset = 70f;

    [Tooltip("Sound effect played through SoundManager when the orb hits the progression bar.")]
    [SerializeField] private SoundType _arrivalSound = SoundType.ProgressBarHit;

    private Tween fillTween;
    private Tween punchTween;
    private Vector3 initialScale;
    private bool isInitialized;

    private readonly Queue<RectTransform> _orbPool = new();
    private readonly List<RectTransform> _activeOrbs = new();
    private readonly List<Tween> _activeFlyTweens = new();
    private Canvas _parentCanvas;
    private RectTransform _flySpaceRect;

    public bool IsInitialized => isInitialized;
    public TextMeshProUGUI LevelNoText => levelNoText;

    private void Awake()
    {
        CacheComponents();
        UpdateLevelText();
    }

    private void OnDisable()
    {
        KillTweens();
    }

    /// <summary>
    /// Called directly by LevelManager at level start.
    /// Immediately sets up initial progress state and level number.
    /// </summary>
    public void Initialize(int remainingItems = 0, int totalItems = 0, int levelNumber = -1)
    {
        CacheComponents();

        if (levelNumber > 0)
        {
            SetLevelNumber(levelNumber);
        }
        else
        {
            UpdateLevelText();
        }

        if (totalItems > 0)
        {
            UpdateProgressImmediate(remainingItems, totalItems);
        }
        else if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
        }

        isInitialized = true;
    }

    /// <summary>
    /// Reads current level from GameData and updates text.
    /// </summary>
    public void UpdateLevelText()
    {
        SetLevelNumber(GameData.CurrentLevel);
    }

    /// <summary>
    /// Sets the level number directly to the specified value.
    /// </summary>
    public void SetLevelNumber(int levelNumber)
    {
        if (levelNoText != null)
        {
            levelNoText.text = $"{levelPrefix}{levelNumber}";
        }
    }

    private void CacheComponents()
    {
        if (initialScale == Vector3.zero)
        {
            initialScale = transform.localScale;
        }

        if (fillImage == null)
        {
            fillImage = GetComponentInChildren<Image>();
        }

        if (levelNoText == null)
        {
            levelNoText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (_parentCanvas == null)
        {
            _parentCanvas = GetComponentInParent<Canvas>();
        }

        if (_flySpaceRect == null && _parentCanvas != null)
        {
            _flySpaceRect = _parentCanvas.transform as RectTransform;
        }
    }

    /// <summary>
    /// Spawns a pooled UI orb at the match world position, flies it along a curved arc to the bar,
    /// and triggers bar fill, punch, VFX, and arrival SFX when it lands.
    /// </summary>
    public void PlayMatchFlyAndUpdateProgress(Vector3 worldMatchPos, int remaining, int total)
    {
        CacheComponents();

        Camera mainCam = Camera.main;
        if (_flyOrbPrefab == null || mainCam == null)
        {
            TriggerArrivalFeedback(remaining, total);
            return;
        }

        RectTransform targetRect = GetFlyTargetRect();
        if (targetRect == null)
        {
            TriggerArrivalFeedback(remaining, total);
            return;
        }

        Camera uiCam = (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _parentCanvas.worldCamera
            : null;

        Vector2 startScreenPos = mainCam.WorldToScreenPoint(worldMatchPos);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, startScreenPos, uiCam, out Vector2 startLocalPos))
        {
            TriggerArrivalFeedback(remaining, total);
            return;
        }

        RectTransform orb = GetOrbFromPool(targetRect);
        Vector3 baseScale = _flyOrbPrefab.localScale;
        Vector3 startPos = new Vector3(startLocalPos.x, startLocalPos.y, 0f);
        Vector3 endPos = Vector3.zero;
        orb.localPosition = startPos;
        orb.localScale = baseScale;
        orb.gameObject.SetActive(true);

        // Quadratic Bezier control point in targetRect's local space toward (0,0,0)
        float sideOffset = Random.Range(-_flyArcOffset, _flyArcOffset);
        Vector3 controlPoint = (startPos + endPos) * 0.5f + new Vector3(sideOffset, _flyArcOffset * 0.5f, 0f);

        Sequence seq = DOTween.Sequence();
        seq.Join(DOVirtual.Float(0f, 1f, _flyDuration, t =>
        {
            if (orb == null) return;
            float inv = 1f - t;
            orb.localPosition = (inv * inv * startPos) + (2f * inv * t * controlPoint) + (t * t * endPos);
        }).SetEase(_flyEase));

        _activeFlyTweens.Add(seq);
        _activeOrbs.Add(orb);

        seq.OnComplete(() =>
        {
            _activeFlyTweens.Remove(seq);
            _activeOrbs.Remove(orb);
            ReturnOrbToPool(orb);
            TriggerArrivalFeedback(remaining, total);
        });
    }

    private void TriggerArrivalFeedback(int remaining, int total)
    {
        UpdateProgress(remaining, total);

        if (_barHitVfx != null)
        {
            _barHitVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _barHitVfx.Play(true);
        }

        SoundManager.PlaySound(_arrivalSound);

        if (GameData.IsHapticEnabled)
        {
            Taptic.Light();
        }
    }

    /// <summary>
    /// Called directly when progress updates.
    /// Animates bar fill as remaining item count decreases.
    /// </summary>
    public void UpdateProgress(int remaining, int total)
    {
        if (fillImage == null || total <= 0) return;

        float targetFill = Mathf.Clamp01(1f - ((float)remaining / total));
        AnimateProgress(targetFill);
    }

    private void AnimateProgress(float targetFill)
    {
        fillTween?.Kill();
        fillTween = fillImage.DOFillAmount(targetFill, fillDuration).SetEase(fillEase);

        if (usePunchEffect)
        {
            punchTween?.Kill();
            transform.localScale = initialScale;
            punchTween = transform.DOPunchScale(initialScale * punchStrength, punchDuration, 1, 0.5f);
        }
    }

    public void UpdateProgressImmediate(int remaining, int total)
    {
        if (fillImage == null || total <= 0) return;

        float targetFill = Mathf.Clamp01(1f - ((float)remaining / total));
        fillImage.fillAmount = targetFill;
    }

    public void ResetBar()
    {
        KillTweens();
        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
        }
    }

    private RectTransform GetFlyTargetRect()
    {
        if (_flyTargetPoint != null) return _flyTargetPoint;
        if (fillImage != null) return fillImage.rectTransform;
        return transform as RectTransform;
    }

    private RectTransform GetOrbFromPool(RectTransform parent)
    {
        while (_orbPool.Count > 0)
        {
            RectTransform pooled = _orbPool.Dequeue();
            if (pooled != null)
            {
                if (pooled.parent != parent)
                {
                    pooled.SetParent(parent, false);
                }
                return pooled;
            }
        }

        RectTransform created = Instantiate(_flyOrbPrefab, parent);
        created.gameObject.SetActive(false);
        return created;
    }

    private void ReturnOrbToPool(RectTransform orb)
    {
        if (orb == null) return;
        orb.gameObject.SetActive(false);
        _orbPool.Enqueue(orb);
    }

    private void KillTweens()
    {
        fillTween?.Kill();
        punchTween?.Kill();

        for (int i = _activeFlyTweens.Count - 1; i >= 0; i--)
        {
            _activeFlyTweens[i]?.Kill();
        }
        _activeFlyTweens.Clear();

        for (int i = _activeOrbs.Count - 1; i >= 0; i--)
        {
            ReturnOrbToPool(_activeOrbs[i]);
        }
        _activeOrbs.Clear();

        if (initialScale != Vector3.zero)
        {
            transform.localScale = initialScale;
        }
    }
}
