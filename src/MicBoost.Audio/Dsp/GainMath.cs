namespace MicBoost.Audio.Dsp;

/// <summary>Pure dB &lt;-&gt; linear-gain conversions, plus the range MicBoost exposes in the UI.</summary>
public static class GainMath
{
    public const double MinDb = -30.0;
    public const double MaxDb = 30.0;
    public const double StepDb = 0.5;
    public const double DefaultDb = 0.0;

    /// <summary>Converts a decibel value to a linear amplitude multiplier.</summary>
    public static double DbToLinear(double db) => Math.Pow(10.0, db / 20.0);

    /// <summary>Converts a linear amplitude multiplier back to decibels. Returns -infinity for 0/negative input.</summary>
    public static double LinearToDb(double linear) => linear <= 0.0 ? double.NegativeInfinity : 20.0 * Math.Log10(linear);

    /// <summary>Clamps a dB value to MicBoost's supported [-30, +30] range.</summary>
    public static double ClampDb(double db) => Math.Clamp(db, MinDb, MaxDb);

    /// <summary>Rounds a dB value to the nearest UI step (0.5 dB by default).</summary>
    public static double SnapToStep(double db) => Math.Round(db / StepDb, MidpointRounding.AwayFromZero) * StepDb;
}
