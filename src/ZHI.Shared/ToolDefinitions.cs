namespace ZHI.Shared;

public enum ZhiAction
{
    MoveUp = 0,
    MoveDown = 1,
    MoveLeft = 2,
    MoveRight = 3,
    Eat = 4,
    Attack = 5,
    EmitChemical = 6,
    Drink = 7
}

public static class ToolDefinitions
{
    public const int ActionCount = 8;
    public const int ChemicalEmitValues = 1; // continuous 0–1 emission (sampled via beta distribution)
    public const int StateSize = 340; // 294 grid (7×7×6ch) + 46 non-grid
    public static int GridWidth = 64;
    public static int GridHeight = 64;
    public const int VisionRadius = 3; // 7×7 window = radius 3
    public const int SignalWaveRadius = 4; // 9×9 chemical diffusion pattern

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
        "emit_chemical",
        "drink"
    ];

    public static string GetToolName(ZhiAction action) => ActionNames[(int)action];
}
