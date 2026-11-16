using System;
using UnityEngine;

/// <summary>
/// Abstract base class for all in-game power-up prefabs.
/// Spawned and explicitly executed by PowerupManager; cleans itself up upon completion.
/// </summary>
public abstract class PowerupBase : MonoBehaviour
{
    private Action _onFinished;
    private bool _isCompleted;

    protected PowerupDataSO Data { get; private set; }

    protected virtual void OnDestroy()
    {
        _onFinished = null;
    }

    /// <summary>
    /// Validates whether this power-up can currently be activated.
    /// Evaluated on the prefab before consuming a player charge.
    /// </summary>
    public virtual bool CanActivate(PowerupContext context)
    {
        return context.LevelManager != null &&
               context.LevelManager.CurrentState == LevelManager.LevelState.Playing;
    }

    /// <summary>
    /// Entry point called explicitly by PowerupManager right after instantiation.
    /// </summary>
    public void Execute(PowerupContext context, Action onFinished)
    {
        Data = context.Data;
        _onFinished = onFinished;
        _isCompleted = false;

        if (GameData.IsHapticEnabled)
        {
            Taptic.Light();
        }

        OnActivate(context);
    }

    /// <summary>
    /// Implements the specific power-up gameplay behavior.
    /// Call Complete() when the power-up finishes its effect.
    /// </summary>
    protected abstract void OnActivate(PowerupContext context);

    /// <summary>
    /// Marks the power-up as finished, notifies PowerupManager, and destroys this instance.
    /// </summary>
    protected void Complete()
    {
        if (_isCompleted) return;
        _isCompleted = true;

        Action callback = _onFinished;
        _onFinished = null;
        callback?.Invoke();

        Destroy(gameObject);
    }
}
