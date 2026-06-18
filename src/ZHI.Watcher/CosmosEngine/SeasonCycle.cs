namespace ZHI.Watcher;

public partial class CosmosEngine
{
    private float _seasonProgress;
    private float _seasonTemperatureModifier = 1f;

    public float SeasonProgress => _seasonProgress;
    public bool IsWetSeason => _seasonProgress > 0.25f && _seasonProgress < 0.75f;

    private void ApplySeasonCycle()
    {
        // One full cycle per ~10,000 ticks
        _seasonProgress += 1f / 10000f;
        if (_seasonProgress >= 1f) _seasonProgress -= 1f;

        float seasonSin = MathF.Sin(_seasonProgress * MathF.PI * 2);
        float seasonFactor = (seasonSin + 1f) / 2f; // 0 (dry) to 1 (wet)

        // Season drives humidity toward its target
        _humidity += (seasonFactor - _humidity) * 0.01f;

        // Dry season: wider temperature swing
        _seasonTemperatureModifier = 1f + (1f - seasonFactor) * 0.3f;
    }
}
