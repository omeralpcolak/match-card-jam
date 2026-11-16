using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI button component representing a single power-up in the Game scene panel.
/// Initialized explicitly by PowerupManager; avoids Awake/OnEnable/OnDisable hooks.
/// </summary>
[RequireComponent(typeof(Button))]
public class PowerupUiItem : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Button component handling user clicks.")]
    [SerializeField] private Button _button;

    [Tooltip("Image displaying the power-up icon when unlocked.")]
    [SerializeField] private Image _iconImage;

    [Tooltip("CanvasGroup on IconArea (parent of IconImage and Amount) used to show/hide and dim when unavailable.")]
    [SerializeField] private CanvasGroup _iconCanvasGroup;

    [Tooltip("Image overlay shown when the power-up is locked.")]
    [SerializeField] private Image _lockedImage;

    [Tooltip("Text displaying the unlock level inside LockedImage.")]
    [SerializeField] private TextMeshProUGUI _unlockLevelText;

    [Header("Optional Count UI")]
    [Tooltip("Optional text displaying the remaining charge count when unlocked.")]
    [SerializeField] private TextMeshProUGUI _countText;

    [Header("Availability & Shake Settings")]
    [Tooltip("Alpha applied to IconCanvasGroup when unlocked but currently unavailable.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float _unavailableAlpha = 0.45f;

    [Tooltip("Duration of the horizontal shake when clicked while locked or unavailable.")]
    [SerializeField] private float _shakeDuration = 0.35f;

    [Tooltip("Horizontal shake amplitude in UI units.")]
    [SerializeField] private float _shakeStrength = 10f;

    [Tooltip("Number of shake oscillations.")]
    [SerializeField] private int _shakeVibrato = 14;

    private PowerupDataSO _data;
    private Action<PowerupUiItem> _onClicked;
    private bool _isUnlocked;
    private Tween _lockedShakeTween;
    private Tween _iconShakeTween;

    public PowerupDataSO Data => _data;

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
        }

        _lockedShakeTween?.Kill();
        _iconShakeTween?.Kill();
    }

    /// <summary>
    /// Initializes this UI item with power-up configuration and click callback.
    /// Called explicitly by PowerupManager immediately after instantiation.
    /// </summary>
    public void Initialize(PowerupDataSO powerupData, Action<PowerupUiItem> clickCallback)
    {
        _data = powerupData;
        _onClicked = clickCallback;

        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        if (_iconCanvasGroup == null && _iconImage != null)
        {
            _iconCanvasGroup = _iconImage.GetComponentInParent<CanvasGroup>();
        }

        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
        }

        if (_iconImage != null && _data != null && _data.Icon != null)
        {
            _iconImage.sprite = _data.Icon;
        }

        Refresh();
    }

    /// <summary>
    /// Updates locked/unlocked state and current charge count from GameData.
    /// </summary>
    public void Refresh()
    {
        if (_data == null) return;

        _isUnlocked = GameData.CurrentLevel >= _data.UnlockLevel;

        if (_lockedImage != null)
        {
            _lockedImage.gameObject.SetActive(!_isUnlocked);
        }

        if (_iconCanvasGroup != null)
        {
            _iconCanvasGroup.gameObject.SetActive(_isUnlocked);
        }
        else if (_iconImage != null)
        {
            _iconImage.gameObject.SetActive(_isUnlocked);
        }

        if (!_isUnlocked && _unlockLevelText != null)
        {
            _unlockLevelText.text = $"Lv. {_data.UnlockLevel}";
        }

        if (_countText != null)
        {
            _countText.gameObject.SetActive(_isUnlocked);
            if (_isUnlocked)
            {
                _countText.text = GameData.GetPowerupCount(_data.PowerupType).ToString();
            }
        }
    }

    /// <summary>
    /// Dims or brightens the icon CanvasGroup based on whether this power-up can currently be activated.
    /// </summary>
    public void SetAvailable(bool isAvailable)
    {
        if (_iconCanvasGroup == null || !_isUnlocked) return;

        _iconCanvasGroup.alpha = isAvailable ? 1f : _unavailableAlpha;
    }

    /// <summary>
    /// Plays a horizontal shake on the icon and triggers a warning haptic when clicked while unavailable.
    /// </summary>
    public void PlayUnavailableShake()
    {
        if (GameData.IsHapticEnabled)
        {
            Taptic.Warning();
        }

        if (_iconImage == null) return;

        _iconShakeTween?.Complete();
        _iconShakeTween = _iconImage.rectTransform
            .DOShakeAnchorPos(_shakeDuration, new Vector2(_shakeStrength, 0f), _shakeVibrato, randomness: 0f, snapping: false, fadeOut: true)
            .SetUpdate(true);
    }

    private void HandleClick()
    {
        if (_data == null) return;

        if (!_isUnlocked)
        {
            PlayLockedShake();
            return;
        }

        _onClicked?.Invoke(this);
    }

    private void PlayLockedShake()
    {
        if (GameData.IsHapticEnabled)
        {
            Taptic.Warning();
        }

        if (_lockedImage == null) return;

        // Complete any active shake first so repeated clicks do not drift the resting position.
        _lockedShakeTween?.Complete();
        _lockedShakeTween = _lockedImage.rectTransform
            .DOShakeAnchorPos(_shakeDuration, new Vector2(_shakeStrength, 0f), _shakeVibrato, randomness: 0f, snapping: false, fadeOut: true)
            .SetUpdate(true);
    }
}
