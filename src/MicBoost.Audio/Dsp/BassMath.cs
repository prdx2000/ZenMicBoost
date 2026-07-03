namespace MicBoost.Audio.Dsp;

/// <summary>Range and defaults for the low-shelf "bass" EQ control.</summary>
public static class BassMath
{
    public const double MinDb = -30.0;
    public const double MaxDb = 30.0;
    public const double StepDb = 0.5;
    public const double DefaultDb = 0.0;

    /// <summary>Clamps a dB value to MicBoost's supported bass range.</summary>
    public static double ClampDb(double db) => Math.Clamp(db, MinDb, MaxDb);
}
