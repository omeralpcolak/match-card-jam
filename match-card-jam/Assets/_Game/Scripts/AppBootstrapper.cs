using UnityEngine;

/// <summary>
/// Initializes global engine and application-level settings before any scene loads.
/// Configures target frame rate (60 FPS on mobile / standalone), disables vSync to allow
/// custom frame pacing, and disables screen sleeping during gameplay.
/// </summary>
public static class AppBootstrapper
{
    public const int TARGET_FRAME_RATE = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        // Disable VSync so Application.targetFrameRate takes priority
        QualitySettings.vSyncCount = 0;

        // Set target frame rate (defaults to 30 on iOS / many Android devices if omitted)
        Application.targetFrameRate = TARGET_FRAME_RATE;

        // Keep device screen active during gameplay
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        Debug.Log($"[AppBootstrapper] Initialized: targetFrameRate={Application.targetFrameRate}, vSyncCount={QualitySettings.vSyncCount}");
    }
}
