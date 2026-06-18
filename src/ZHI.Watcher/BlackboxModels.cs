namespace ZHI.Watcher;

public class DeathRecord
{
    public int Id { get; set; }
    public int Generation { get; set; }
    public string Cause { get; set; } = "";
    public float StressAtDeath { get; set; }
    public float EnergyAtDeath { get; set; }
    public float WaterAtDeath { get; set; }
    public float Temperature { get; set; }
    public float TimeOfDay { get; set; }
    public int PosX { get; set; }
    public int PosY { get; set; }
    public int AttackCount { get; set; }
    public int EatCount { get; set; }
    public int EmitCount { get; set; }
    public int RespawnCount { get; set; }
    public string PreDeathStatesJson { get; set; } = "[]";
    public DateTime DeathTime { get; set; }
    public double AliveSeconds { get; set; }
}

public class GenerationRecord
{
    public int Generation { get; set; }
    public int AgentCount { get; set; }
    public int TotalTicks { get; set; }
    public int Attacks { get; set; }
    public int FoodEaten { get; set; }
    public int CorpsesEaten { get; set; }
    public float BestFitness { get; set; }
    public float BestAliveSeconds { get; set; }
    public float AvgAliveSeconds { get; set; }
    public string StartedAt { get; set; } = "";
    public string EndedAt { get; set; } = "";
}

public class StatsResult
{
    public int TotalGenerations { get; set; }
    public int TotalDeaths { get; set; }
    public float SuicideRateAll { get; set; }
    public float SuicideRateRecent_10 { get; set; }
    public double AvgAliveSecondsAll { get; set; }
    public double AvgAliveSecondsRecent_10 { get; set; }
    public double AvgEnergyAtDeath { get; set; }
    public double AvgWaterAtDeath { get; set; }
    public double AvgTemperatureAtDeath { get; set; }
    public double AvgAttacksPerLife { get; set; }
    public double AvgEatsPerLife { get; set; }
    public double AvgEmitsPerLife { get; set; }
    public float NightDeathRate { get; set; }
    public Dictionary<string, int> CauseDistribution { get; set; } = new();
    public List<GenerationStat> Generations { get; set; } = new();
}

public class GenerationStat
{
    public int Generation { get; set; }
    public string Cause { get; set; } = "";
    public double AliveSeconds { get; set; }
}
