using System;
using UnityEngine;

/// <summary>
/// Game sahnesinin ana yöneticisi.
/// Sahne düzeyindeki servisleri (InputManager, ProgressionUIManager) barındırır,
/// LevelManager'ı başlatır ve seviyenin sonuçlarını doğrudan callback (delegation) ile dinler.
/// Statik event veya OnEnable/OnDisable aboneliği içermez.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scene Subsystems")]
    [Tooltip("Input yöneticisi. Boşsa alt objelerde veya sahnede aranır.")]
    [SerializeField] private InputManager inputManager;

    [Tooltip("Progression UI yöneticisi. Boşsa alt objelerde veya sahnede aranır.")]
    [SerializeField] private ProgressionUIManager progressionUIManager;

    [Tooltip("Timer UI yöneticisi. Boşsa alt objelerde veya sahnede aranır.")]
    [SerializeField] private TimerUIManager timerUIManager;

    [Header("Level Progression")]
    [Tooltip("Database holding all level prefabs.")]
    [SerializeField] private LevelDatabase levelDatabase;

    [Header("Active Level")]
    [Tooltip("Şu anki aktif seviye yöneticisi. Boşsa sahnede aranır veya LevelDatabase'den yaratılır.")]
    [SerializeField] private LevelManager currentLevelManager;

    public InputManager InputManager => inputManager;
    public ProgressionUIManager ProgressionUIManager => progressionUIManager;
    public TimerUIManager TimerUIManager => timerUIManager;
    public LevelManager CurrentLevelManager => currentLevelManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ResolveSubsystems();
    }

    private void Start()
    {
        InitializeActiveLevel();
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
            int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
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

        // Advance level progression
        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        PlayerPrefs.SetInt("CurrentLevel", currentLevel + 1);
        PlayerPrefs.Save();

        // Wait briefly for the last match animations to finish, then show the win popup
        DG.Tweening.DOVirtual.DelayedCall(0.5f, () =>
        {
            LevelWinPopup.Show();
        });
    }

    private void HandleLevelLost(LevelManager.LoseReason reason)
    {
        Debug.Log($"<color=#e74c3c><b>[GameManager]</b> Seviye kaybedildi. Sebep: {reason}</color>");
    }

    /// <summary>
    /// Opens the Settings popup.
    /// </summary>
    public void OpenSettingsPopup()
    {
        SettingsPopup.Show();
    }

    /// <summary>
    /// Reloads the active game scene using a smooth fade transition.
    /// Can be linked directly to HUD RestartButton onClick in inspector.
    /// </summary>
    public void RestartCurrentScene()
    {
        SceneTransition.ReloadCurrentScene();
    }
}
