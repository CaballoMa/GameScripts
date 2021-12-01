
/// <summary>
/// Player Move in Different Places with Different Speed State
/// </summary>
public enum PlayerPlaceState
{
    Dive,
    Float,
    WaterFall
}

/// <summary>
/// Player Speed State : 
/// Normal    Player with no Input
/// Fast    Player with Add Speed KeyCode
/// Slow    Player with Decrease Speed
/// </summary>
public enum PlayerSpeedState
{
    Normal,
    Fast,
    Stop
}

/// <summary>
/// Player Health State:
/// Strong  Player Power Value and Clean Value is health
/// Weak    Player Power Value or Clean Value is not good (slow)
/// Agony   Player Power Value or Clean Value is bad (very slow)
/// Dead    Player Dead
/// </summary>
public enum PlayerFullState 
{
    Strong,
    Hungry,
    Agony,
    Dead
}

public enum PlayerCleanState
{
    Clean,
    Dirty,
    TwiceDirty,
    Weak
}

public enum PlayerInteractAniState
{
    Idle,
    Eat,
    Knock,
    Cast,
    Grab,
    Release,
    Clean,
    Sleep,
    Celebrate
}

