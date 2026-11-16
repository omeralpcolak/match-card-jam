using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Automatically pulls matching MatchItems from the board into the dock to complete a 3-item match.
/// Attaches an optional particle prefab as a child of each pulled item while flying and stops it upon dock arrival.
/// </summary>
public class MagnetPowerup : PowerupBase
{
    private struct AttachedVfx
    {
        public MatchItem Item;
        public GameObject VfxObject;
    }

    [Header("Magnet Settings")]
    [Tooltip("Delay in seconds between pulling each item into the dock.")]
    [SerializeField] private float _pullStaggerDelay = 0.08f;

    [Header("VFX & Audio")]
    [Tooltip("Optional particle prefab attached as a child to each pulled MatchItem while flying to the dock.")]
    [SerializeField] private GameObject _itemPullVfxPrefab;

    [Tooltip("Sound effect played through SoundManager when activated.")]
    [SerializeField] private SoundType _magnetSound = SoundType.PowerupMagnet;

    private readonly List<MatchItem> _targetItems = new();
    private readonly List<AttachedVfx> _attachedVfx = new();
    private LevelManager _levelManager;
    private DockManager _dockManager;
    private int _nextPullIndex;
    private float _staggerTimer;
    private bool _isPulling;
    private bool _isRunning;

    protected override void OnDestroy()
    {
        base.OnDestroy();

        for (int i = _attachedVfx.Count - 1; i >= 0; i--)
        {
            StopVfxGracefully(_attachedVfx[i].VfxObject);
        }
        _attachedVfx.Clear();
    }

    public override bool CanActivate(PowerupContext context)
    {
        if (!base.CanActivate(context)) return false;
        if (context.DockManager == null) return false;
        if (context.DockManager.IsResolvingMatch || context.DockManager.ActiveMovingCount > 0) return false;

        return TryFindBestMagnetTargets(context, _targetItems);
    }

    protected override void OnActivate(PowerupContext context)
    {
        _levelManager = context.LevelManager;
        _dockManager = context.DockManager;
        _targetItems.Clear();

        if (!TryFindBestMagnetTargets(context, _targetItems) || _targetItems.Count == 0)
        {
            Complete();
            return;
        }

        // 1. Pull first item immediately, stagger the rest in Update
        _nextPullIndex = 0;
        _staggerTimer = 0f;
        _isPulling = true;
        _isRunning = true;
        PullNextItem();
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
                _isPulling = false;
                Complete();
                return;
            }
        }

        if (_isPulling)
        {
            _staggerTimer -= Time.deltaTime;
            if (_staggerTimer <= 0f)
            {
                PullNextItem();
            }
        }

        UpdateAttachedVfx();

        if (!_isPulling && _attachedVfx.Count == 0)
        {
            _isRunning = false;
            Complete();
        }
    }

    private void PullNextItem()
    {
        while (_nextPullIndex < _targetItems.Count)
        {
            MatchItem item = _targetItems[_nextPullIndex];
            _nextPullIndex++;

            if (item != null && item.CurrentState == MatchItem.ItemState.InPhysics && _dockManager != null)
            {
                if (_dockManager.TryAddItem(item))
                {
                    SoundManager.PlaySound(_magnetSound);
                    AttachVfxToItem(item);
                    _staggerTimer = _pullStaggerDelay;
                    return;
                }
            }
        }

        _isPulling = false;
    }

    private void AttachVfxToItem(MatchItem item)
    {
        if (_itemPullVfxPrefab == null || item == null) return;

        GameObject vfx = Instantiate(_itemPullVfxPrefab, item.transform);
        vfx.transform.localPosition = Vector3.zero;
        vfx.transform.localRotation = _itemPullVfxPrefab.transform.localRotation;

        _attachedVfx.Add(new AttachedVfx
        {
            Item = item,
            VfxObject = vfx
        });
    }

    private void UpdateAttachedVfx()
    {
        for (int i = _attachedVfx.Count - 1; i >= 0; i--)
        {
            AttachedVfx entry = _attachedVfx[i];
            if (entry.Item == null || entry.Item.CurrentState != MatchItem.ItemState.Flying)
            {
                StopVfxGracefully(entry.VfxObject);
                _attachedVfx.RemoveAt(i);
            }
        }
    }

    private void StopVfxGracefully(GameObject vfxObject)
    {
        if (vfxObject == null) return;

        ParticleSystem[] systems = vfxObject.GetComponentsInChildren<ParticleSystem>(true);
        float maxLifetime = 0f;

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null) continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            float lt = ps.main.startLifetime.constantMax;
            if (lt > maxLifetime)
            {
                maxLifetime = lt;
            }
        }

        // Detach so match shrink/destroy does not abruptly cut off fading trail particles.
        vfxObject.transform.SetParent(null, true);
        Destroy(vfxObject, maxLifetime + 0.1f);
    }

    private bool TryFindBestMagnetTargets(PowerupContext context, List<MatchItem> results)
    {
        results.Clear();
        if (context.LevelManager == null || context.LevelManager.BoardItems == null || context.DockManager == null)
        {
            return false;
        }

        int emptySlots = CountEmptyDockSlots(context.DockManager);
        if (emptySlots <= 0) return false;

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

        // Count items currently in dock by Id
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

        // Priority 1: 2 in dock -> pull 1 from board (requires >= 1 empty slot)
        if (emptySlots >= 1 && TryPickFromDock(dockCounts, boardGroups, desiredDockCount: 2, neededFromBoard: 1, results))
        {
            return true;
        }

        // Priority 2: 1 in dock -> pull 2 from board (requires >= 2 empty slots)
        if (emptySlots >= 2 && TryPickFromDock(dockCounts, boardGroups, desiredDockCount: 1, neededFromBoard: 2, results))
        {
            return true;
        }

        // Priority 3: Full triplet from board (requires >= 3 empty slots)
        if (emptySlots >= 3)
        {
            foreach (KeyValuePair<int, List<MatchItem>> pair in boardGroups)
            {
                List<MatchItem> group = pair.Value;
                if (group.Count < 3) continue;

                group.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
                results.Add(group[0]);
                results.Add(group[1]);
                results.Add(group[2]);
                return true;
            }
        }

        return false;
    }

    private static bool TryPickFromDock(
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

    private static int CountEmptyDockSlots(DockManager dockManager)
    {
        if (dockManager == null || dockManager.Slots == null) return 0;

        int empty = 0;
        IReadOnlyList<DockSlot> slots = dockManager.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].IsEmpty)
            {
                empty++;
            }
        }
        return empty;
    }
}
