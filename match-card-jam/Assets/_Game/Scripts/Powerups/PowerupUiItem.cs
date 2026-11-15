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

    [Tooltip("Image overlay shown when the power-up is locked.")]
    [SerializeField] private Image _lockedImage;

    [Tooltip("Text displaying the unlock level inside LockedImage.")]
    [SerializeField] private TextMeshProUGUI _unlockLevelText;

    [Header("Optional Count UI")]
    [Tooltip("Optional text displaying the remaining charge count when unlocked.")]
    [SerializeField] private TextMeshProUGUI _countText;

    [Header("Locked Shake Settings")]
    [Tooltip("Duration of the horizontal shake when clicked while locked.")]
    [SerializeField] private float _shakeDuration = 0.35f;

    [Tooltip("Horizontal shake amplitude in UI units.")]
    [SerializeField] private float _shakeStrength = 10f;

    [Tooltip("Number of shake oscillations.")]
    [SerializeField] private int _shakeVibrato = 14;

    private PowerupDataSO _data;
    private Action<PowerupDataSO> _onClicked;
    private bool _isUnlocked;
    private Tween _shakeTween;

    public PowerupDataSO Data => _data;

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
        }

        _shakeTween?.Kill();
    }

    /// <summary>
    /// Initializes this UI item with power-up configuration and click callback.
    /// Called explicitly by PowerupManager immediately after instantiation.
    /// </summary>
    public void Initialize(PowerupDataSO powerupData, Action<PowerupDataSO> clickCallback)
    {
        _data = powerupData;
        _onClicked = clickCallback;

        if (_button == null)
        {
            _button = GetComponent<Button>();
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

        if (_iconImage != null)
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

    private void HandleClick()
    {
        if (_data == null) return;

        if (!_isUnlocked)
        {
            PlayLockedShake();
            return;
        }

        _onClicked?.Invoke(_data);
    }

    private void PlayLockedShake()
    {
        if (_lockedImage == null) return;

        // Complete any active shake first so repeated clicks do not drift the resting position.
        _shakeTween?.Complete();
        _shakeTween = _lockedImage.rectTransform
            .DOShakeAnchorPos(_shakeDuration, new Vector2(_shakeStrength, 0f), _shakeVibrato, randomness: 0f, snapping: false, fadeOut: true)
            .SetUpdate(true);
    }
}
