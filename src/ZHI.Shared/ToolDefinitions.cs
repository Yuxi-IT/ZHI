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

public enum PlantSpecies : byte
{
    Grass = 0,
    Bush = 1,
    Tree = 2
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

    // Aspect directions (坡向, 8-direction compass)
    public const byte AspectN = 0;
    public const byte AspectNE = 1;
    public const byte AspectE = 2;
    public const byte AspectSE = 3;
    public const byte AspectS = 4;
    public const byte AspectSW = 5;
    public const byte AspectW = 6;
    public const byte AspectNW = 7;
    public const byte AspectFlat = 255;  // slope < epsilon, no meaningful aspect

    // River flow directions (8-direction compass)
    // 0=none, 1=N, 2=NE, 3=E, 4=SE, 5=S, 6=SW, 7=W, 8=NW

    // Biome types (derived from environment, not hand-placed)
    public const byte BiomeWater = 0;       // river/lake cell
    public const byte BiomeRiverBank = 1;   // near river (dist <= 3)
    public const byte BiomeDesert = 2;      // arid: low groundwater, low nutrient
    public const byte BiomeGrassland = 3;   // moderate: medium water/nutrient
    public const byte BiomeJungle = 4;      // lush: high water, high nutrient, warm
    public const byte BiomeWetland = 5;     // wet: surface water or saturated ground
    public const byte BiomeHighland = 6;    // high elevation (>180)
    public const byte BiomeValley = 7;      // low elevation (<70), not water

    // Biomes that get extra sunlight on S/SE/SW aspects (northern hemisphere)
    public static bool IsSunFacingAspect(byte aspect) =>
        aspect == AspectS || aspect == AspectSE || aspect == AspectSW;
    // Biomes that get less sunlight (N-facing slopes)
    public static bool IsShadeFacingAspect(byte aspect) =>
        aspect == AspectN || aspect == AspectNE || aspect == AspectNW;

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
