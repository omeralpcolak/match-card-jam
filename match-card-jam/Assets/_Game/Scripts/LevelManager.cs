using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages level lifecycle, initializes subsystems (DockManager, InputManager),
/// scans objects under MatchItemRoot, and manages Win / Lose conditions.
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
        TimeOut,
        BombExploded
    }

    public static LevelManager Instance { get; private set; }

    [Header("Board References")]
    [Tooltip("Main Transform holding all MatchItem objects. If empty, searches scene for 'MatchItemRoot'.")]
    [SerializeField] private Transform matchItemRoot;

    [Tooltip("List of MatchItem objects scanned under MatchItemRoot.")]
    [SerializeField] private List<MatchItem> boardItems = new();

    [Header("Subsystem References")]
    [Tooltip("Dock manager reference. If null, automatically located in scene.")]
    [SerializeField] private DockManager dockManager;

    [Tooltip("Input manager reference. If null, automatically located in scene.")]
    [SerializeField] private InputManager inputManager;

    [Tooltip("Progression UI manager reference. If null, automatically located in scene.")]
    [SerializeField] private ProgressionUIManager progressionUIManager;

    [Tooltip("Timer UI manager reference. If null, automatically located in scene.")]
    [SerializeField] private TimerUIManager timerUIManager;

    [Header("Timer Settings")]
    [Tooltip("Whether this level has a time limit.")]
    [SerializeField] private bool useTimer = false;

    [Tooltip("Level duration in seconds.")]
    [SerializeField] private float timeLimit = 120f;

    // State and Tracking Variables
    private LevelState currentState = LevelState.Playing;
    private int totalItemCount;
    private int remainingItemCount;
    private float currentTimer;
    private bool isTimerFrozen;
    private bool isInitialized;

    // Direct callbacks for GameManager or external systems (instead of static events)
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
    public bool IsTimerFrozen => isTimerFrozen;

    // Events for external systems (UI, Audio, VFX, etc.)
    public static event Action<LevelState> OnStateChanged;
    public static event Action<int, int> OnItemCountChanged; // (Remaining, Total)
    public static event Action<float> OnTimerTick;           // Remaining seconds
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
        // If GameManager is not present in scene (isolated test mode), self-initialize
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

        // Timer tracking
        if (useTimer && !isTimerFrozen)
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
    /// Initializes level, subsystems, and MatchItem objects in an explicit order.
    /// When invoked by GameManager, InputManager, ProgressionUIManager, TimerUIManager, and Win/Lose callbacks are injected directly.
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

        // 1. Resolve or assign InputManager
        if (inputMgr != null) inputManager = inputMgr;
        if (inputManager == null && GameManager.Instance != null) inputManager = GameManager.Instance.InputManager;
        if (inputManager == null) inputManager = FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);

        // 2. Resolve or assign ProgressionUIManager
        if (progressionUIMgr != null) progressionUIManager = progressionUIMgr;
        if (progressionUIManager == null && GameManager.Instance != null) progressionUIManager = GameManager.Instance.ProgressionUIManager;
        if (progressionUIManager == null) progressionUIManager = FindFirstObjectByType<ProgressionUIManager>(FindObjectsInactive.Include);

        // 3. Resolve or assign TimerUIManager
        if (timerUIMgr != null) timerUIManager = timerUIMgr;
        if (timerUIManager == null && GameManager.Instance != null) timerUIManager = GameManager.Instance.TimerUIManager;
        if (timerUIManager == null) timerUIManager = FindFirstObjectByType<TimerUIManager>(FindObjectsInactive.Include);

        // 4. Locate DockManager (first in children, then in scene)
        if (dockManager == null) dockManager = GetComponentInChildren<DockManager>(true);
        if (dockManager == null) dockManager = FindFirstObjectByType<DockManager>(FindObjectsInactive.Include);

        dockManager?.Initialize();
        inputManager?.Initialize();

        // 5. Locate MatchItemRoot (first in children, then in scene)
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

        // 6. Configure timer and initialize Timer UI (bar auto-hides if useTimer is false)
        isTimerFrozen = false;
        currentTimer = timeLimit;
        timerUIManager?.Initialize(useTimer, currentTimer);

        // 7. Scan and initialize MatchItem objects on board
        ScanBoardItems();

        // 8. Initialize Progression UI with board data
        progressionUIManager?.Initialize(remainingItemCount, totalItemCount);

        currentState = LevelState.Playing;
        OnStateChanged?.Invoke(currentState);
        OnItemCountChanged?.Invoke(remainingItemCount, totalItemCount);

        isInitialized = true;
    }

    /// <summary>
    /// Pauses or resumes the level countdown timer and updates TimerUIManager visuals.
    /// </summary>
    public void SetTimerFrozen(bool frozen)
    {
        isTimerFrozen = frozen;
        timerUIManager?.SetFrozen(frozen);
    }

    /// <summary>
    /// Collects all MatchItem objects under MatchItemRoot into list and initializes each.
    /// </summary>
    [ContextMenu("Scan & Collect MatchItems")]
    public void ScanBoardItems()
    {
        boardItems.Clear();

        Transform root = matchItemRoot != null ? matchItemRoot : transform;
        MatchItem[] foundItems = root.GetComponentsInChildren<MatchItem>(true);

        int matchableCount = 0;
        for (int i = 0; i < foundItems.Length; i++)
        {
            MatchItem item = foundItems[i];
            if (item != null)
            {
                // Explicitly initialize each MatchItem
                item.Initialize();

                if (item.gameObject.activeInHierarchy && item.CurrentState == MatchItem.ItemState.InPhysics)
                {
                    boardItems.Add(item);
                    if (!item.IsBomb)
                    {
                        matchableCount++;
                    }
                }
            }
        }

        totalItemCount = matchableCount;
        remainingItemCount = matchableCount;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// Triggered when DockManager reports a 3-item match.
    /// </summary>
    private void HandleItemsMatched(int itemId, Vector3 matchPos)
    {
        if (currentState != LevelState.Playing) return;

        remainingItemCount = Mathf.Max(0, remainingItemCount - 3);
        progressionUIManager?.PlayMatchFlyAndUpdateProgress(matchPos, remainingItemCount, totalItemCount);
        OnItemCountChanged?.Invoke(remainingItemCount, totalItemCount);

        // Victory check (if no remaining items to collect on board)
        if (remainingItemCount <= 0)
        {
            CheckWinCondition();
        }
    }

    /// <summary>
    /// Waits for remaining movements in dock after all board items collected, then declares victory.
    /// </summary>
    public void CheckWinCondition()
    {
        if (currentState != LevelState.Playing) return;

        // If no items remain on board and dock is completely clear of pending items: WIN!
        if (remainingItemCount <= 0 && (dockManager == null || (dockManager.NonBombItemCount == 0 && dockManager.ActiveMovingCount == 0)))
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

        Debug.Log("<color=#2ecc71><b>[LevelManager]</b> CONGRATULATIONS! Board cleared, level won!</color>");
    }

    public void LoseLevel(LoseReason reason)
    {
        if (currentState != LevelState.Playing) return;

        currentState = LevelState.Lost;
        onLevelLostCallback?.Invoke(reason);
        OnStateChanged?.Invoke(currentState);
        OnLevelLost?.Invoke(reason);

        Debug.Log($"<color=#e74c3c><b>[LevelManager]</b> GAME OVER! Reason: {reason}</color>");
    }

    public void SetPaused(bool paused)
    {
        if (currentState == LevelState.Won || currentState == LevelState.Lost) return;

        currentState = paused ? LevelState.Paused : LevelState.Playing;
        OnStateChanged?.Invoke(currentState);
    }
}
