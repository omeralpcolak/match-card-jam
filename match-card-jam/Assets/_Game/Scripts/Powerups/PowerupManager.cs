using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages spawning PowerupUiItem buttons from PowerupDatabase (sorted by UnlockLevel)
/// and coordinates power-up activation in the Game scene.
/// Initialized explicitly by GameManager; avoids Awake/Start/OnEnable/OnDisable hooks.
/// </summary>
public class PowerupManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Prefab containing a PowerupUiItem component.")]
    [SerializeField] private PowerupUiItem _itemPrefab;

    [Tooltip("Parent Transform where PowerupUiItem instances will be spawned.")]
    [SerializeField] private Transform _itemContainer;

    [Tooltip("Optional parent Transform for spawned PowerupBase instances. Defaults to this Transform.")]
    [SerializeField] private Transform _activePowerupRoot;

    private readonly List<PowerupUiItem> _spawnedItems = new();
    private readonly HashSet<PowerupType> _activeTypes = new();
    private readonly List<PowerupBase> _activeInstances = new();

    private void OnDestroy()
    {
        GameData.OnPowerupCountChanged -= HandlePowerupCountChanged;
        CleanupActivePowerups();
    }

    /// <summary>
    /// Initializes the power-up system, subscribes to data events, and builds the UI items.
    /// Called explicitly by GameManager.
    /// </summary>
    public void Initialize()
    {
        GameData.OnPowerupCountChanged -= HandlePowerupCountChanged;
        GameData.OnPowerupCountChanged += HandlePowerupCountChanged;

        BuildPowerupUI();
    }

    /// <summary>
    /// Reads power-ups from PowerupDatabase, sorts them by UnlockLevel,
    /// and instantiates PowerupUiItem buttons under _itemContainer.
    /// </summary>
    public void BuildPowerupUI()
    {
        if (_itemPrefab == null || _itemContainer == null)
        {
            Debug.LogError("[PowerupManager] _itemPrefab or _itemContainer is not assigned!");
            return;
        }

        ClearSpawnedItems();

        PowerupDatabase db = PowerupDatabase.Instance;
        if (db == null || db.Powerups == null || db.Powerups.Count == 0)
        {
            Debug.LogWarning("[PowerupManager] PowerupDatabase is empty or missing.");
            return;
        }

        List<PowerupDataSO> sortedList = new();
        for (int i = 0; i < db.Powerups.Count; i++)
        {
            if (db.Powerups[i] != null)
            {
                sortedList.Add(db.Powerups[i]);
            }
        }

        sortedList.Sort((a, b) => a.UnlockLevel.CompareTo(b.UnlockLevel));

        for (int i = 0; i < sortedList.Count; i++)
        {
            PowerupUiItem item = Instantiate(_itemPrefab, _itemContainer);
            item.name = $"PowerupUiItem_{sortedList[i].PowerupType}";
            item.Initialize(sortedList[i], HandlePowerupClicked);
            _spawnedItems.Add(item);
        }
    }

    /// <summary>
    /// Refreshes all spawned PowerupUiItem visuals.
    /// </summary>
    public void RefreshAllItems()
    {
        for (int i = 0; i < _spawnedItems.Count; i++)
        {
            if (_spawnedItems[i] != null)
            {
                _spawnedItems[i].Refresh();
            }
        }
    }

    private void ClearSpawnedItems()
    {
        _spawnedItems.Clear();

        for (int i = _itemContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(_itemContainer.GetChild(i).gameObject);
        }
    }

    private void HandlePowerupCountChanged(PowerupType type, int newCount)
    {
        for (int i = 0; i < _spawnedItems.Count; i++)
        {
            PowerupUiItem item = _spawnedItems[i];
            if (item != null && item.Data != null && item.Data.PowerupType == type)
            {
                item.Refresh();
            }
        }
    }

    private void HandlePowerupClicked(PowerupDataSO data)
    {
        if (data == null) return;

        if (!GameData.IsPowerupUnlocked(data.PowerupType)) return;

        if (_activeTypes.Contains(data.PowerupType))
        {
            Debug.Log($"[PowerupManager] {data.PowerupType} is already active.");
            return;
        }

        if (data.PowerupPrefab == null)
        {
            Debug.LogWarning($"[PowerupManager] No PowerupPrefab assigned on PowerupDataSO for {data.PowerupType}.");
            return;
        }

        PowerupContext context = BuildContext(data);
        if (!data.PowerupPrefab.CanActivate(context))
        {
            Debug.Log($"[PowerupManager] {data.PowerupType} cannot be activated right now.");
            return;
        }

        if (!GameData.TryUsePowerup(data.PowerupType))
        {
            Debug.Log($"[PowerupManager] {data.PowerupType} has 0 charges remaining.");
            return;
        }

        ActivatePowerup(data, context);
    }

    private PowerupContext BuildContext(PowerupDataSO data)
    {
        GameManager gm = GameManager.Instance;
        LevelManager lm = gm != null && gm.CurrentLevelManager != null
            ? gm.CurrentLevelManager
            : LevelManager.Instance;

        DockManager dm = lm != null ? lm.DockManager : DockManager.Instance;
        TimerUIManager tm = gm != null && gm.TimerUIManager != null
            ? gm.TimerUIManager
            : (lm != null ? lm.TimerUIManager : null);
        InputManager im = gm != null && gm.InputManager != null
            ? gm.InputManager
            : (lm != null ? lm.InputManager : null);

        return new PowerupContext(data, lm, dm, tm, im);
    }

    private void ActivatePowerup(PowerupDataSO data, PowerupContext context)
    {
        Transform parent = _activePowerupRoot != null ? _activePowerupRoot : transform;
        PowerupBase instance = Instantiate(data.PowerupPrefab, parent);
        instance.name = $"ActivePowerup_{data.PowerupType}";

        _activeTypes.Add(data.PowerupType);
        _activeInstances.Add(instance);

        instance.Execute(context, () => HandlePowerupFinished(data.PowerupType, instance));
    }

    private void HandlePowerupFinished(PowerupType type, PowerupBase instance)
    {
        _activeTypes.Remove(type);
        _activeInstances.Remove(instance);
    }

    private void CleanupActivePowerups()
    {
        for (int i = _activeInstances.Count - 1; i >= 0; i--)
        {
            if (_activeInstances[i] != null)
            {
                Destroy(_activeInstances[i].gameObject);
            }
        }

        _activeInstances.Clear();
        _activeTypes.Clear();
    }
}
