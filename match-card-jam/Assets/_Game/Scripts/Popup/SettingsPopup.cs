using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Settings popup dialog.
/// Provides Sound, Music, and Haptic toggle controls (saved to PlayerPrefs),
/// a Home button to navigate to the Main Menu, a Restart button to reload the active scene,
/// and a Close button to resume gameplay.
/// </summary>
public class SettingsPopup : PopupBase<SettingsPopup>
{
    private const string SOUND_KEY = "Setting_Sound";
    private const string MUSIC_KEY = "Setting_Music";
    private const string HAPTIC_KEY = "Setting_Haptic";

    [Header("Close & Backdrop Controls")]
    [Tooltip("Button used to close the popup and resume gameplay.")]
    [SerializeField] private Button closeButton;

    [Tooltip("Whether clicking the backdrop closes the popup.")]
    [SerializeField] private bool closeOnBackdropClick = true;

    [Tooltip("Optional backdrop button component.")]
    [SerializeField] private Button backdropButton;

    [Header("Sound Settings")]
    [Tooltip("Button used to toggle sound effects on/off.")]
    [SerializeField] private Button soundButton;

    [Tooltip("Target Image component displaying the sound icon.")]
    [SerializeField] private Image soundIconImage;

    [Tooltip("Sprite displayed when sound is enabled (Optional, defaults to image's initial sprite if null).")]
    [SerializeField] private Sprite soundOnSprite;

    [Tooltip("Sprite displayed when sound is disabled.")]
    [SerializeField] private Sprite soundOffSprite;

    [Header("Music Settings")]
    [Tooltip("Button used to toggle music on/off.")]
    [SerializeField] private Button musicButton;

    [Tooltip("Target Image component displaying the music icon.")]
    [SerializeField] private Image musicIconImage;

    [Tooltip("Sprite displayed when music is enabled (Optional, defaults to image's initial sprite if null).")]
    [SerializeField] private Sprite musicOnSprite;

    [Tooltip("Sprite displayed when music is disabled.")]
    [SerializeField] private Sprite musicOffSprite;

    [Header("Haptic Settings")]
    [Tooltip("Button used to toggle haptic vibration on/off.")]
    [SerializeField] private Button hapticButton;

    [Tooltip("Target Image component displaying the haptic icon.")]
    [SerializeField] private Image hapticIconImage;

    [Tooltip("Sprite displayed when haptic feedback is enabled (Optional, defaults to image's initial sprite if null).")]
    [SerializeField] private Sprite hapticOnSprite;

    [Tooltip("Sprite displayed when haptic feedback is disabled.")]
    [SerializeField] private Sprite hapticOffSprite;

    [Header("Navigation Buttons")]
    [Tooltip("Button that navigates to the main menu.")]
    [SerializeField] private Button homeButton;

    [Tooltip("Name of the main menu scene.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // Static query helpers for other subsystems
    public static bool IsSoundEnabled => PlayerPrefs.GetInt(SOUND_KEY, 1) == 1;
    public static bool IsMusicEnabled => PlayerPrefs.GetInt(MUSIC_KEY, 1) == 1;
    public static bool IsHapticEnabled => PlayerPrefs.GetInt(HAPTIC_KEY, 1) == 1;

    public static event Action<bool> OnSoundToggled;
    public static event Action<bool> OnMusicToggled;
    public static event Action<bool> OnHapticToggled;

    protected override void Awake()
    {
        base.Awake();

        CacheDefaultSprites();

        // Close Buttons
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backdropButton != null && closeOnBackdropClick) backdropButton.onClick.AddListener(Close);

        // Toggle Buttons
        if (soundButton != null) soundButton.onClick.AddListener(ToggleSound);
        if (musicButton != null) musicButton.onClick.AddListener(ToggleMusic);
        if (hapticButton != null) hapticButton.onClick.AddListener(ToggleHaptic);

        // Navigation Buttons
        if (homeButton != null) homeButton.onClick.AddListener(OnHomeClicked);

        UpdateAllVisuals();
    }

    protected override void OnOpened()
    {
        base.OnOpened();

        // Pause gameplay (timer and input) while settings popup is open
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.SetPaused(true);
        }
    }

    protected override void OnClosed()
    {
        base.OnClosed();

        // Resume gameplay when settings popup closes
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.SetPaused(false);
        }
    }

    #region Toggles (Sound, Music, Haptic)

    public void ToggleSound()
    {
        SoundManager.PlayButtonSound();
        bool newState = !IsSoundEnabled;
        PlayerPrefs.SetInt(SOUND_KEY, newState ? 1 : 0);
        PlayerPrefs.Save();

        UpdateSoundVisual(newState);
        OnSoundToggled?.Invoke(newState);
    }

    public void ToggleMusic()
    {
        SoundManager.PlayButtonSound();
        bool newState = !IsMusicEnabled;
        PlayerPrefs.SetInt(MUSIC_KEY, newState ? 1 : 0);
        PlayerPrefs.Save();

        UpdateMusicVisual(newState);
        OnMusicToggled?.Invoke(newState);
    }

    public void ToggleHaptic()
    {
        SoundManager.PlayButtonSound();
        bool newState = !IsHapticEnabled;
        PlayerPrefs.SetInt(HAPTIC_KEY, newState ? 1 : 0);
        PlayerPrefs.Save();

        UpdateHapticVisual(newState);
        OnHapticToggled?.Invoke(newState);
    }

    private void UpdateAllVisuals()
    {
        UpdateSoundVisual(IsSoundEnabled);
        UpdateMusicVisual(IsMusicEnabled);
        UpdateHapticVisual(IsHapticEnabled);
    }

    private void UpdateSoundVisual(bool enabled)
    {
        if (soundIconImage == null) return;
        soundIconImage.sprite = enabled ? soundOnSprite : (soundOffSprite != null ? soundOffSprite : soundOnSprite);
    }

    private void UpdateMusicVisual(bool enabled)
    {
        if (musicIconImage == null) return;
        musicIconImage.sprite = enabled ? musicOnSprite : (musicOffSprite != null ? musicOffSprite : musicOnSprite);
    }

    private void UpdateHapticVisual(bool enabled)
    {
        if (hapticIconImage == null) return;
        hapticIconImage.sprite = enabled ? hapticOnSprite : (hapticOffSprite != null ? hapticOffSprite : hapticOnSprite);
    }

    private void CacheDefaultSprites()
    {
        if (soundIconImage != null && soundOnSprite == null)
        {
            soundOnSprite = soundIconImage.sprite;
        }

        if (musicIconImage != null && musicOnSprite == null)
        {
            musicOnSprite = musicIconImage.sprite;
        }

        if (hapticIconImage != null && hapticOnSprite == null)
        {
            hapticOnSprite = hapticIconImage.sprite;
        }
    }

    #endregion

    #region Navigation (Home, Restart)

    private void OnHomeClicked()
    {
        SoundManager.PlayButtonSound();

        if (homeButton != null) homeButton.interactable = false;

        // Seviye ortasında ayarlardan çıkıldığı için can iade hakkı düşer (can geri verilmez)
        LifeManager.ForfeitPendingRefund();

        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneTransition.LoadScene(mainMenuSceneName);
        }
        else
        {
            Debug.LogWarning($"[SettingsPopup] Scene '{mainMenuSceneName}' is not added to Build Settings or does not exist yet!");
        }
    }

    #endregion
}
