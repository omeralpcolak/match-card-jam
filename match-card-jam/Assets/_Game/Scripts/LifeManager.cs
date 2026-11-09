using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Master persistent manager for the player's Life (Energy/Heart) system.
/// - Controls life deduction, refund on win, and real-time/offline regeneration.
/// - Survives scene loads and automatically initializes before any scene starts.
/// </summary>
public class LifeManager : MonoBehaviour
{
    public static LifeManager Instance { get; private set; }

    public const string KEY_PLAYER_LIVES = "PlayerLives";
    public const string KEY_NEXT_REGEN_TIME = "LifeManager_NextRegenUtc";
    public const string KEY_PENDING_REFUND = "LifeManager_HasPendingRefund";

    public const int MAX_LIVES = 5;
    public const float REGEN_DURATION_SECONDS = 900f; // 15 minutes per life (15 * 60s)

    public static event Action<int> OnLivesChanged;
    public static event Action<float> OnTimerTick; // Remaining seconds until next life

    private float timerUpdateCounter;

    public static int CurrentLives
    {
        get => PlayerPrefs.GetInt(KEY_PLAYER_LIVES, MAX_LIVES);
        private set
        {
            int clamped = Mathf.Clamp(value, 0, MAX_LIVES);
            PlayerPrefs.SetInt(KEY_PLAYER_LIVES, clamped);
            PlayerPrefs.Save();
            OnLivesChanged?.Invoke(clamped);
        }
    }

    public static bool HasLives => CurrentLives > 0;
    public static bool IsFull => CurrentLives >= MAX_LIVES;

