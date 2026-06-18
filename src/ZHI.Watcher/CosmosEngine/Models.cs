namespace ZHI.Watcher;

public class GenerationResult
{
    public int AgentId { get; set; }
    public byte[] Weights { get; set; } = Array.Empty<byte>();
    public double Fitness { get; set; }
    public double AliveSeconds { get; set; }
    public string Cause { get; set; } = "";
    public int AttackCount { get; set; }
    public int TickCount { get; set; }
    public int EatCount { get; set; }
    public int EmitCount { get; set; }
}

public class WorldEvent
{
    public string Type { get; set; } = "";
    public int AgentId { get; set; }
    public int TargetId { get; set; } = -1;
    public int ChildId { get; set; } = -1;
    public string FoodType { get; set; } = "";
    public int SignalValue { get; set; } = -1;
    public float Value { get; set; }
    public int Tick { get; set; }
}
