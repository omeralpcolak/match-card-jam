using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Centralized sound and music manager for the entire game.
/// Persists across scenes (DontDestroyOnLoad), manages SFX pooling,
/// provides seamless music crossfading, and automatically wires button click sounds.
/// </summary>
[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    private const string RESOURCE_PATH = "SoundManager";
    private static SoundManager instance;

    [System.Serializable]
    public class SoundConfig
    {
        public SoundType soundType;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.8f, 1.2f)] public float minPitch = 1f;
        [Range(0.8f, 1.2f)] public float maxPitch = 1f;
    }

    [System.Serializable]
    public class MusicConfig
    {
        public MusicType musicType;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.6f;
    }

    [Header("Configurations")]
    [SerializeField] private List<SoundConfig> soundConfigs = new();
    [SerializeField] private List<MusicConfig> musicConfigs = new();

    [Header("Pool Settings")]
    [SerializeField] private int initialSfxPoolSize = 8;

    private readonly List<AudioSource> sfxPool = new();
    private AudioSource musicSourceA;
    private AudioSource musicSourceB;
    private AudioSource activeMusicSource;
    private Tween musicFadeTween;
    private Tween musicDuckTween;
    private float currentMusicTargetVolume = 0.5f;

    private readonly Dictionary<SoundType, SoundConfig> soundConfigMap = new();
    private readonly Dictionary<MusicType, MusicConfig> musicConfigMap = new();

    private float lastButtonClickTime = -1f;
    private const float BUTTON_CLICK_COOLDOWN = 0.05f;

    /// <summary>
    /// Guarantees SoundManager is automatically instantiated before any scene loads.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    public static SoundManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SoundManager>();
                if (instance == null)
                {
                    SoundManager prefab = Resources.Load<SoundManager>(RESOURCE_PATH);
                    if (prefab != null)
                    {
                        instance = Instantiate(prefab);
                        instance.name = "SoundManager";
                    }
                    else
                    {
                        GameObject go = new GameObject("SoundManager");
                        instance = go.AddComponent<SoundManager>();
                    }
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        BuildLookups();
        InitializeAudioSources();
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            UnsubscribeFromEvents();
            instance = null;
        }
    }

    private void BuildLookups()
    {
        soundConfigMap.Clear();
        foreach (var cfg in soundConfigs)
        {
            if (cfg != null && !soundConfigMap.ContainsKey(cfg.soundType))
            {
                soundConfigMap.Add(cfg.soundType, cfg);
            }
        }

        musicConfigMap.Clear();
        foreach (var cfg in musicConfigs)
        {
            if (cfg != null && !musicConfigMap.ContainsKey(cfg.musicType))
            {
                musicConfigMap.Add(cfg.musicType, cfg);
            }
        }
    }

    private void InitializeAudioSources()
    {
        // SFX pool
        for (int i = 0; i < initialSfxPoolSize; i++)
        {
            CreateSfxSource();
        }

        // Dual Music sources for smooth crossfading
        musicSourceA = gameObject.AddComponent<AudioSource>();
        musicSourceA.playOnAwake = false;
        musicSourceA.loop = true;

        musicSourceB = gameObject.AddComponent<AudioSource>();
        musicSourceB.playOnAwake = false;
        musicSourceB.loop = true;

        activeMusicSource = musicSourceA;
    }

    private AudioSource CreateSfxSource()
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        sfxPool.Add(src);
        return src;
    }

    private void SubscribeToEvents()
    {
        SettingsPopup.OnSoundToggled += HandleSoundToggled;
        SettingsPopup.OnMusicToggled += HandleMusicToggled;
        SettingsMainMenuPopup.OnSoundToggled += HandleSoundToggled;
        SettingsMainMenuPopup.OnMusicToggled += HandleMusicToggled;
    }

    private void UnsubscribeFromEvents()
    {
        SettingsPopup.OnSoundToggled -= HandleSoundToggled;
        SettingsPopup.OnMusicToggled -= HandleMusicToggled;
        SettingsMainMenuPopup.OnSoundToggled -= HandleSoundToggled;
        SettingsMainMenuPopup.OnMusicToggled -= HandleMusicToggled;
    }

    #region Settings Change Handlers

    private void HandleSoundToggled(bool isEnabled)
    {
        if (!isEnabled)
        {
            StopAllSfx();
        }
    }

    private void HandleMusicToggled(bool isEnabled)
    {
        if (isEnabled)
        {
            if (activeMusicSource != null && activeMusicSource.clip != null && !activeMusicSource.isPlaying)
            {
                activeMusicSource.Play();
            }
        }
        else
        {
            if (activeMusicSource != null && activeMusicSource.isPlaying)
            {
                activeMusicSource.Pause();
            }
        }
    }

    #endregion

    #region SFX Playback

    public static void PlaySound(SoundType type, float volumeMultiplier = 1f)
    {
        Instance.PlaySoundInternal(type, volumeMultiplier);
    }

    public static void PlaySound(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        Instance.PlaySoundInternal(clip, volume, pitch);
    }

    private void PlaySoundInternal(SoundType type, float volumeMultiplier)
    {
        if (!GameData.IsSoundEnabled) return;

        if (!soundConfigMap.TryGetValue(type, out SoundConfig cfg) || cfg.clip == null)
        {
            return;
        }

        float pitch = (cfg.minPitch == cfg.maxPitch) ? cfg.minPitch : UnityEngine.Random.Range(cfg.minPitch, cfg.maxPitch);
        float volume = cfg.volume * volumeMultiplier;

        PlaySoundInternal(cfg.clip, volume, pitch);
    }

    private void PlaySoundInternal(AudioClip clip, float volume, float pitch)
    {
        if (!GameData.IsSoundEnabled || clip == null) return;

        AudioSource src = GetAvailableSfxSource();
        src.pitch = pitch;
        src.volume = Mathf.Clamp01(volume);
        src.clip = clip;
        src.Play();
    }

    private AudioSource GetAvailableSfxSource()
    {
        for (int i = 0; i < sfxPool.Count; i++)
        {
            if (!sfxPool[i].isPlaying)
            {
                return sfxPool[i];
            }
        }

        // Expand pool if necessary
        return CreateSfxSource();
    }

    public static void StopAllSfx()
    {
        if (instance == null) return;
        for (int i = 0; i < instance.sfxPool.Count; i++)
        {
            instance.sfxPool[i].Stop();
        }
    }

    #endregion

    #region Music Playback (Crossfade)

    public static void PlayMusic(MusicType type, float fadeDuration = 1.5f, bool loop = true)
    {
        Instance.PlayMusicInternal(type, fadeDuration, loop);
    }

    public static void PlayMusic(AudioClip clip, float targetVolume = 0.6f, float fadeDuration = 1.5f, bool loop = true)
    {
        if (clip == null) return;
        Instance.PlayMusicInternal(clip, targetVolume, fadeDuration, loop);
    }

    private void PlayMusicInternal(MusicType type, float fadeDuration, bool loop)
    {
        if (!musicConfigMap.TryGetValue(type, out MusicConfig cfg) || cfg.clip == null)
        {
            BuildLookups();
            if (!musicConfigMap.TryGetValue(type, out cfg) || cfg.clip == null)
            {
                Debug.LogWarning($"[SoundManager] MusicConfig for '{type}' not found or clip is null!");
                return;
            }
        }

        PlayMusicInternal(cfg.clip, cfg.volume, fadeDuration, loop);
    }

    private void PlayMusicInternal(AudioClip clip, float targetVolume, float fadeDuration, bool loop)
    {
        if (clip == null) return;

        currentMusicTargetVolume = targetVolume;
        musicDuckTween?.Kill();

        // If same clip is already playing on active source, do nothing
        if (activeMusicSource != null && activeMusicSource.isPlaying && activeMusicSource.clip == clip)
        {
            return;
        }

        musicFadeTween?.Kill();

        AudioSource incomingSource = (activeMusicSource == musicSourceA) ? musicSourceB : musicSourceA;
        AudioSource outgoingSource = activeMusicSource;

        incomingSource.clip = clip;
        incomingSource.loop = loop;
        incomingSource.volume = 0f;

        if (GameData.IsMusicEnabled)
        {
            incomingSource.Play();
        }

        activeMusicSource = incomingSource;

        if (fadeDuration <= 0f)
        {
            if (outgoingSource != null)
            {
                outgoingSource.Stop();
                outgoingSource.volume = 0f;
            }
            incomingSource.volume = targetVolume;
            return;
        }

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        if (outgoingSource != null && outgoingSource.isPlaying)
        {
            seq.Join(outgoingSource.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() => outgoingSource.Stop()));
        }

        seq.Join(incomingSource.DOFade(targetVolume, fadeDuration).SetUpdate(true));
        musicFadeTween = seq;
    }

    /// <summary>
    /// Smoothly lowers (ducks) the active music volume (e.g. during Win/Fail fanfare).
    /// </summary>
    public static void DuckMusic(float duckMultiplier = 0.2f, float duration = 0.5f)
    {
        if (instance == null || instance.activeMusicSource == null) return;

        instance.musicDuckTween?.Kill();
        float target = instance.currentMusicTargetVolume * duckMultiplier;
        instance.musicDuckTween = instance.activeMusicSource.DOFade(target, duration).SetUpdate(true);
    }

    /// <summary>
    /// Restores the active music volume back to its normal level after being ducked.
    /// </summary>
    public static void UnduckMusic(float duration = 0.8f)
    {
        if (instance == null || instance.activeMusicSource == null) return;

        instance.musicDuckTween?.Kill();
        if (GameData.IsMusicEnabled)
        {
            instance.musicDuckTween = instance.activeMusicSource.DOFade(instance.currentMusicTargetVolume, duration).SetUpdate(true);
        }
    }

    public static void StopMusic(float fadeDuration = 0.5f)
    {
        if (instance == null || instance.activeMusicSource == null) return;

        instance.musicFadeTween?.Kill();
        if (fadeDuration <= 0f)
        {
            instance.activeMusicSource.Stop();
            instance.activeMusicSource.volume = 0f;
        }
        else
        {
            instance.musicFadeTween = instance.activeMusicSource.DOFade(0f, fadeDuration)
                .SetUpdate(true)
                .OnComplete(() => instance.activeMusicSource.Stop());
        }
    }

    public static void PauseMusic()
    {
        if (instance == null || instance.activeMusicSource == null) return;
        instance.activeMusicSource.Pause();
    }

    public static void ResumeMusic()
    {
        if (instance == null || instance.activeMusicSource == null) return;
        if (GameData.IsMusicEnabled && !instance.activeMusicSource.isPlaying)
        {
            instance.activeMusicSource.UnPause();
        }
    }

    #endregion

    #region Automated Button Click Registration

    /// <summary>
    /// Plays the standard UI button click sound.
    /// Call this inside UI button onClick callbacks.
    /// </summary>
    public static void PlayButtonSound()
    {
        if (Time.unscaledTime - Instance.lastButtonClickTime < BUTTON_CLICK_COOLDOWN)
        {
            return;
        }

        Instance.lastButtonClickTime = Time.unscaledTime;
        PlaySound(SoundType.ButtonClick);
    }

    #endregion

