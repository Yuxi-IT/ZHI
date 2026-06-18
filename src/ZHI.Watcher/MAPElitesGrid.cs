namespace ZHI.Watcher;

/// <summary>
/// MAP-Elites 10x10 behavioral diversity grid.
/// X-axis: Aggression (steal+attack frequency), Y-axis: Construction (expand+compress frequency).
/// Each cell preserves the best weights for that behavioral niche.
/// </summary>
public class MAPElitesGrid
{
    public const int GridSize = 10;

    public struct Cell
    {
        public byte[] Weights;
        public double Fitness;
        public float AggressionScore;
        public float ConstructionScore;
        public bool Occupied;

        public Cell()
        {
            Weights = Array.Empty<byte>();
            Fitness = 0;
            AggressionScore = 0;
            ConstructionScore = 0;
            Occupied = false;
        }
    }

    private readonly Cell[,] _grid = new Cell[GridSize, GridSize];
    private readonly Random _rng;

    public MAPElitesGrid(Random? rng = null)
    {
        _rng = rng ?? new Random();
    }

    /// <summary>
    /// Bin a behavior score [0, 1] to grid index 0-9.
    /// Uses equal-width bins (0.1 each, with edge at 1.0 going to bin 9).
    /// </summary>
    public static int Bin(float score) => Math.Clamp((int)(score * GridSize), 0, GridSize - 1);

    /// <summary>
    /// Try to insert an agent into the grid. Succeeds if cell is empty or new fitness is higher.
    /// Returns true if inserted (cell was empty or fitness improved).
    /// </summary>
    public bool TryInsert(byte[] weights, double fitness, float aggression, float construction)
    {
        int x = Bin(aggression);
        int y = Bin(construction);

        ref var cell = ref _grid[x, y];

        if (!cell.Occupied || fitness > cell.Fitness)
        {
            cell.Weights = weights;
            cell.Fitness = fitness;
            cell.AggressionScore = aggression;
            cell.ConstructionScore = construction;
            cell.Occupied = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get all occupied cells as a flat list for parent sampling.
    /// </summary>
    public List<(int x, int y, Cell cell)> GetOccupiedCells()
    {
        var result = new List<(int, int, Cell)>();
        for (int x = 0; x < GridSize; x++)
            for (int y = 0; y < GridSize; y++)
                if (_grid[x, y].Occupied)
                    result.Add((x, y, _grid[x, y]));
        return result;
    }

    /// <summary>
    /// Randomly sample a parent from the occupied cells.
    /// Returns null if grid is empty.
    /// </summary>
    public Cell? SampleParent(Random rng)
    {
        var occupied = GetOccupiedCells();
        if (occupied.Count == 0) return null;
        return occupied[rng.Next(occupied.Count)].cell;
    }

    /// <summary>
    /// Get grid diversity stats for logging.
    /// </summary>
    public int OccupiedCount => GetOccupiedCells().Count;
    public int TotalCells => GridSize * GridSize;

    /// <summary>
    /// Clear the grid for a new experiment (not used per-generation; grid persists).
    /// </summary>
    public void Clear()
    {
        for (int x = 0; x < GridSize; x++)
            for (int y = 0; y < GridSize; y++)
                _grid[x, y] = new Cell();
    }
}
