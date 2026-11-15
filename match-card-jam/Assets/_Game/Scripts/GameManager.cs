using System;
using UnityEngine;

/// <summary>
/// Primary manager for the Game scene.
/// Hosts scene-level services (InputManager, ProgressionUIManager),
/// initializes LevelManager, and listens to level outcomes via direct callbacks (delegation).
/// Contains no static event subscriptions or OnEnable/OnDisable couplings.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scene Subsystems")]
    [Tooltip("Input manager reference. If empty, searched in children or scene.")]
    [SerializeField] private InputManager inputManager;

    [Tooltip("Progression UI manager reference. If empty, searched in children or scene.")]
    [SerializeField] private ProgressionUIManager progressionUIManager;

    [Tooltip("Timer UI manager reference. If empty, searched in children or scene.")]
    [SerializeField] private TimerUIManager timerUIManager;

    [Tooltip("Powerup manager reference. If empty, searched in children or scene.")]
    [SerializeField] private PowerupManager powerupManager;

    [Header("Level Progression")]
    [Tooltip("Database holding all level prefabs.")]
    [SerializeField] private LevelDatabase levelDatabase;

    [Header("Active Level")]
    [Tooltip("Currently active level manager. If empty, searched in scene or instantiated from LevelDatabase.")]
    [SerializeField] private LevelManager currentLevelManager;

    public InputManager InputManager => inputManager;
    public ProgressionUIManager ProgressionUIManager => progressionUIManager;
    public TimerUIManager TimerUIManager => timerUIManager;
    public PowerupManager PowerupManager => powerupManager;
    public LevelManager CurrentLevelManager => currentLevelManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        AppBootstrapper.Initialize();
        ResolveSubsystems();
    }

    private void Start()
    {
        SoundManager.PlayMusic(MusicType.Gameplay, fadeDuration: 1.5f);
        InitializeActiveLevel();
        powerupManager?.Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Sahne alt sistemlerini (Input, UI) otomatik bulur ve bağlar.
    /// </summary>
    public void ResolveSubsystems()
    {
        if (inputManager == null)
        {
            inputManager = GetComponentInChildren<InputManager>(true);
            if (inputManager == null)
            {
                inputManager = FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
            }
        }

        if (progressionUIManager == null)
        {
            progressionUIManager = GetComponentInChildren<ProgressionUIManager>(true);
            if (progressionUIManager == null)
            {
                progressionUIManager = FindFirstObjectByType<ProgressionUIManager>(FindObjectsInactive.Include);
            }
        }

        if (timerUIManager == null)
        {
            timerUIManager = GetComponentInChildren<TimerUIManager>(true);
            if (timerUIManager == null)
            {
                timerUIManager = FindFirstObjectByType<TimerUIManager>(FindObjectsInactive.Include);
            }
        }

        if (powerupManager == null)
        {
            powerupManager = GetComponentInChildren<PowerupManager>(true);
            if (powerupManager == null)
            {
                powerupManager = FindFirstObjectByType<PowerupManager>(FindObjectsInactive.Include);
            }
        }
    }

    /// <summary>
    /// Sahnedeki veya dışarıdan verilen LevelManager'ı başlatır.
    /// Sahnede yoksa LevelDatabase'den mevcut seviyenin prefab'ini yaratır.
    /// Kazanma ve kaybetme fonksiyonları doğrudan parametre olarak aktarılır (sıfır statik event).
    /// </summary>
    public void InitializeActiveLevel(LevelManager levelManager = null)
    {
        if (levelManager != null)
        {
            currentLevelManager = levelManager;
        }

        // 1. Sahnede zaten bir LevelManager duruyor mu kontrol et (Editör test desteği)
        if (currentLevelManager == null)
        {
            currentLevelManager = FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);
        }

        // 2. Sahnede yoksa LevelDatabase'den mevcut seviyeyi masaya yarat
        if (currentLevelManager == null)
        {
            int currentLevel = GameData.CurrentLevel;
            GameObject levelPrefab = (levelDatabase != null)
                ? levelDatabase.GetLevelPrefab(currentLevel)
                : LevelDatabase.GetPrefab(currentLevel);

            if (levelPrefab != null)
            {
                GameObject spawnedLevel = Instantiate(levelPrefab);
                currentLevelManager = spawnedLevel.GetComponent<LevelManager>();
            }
        }

        if (currentLevelManager != null)
        {
            currentLevelManager.InitializeLevel(
                inputManager,
                progressionUIManager,
                timerUIManager,
                onWon: HandleLevelWon,
                onLost: HandleLevelLost
            );
        }
        else
        {
            Debug.LogWarning("[GameManager] Başlatılacak LevelManager sahnede bulunamadı ve LevelDatabase atanmamış!");
        }
    }

    private void HandleLevelWon()
    {
        Debug.Log("<color=#2ecc71><b>[GameManager]</b> Seviye başarıyla tamamlandı!</color>");

        // 1. Seviye kazanıldığı için eksilen can iade edilir
        GameData.RefundLife();

        // 2. Advance level progression
        GameData.AdvanceLevel();

        // Wait briefly for the last match animations to finish, then show the win popup
        DG.Tweening.DOVirtual.DelayedCall(0.5f, () =>
        {
            LevelWinPopup.Show();
        });
    }

    private void HandleLevelLost(LevelManager.LoseReason reason)
    {
        Debug.Log($"<color=#e74c3c><b>[GameManager]</b> Seviye kaybedildi. Sebep: {reason}</color>");

        // Seviye kaybedildiği için can iade hakkı düşer, can yenilenme süreci kesintisiz devam eder
        LifeManager.ForfeitPendingRefund();

        // Wait briefly for the last card dock animation or timer to settle, then show fail popup
        DG.Tweening.DOVirtual.DelayedCall(0.5f, () =>
        {
            LevelFailPopup.Show();
        });
    }

    /// <summary>
    /// Opens the Settings popup.
    /// </summary>
    public void OpenSettingsPopup()
    {
        SoundManager.PlayButtonSound();
        SettingsPopup.Show();
    }

    /// <summary>
    /// Opens the Restart Warning popup before reloading the level.
    /// Linked directly to HUD RestartButton onClick in GameCanvas inspector.
    /// </summary>
    public void RestartCurrentScene()
    {
        SoundManager.PlayButtonSound();
        RestartWarningPopup.Show();
    }
}