#if UNITY_EDITOR
    private void Reset()
    {
        PopulateDefaultClips();
    }

    [ContextMenu("Populate Default Clips")]
    public void PopulateDefaultClips()
    {
        if (soundConfigs == null) soundConfigs = new List<SoundConfig>();
        if (soundConfigs.Count == 0)
        {
            AudioClip btnClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audios/Sounds/button_sound.mp3");
            AudioClip winClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audios/Sounds/level_complete_sound.mp3");
            AudioClip failClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audios/Sounds/level_fail_sound.mp3");

            soundConfigs.Add(new SoundConfig { soundType = SoundType.ButtonClick, clip = btnClip, volume = 1f, minPitch = 0.98f, maxPitch = 1.02f });
            soundConfigs.Add(new SoundConfig { soundType = SoundType.LevelComplete, clip = winClip, volume = 1f, minPitch = 1f, maxPitch = 1f });
            soundConfigs.Add(new SoundConfig { soundType = SoundType.LevelFail, clip = failClip, volume = 1f, minPitch = 1f, maxPitch = 1f });
        }

        if (musicConfigs == null) musicConfigs = new List<MusicConfig>();
        if (musicConfigs.Count == 0)
        {
            AudioClip menuMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audios/Musics/main_menu_music.mp3");
            AudioClip gameMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audios/Musics/gameplay_music.mp3");

            if (menuMusic != null)
            {
                musicConfigs.Add(new MusicConfig { musicType = MusicType.MainMenu, clip = menuMusic, volume = 0.5f });
            }
            if (gameMusic != null)
            {
                musicConfigs.Add(new MusicConfig { musicType = MusicType.Gameplay, clip = gameMusic, volume = 0.45f });
            }
        }
    }
#endif
}
