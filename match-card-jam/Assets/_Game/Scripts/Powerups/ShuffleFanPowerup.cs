using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Launches all InPhysics MatchItems on the board upward with randomized spin and swirl
/// to uncover buried items, while keeping them nudged toward the board center.
/// </summary>
public class ShuffleFanPowerup : PowerupBase
{
    [Header("Force Settings")]
    [Tooltip("Minimum upward impulse applied to each MatchItem.")]
    [SerializeField] private float _minUpwardImpulse = 7f;

    [Tooltip("Maximum upward impulse applied to each MatchItem.")]
    [SerializeField] private float _maxUpwardImpulse = 11f;

    [Tooltip("Horizontal scatter impulse strength.")]
    [SerializeField] private float _horizontalImpulse = 3.5f;

    [Tooltip("Gentle inward pull toward the board center so items stay within bounds.")]
    [SerializeField] private float _centerPullStrength = 0.4f;

    [Tooltip("Random rotational torque impulse applied to tumble items in mid-air.")]
    [SerializeField] private float _torqueStrength = 4f;

    [Header("VFX & Audio")]
    [Tooltip("Optional wind/tornado particle prefab spawned at the board center.")]
    [SerializeField] private GameObject _windEffectPrefab;

    [Tooltip("Cooldown duration before another ShuffleFan can be triggered (uses PowerupDataSO.Duration if > 0).")]
    [SerializeField] private float _fallbackDuration = 1.0f;

    [Tooltip("Sound effect played through SoundManager when activated.")]
    [SerializeField] private SoundType _shuffleSound = SoundType.PowerupShuffle;

    private readonly List<MatchItem> _targetItems = new();
    private LevelManager _levelManager;
    private float _remainingDuration;
    private bool _isRunning;

    public override bool CanActivate(PowerupContext context)
    {
        if (!base.CanActivate(context)) return false;

        return CollectPhysicsItems(context.LevelManager, _targetItems) > 0;
    }

    protected override void OnActivate(PowerupContext context)
    {
        _levelManager = context.LevelManager;
        _targetItems.Clear();

        if (CollectPhysicsItems(_levelManager, _targetItems) == 0)
        {
            Complete();
            return;
        }

        // 1. Audio & Haptics
        SoundManager.PlaySound(_shuffleSound);
        if (GameData.IsHapticEnabled)
        {
            Taptic.Medium();
        }

        // 2. Compute board centroid so forces and optional VFX work regardless of level offset
        Vector3 boardCenter = ComputeBoardCenter(_targetItems);

        if (_windEffectPrefab != null)
        {
            Instantiate(_windEffectPrefab, boardCenter, Quaternion.identity, transform);
        }

        // 3. Apply upward + swirl + torque impulses
        ApplyShuffleForces(boardCenter);

        // 4. Short cooldown before Complete() so particles finish and rapid double-taps are prevented
        _remainingDuration = Data != null && Data.Duration > 0f ? Data.Duration : _fallbackDuration;
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
        }
    }

    private void ApplyShuffleForces(Vector3 boardCenter)
    {
        for (int i = 0; i < _targetItems.Count; i++)
        {
            MatchItem item = _targetItems[i];
            if (item == null || item.Rb == null) continue;

            Rigidbody rb = item.Rb;
            rb.WakeUp();

            // Reset downward velocity so every item gets a clean lift
            Vector3 vel = rb.linearVelocity;
            if (vel.y < 0f) vel.y = 0f;
            rb.linearVelocity = vel;

            float upForce = Random.Range(_minUpwardImpulse, _maxUpwardImpulse);
            Vector2 randomDir2D = Random.insideUnitCircle.normalized;
            Vector3 scatter = new Vector3(randomDir2D.x, 0f, randomDir2D.y) * Random.Range(0.3f, 1f) * _horizontalImpulse;

            // Nudge outer items slightly inward toward boardCenter
            Vector3 toCenter = boardCenter - item.transform.position;
            toCenter.y = 0f;
            Vector3 inwardPull = toCenter * _centerPullStrength;

            Vector3 totalImpulse = Vector3.up * upForce + scatter + inwardPull;
            rb.AddForce(totalImpulse, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * _torqueStrength, ForceMode.Impulse);
        }
    }

    private static int CollectPhysicsItems(LevelManager levelManager, List<MatchItem> results)
    {
        results.Clear();
        if (levelManager == null || levelManager.BoardItems == null) return 0;

        IReadOnlyList<MatchItem> boardItems = levelManager.BoardItems;
        for (int i = 0; i < boardItems.Count; i++)
        {
            MatchItem item = boardItems[i];
            if (item != null && item.gameObject.activeInHierarchy && item.CurrentState == MatchItem.ItemState.InPhysics)
            {
                results.Add(item);
            }
        }

        return results.Count;
    }

    private static Vector3 ComputeBoardCenter(List<MatchItem> items)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
            {
                sum += items[i].transform.position;
                count++;
            }
        }

        return count > 0 ? sum / count : Vector3.zero;
    }
}
