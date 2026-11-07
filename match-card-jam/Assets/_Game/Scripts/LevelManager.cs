using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Oyun seviyesinin yönetimini, alt sistemlerin (DockManager, InputManager) başlatılmasını,
/// MatchItemRoot altındaki nesnelerin taranmasını ve Kazanma / Kaybetme (Win / Lose) koşullarını yöneten ana yönetici.
/// </summary>
public class LevelManager : MonoBehaviour
{
    public enum LevelState
    {
        Playing,
        Paused,
        Won,
        Lost
    }

    public enum LoseReason
    {
        DockFull,
        TimeOut
    }

    public static LevelManager Instance { get; private set; }

    [Header("Board References")]
    [Tooltip("Tüm MatchItem nesnelerinin altında toplandığı ana Transform. Boşsa sahnede 'MatchItemRoot' aranır.")]
    [SerializeField] private Transform matchItemRoot;

    [Tooltip("MatchItemRoot altından taranan nesnelerin listesi.")]
    [SerializeField] private List<MatchItem> boardItems = new();

    [Header("Subsystem References")]
    [Tooltip("Dock yöneticisi. Boşsa sahnede otomatik aranır.")]
    [SerializeField] private DockManager dockManager;

    [Tooltip("Input yöneticisi. Boşsa sahnede otomatik aranır.")]
    [SerializeField] private InputManager inputManager;

    [Tooltip("Progression UI yöneticisi. Boşsa sahnede otomatik aranır.")]
    [SerializeField] private ProgressionUIManager progressionUIManager;

    [Tooltip("Timer UI yöneticisi. Boşsa sahnede otomatik aranır.")]
    [SerializeField] private TimerUIManager timerUIManager;

    [Header("Timer Settings")]
    [Tooltip("Süre sınırı olsun mu?")]
    [SerializeField] private bool useTimer = false;

    [Tooltip("Saniye cinsinden seviye süresi.")]
    [SerializeField] private float timeLimit = 120f;

    // Durum ve Takip Değişkenleri
    private LevelState currentState = LevelState.Playing;
    private int totalItemCount;
    private int remainingItemCount;
    private float currentTimer;
    private bool isInitialized;

    // GameManager veya dış sistemlerin doğrudan dinlemesi için callback'ler (Statik event yerine)
    private Action onLevelWonCallback;
    private Action<LoseReason> onLevelLostCallback;

    public LevelState CurrentState => currentState;
    public Transform MatchItemRoot => matchItemRoot;
    public IReadOnlyList<MatchItem> BoardItems => boardItems;
    public DockManager DockManager => dockManager;
    public InputManager InputManager => inputManager;
    public ProgressionUIManager ProgressionUIManager => progressionUIManager;
    public TimerUIManager TimerUIManager => timerUIManager;
    public int TotalItemCount => totalItemCount;
    public int RemainingItemCount => remainingItemCount;
    public float CurrentTimer => currentTimer;
    public bool UseTimer => useTimer;

    // Dış Sistemler İçin Olaylar (UI, Ses, Efekt vb.)
    public static event Action<LevelState> OnStateChanged;
    public static event Action<int, int> OnItemCountChanged; // (Kalan, Toplam)
    public static event Action<float> OnTimerTick;           // Kalan saniye
    public static event Action OnLevelWon;
    public static event Action<LoseReason> OnLevelLost;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // GameManager sahnede yoksa (izole test modu), kendi kendini başlat
        if (!isInitialized && GameManager.Instance == null)
        {
            InitializeLevel();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        DockManager.OnItemsMatched += HandleItemsMatched;
    }

    private void OnDisable()
    {
        DockManager.OnItemsMatched -= HandleItemsMatched;
    }

    private void Update()
    {
        if (currentState != LevelState.Playing) return;

        // Süre Takibi
        if (useTimer)
        {
            currentTimer -= Time.deltaTime;
            timerUIManager?.UpdateTimer(currentTimer);
            OnTimerTick?.Invoke(Mathf.Max(0f, currentTimer));

            if (currentTimer <= 0f)
            {
                currentTimer = 0f;
                timerUIManager?.UpdateTimer(0f);
                LoseLevel(LoseReason.TimeOut);
            }
        }
    }

    /// <summary>
    /// Seviyeyi, alt yöneticileri ve MatchItem nesnelerini açık bir sıralamayla başlatır.
    /// GameManager tarafından çağrıldığında InputManager, ProgressionUIManager, TimerUIManager ve Win/Lose callback'leri doğrudan aktarılır.
    /// </summary>
    public void InitializeLevel(
        InputManager inputMgr = null,
        ProgressionUIManager progressionUIMgr = null,
        TimerUIManager timerUIMgr = null,
        Action onWon = null,
        Action<LoseReason> onLost = null)
    {
        if (isInitialized) return;

        onLevelWonCallback = onWon;
        onLevelLostCallback = onLost;

        // 1. InputManager'ı al veya GameManager'dan çöz
        if (inputMgr != null) inputManager = inputMgr;
        if (inputManager == null && GameManager.Instance != null) inputManager = GameManager.Instance.InputManager;
        if (inputManager == null) inputManager = FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);

        // 2. ProgressionUIManager'ı al veya GameManager'dan çöz
        if (progressionUIMgr != null) progressionUIManager = progressionUIMgr;
        if (progressionUIManager == null && GameManager.Instance != null) progressionUIManager = GameManager.Instance.ProgressionUIManager;
        if (progressionUIManager == null) progressionUIManager = FindFirstObjectByType<ProgressionUIManager>(FindObjectsInactive.Include);