    public static bool HasPendingRefund
    {
        get => PlayerPrefs.GetInt(KEY_PENDING_REFUND, 0) == 1;
        private set
        {
            PlayerPrefs.SetInt(KEY_PENDING_REFUND, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("LifeManager");
            Instance = go.AddComponent<LifeManager>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Verify and process any offline regeneration immediately on startup
        UpdateRegeneration();
    }

    private void Update()
    {
        // Periodic 1-second tick for real-time regeneration and UI countdown
        timerUpdateCounter += Time.unscaledDeltaTime;
        if (timerUpdateCounter >= 1f)
        {
            timerUpdateCounter = 0f;
            UpdateRegeneration();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // When returning to app from background or device sleep, recalculate offline elapsed time
        if (!pauseStatus)
        {
            UpdateRegeneration();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // When window or app regains focus, refresh offline regeneration
        if (hasFocus)
        {
            UpdateRegeneration();
        }
    }

    /// <summary>
    /// Checks real-time / offline elapsed time and adds regenerated lives.
    /// </summary>
    public static void UpdateRegeneration()
    {
        if (CurrentLives >= MAX_LIVES)
        {
            ClearNextRegenTime();
            OnTimerTick?.Invoke(0f);
            return;
        }

        DateTime nextRegenTime = GetNextRegenTime();

        // If no regen timer was running yet, start it now
        if (nextRegenTime == DateTime.MinValue)
        {
            nextRegenTime = DateTime.UtcNow.AddSeconds(REGEN_DURATION_SECONDS);
            SetNextRegenTime(nextRegenTime);
        }

        DateTime now = DateTime.UtcNow;

        if (now >= nextRegenTime)
        {
            // Calculate how many lives have regenerated during the elapsed time
            TimeSpan overdue = now - nextRegenTime;
            int livesToAdd = 1 + (int)(overdue.TotalSeconds / REGEN_DURATION_SECONDS);

            CurrentLives = Mathf.Min(MAX_LIVES, CurrentLives + livesToAdd);

            if (CurrentLives >= MAX_LIVES)
            {
                ClearNextRegenTime();
                OnTimerTick?.Invoke(0f);
                return;
            }
            else
            {
                // Set the timestamp for the next heart regeneration
                double remainingRemainder = REGEN_DURATION_SECONDS - (overdue.TotalSeconds % REGEN_DURATION_SECONDS);
                nextRegenTime = now.AddSeconds(remainingRemainder);
                SetNextRegenTime(nextRegenTime);
            }
        }

        // Notify listeners with remaining seconds until the next life
        float remainingSeconds = (float)(nextRegenTime - now).TotalSeconds;
        OnTimerTick?.Invoke(Mathf.Max(0f, remainingSeconds));
    }

    /// <summary>
    /// Attempts to consume 1 life to start/restart a level.
    /// Returns true if successful, false if player has 0 lives.
    /// Starts the regen timer immediately if not already running.
    /// </summary>
    public static bool TryConsumeLife()
    {
        if (!HasLives)
        {
            Debug.LogWarning("<color=#e74c3c><b>[LifeManager]</b> Yeterli can yok! Seviye başlatılamıyor.</color>");
            return false;
        }

        CurrentLives--;
        HasPendingRefund = true;

        // If lives dropped below max and no regen timer is running, start it immediately
        if (CurrentLives < MAX_LIVES && GetNextRegenTime() == DateTime.MinValue)
        {
            SetNextRegenTime(DateTime.UtcNow.AddSeconds(REGEN_DURATION_SECONDS));
        }

        Debug.Log($"<color=#f39c12><b>[LifeManager]</b> 1 can eksildi. Kalan can: {CurrentLives}</color>");
        UpdateRegeneration();
        return true;
    }

    /// <summary>
    /// Called when the player wins a level.
    /// Refunds the consumed life back to the player.
    /// </summary>
    public static void RefundLife()
    {
        if (HasPendingRefund)
        {
            HasPendingRefund = false;
            CurrentLives = Mathf.Min(MAX_LIVES, CurrentLives + 1);
            Debug.Log($"<color=#2ecc71><b>[LifeManager]</b> Seviye kazanıldı! Can iade edildi. Mevcut can: {CurrentLives}</color>");

            if (CurrentLives >= MAX_LIVES)
            {
                ClearNextRegenTime();
                OnTimerTick?.Invoke(0f);
            }
        }
    }

    /// <summary>
    /// Forfeits the pending refund when player fails or quits a level.
    /// The life remains lost, and the regen timer continues uninterrupted.
    /// </summary>
    public static void ForfeitPendingRefund()
    {
        if (HasPendingRefund)
        {
            HasPendingRefund = false;
            Debug.Log("<color=#e74c3c><b>[LifeManager]</b> Can iade hakkı iptal edildi (seviye kaybedildi veya çıkıldı).</color>");
        }
    }

    /// <summary>
    /// Manually sets player lives (e.g. for editor testing or in-app purchases).
    /// </summary>
    public static void SetLives(int lives)
    {
        CurrentLives = Mathf.Clamp(lives, 0, MAX_LIVES);
        if (CurrentLives >= MAX_LIVES)
        {
            ClearNextRegenTime();
        }
        else if (GetNextRegenTime() == DateTime.MinValue)
        {
            SetNextRegenTime(DateTime.UtcNow.AddSeconds(REGEN_DURATION_SECONDS));
        }
        UpdateRegeneration();
    }

    public static float GetRemainingSeconds()
    {
        if (CurrentLives >= MAX_LIVES) return 0f;

        DateTime next = GetNextRegenTime();
        if (next == DateTime.MinValue) return 0f;

        return Mathf.Max(0f, (float)(next - DateTime.UtcNow).TotalSeconds);
    }

    #region Timestamp Persistence Helpers

    private static DateTime GetNextRegenTime()
    {
        string raw = PlayerPrefs.GetString(KEY_NEXT_REGEN_TIME, string.Empty);
        if (string.IsNullOrEmpty(raw)) return DateTime.MinValue;

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            return parsed;
        }

        return DateTime.MinValue;
    }

    private static void SetNextRegenTime(DateTime utcTime)
    {
        PlayerPrefs.SetString(KEY_NEXT_REGEN_TIME, utcTime.ToString("o", CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    private static void ClearNextRegenTime()
    {
        PlayerPrefs.DeleteKey(KEY_NEXT_REGEN_TIME);
        PlayerPrefs.Save();
    }

    #endregion
}
