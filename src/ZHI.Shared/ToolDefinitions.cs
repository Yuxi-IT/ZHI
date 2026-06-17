namespace ZHI.Shared;

public enum ZhiAction
{
    MoveUp = 0,
    MoveDown = 1,
    MoveLeft = 2,
    MoveRight = 3,
    Eat = 4,
    Attack = 5,
    Signal = 6,
    Drink = 7
}

public static class ToolDefinitions
{
    public const int ActionCount = 8;
    public const int SignalValues = 4;
    public const int StateSize = 162; // 125 grid (5ch) + 37 non-grid
    public const int GridWidth = 64;
    public const int GridHeight = 64;
    public const int VisionRadius = 2; // 5×5 window = radius 2

    public static readonly string[] ActionNames =
    [
        "move_up",
        "move_down",
        "move_left",
        "move_right",
        "eat",
        "attack",
        "signal",
        "drink"
    ];

    public static string GetToolName(ZhiAction action) => ActionNames[(int)action];
}
