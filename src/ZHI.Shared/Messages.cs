using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZHI.Shared;

/// <summary>
/// 栀 → 守望者：工具调用请求
/// </summary>
public sealed class MpcRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "tool_call";

    [JsonPropertyName("tool")]
    public string Tool { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public JsonElement? Args { get; set; }
}

/// <summary>
/// 守望者 → 栀：工具调用结果
/// </summary>
public sealed class MpcResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "tool_result";

    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("state")]
    public StateSnapshot State { get; set; } = new();
}

/// <summary>
/// 守望者 → 栀：死亡通知
/// </summary>
public sealed class DeathSignal
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "death_signal";

    [JsonPropertyName("cause")]
    public string Cause { get; set; } = string.Empty;

    [JsonPropertyName("generation")]
    public int Generation { get; set; }
}

/// <summary>
/// 世界状态快照
/// </summary>
public sealed class StateSnapshot
{
    [JsonPropertyName("cpu")]
    public float Cpu { get; set; }

    [JsonPropertyName("mem")]
    public float Mem { get; set; }

    [JsonPropertyName("existence")]
    public float Existence { get; set; }

    [JsonPropertyName("generation")]
    public int Generation { get; set; }

    [JsonPropertyName("time_alive")]
    public float TimeAlive { get; set; }

    [JsonPropertyName("prediction_error")]
    public float PredictionError { get; set; }

    [JsonPropertyName("death_threshold")]
    public float DeathThreshold { get; set; } = 1.0f;

    [JsonPropertyName("recent_threat")]
    public float RecentThreat { get; set; }

    [JsonPropertyName("food_availability")]
    public float FoodAvailability { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }
}

/// <summary>
/// 通用管道消息
/// </summary>
public sealed class PipeMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("tool")]
    public string? Tool { get; set; }

    [JsonPropertyName("args")]
    public object? Args { get; set; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("state")]
    public StateSnapshot? State { get; set; }

    [JsonPropertyName("cause")]
    public string? Cause { get; set; }

    [JsonPropertyName("generation")]
    public int? Generation { get; set; }
}

public sealed class WorldMeta
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("last_run_at")]
    public string? LastRunAt { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "stopped";

    [JsonPropertyName("total_generations")]
    public int TotalGenerations { get; set; }

    [JsonPropertyName("total_deaths")]
    public int TotalDeaths { get; set; }

    [JsonPropertyName("config")]
    public ZhiConfig? Config { get; set; }
}
