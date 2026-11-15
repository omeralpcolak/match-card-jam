/// <summary>
/// Dependency container passed to power-up instances upon validation and activation.
/// </summary>
public readonly struct PowerupContext
{
    public readonly PowerupDataSO Data;
    public readonly LevelManager LevelManager;
    public readonly DockManager DockManager;
    public readonly TimerUIManager TimerUIManager;
    public readonly InputManager InputManager;

    public PowerupContext(
        PowerupDataSO data,
        LevelManager levelManager,
        DockManager dockManager,
        TimerUIManager timerUIManager,
        InputManager inputManager)
    {
        Data = data;
        LevelManager = levelManager;
        DockManager = dockManager;
        TimerUIManager = timerUIManager;
        InputManager = inputManager;
    }
}
