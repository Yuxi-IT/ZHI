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
    Drink = 7,
    Push = 8,
    Terraform = 9
}

public static class ToolDefinitions
{
    public const int ActionCount = 10;
    public const int SignalValues = 4;
    public const int StateSize = 334; // 294 grid (7×7×6ch) + 40 non-grid (stamina, stationary)
    public static int GridWidth = 64;
    public static int GridHeight = 64;
    public const int VisionRadius = 3; // 7×7 window = radius 3

    // Terrain types
    public const byte TerrainFlat = 0;
    public const byte TerrainPit = 1;
    public const byte TerrainMound = 2;

    public static readonly string[] ActionNames =
    [
        "move_up",
        "move_down",
        "move_left",
        "move_right",
        "eat",
        "attack",
        "signal",
        "drink",
        "push",
        "terraform"
    ];

    public static string GetToolName(ZhiAction action) => ActionNames[(int)action];
}
