using System;
using UnityEngine;

/// <summary>
/// Centralized static data repository for core player state (Coins, Lives, Progression, Settings).
/// Acts as a single source of truth, isolating PlayerPrefs and broadcasting change events.
/// </summary>
public static class GameData
{
    // Persistence Keys
    public const string KEY_COINS = "PlayerCoins";
    public const string KEY_CURRENT_LEVEL = "CurrentLevel";
    public const string KEY_POWERUP_PREFIX = "PowerupAmount_";

    #region Coins / Economy

    public static event Action<int> OnCoinsChanged;

    /// <summary>
    /// Current coin balance of the player.
    /// </summary>
    public static int Coins
    {
        get => PlayerPrefs.GetInt(KEY_COINS, 0);
        set
        {
            int clamped = Mathf.Max(0, value);
            PlayerPrefs.SetInt(KEY_COINS, clamped);
            PlayerPrefs.Save();
            OnCoinsChanged?.Invoke(clamped);
        }
    }

    /// <summary>
    /// Adds coins to player's balance.
    /// </summary>
    public static void AddCoins(int amount)
    {
        if (amount <= 0) return;
        Coins += amount;
        Debug.Log($"<color=#f1c40f><b>[GameData]</b> +{amount} Coins added. Total: {Coins}</color>");
    }

    /// <summary>
    /// Attempts to spend coins. Returns true if successful, false if insufficient balance.
    /// </summary>
    public static bool TrySpendCoins(int amount)
    {
        if (amount < 0) return false;
        if (Coins >= amount)
        {
            Coins -= amount;
            Debug.Log($"<color=#e67e22><b>[GameData]</b> -{amount} Coins spent. Remaining: {Coins}</color>");
            return true;
        }

        Debug.LogWarning($"[GameData] Insufficient balance! Requested: {amount}, Available: {Coins}");
        return false;
    }

    #endregion

    #region Lives / Energy

    public static event Action<int> OnLivesChanged
    {
        add => LifeManager.OnLivesChanged += value;
        remove => LifeManager.OnLivesChanged -= value;
    }

    public static event Action<float> OnLifeTimerTick
    {
        add => LifeManager.OnTimerTick += value;
        remove => LifeManager.OnTimerTick -= value;
    }

    public static int Lives
    {
        get => LifeManager.CurrentLives;
        set => LifeManager.SetLives(value);
    }

    public static bool HasLives => LifeManager.HasLives;
    public static bool IsFullLives => LifeManager.IsFull;
    public static float RemainingLifeRegenSeconds => LifeManager.GetRemainingSeconds();

    public static bool TryConsumeLife() => LifeManager.TryConsumeLife();
    public static void RefundLife() => LifeManager.RefundLife();
    public static void ForfeitPendingRefund() => LifeManager.ForfeitPendingRefund();
    public static void ShowNoLivesPopup() => NoLivesLeftPopup.Show();

    #endregion

    #region Level Progression

    public static event Action<int> OnLevelChanged;

    /// <summary>
    /// Player's current level index (1-based).
    /// </summary>
    public static int CurrentLevel
    {
        get => PlayerPrefs.GetInt(KEY_CURRENT_LEVEL, 1);
        set
        {
            int clamped = Mathf.Max(1, value);
            PlayerPrefs.SetInt(KEY_CURRENT_LEVEL, clamped);
            PlayerPrefs.Save();
            OnLevelChanged?.Invoke(clamped);
        }
    }

    /// <summary>
    /// Advances current level by 1 and returns the new level.
    /// </summary>
    public static int AdvanceLevel()
    {
        CurrentLevel++;
        Debug.Log($"<color=#2ecc71><b>[GameData]</b> Advanced to new level: Level {CurrentLevel}</color>");
        return CurrentLevel;
    }

    #endregion

    #region Settings

    public static bool IsSoundEnabled => SettingsPopup.IsSoundEnabled;
    public static bool IsMusicEnabled => SettingsPopup.IsMusicEnabled;
    public static bool IsHapticEnabled => SettingsPopup.IsHapticEnabled;

    #endregion

    #region Powerups

    public static event Action<PowerupType, int> OnPowerupCountChanged;

    private static string GetPowerupKey(PowerupType type) => $"{KEY_POWERUP_PREFIX}{type}";

    /// <summary>
    /// Returns the player's current count for the given power-up.
    /// Initializes from PowerupDataSO.DefaultAmount on first access.
    /// </summary>
    public static int GetPowerupCount(PowerupType type)
    {
        string key = GetPowerupKey(type);
        if (!PlayerPrefs.HasKey(key))
        {
            PowerupDataSO data = PowerupDatabase.Get(type);
            int defaultAmount = data != null ? data.DefaultAmount : 0;
            PlayerPrefs.SetInt(key, defaultAmount);
            PlayerPrefs.Save();
            return defaultAmount;
        }

        return Mathf.Max(0, PlayerPrefs.GetInt(key, 0));
    }

    /// <summary>
    /// Sets the count for the given power-up and notifies listeners.
    /// </summary>
    public static void SetPowerupCount(PowerupType type, int amount)
    {
        int clamped = Mathf.Max(0, amount);
        PlayerPrefs.SetInt(GetPowerupKey(type), clamped);
        PlayerPrefs.Save();
        OnPowerupCountChanged?.Invoke(type, clamped);
    }

    /// <summary>
    /// Adds charges to the given power-up.
    /// </summary>
    public static void AddPowerup(PowerupType type, int amount)
    {
        if (amount <= 0) return;
        SetPowerupCount(type, GetPowerupCount(type) + amount);
    }

    /// <summary>
    /// Consumes 1 charge of the given power-up if available.
    /// </summary>
    public static bool TryUsePowerup(PowerupType type)
    {
        int current = GetPowerupCount(type);
        if (current <= 0) return false;

        SetPowerupCount(type, current - 1);
        return true;
    }

    /// <summary>
    /// Returns true if the player's current level meets or exceeds the power-up's unlock level.
    /// </summary>
    public static bool IsPowerupUnlocked(PowerupType type)
    {
        PowerupDataSO data = PowerupDatabase.Get(type);
        if (data == null) return false;
        return CurrentLevel >= data.UnlockLevel;
    }

    #endregion
}
