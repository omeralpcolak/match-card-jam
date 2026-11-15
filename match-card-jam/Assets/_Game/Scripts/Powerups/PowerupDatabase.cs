using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject database holding all PowerupDataSO assets.
/// Self-loads from Resources/PowerupDatabase, matching LevelDatabase's pattern.
/// </summary>
[CreateAssetMenu(fileName = "PowerupDatabase", menuName = "Game/Powerup Database")]
public class PowerupDatabase : ScriptableObject
{
    private const string RESOURCE_PATH = "PowerupDatabase";

    private static PowerupDatabase instance;

    /// <summary>
    /// Self-loading singleton instance loaded automatically from Resources/PowerupDatabase.
    /// </summary>
    public static PowerupDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<PowerupDatabase>(RESOURCE_PATH);

                if (instance == null)
                {
                    PowerupDatabase[] all = Resources.LoadAll<PowerupDatabase>("");
                    if (all != null && all.Length > 0)
                    {
                        instance = all[0];
                    }
                }

                if (instance == null)
                {
                    Debug.LogError("[PowerupDatabase] PowerupDatabase asset not found in Resources! Please ensure your PowerupDatabase asset is inside 'Assets/_Game/Resources/PowerupDatabase'.");
                }
            }

            return instance;
        }
    }

    [Header("Powerups")]
    [Tooltip("Ordered list of power-ups displayed in the Game scene panel.")]
    [SerializeField] private List<PowerupDataSO> powerups = new();

    public IReadOnlyList<PowerupDataSO> Powerups => powerups;

    /// <summary>
    /// Returns the PowerupDataSO matching the given PowerupType, or null if not found.
    /// </summary>
    public PowerupDataSO GetPowerup(PowerupType type)
    {
        if (powerups == null) return null;

        for (int i = 0; i < powerups.Count; i++)
        {
            if (powerups[i] != null && powerups[i].PowerupType == type)
            {
                return powerups[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Static shortcut to fetch a PowerupDataSO from anywhere in the codebase.
    /// </summary>
    public static PowerupDataSO Get(PowerupType type)
    {
        return Instance != null ? Instance.GetPowerup(type) : null;
    }
}
