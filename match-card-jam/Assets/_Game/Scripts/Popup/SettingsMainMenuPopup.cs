using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings popup dialog dedicated to the Main Menu.
/// Contains Sound, Music, and Haptic toggle controls (synchronized with the in-game SettingsPopup via PlayerPrefs),
/// a Quit button to exit the application, and a Close button.
/// Loaded automatically via SettingsMainMenuPopup.Show() from Resources/Popups/SettingsMainMenuPopup.
/// </summary>
[DisallowMultipleComponent]
public class SettingsMainMenuPopup : PopupBase<SettingsMainMenuPopup>
{
    private const string SOUND_KEY = "Setting_Sound";
    private const string MUSIC_KEY = "Setting_Music";
    private const string HAPTIC_KEY = "Setting_Haptic";

    [Header("Close & Backdrop Controls")]
    [Tooltip("Button used to close the popup.")]
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

    [Header("Application Quit")]
    [Tooltip("Button used to exit the application.")]
    [SerializeField] private Button quitButton;

    // Static query helpers shared across all subsystems
    public static bool IsSoundEnabled => PlayerPrefs.GetInt(SOUND_KEY, 1) == 1;
    public static bool IsMusicEnabled => PlayerPrefs.GetInt(MUSIC_KEY, 1) == 1;
    public static bool IsHapticEnabled => PlayerPrefs.GetInt(HAPTIC_KEY, 1) == 1;

    public static event Action<bool> OnSoundToggled;
    public static event Action<bool> OnMusicToggled;
    public static event Action<bool> OnHapticToggled;

    protected override void Awake()
    {
        base.Awake();

        AutoResolveReferences();
        CacheDefaultSprites();

        // Close Buttons
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backdropButton != null && closeOnBackdropClick) backdropButton.onClick.AddListener(Close);

        // Toggle Buttons
        if (soundButton != null) soundButton.onClick.AddListener(ToggleSound);
        if (musicButton != null) musicButton.onClick.AddListener(ToggleMusic);
        if (hapticButton != null) hapticButton.onClick.AddListener(ToggleHaptic);

        // Quit Button
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        UpdateAllVisuals();
    }

    private void Reset()
    {
        AutoResolveReferences();
    }

    #region Toggles (Sound, Music, Haptic)

    public void ToggleSound()
    {
        bool newState = !IsSoundEnabled;
        PlayerPrefs.SetInt(SOUND_KEY, newState ? 1 : 0);
        PlayerPrefs.Save();

        UpdateSoundVisual(newState);
        OnSoundToggled?.Invoke(newState);
    }

    public void ToggleMusic()
    {
        bool newState = !IsMusicEnabled;
        PlayerPrefs.SetInt(MUSIC_KEY, newState ? 1 : 0);
        PlayerPrefs.Save();

        UpdateMusicVisual(newState);
        OnMusicToggled?.Invoke(newState);
    }

    public void ToggleHaptic()
    {
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

    #region Quit Action

    private void OnQuitClicked()
    {
        if (quitButton != null)
        {
            quitButton.transform.DOKill();
            quitButton.transform.localScale = Vector3.one;
            quitButton.transform.DOPunchScale(Vector3.one * 0.12f, 0.2f, 1, 0.5f);
        }

        Debug.Log("[SettingsMainMenuPopup] Application quit requested.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Auto-Resolve

    private void AutoResolveReferences()
    {
        if (popupContent == null)
        {
            Transform t = transform.Find("inside") ?? transform.Find("Content") ?? transform.Find("Panel");
            if (t != null) popupContent = t as RectTransform;
        }

        if (backdropButton == null)
        {
            Transform t = transform.Find("Bg") ?? transform.Find("Backdrop");
            if (t != null) backdropButton = t.GetComponent<Button>();
        }

        if (closeButton == null)
        {
            Transform t = transform.Find("CloseButton") ?? transform.Find("inside/CloseButton");
            if (t != null) closeButton = t.GetComponent<Button>();
        }

        if (soundButton == null)
        {
            Transform t = transform.Find("Sound") ?? transform.Find("inside/Sound");
            if (t != null) soundButton = t.GetComponent<Button>();
        }

        if (soundIconImage == null)
        {
            Transform t = transform.Find("Sound/SoundIcon") ?? transform.Find("inside/Sound/SoundIcon");
            if (t != null) soundIconImage = t.GetComponent<Image>();
        }

        if (musicButton == null)
        {
            Transform t = transform.Find("Music") ?? transform.Find("inside/Music");
            if (t != null) musicButton = t.GetComponent<Button>();
        }

        if (musicIconImage == null)
        {
            Transform t = transform.Find("Music/MusicIcon") ?? transform.Find("inside/Music/MusicIcon");
            if (t != null) musicIconImage = t.GetComponent<Image>();
        }

        if (hapticButton == null)
        {
            Transform t = transform.Find("Haptic") ?? transform.Find("inside/Haptic");
            if (t != null) hapticButton = t.GetComponent<Button>();
        }

        if (hapticIconImage == null)
        {
            Transform t = transform.Find("Haptic/HapticIcon") ?? transform.Find("inside/Haptic/HapticIcon");
            if (t != null) hapticIconImage = t.GetComponent<Image>();
        }

        if (quitButton == null)
        {
            Transform t = transform.Find("Quit") ?? transform.Find("inside/Quit");
            if (t != null) quitButton = t.GetComponent<Button>();
        }
    }

    #endregion
}
