using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// ScriptableObject holding static configuration data for a single power-up.
/// Runtime player inventory counts are persisted separately in GameData.
/// </summary>
[CreateAssetMenu(fileName = "PowerupData", menuName = "Game/Powerup Data")]
public class PowerupDataSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique type identifier for this power-up.")]
    [FormerlySerializedAs("powerupType")]
    [SerializeField] private PowerupType _powerupType;

    [Tooltip("Display name shown in UI / unlock popups.")]
    [FormerlySerializedAs("displayName")]
    [SerializeField] private string _displayName;

    [Header("UI")]
    [Tooltip("Icon sprite displayed on the PowerupUiItem button.")]
    [FormerlySerializedAs("icon")]
    [SerializeField] private Sprite _icon;

    [Header("Progression & Economy")]
    [Tooltip("1-based level at which this power-up unlocks.")]
    [Min(1)]
    [FormerlySerializedAs("unlockLevel")]
    [SerializeField] private int _unlockLevel = 1;

    [Tooltip("Initial amount granted to the player when first initialized.")]
    [Min(0)]
    [FormerlySerializedAs("defaultAmount")]
    [SerializeField] private int _defaultAmount = 3;

    [Tooltip("Coin cost to purchase 1 charge when the player runs out.")]
    [Min(0)]
    [FormerlySerializedAs("coinCost")]
    [SerializeField] private int _coinCost = 100;

    [Header("Gameplay")]
    [Tooltip("Effect duration in seconds (used by timed power-ups like FreezeTime or HintSpotlight).")]
    [Min(0f)]
    [FormerlySerializedAs("duration")]
    [SerializeField] private float _duration = 10f;

    [Tooltip("PowerupBase prefab instantiated in the scene when activated.")]
    [SerializeField] private PowerupBase _powerupPrefab;

    public PowerupType PowerupType => _powerupType;
    public string DisplayName => string.IsNullOrEmpty(_displayName) ? _powerupType.ToString() : _displayName;
    public Sprite Icon => _icon;
    public int UnlockLevel => _unlockLevel;
    public int DefaultAmount => _defaultAmount;
    public int CoinCost => _coinCost;
    public float Duration => _duration;
    public PowerupBase PowerupPrefab => _powerupPrefab;
}
