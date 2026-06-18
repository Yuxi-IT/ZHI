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
    Terraform = 9,
    Shove = 10,
    Pull = 11
}

public static class ToolDefinitions
{
    public const int ActionCount = 12;
    public const int SignalValues = 4;
    public const int StateSize = 334; // 294 grid (7×7×6ch) + 40 non-grid (stamina, stationary)
    public static int GridWidth = 64;
    public static int GridHeight = 64;
    public const int VisionRadius = 3; // 7×7 window = radius 3
    public const int SignalWaveRadius = 4; // 9×9 wave pattern

    // Terrain types
    public const byte TerrainFlat = 0;
    public const byte TerrainPit = 1;
    public const byte TerrainMound = 2;
    public const byte TerrainDynamicWater = 3; // flooded pit — shallow water from terrain physics

    public const int TerrainTTL = 2000;       // pit/mound lifespan in ticks (v4.2: extended to ~33 min)
    public const int PermaWaterTTL = -1;       // sentinel for permanent water (no weathering)

    // River flow directions (8-direction compass)
    // 0=none, 1=N, 2=NE, 3=E, 4=SE, 5=S, 6=SW, 7=W, 8=NW

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
        "terraform",
        "shove",
        "pull"
    ];

    public static string GetToolName(ZhiAction action) => ActionNames[(int)action];
}
