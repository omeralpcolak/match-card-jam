using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Freezes the level countdown timer for the duration configured on PowerupDataSO,
/// displays a screen-edge frost overlay and a draining duration bar, and plays freeze SFX.
/// Pauses its own duration countdown whenever LevelManager is paused.
/// </summary>
public class FreezeTimePowerup : PowerupBase
{
    [Header("UI References")]
    [Tooltip("CanvasGroup controlling fade-in and fade-out of the frost overlay and duration bar.")]
    [SerializeField] private CanvasGroup _overlayCanvasGroup;

    [Tooltip("Full-screen edge frost vignette image.")]
    [SerializeField] private Image _frostOverlayImage;

    [Tooltip("Filled Image (Fill Method: Horizontal/Radial) showing remaining freeze duration.")]
    [SerializeField] private Image _durationFillImage;

    [Header("Animation & Audio")]
    [Tooltip("Fade-in duration in seconds when freeze starts.")]
    [SerializeField] private float _fadeInDuration = 0.3f;

    [Tooltip("Fade-out duration in seconds when freeze ends.")]
    [SerializeField] private float _fadeOutDuration = 0.3f;

    [Tooltip("Sound effect played through SoundManager when activated.")]
    [SerializeField] private SoundType _freezeSound = SoundType.PowerupFreeze;

    private LevelManager _levelManager;
    private Tween _fadeTween;
    private float _totalDuration;
    private float _remainingDuration;
    private bool _isRunning;

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _fadeTween?.Kill();

        if (_levelManager != null && _levelManager.IsTimerFrozen)
        {
            _levelManager.SetTimerFrozen(false);
        }
    }

    public override bool CanActivate(PowerupContext context)
    {
        if (!base.CanActivate(context)) return false;

        // Only allow freezing if the current level uses a timer and is not already frozen.
        return context.LevelManager.UseTimer && !context.LevelManager.IsTimerFrozen;
    }

    protected override void OnActivate(PowerupContext context)
    {
        _levelManager = context.LevelManager;
        _totalDuration = Data != null && Data.Duration > 0f ? Data.Duration : 10f;
        _remainingDuration = _totalDuration;
        _isRunning = true;

        // 1. Audio
        SoundManager.PlaySound(_freezeSound);

        // 2. Ensure overlay visuals never block gameplay clicks
        PrepareVisuals();

        // 3. Freeze timer
        _levelManager.SetTimerFrozen(true);

        if (_durationFillImage != null)
        {
            _durationFillImage.fillAmount = 1f;
        }
    }

    private void Update()
    {
        if (!_isRunning || _levelManager == null) return;

        // Pause freeze countdown while Settings / Restart / NoLives popups pause the level.
        if (_levelManager.CurrentState == LevelManager.LevelState.Paused) return;

        // If the level ended (Won or Lost) during freeze, clean up immediately.
        if (_levelManager.CurrentState != LevelManager.LevelState.Playing)
        {
            _isRunning = false;
            HandleFreezeExpired();
            return;
        }

        _remainingDuration -= Time.deltaTime;

        if (_durationFillImage != null)
        {
            _durationFillImage.fillAmount = Mathf.Clamp01(_remainingDuration / _totalDuration);
        }

        if (_remainingDuration <= 0f)
        {
            _isRunning = false;
            HandleFreezeExpired();
        }
    }

    private void PrepareVisuals()
    {
        if (_frostOverlayImage != null)
        {
            _frostOverlayImage.raycastTarget = false;
        }

        if (_durationFillImage != null)
        {
            _durationFillImage.raycastTarget = false;
        }

        if (_overlayCanvasGroup != null)
        {
            _overlayCanvasGroup.blocksRaycasts = false;
            _overlayCanvasGroup.interactable = false;
            _overlayCanvasGroup.alpha = 0f;

            _fadeTween?.Kill();
            _fadeTween = _overlayCanvasGroup
                .DOFade(1f, _fadeInDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(false);
        }
    }

    private void HandleFreezeExpired()
    {
        if (_levelManager != null)
        {
            _levelManager.SetTimerFrozen(false);
        }

        if (_overlayCanvasGroup != null && _fadeOutDuration > 0f)
        {
            _fadeTween?.Kill();
            _fadeTween = _overlayCanvasGroup
                .DOFade(0f, _fadeOutDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(false)
                .OnComplete(Complete);
        }
        else
        {
            Complete();
        }
    }
}
