using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Master orchestrator for the Main Menu scene.
/// Reads player progression (CurrentLevel), coordinates LevelPathManager,
/// and handles Play button navigation to the Game scene via SceneTransition.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    public static MainMenuManager Instance { get; private set; }

    [Header("Level Data")]
    [Tooltip("Database holding all level prefabs.")]
    [SerializeField] private LevelDatabase levelDatabase;

    [Header("UI References")]
    [Tooltip("Level path manager controlling the 3 level badges.")]
    [SerializeField] private LevelPathManager levelPathManager;

    [Tooltip("Main Play button.")]
    [SerializeField] private Button playButton;

    [Tooltip("Optional settings button.")]
    [SerializeField] private Button settingsButton;

    [Header("Scene Navigation")]
    [Tooltip("Name of the core gameplay scene.")]
    [SerializeField] private string gameSceneName = "Game";

    private const string KEY_CURRENT_LEVEL = "CurrentLevel";

    public LevelDatabase LevelDatabase => levelDatabase != null ? levelDatabase : LevelDatabase.Instance;

    private void Awake()
    {
        Instance = this;

        AppBootstrapper.Initialize();
        AutoResolveReferences();
        SetupButtonListeners();
    }

    private void Start()
    {
        RefreshMenu();
    }

    private void SetupButtonListeners()
    {
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }
    }

    /// <summary>
    /// Reads the player's saved progression and updates the level path display.
    /// </summary>
    public void RefreshMenu()
    {
        int currentLevel = PlayerPrefs.GetInt(KEY_CURRENT_LEVEL, 1);

        if (levelPathManager != null)
        {
            levelPathManager.UpdateLevelPath(currentLevel);
        }
    }

    private void OnPlayClicked()
    {
        if (playButton != null)
        {
            playButton.transform.DOKill();
            playButton.transform.localScale = Vector3.one;
            playButton.transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 1, 0.5f);
        }

        // Smooth fade-to-black transition into the gameplay scene
        SceneTransition.LoadScene(gameSceneName);
    }

    private void OnSettingsClicked()
    {
        SettingsMainMenuPopup.Show();
    }

    private void AutoResolveReferences()
    {
        if (levelPathManager == null)
        {
            levelPathManager = FindFirstObjectByType<LevelPathManager>(FindObjectsInactive.Include);
        }

        if (playButton == null)
        {
            GameObject playGo = GameObject.Find("PlayButton") ?? GameObject.Find("Play");
            if (playGo != null) playButton = playGo.GetComponent<Button>();
        }

        if (settingsButton == null)
        {
            GameObject settingsGo = GameObject.Find("SettingsButton")
                ?? GameObject.Find("Settings")
                ?? GameObject.Find("MainMenuSettings");

            if (settingsGo != null)
            {
                settingsButton = settingsGo.GetComponentInChildren<Button>(true);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
