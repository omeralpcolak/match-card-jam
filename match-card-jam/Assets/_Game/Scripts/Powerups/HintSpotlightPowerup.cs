using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Finds the best matchable items on the board (prioritizing items that complete
/// partial sets already in the dock) and highlights them with a pulsing outline
/// and optional spotlight VFX for the configured duration.
/// </summary>
public class HintSpotlightPowerup : PowerupBase
{
    [Header("Visuals & Audio")]
    [Tooltip("Primary highlight color applied to target MatchItem outlines.")]
    [SerializeField] private Color _highlightColor = new Color(1f, 0.85f, 0.1f, 1f);

    [Tooltip("Secondary pulse color for the outline yoyo effect.")]
    [SerializeField] private Color _pulseColor = new Color(1f, 1f, 0.65f, 1f);

    [Tooltip("Duration of one pulse half-cycle in seconds.")]
    [SerializeField] private float _pulseHalfDuration = 0.35f;

    [Tooltip("Optional world-space spotlight/arrow VFX prefab spawned on each hinted item.")]
    [SerializeField] private GameObject _itemIndicatorPrefab;

    [Tooltip("Vertical offset for the optional indicator prefab above each hinted item.")]
    [SerializeField] private float _indicatorYOffset = 0.8f;

    [Tooltip("Sound effect played through SoundManager when activated.")]
    [SerializeField] private SoundType _hintSound = SoundType.PowerupHint;

    private readonly List<MatchItem> _hintedItems = new();
    private readonly List<GameObject> _spawnedIndicators = new();
    private LevelManager _levelManager;
    private Tween _pulseTween;
    private float _remainingDuration;
    private bool _isRunning;

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _pulseTween?.Kill();
        ClearVisuals();
    }

    public override bool CanActivate(PowerupContext context)
    {
        if (!base.CanActivate(context)) return false;

        List<MatchItem> candidates = new();
        return TryFindBestHintTargets(context, candidates);
    }

    protected override void OnActivate(PowerupContext context)
    {
        _levelManager = context.LevelManager;
        _hintedItems.Clear();
        if (!TryFindBestHintTargets(context, _hintedItems) || _hintedItems.Count == 0)
        {
            Complete();
            return;
        }

        // 1. Audio & Haptics
        SoundManager.PlaySound(_hintSound);
        if (GameData.IsHapticEnabled)
        {
            Taptic.Light();
        }

        // 2. Spawn optional world indicators
        SpawnIndicators();

        // 3. Pulse outline colors on hinted items
        StartOutlinePulse();

        // 4. Track remaining duration in Update so pause popups freeze the countdown
        _remainingDuration = Data != null && Data.Duration > 0f ? Data.Duration : 5f;
        _isRunning = true;
    }

    private void Update()
    {
        if (!_isRunning) return;

        if (_levelManager != null)
        {
            if (_levelManager.CurrentState == LevelManager.LevelState.Paused) return;

            if (_levelManager.CurrentState != LevelManager.LevelState.Playing)
            {
                _isRunning = false;
                Complete();
                return;
            }
        }

        _remainingDuration -= Time.deltaTime;
        if (_remainingDuration <= 0f)
        {
            _isRunning = false;
            Complete();
            return;
        }

        if (_hintedItems.Count == 0) return;

        bool anyRemainingInPhysics = false;
        for (int i = _hintedItems.Count - 1; i >= 0; i--)
        {
            MatchItem item = _hintedItems[i];
            if (item == null || item.CurrentState != MatchItem.ItemState.InPhysics)
            {
                // Player picked this item; remove its indicator so it doesn't linger on the table.
                if (i < _spawnedIndicators.Count && _spawnedIndicators[i] != null)
                {
                    Destroy(_spawnedIndicators[i]);
                    _spawnedIndicators[i] = null;
                }
                _hintedItems.RemoveAt(i);
                if (i < _spawnedIndicators.Count)
                {
                    _spawnedIndicators.RemoveAt(i);
                }
            }
            else
            {
                anyRemainingInPhysics = true;
                if (i < _spawnedIndicators.Count && _spawnedIndicators[i] != null)
                {
                    _spawnedIndicators[i].transform.position = item.transform.position + Vector3.up * _indicatorYOffset;
                }
            }
        }

        if (!anyRemainingInPhysics)
        {
            Complete();
        }
    }

    private void StartOutlinePulse()
    {
        ApplyOutlineColor(_highlightColor);

        _pulseTween = DOVirtual.Float(0f, 1f, _pulseHalfDuration, t =>
        {
            Color c = Color.Lerp(_highlightColor, _pulseColor, t);
            ApplyOutlineColor(c);
        })
        .SetLoops(-1, LoopType.Yoyo)
        .SetEase(Ease.InOutSine)
        .SetUpdate(false);
    }

    private void ApplyOutlineColor(Color color)
    {
        for (int i = 0; i < _hintedItems.Count; i++)
        {
            MatchItem item = _hintedItems[i];
            if (item != null && item.CurrentState == MatchItem.ItemState.InPhysics && item.Outline != null)
            {
                item.Outline.SetOutlineColor(color);
            }
        }
    }

    private void SpawnIndicators()
    {
        if (_itemIndicatorPrefab == null) return;

        for (int i = 0; i < _hintedItems.Count; i++)
        {
            MatchItem item = _hintedItems[i];
            if (item == null) continue;

            Vector3 pos = item.transform.position + Vector3.up * _indicatorYOffset;
            GameObject indicator = Instantiate(_itemIndicatorPrefab, pos, Quaternion.identity, transform);
            _spawnedIndicators.Add(indicator);
        }
    }

    private void ClearVisuals()
    {
        for (int i = 0; i < _hintedItems.Count; i++)
        {
            MatchItem item = _hintedItems[i];
            if (item != null && item.CurrentState == MatchItem.ItemState.InPhysics && item.Outline != null)
            {
                item.Outline.SetHighlighted(false);
            }
        }
        _hintedItems.Clear();

        for (int i = 0; i < _spawnedIndicators.Count; i++)
        {
            if (_spawnedIndicators[i] != null)
            {
                Destroy(_spawnedIndicators[i]);
            }
        }
        _spawnedIndicators.Clear();
    }

    private bool TryFindBestHintTargets(PowerupContext context, List<MatchItem> results)
    {
        results.Clear();
        if (context.LevelManager == null || context.LevelManager.BoardItems == null) return false;

        // Group available InPhysics items on the board by Item Id
        Dictionary<int, List<MatchItem>> boardGroups = new();
        IReadOnlyList<MatchItem> boardItems = context.LevelManager.BoardItems;

        for (int i = 0; i < boardItems.Count; i++)
        {
            MatchItem item = boardItems[i];
            if (item == null || !item.gameObject.activeInHierarchy || item.CurrentState != MatchItem.ItemState.InPhysics)
            {
                continue;
            }

            if (!boardGroups.TryGetValue(item.Id, out List<MatchItem> group))
            {
                group = new List<MatchItem>();
                boardGroups[item.Id] = group;
            }
            group.Add(item);
        }

        if (boardGroups.Count == 0) return false;

        // Priority 1: Check items already in the dock (prefer 2-in-dock -> need 1, then 1-in-dock -> need 2)
        if (context.DockManager != null && context.DockManager.Slots != null)
        {
            Dictionary<int, int> dockCounts = new();
            IReadOnlyList<DockSlot> slots = context.DockManager.Slots;

            for (int i = 0; i < slots.Count; i++)
            {
                DockSlot slot = slots[i];
                if (slot != null && !slot.IsEmpty && slot.CurrentItem != null)
                {
                    int id = slot.CurrentItem.Id;
                    dockCounts[id] = dockCounts.TryGetValue(id, out int count) ? count + 1 : 1;
                }
            }

            if (TryPickFromDockPriority(dockCounts, boardGroups, desiredDockCount: 2, neededFromBoard: 1, results))
            {
                return true;
            }

            if (TryPickFromDockPriority(dockCounts, boardGroups, desiredDockCount: 1, neededFromBoard: 2, results))
            {
                return true;
            }
        }

        // Priority 2: Pick a full triplet (3 items) on the board with the highest average Y position
        List<MatchItem> bestGroup = null;
        float bestAverageY = float.MinValue;

        foreach (KeyValuePair<int, List<MatchItem>> pair in boardGroups)
        {
            List<MatchItem> group = pair.Value;
            if (group.Count < 3) continue;

            // Sort descending by Y so we highlight the topmost 3 items of that type
            group.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
            float avgY = (group[0].transform.position.y + group[1].transform.position.y + group[2].transform.position.y) / 3f;

            if (avgY > bestAverageY)
            {
                bestAverageY = avgY;
                bestGroup = group;
            }
        }

        if (bestGroup != null)
        {
            results.Add(bestGroup[0]);
            results.Add(bestGroup[1]);
            results.Add(bestGroup[2]);
            return true;
        }

        return false;
    }

    private bool TryPickFromDockPriority(
        Dictionary<int, int> dockCounts,
        Dictionary<int, List<MatchItem>> boardGroups,
        int desiredDockCount,
        int neededFromBoard,
        List<MatchItem> results)
    {
        foreach (KeyValuePair<int, int> pair in dockCounts)
        {
            if (pair.Value != desiredDockCount) continue;

            if (boardGroups.TryGetValue(pair.Key, out List<MatchItem> group) && group.Count >= neededFromBoard)
            {
                group.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
                for (int i = 0; i < neededFromBoard; i++)
                {
                    results.Add(group[i]);
                }
                return true;
            }
        }

        return false;
    }
}
