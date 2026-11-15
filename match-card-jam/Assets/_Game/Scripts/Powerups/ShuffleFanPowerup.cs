using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Launches all InPhysics MatchItems on the board upward with randomized spin and swirl
/// to uncover buried items, while keeping them nudged toward the board center.
/// </summary>
public class ShuffleFanPowerup : PowerupBase
{
    [Header("Force Settings")]
    [Tooltip("Minimum upward velocity change applied to each MatchItem (mass-independent).")]
    [SerializeField] private float _minUpwardVelocity = 10f;

    [Tooltip("Maximum upward velocity change applied to each MatchItem (mass-independent).")]
    [SerializeField] private float _maxUpwardVelocity = 15f;

    [Tooltip("Horizontal scatter velocity change strength.")]
    [SerializeField] private float _horizontalVelocity = 5f;

    [Tooltip("Gentle inward pull toward the board center so items stay within bounds.")]
    [SerializeField] private float _centerPullStrength = 0.45f;

    [Tooltip("Random rotational angular velocity applied to tumble items in mid-air.")]
    [SerializeField] private float _torqueStrength = 8f;

    [Header("VFX & Audio")]
    [Tooltip("Optional wind/tornado particle prefab spawned at the board center (preserves prefab rotation).")]
    [SerializeField] private GameObject _windEffectPrefab;

    [Tooltip("Duration in seconds the VFX emits before gracefully stopping (uses PowerupDataSO.Duration if > 0).")]
    [SerializeField] private float _fallbackDuration = 1.2f;

    [Tooltip("Sound effect played through SoundManager when activated.")]
    [SerializeField] private SoundType _shuffleSound = SoundType.PowerupShuffle;

    private readonly List<MatchItem> _targetItems = new();
    private LevelManager _levelManager;
    private GameObject _spawnedVfx;
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

        // 2. Compute board centroid and spawn VFX preserving prefab rotation (e.g. X = -90)
        Vector3 boardCenter = ComputeBoardCenter(_targetItems);

        if (_windEffectPrefab != null)
        {
            _spawnedVfx = Instantiate(_windEffectPrefab, boardCenter, _windEffectPrefab.transform.rotation, transform);
        }

        // 3. Apply upward + swirl + torque forces
        ApplyShuffleForces(boardCenter);

        // 4. Track emission duration (also acts as spam-click cooldown)
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
                StopVfxGracefully();
                Complete();
                return;
            }
        }

        _remainingDuration -= Time.deltaTime;
        if (_remainingDuration <= 0f)
        {
            _isRunning = false;
            FlattenTiltedItems();
            StopVfxGracefully();
            Complete();
        }
    }

    private void FlattenTiltedItems()
    {
        for (int i = 0; i < _targetItems.Count; i++)
        {
            MatchItem item = _targetItems[i];
            if (item == null || item.CurrentState != MatchItem.ItemState.InPhysics) continue;

            // If an item landed on its thin edge or upside down after collisions, lay it flat.
            if (Vector3.Dot(item.transform.up, Vector3.up) < 0.85f)
            {
                if (item.Rb != null)
                {
                    item.Rb.angularVelocity = Vector3.zero;
                }

                Vector3 euler = item.transform.eulerAngles;
                item.transform.DORotate(new Vector3(0f, euler.y, 0f), 0.2f).SetEase(Ease.OutQuad);
            }
        }
    }

    private void StopVfxGracefully()
    {
        if (_spawnedVfx == null) return;

        ParticleSystem[] systems = _spawnedVfx.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0) return;

        float maxRemainingLifetime = 0f;
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null) continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            float lifetime = ps.main.startLifetime.constantMax;
            if (lifetime > maxRemainingLifetime)
            {
                maxRemainingLifetime = lifetime;
            }
        }

        // Detach VFX so existing particles finish fading out naturally after this power-up destroys itself.
        _spawnedVfx.transform.SetParent(null, true);
        Destroy(_spawnedVfx, Mathf.Max(0.5f, maxRemainingLifetime + 0.2f));
        _spawnedVfx = null;
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

            float upVel = Random.Range(_minUpwardVelocity, _maxUpwardVelocity);
            Vector2 randomDir2D = Random.insideUnitCircle.normalized;
            Vector3 scatter = new Vector3(randomDir2D.x, 0f, randomDir2D.y) * Random.Range(0.35f, 1f) * _horizontalVelocity;

            // Nudge outer items slightly inward toward boardCenter
            Vector3 toCenter = boardCenter - item.transform.position;
            toCenter.y = 0f;
            Vector3 inwardPull = toCenter * _centerPullStrength;

            Vector3 totalVelocityChange = Vector3.up * upVel + scatter + inwardPull;
            rb.AddForce(totalVelocityChange, ForceMode.VelocityChange);

            // Spin around Y axis only so flat cards do not flip upside down or balance on their thin edges
            float yawSpin = Random.Range(-_torqueStrength, _torqueStrength);
            rb.AddTorque(Vector3.up * yawSpin, ForceMode.VelocityChange);
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