        // 3. TimerUIManager'ı al veya GameManager'dan çöz
        if (timerUIMgr != null) timerUIManager = timerUIMgr;
        if (timerUIManager == null && GameManager.Instance != null) timerUIManager = GameManager.Instance.TimerUIManager;
        if (timerUIManager == null) timerUIManager = FindFirstObjectByType<TimerUIManager>(FindObjectsInactive.Include);

        // 4. DockManager'ı bul (Önce kendi alt objelerinde, sonra sahnede)
        if (dockManager == null) dockManager = GetComponentInChildren<DockManager>(true);
        if (dockManager == null) dockManager = FindFirstObjectByType<DockManager>(FindObjectsInactive.Include);

        dockManager?.Initialize();
        inputManager?.Initialize();

        // 5. MatchItemRoot'u kontrol et (Önce kendi alt objelerinde, sonra sahnede)
        if (matchItemRoot == null)
        {
            Transform found = transform.Find("MatchItemRoot");
            if (found != null)
            {
                matchItemRoot = found;
            }
            else
            {
                GameObject foundRoot = GameObject.Find("MatchItemRoot");
                if (foundRoot != null)
                {
                    matchItemRoot = foundRoot.transform;
                }
            }
        }

        // 6. Süreyi Ayarla ve Timer UI'ı Başlat (useTimer kapalıysa bar otomatik gizlenir)
        currentTimer = timeLimit;
        timerUIManager?.Initialize(useTimer, currentTimer);

        // 7. Masadaki MatchItem nesnelerini tara ve başlat
        ScanBoardItems();

        // 8. Progression UI'ı masa verileriyle başlat
        progressionUIManager?.Initialize(remainingItemCount, totalItemCount);

        currentState = LevelState.Playing;
        OnStateChanged?.Invoke(currentState);
        OnItemCountChanged?.Invoke(remainingItemCount, totalItemCount);

        isInitialized = true;
    }

    /// <summary>
    /// MatchItemRoot altındaki tüm MatchItem nesnelerini listeye alır ve her birini initialize eder.
    /// </summary>
    [ContextMenu("Scan & Collect MatchItems")]
    public void ScanBoardItems()
    {
        boardItems.Clear();

        Transform root = matchItemRoot != null ? matchItemRoot : transform;
        MatchItem[] foundItems = root.GetComponentsInChildren<MatchItem>(true);

        for (int i = 0; i < foundItems.Length; i++)
        {
            MatchItem item = foundItems[i];
            if (item != null)
            {
                // LevelManager her MatchItem'ı açıkça initialize eder
                item.Initialize();

                if (item.gameObject.activeInHierarchy && item.CurrentState == MatchItem.ItemState.InPhysics)
                {
                    boardItems.Add(item);
                }
            }
        }

        totalItemCount = boardItems.Count;
        remainingItemCount = boardItems.Count;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// DockManager'dan 3'lü eşleşme sinyali geldiğinde tetiklenir.
    /// </summary>
    private void HandleItemsMatched(int itemId, Vector3 matchPos)
    {
        if (currentState != LevelState.Playing) return;

        remainingItemCount = Mathf.Max(0, remainingItemCount - 3);
        progressionUIManager?.UpdateProgress(remainingItemCount, totalItemCount);
        OnItemCountChanged?.Invoke(remainingItemCount, totalItemCount);

        // Zafer Kontrolü (Masada toplanacak nesne kalmadıysa)
        if (remainingItemCount <= 0)
        {
            CheckWinCondition();
        }
    }

    /// <summary>
    /// Tüm nesneler toplandıktan sonra dock'taki son hareketlerin bitişini bekleyip zaferi ilan eder.
    /// </summary>
    public void CheckWinCondition()
    {
        if (currentState != LevelState.Playing) return;

        // Masada nesne kalmadı ve dock'ta başka bekleyen/uçan nesne kalmadıysa: KAZANDIK!
        if (remainingItemCount <= 0 && (dockManager == null || (dockManager.CurrentItemCount == 0 && dockManager.ActiveMovingCount == 0)))
        {
            WinLevel();
        }
    }

    public void WinLevel()
    {
        if (currentState != LevelState.Playing) return;

        currentState = LevelState.Won;
        onLevelWonCallback?.Invoke();
        OnStateChanged?.Invoke(currentState);
        OnLevelWon?.Invoke();

        Debug.Log("<color=#2ecc71><b>[LevelManager]</b> TEBRİKLER! Tüm masa temizlendi, seviye kazanıldı!</color>");
    }

    public void LoseLevel(LoseReason reason)
    {
        if (currentState != LevelState.Playing) return;

        currentState = LevelState.Lost;
        onLevelLostCallback?.Invoke(reason);
        OnStateChanged?.Invoke(currentState);
        OnLevelLost?.Invoke(reason);

        Debug.Log($"<color=#e74c3c><b>[LevelManager]</b> OYUN BİTTİ! Sebep: {reason}</color>");
    }

    public void SetPaused(bool paused)
    {
        if (currentState == LevelState.Won || currentState == LevelState.Lost) return;

        currentState = paused ? LevelState.Paused : LevelState.Playing;
        OnStateChanged?.Invoke(currentState);
    }
}
