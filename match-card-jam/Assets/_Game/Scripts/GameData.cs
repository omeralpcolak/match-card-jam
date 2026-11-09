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
        Debug.Log($"<color=#f1c40f><b>[GameData]</b> +{amount} Coin eklendi. Toplam: {Coins}</color>");
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
            Debug.Log($"<color=#e67e22><b>[GameData]</b> -{amount} Coin harcandı. Kalan: {Coins}</color>");
            return true;
        }

        Debug.LogWarning($"[GameData] Yetersiz bakiye! İstenen: {amount}, Mevcut: {Coins}");
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
        Debug.Log($"<color=#2ecc71><b>[GameData]</b> Yeni seviyeye geçildi: Seviye {CurrentLevel}</color>");
        return CurrentLevel;
    }

    #endregion

    #region Settings

    public static bool IsSoundEnabled => SettingsPopup.IsSoundEnabled;
    public static bool IsMusicEnabled => SettingsPopup.IsMusicEnabled;
    public static bool IsHapticEnabled => SettingsPopup.IsHapticEnabled;

    #endregion
}
