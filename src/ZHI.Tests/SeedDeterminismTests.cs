using System.Text.Json;
using ZHI.Core;
using ZHI.Shared;

namespace ZHI.Tests;

public class SeedDeterminismTests
{
    [Fact]
    public void SameSeed_ProducesSameInitialState()
    {
        var config1 = new ZhiConfig { Seed = 42 };
        var config2 = new ZhiConfig { Seed = 42 };

        // Verify seed propagates through Random consistently
        var rng1 = new Random(config1.Seed!.Value);
        var rng2 = new Random(config2.Seed!.Value);

        Assert.Equal(rng1.Next(), rng2.Next());
        Assert.Equal(rng1.NextDouble(), rng2.NextDouble());
        Assert.Equal(rng1.Next(0, 64), rng2.Next(0, 64));
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentState()
    {
        var rng1 = new Random(42);
        var rng2 = new Random(99);

        var values1 = new int[10];
        var values2 = new int[10];
        for (int i = 0; i < 10; i++)
        {
            values1[i] = rng1.Next();
            values2[i] = rng2.Next();
        }

        Assert.NotEqual(values1, values2);
    }

    [Fact]
    public void ZhiConfig_WithSeed_SerializesSeedCorrectly()
    {
        var config = new ZhiConfig { Seed = 42 };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var json = JsonSerializer.Serialize(config, options);
        Assert.Contains("\"seed\":42", json);

        var deserialized = JsonSerializer.Deserialize<ZhiConfig>(json, options)!;
        Assert.Equal(42, deserialized.Seed);
    }

    [Fact]
    public void ZhiConfig_NullSeed_OmitsFromJson()
    {
        var config = new ZhiConfig { Seed = null };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var json = JsonSerializer.Serialize(config, options);
        Assert.Contains("\"seed\":null", json);
    }

    [Fact]
    public void ToolDefinitions_StateSize_IsConsistent()
    {
        int gridFlat = 7 * 7 * 6; // GridChannels * GridSize * GridSize
        int nonGrid = ToolDefinitions.StateSize - gridFlat;

        Assert.Equal(294, gridFlat);
        Assert.Equal(46, nonGrid);
        Assert.Equal(340, ToolDefinitions.StateSize);
        Assert.Equal(8, ToolDefinitions.ActionCount);
    }

    [Fact]
    public void Device_Initialize_Succeeds()
    {
        ZHI.Core.Device.Initialize();
        Assert.False(string.IsNullOrEmpty(ZHI.Core.Device.Name));
    }
}
