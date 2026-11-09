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

    [Header("Life UI References")]
    [Tooltip("Text inside the heart icon showing current life count.")]
    [SerializeField] private TMPro.TextMeshProUGUI lifeCountText;

    [Tooltip("Text next to the heart icon showing regen countdown timer or FULL.")]
    [SerializeField] private TMPro.TextMeshProUGUI lifeTimeText;

    [Tooltip("Green plus icon next to the life container.")]
    [SerializeField] private GameObject plusIcon;

    [Header("Economy UI References")]
    [Tooltip("Text displaying current player coin balance.")]
    [SerializeField] private TMPro.TextMeshProUGUI coinText;

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
        SetupLifeListeners();
    }

    private void Start()
    {
        SoundManager.PlayMusic(MusicType.MainMenu, fadeDuration: 1.5f);

        RefreshMenu();
        UpdateLifeDisplay(GameData.Lives);
        UpdateTimerDisplay(GameData.RemainingLifeRegenSeconds);
        UpdateCoinDisplay(GameData.Coins);
    }

    private void SetupLifeListeners()
    {
        GameData.OnLivesChanged += UpdateLifeDisplay;
        GameData.OnLifeTimerTick += UpdateTimerDisplay;
        GameData.OnCoinsChanged += UpdateCoinDisplay;
    }

    private void UpdateCoinDisplay(int coins)
    {
        if (coinText != null)
        {
            coinText.text = coins.ToString("N0");
        }
    }

    private void UpdateLifeDisplay(int lives)
    {
        if (lifeCountText != null)
        {
            lifeCountText.text = lives.ToString();
        }

        bool isFull = lives >= LifeManager.MAX_LIVES;
        if (plusIcon != null)
        {
            plusIcon.SetActive(!isFull);
        }

        if (isFull && lifeTimeText != null)
        {
            lifeTimeText.text = "FULL";
        }
    }

    private void UpdateTimerDisplay(float remainingSeconds)
    {
        if (lifeTimeText == null) return;

        if (LifeManager.IsFull)
        {
            lifeTimeText.text = "FULL";
            return;
        }

        int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
        int seconds = Mathf.FloorToInt(remainingSeconds % 60f);
        lifeTimeText.text = $"{minutes:00}:{seconds:00}";
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
        if (levelPathManager != null)
        {
            levelPathManager.UpdateLevelPath(GameData.CurrentLevel);
        }
    }

    private void OnPlayClicked()
    {
        SoundManager.PlayButtonSound();

        if (playButton != null)
        {
            playButton.transform.DOKill();
            playButton.transform.localScale = Vector3.one;
            playButton.transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 1, 0.5f);
        }

        // 1. Can 0 Kontrolü: Seviyeye giriş engellenir ve NoLivesLeftPopup açılır
        if (!LifeManager.HasLives)
        {
            if (playButton != null)
            {
                playButton.transform.DOShakePosition(0.35f, 12f, 14, 90f, false, true);
            }
            NoLivesLeftPopup.Show();
            return;
        }

        // 2. Sahne kararma geçişi sırasında UI'ın (özellikle FULL yazısının aniden süreye) değişmesini dondur
        LifeManager.OnLivesChanged -= UpdateLifeDisplay;
        LifeManager.OnTimerTick -= UpdateTimerDisplay;

        // 3. Can 1 eksiltilir ve seviyeye girilir
        if (LifeManager.TryConsumeLife())
        {
            SceneTransition.LoadScene(gameSceneName);
        }
    }

    private void OnSettingsClicked()
    {
        SoundManager.PlayButtonSound();
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

        // Auto-resolve Life UI elements if not explicitly assigned
        if (lifeCountText == null)
        {
            GameObject countGo = GameObject.Find("LifeIcon/Text (TMP)");
            if (countGo != null)
            {
                lifeCountText = countGo.GetComponent<TMPro.TextMeshProUGUI>();
            }
            else
            {
                GameObject lifeIconGo = GameObject.Find("LifeIcon");
                if (lifeIconGo != null)
                {
                    lifeCountText = lifeIconGo.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                }
            }
        }

        if (lifeTimeText == null)
        {
            GameObject timeGo = GameObject.Find("LifeTimeText");
            if (timeGo != null)
            {
                lifeTimeText = timeGo.GetComponent<TMPro.TextMeshProUGUI>();
            }
        }

        if (plusIcon == null)
        {
            plusIcon = GameObject.Find("PlusIconImg");
        }

        if (coinText == null)
        {
            GameObject coinGo = GameObject.Find("Coin/Bg/Text (TMP)");
            if (coinGo != null)
            {
                coinText = coinGo.GetComponent<TMPro.TextMeshProUGUI>();
            }
        }
    }

    private void OnDestroy()
    {
        GameData.OnLivesChanged -= UpdateLifeDisplay;
        GameData.OnLifeTimerTick -= UpdateTimerDisplay;
        GameData.OnCoinsChanged -= UpdateCoinDisplay;

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
