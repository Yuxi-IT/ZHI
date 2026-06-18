namespace ZHI.Shared;

/// <summary>
/// Heritable body parameters. Every agent carries a Genome that governs its physical traits.
/// All traits are continuous and subject to energy-economy trade-offs (e.g. large body = strength ↑, speed ↓, energy cost ↑).
/// </summary>
public sealed class Genome
{
    /// <summary>Body size multiplier (0.5–2.0). Affects energy consumption, strength, and visibility.</summary>
    public float Size { get; set; } = 1.0f;

    /// <summary>Movement speed multiplier (0.5–2.0). Larger values reduce move stamina cost.</summary>
    public float Speed { get; set; } = 1.0f;

    /// <summary>Attack strength multiplier (0.5–2.0). Scales damage dealt.</summary>
    public float Strength { get; set; } = 1.0f;

    /// <summary>Vision radius multiplier (0.5–2.0). Scales the 7×7 base vision window.</summary>
    public float VisionRange { get; set; } = 1.0f;

    /// <summary>Fat storage efficiency (0.0–1.0). Higher = more energy reserve before starvation.</summary>
    public float FatStorage { get; set; } = 0.5f;

    /// <summary>Cold resistance (0.0–1.0). Higher = less HP loss in cold environments.</summary>
    public float ColdResistance { get; set; } = 0.5f;

    /// <summary>Heat resistance (0.0–1.0). Higher = less water loss in hot environments.</summary>
    public float HeatResistance { get; set; } = 0.5f;

    /// <summary>Create a random genome with values centered at 1.0.</summary>
    public static Genome Random(Random rng, float mutationStd = 0.2f)
    {
        float Sample() => Math.Clamp(1.0f + (float)(rng.NextDouble() * 2 - 1) * mutationStd, 0.1f, 3.0f);
        return new Genome
        {
            Size = Math.Clamp(Sample(), 0.3f, 2.5f),
            Speed = Math.Clamp(Sample(), 0.3f, 2.5f),
            Strength = Math.Clamp(Sample(), 0.3f, 2.5f),
            VisionRange = Math.Clamp(Sample(), 0.3f, 2.5f),
            FatStorage = Math.Clamp(Sample(), 0.0f, 1.0f),
            ColdResistance = Math.Clamp(Sample(), 0.0f, 1.0f),
            HeatResistance = Math.Clamp(Sample(), 0.0f, 1.0f)
        };
    }

    /// <summary>Mutate this genome by adding gaussian noise to each trait.</summary>
    public Genome Mutate(Random rng, float std = 0.05f)
    {
        float Mut(float v, float min, float max) =>
            Math.Clamp(v + (float)(rng.NextDouble() * 2 - 1) * std, min, max);

        return new Genome
        {
            Size = Mut(Size, 0.3f, 2.5f),
            Speed = Mut(Speed, 0.3f, 2.5f),
            Strength = Mut(Strength, 0.3f, 2.5f),
            VisionRange = Mut(VisionRange, 0.3f, 2.5f),
            FatStorage = Mut(FatStorage, 0.0f, 1.0f),
            ColdResistance = Mut(ColdResistance, 0.0f, 1.0f),
            HeatResistance = Mut(HeatResistance, 0.0f, 1.0f)
        };
    }

    /// <summary>Energy consumption multiplier derived from body size (larger = hungrier).</summary>
    public float EnergyRate => 0.5f + Size * 0.5f;

    /// <summary>Effective move cost multiplier (faster = cheaper per cell).</summary>
    public float MoveCostMult => 2.0f - Speed * 0.5f;

    /// <summary>Effective attack damage multiplier.</summary>
    public float DamageMult => 0.5f + Strength * 0.5f;

    public Genome Clone() => new()
    {
        Size = Size, Speed = Speed, Strength = Strength,
        VisionRange = VisionRange, FatStorage = FatStorage,
        ColdResistance = ColdResistance, HeatResistance = HeatResistance
    };
}
