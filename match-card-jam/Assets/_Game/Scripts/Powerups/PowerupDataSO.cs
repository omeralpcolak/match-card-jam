using UnityEngine;

/// <summary>
/// ScriptableObject holding static configuration data for a single power-up.
/// Runtime player inventory counts are persisted separately in GameData.
/// </summary>
[CreateAssetMenu(fileName = "PowerupData", menuName = "Game/Powerup Data")]
public class PowerupDataSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique type identifier for this power-up.")]
    [SerializeField] private PowerupType powerupType;

    [Tooltip("Display name shown in UI / unlock popups.")]
    [SerializeField] private string displayName;

    [Header("UI")]
    [Tooltip("Icon sprite displayed on the PowerupUiItem button.")]
    [SerializeField] private Sprite icon;

    [Header("Progression & Economy")]
    [Tooltip("1-based level at which this power-up unlocks.")]
    [Min(1)]
    [SerializeField] private int unlockLevel = 1;

    [Tooltip("Initial amount granted to the player when first initialized.")]
    [Min(0)]
    [SerializeField] private int defaultAmount = 3;

    [Tooltip("Coin cost to purchase 1 charge when the player runs out.")]
    [Min(0)]
    [SerializeField] private int coinCost = 100;

    [Header("Gameplay")]
    [Tooltip("Effect duration in seconds (used by timed power-ups like FreezeTime or HintSpotlight).")]
    [Min(0f)]
    [SerializeField] private float duration = 10f;

    [Tooltip("PowerupBase prefab instantiated in the scene when activated.")]
    [SerializeField] private PowerupBase _powerupPrefab;

    public PowerupType PowerupType => powerupType;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? powerupType.ToString() : displayName;
    public Sprite Icon => icon;
    public int UnlockLevel => unlockLevel;
    public int DefaultAmount => defaultAmount;
    public int CoinCost => coinCost;
    public float Duration => duration;
    public PowerupBase PowerupPrefab => _powerupPrefab;
}
