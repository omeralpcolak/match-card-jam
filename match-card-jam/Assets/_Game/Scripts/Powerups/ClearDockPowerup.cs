using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Clears all MatchItems currently seated in the dock and launches them back
/// onto the board in a staggered arc, restoring their original scale and physics state.
/// </summary>
public class ClearDockPowerup : PowerupBase
{
    [Header("Launch Settings")]
    [Tooltip("Delay in seconds between launching each item from the dock.")]
    [SerializeField] private float _staggerDelay = 0.06f;

    [Tooltip("Duration in seconds for each item's return flight to the board.")]
    [SerializeField] private float _flyDuration = 0.38f;

    [Tooltip("Arc height of the return jump.")]
    [SerializeField] private float _jumpPower = 2.0f;

    [Tooltip("Height offset above the board center where returned items land.")]
    [SerializeField] private float _dropHeightOffset = 1.5f;

    [Tooltip("Horizontal scatter radius around the board center.")]
    [SerializeField] private float _scatterRadius = 1.8f;

    [Header("Audio")]
    [Tooltip("Sound effect played through SoundManager when activated.")]
    [SerializeField] private SoundType _clearDockSound = SoundType.PowerupClearDock;

    private readonly List<MatchItem> _dockItems = new();
    private readonly List<Tween> _activeTweens = new();
    private LevelManager _levelManager;
    private InputManager _inputManager;
    private Transform _boardRoot;
    private Vector3 _boardCenter;
    private int _nextLaunchIndex;
    private float _staggerTimer;
    private int _inFlightCount;
    private bool _isLaunching;
    private bool _isRunning;

    protected override void OnDestroy()
    {
        base.OnDestroy();
        RestoreInput();

        for (int i = _activeTweens.Count - 1; i >= 0; i--)
        {
            _activeTweens[i]?.Kill();
        }
        _activeTweens.Clear();
    }

    public override bool CanActivate(PowerupContext context)
    {
        if (!base.CanActivate(context)) return false;
        if (context.DockManager == null) return false;
        if (context.DockManager.IsResolvingMatch || context.DockManager.ActiveMovingCount > 0) return false;

        return context.DockManager.CurrentItemCount > 0;
    }

    protected override void OnActivate(PowerupContext context)
    {
        _levelManager = context.LevelManager;
        _inputManager = context.InputManager;
        _boardRoot = _levelManager != null ? _levelManager.MatchItemRoot : null;
        _dockItems.Clear();

        IReadOnlyList<DockSlot> slots = context.DockManager.Slots;
        if (slots == null)
        {
            Complete();
            return;
        }

        // Collect from right to left and clear each slot immediately
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            DockSlot slot = slots[i];
            if (slot != null && !slot.IsEmpty && slot.CurrentItem != null)
            {
                _dockItems.Add(slot.CurrentItem);
                slot.Clear();
            }
        }

        if (_dockItems.Count == 0)
        {
            Complete();
            return;
        }

        if (_inputManager != null)
        {
            _inputManager.enabled = false;
        }

        _boardCenter = ComputeBoardCenter(_levelManager);
        _nextLaunchIndex = 0;
        _staggerTimer = 0f;
        _inFlightCount = 0;
        _isLaunching = true;
        _isRunning = true;

        LaunchNextItem();
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
                _isLaunching = false;
                Complete();
                return;
            }
        }

        if (_isLaunching)
        {
            _staggerTimer -= Time.deltaTime;
            if (_staggerTimer <= 0f)
            {
                LaunchNextItem();
            }
        }

        if (!_isLaunching && _inFlightCount <= 0)
        {
            _isRunning = false;
            Complete();
        }
    }

    private void LaunchNextItem()
    {
        if (_nextLaunchIndex >= _dockItems.Count)
        {
            _isLaunching = false;
            RestoreInput();
            return;
        }

        MatchItem item = _dockItems[_nextLaunchIndex];
        _nextLaunchIndex++;
        _staggerTimer = _staggerDelay;

        if (item == null) return;

        SoundManager.PlaySound(_clearDockSound);

        item.transform.DOKill();
        item.transform.SetParent(_boardRoot, true);
        item.SetState(MatchItem.ItemState.Flying);

        Vector2 randomOffset = Random.insideUnitCircle * _scatterRadius;
        Vector3 targetPos = _boardCenter + new Vector3(randomOffset.x, _dropHeightOffset, randomOffset.y);
        Vector3 targetRot = new Vector3(0f, Random.Range(0f, 360f), 0f);

        _inFlightCount++;

        Sequence seq = DOTween.Sequence();
        seq.Join(item.transform.DOJump(targetPos, _jumpPower, 1, _flyDuration).SetEase(Ease.OutQuad));
        seq.Join(item.transform.DORotate(targetRot, _flyDuration).SetEase(Ease.OutQuad));
        seq.Join(item.transform.DOScale(item.InitialScale, _flyDuration).SetEase(Ease.OutBack));

        _activeTweens.Add(seq);

        seq.OnComplete(() =>
        {
            _activeTweens.Remove(seq);
            _inFlightCount--;

            if (item != null)
            {
                item.transform.localScale = item.InitialScale;
                item.SetState(MatchItem.ItemState.InPhysics);
            }
        });
    }

    private void RestoreInput()
    {
        if (_inputManager != null)
        {
            _inputManager.enabled = true;
            _inputManager = null;
        }
    }

    private static Vector3 ComputeBoardCenter(LevelManager levelManager)
    {
        if (levelManager == null) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        int count = 0;

        if (levelManager.BoardItems != null)
        {
            IReadOnlyList<MatchItem> items = levelManager.BoardItems;
            for (int i = 0; i < items.Count; i++)
            {
                MatchItem item = items[i];
                if (item != null && item.gameObject.activeInHierarchy && item.CurrentState == MatchItem.ItemState.InPhysics)
                {
                    sum += item.transform.position;
                    count++;
                }
            }
        }

        if (count > 0)
        {
            return sum / count;
        }

        return levelManager.MatchItemRoot != null ? levelManager.MatchItemRoot.position : Vector3.zero;
    }
}
