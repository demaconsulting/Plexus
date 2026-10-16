using DemaConsulting.Plexus.Demo.Model;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for the math behind the demo's audio inspectors (<see cref="AudioPreview"/>) and the settings readers.
/// </summary>
public class AudioPreviewTests
{
    /// <summary>
    ///     The tolerance of floating-point comparisons.
    /// </summary>
    private const double Tolerance = 1e-9;

    /// <summary>
    ///     Creates oscillator settings with a waveform at 1 Hz, so a time in seconds is a fraction of a cycle.
    /// </summary>
    /// <param name="waveform">The waveform.</param>
    /// <returns>The settings.</returns>
    private static OscillatorSettings OneHertz(string waveform) => new(waveform, 1.0, 1.0, 0.0, 0.0);

    /// <summary>
    ///     Validates that each waveform has its characteristic values at a quarter, a half and three quarters of a cycle.
    /// </summary>
    [Fact]
    public void AudioPreview_OscillatorSample_EachWaveform_HasItsShape()
    {
        // Arrange
        var sine = OneHertz("Sine");
        var square = OneHertz("Square");
        var saw = OneHertz("Saw");
        var triangle = OneHertz("Triangle");

        // Act / Assert: sine
        Assert.Equal(0.0, AudioPreview.OscillatorSample(sine, 0), Tolerance);
        Assert.Equal(1.0, AudioPreview.OscillatorSample(sine, 0.25), Tolerance);
        Assert.Equal(-1.0, AudioPreview.OscillatorSample(sine, 0.75), Tolerance);

        // Act / Assert: square is +1 in the first half and -1 in the second
        Assert.Equal(1.0, AudioPreview.OscillatorSample(square, 0.1));
        Assert.Equal(-1.0, AudioPreview.OscillatorSample(square, 0.6));

        // Act / Assert: saw rises from 0 to +1, jumps to -1 and rises again
        Assert.Equal(0.0, AudioPreview.OscillatorSample(saw, 0), Tolerance);
        Assert.Equal(0.5, AudioPreview.OscillatorSample(saw, 0.25), Tolerance);
        Assert.Equal(-0.5, AudioPreview.OscillatorSample(saw, 0.75), Tolerance);

        // Act / Assert: triangle peaks at a quarter and has straight sides
        Assert.Equal(1.0, AudioPreview.OscillatorSample(triangle, 0.25), Tolerance);
        Assert.Equal(0.5, AudioPreview.OscillatorSample(triangle, 0.125), Tolerance);
        Assert.Equal(-1.0, AudioPreview.OscillatorSample(triangle, 0.75), Tolerance);
    }

    /// <summary>
    ///     Validates that amplitude scales the wave (clamped to 0..1), phase shifts it and the DC offset is added.
    /// </summary>
    [Fact]
    public void AudioPreview_OscillatorSample_AmplitudePhaseAndOffset_AreApplied()
    {
        // Arrange
        var half = new OscillatorSettings("Sine", 1.0, 0.5, 0.0, 0.0);
        var shifted = new OscillatorSettings("Sine", 1.0, 1.0, 90.0, 0.0);
        var offset = new OscillatorSettings("Sine", 1.0, 1.0, 0.0, 0.25);
        var tooLoud = new OscillatorSettings("Sine", 1.0, 3.0, 0.0, 0.0);
        var silent = new OscillatorSettings("Sine", 1.0, -1.0, 0.0, 0.5);

        // Act / Assert
        Assert.Equal(0.5, AudioPreview.OscillatorSample(half, 0.25), Tolerance);
        Assert.Equal(1.0, AudioPreview.OscillatorSample(shifted, 0), Tolerance);
        Assert.Equal(1.25, AudioPreview.OscillatorSample(offset, 0.25), Tolerance);
        Assert.Equal(1.0, AudioPreview.OscillatorSample(tooLoud, 0.25), Tolerance);
        Assert.Equal(0.5, AudioPreview.OscillatorSample(silent, 0.25), Tolerance);
    }

    /// <summary>
    ///     Validates that the oscillator plot spans two periods in ms with labelled time ticks and amplitude grid lines that
    ///     fit the offset wave.
    /// </summary>
    [Fact]
    public void AudioPreview_Oscillator_SpansTwoCyclesInMilliseconds()
    {
        // Arrange
        var settings = OscillatorSettings.Default;
        var offset = settings with { DcOffset = 0.8 };

        // Act
        var plot = AudioPreview.Oscillator(settings);
        var raised = AudioPreview.Oscillator(offset);

        // Assert: 440 Hz has a period of 2.2727 ms, so two cycles are 4.545 ms
        Assert.Equal(AudioPreview.OscillatorSegments + 1, plot.Points.Count);
        Assert.Equal(0.0, plot.Points[0].X);
        Assert.Equal(2000.0 / 440.0, plot.Points[^1].X, Tolerance);
        Assert.Equal(2000.0 / 440.0, plot.XAxis.Maximum, Tolerance);
        Assert.Equal("Time (ms)", plot.XAxis.Title);
        Assert.Equal(["0", "1", "2", "3", "4"], plot.XAxis.Ticks.Select(t => t.Label));
        Assert.Equal([-1.0, -0.5, 0.0, 0.5, 1.0], plot.YAxis.Ticks.Select(t => t.Value));
        Assert.Equal("Amplitude", plot.YAxis.Title);
        Assert.All(plot.Points, p => Assert.InRange(p.Y, -1.0, 1.0));
        Assert.Equal(-2.0, raised.YAxis.Minimum);
        Assert.Equal(2.0, raised.YAxis.Maximum);
        Assert.All(raised.Points, p => Assert.InRange(p.Y, -0.2 - Tolerance, 1.8 + Tolerance));
    }

    /// <summary>
    ///     Validates the short oscillator description used by the tooltip.
    /// </summary>
    [Fact]
    public void AudioPreview_Describe_ShowsWaveformAndFrequency()
    {
        // Arrange
        var settings = new OscillatorSettings("Saw", 221.5, 1, 0, 0);

        // Act / Assert
        Assert.Equal("Sine, 440 Hz", AudioPreview.Describe(OscillatorSettings.Default));
        Assert.Equal("Saw, 221.5 Hz", AudioPreview.Describe(settings));
    }

    /// <summary>
    ///     Validates the short envelope summary used by the tooltip: the four phases in A-D-S-R order, as set.
    /// </summary>
    [Fact]
    public void AudioPreview_Describe_Envelope_ListsAdsrTimesAndLevel()
    {
        // Arrange
        var settings = new EnvelopeSettings(2.5, 80, 45, 1000);

        // Act / Assert
        Assert.Equal("A 10 ms · D 120 ms · S 70 % · R 300 ms", AudioPreview.Describe(EnvelopeSettings.Default));
        Assert.Equal("A 2.5 ms · D 80 ms · S 45 % · R 1000 ms", AudioPreview.Describe(settings));
        Assert.Throws<ArgumentNullException>(() => AudioPreview.Describe((EnvelopeSettings)null!));
    }

    /// <summary>
    ///     Validates the short filter summary used by the tooltip: the type and the cutoff, as set.
    /// </summary>
    [Fact]
    public void AudioPreview_Describe_Filter_IsTypeAndCutoff()
    {
        // Arrange
        var settings = new FilterSettings("High-pass", 99999);

        // Act / Assert
        Assert.Equal("Low-pass, 1200 Hz", AudioPreview.Describe(FilterSettings.Default));
        Assert.Equal("High-pass, 99999 Hz", AudioPreview.Describe(settings));
        Assert.Throws<ArgumentNullException>(() => AudioPreview.Describe((FilterSettings)null!));
    }

    /// <summary>
    ///     Validates that the envelope follows attack, decay to sustain, the illustrative hold and release, with each phase
    ///     labelled on a ms axis.
    /// </summary>
    [Fact]
    public void AudioPreview_Envelope_FollowsAdsrWithLabelledPhases()
    {
        // Arrange
        var settings = new EnvelopeSettings(10, 120, 70, 300);

        // Act
        var points = AudioPreview.EnvelopePoints(settings);
        var plot = AudioPreview.Envelope(settings);
        var clamped = AudioPreview.EnvelopePoints(new EnvelopeSettings(-5, 50, 150, 20));

        // Assert
        Assert.Equal(
            [new PlotPoint(0, 0), new PlotPoint(10, 1), new PlotPoint(130, 0.7), new PlotPoint(330, 0.7), new PlotPoint(630, 0)],
            points.Select(p => new PlotPoint(p.X, Math.Round(p.Y, 9))));
        Assert.Equal(["A", "D", "S", "R"], plot.Regions.Select(r => r.Label));
        Assert.Equal([0.0, 10.0, 130.0, 330.0], plot.Regions.Select(r => r.Start));
        Assert.Equal(630.0, plot.Regions[^1].End);
        Assert.Equal("Time (ms)", plot.XAxis.Title);
        Assert.Equal(630.0, plot.XAxis.Maximum);
        Assert.Equal("100%", plot.YAxis.Ticks[^1].Label);
        Assert.Equal(new PlotPoint(0, 1), clamped[1]);
        Assert.Equal(1.0, clamped[2].Y);
    }

    /// <summary>
    ///     Validates that the low-pass and high-pass models are 3 dB down at the cutoff and roll off at 12 dB per octave
    ///     on the far side.
    /// </summary>
    [Fact]
    public void AudioPreview_FilterMagnitude_LowAndHighPassAreMinus3dBAtCutoff()
    {
        // Arrange
        var lowPass = new FilterSettings("Low-pass", 1000);
        var highPass = new FilterSettings("High-pass", 1000);

        // Act / Assert: -3 dB at the cutoff
        Assert.Equal(-3.0103, AudioPreview.FilterMagnitudeDb(lowPass, 1000), 3);
        Assert.Equal(-3.0103, AudioPreview.FilterMagnitudeDb(highPass, 1000), 3);

        // Act / Assert: flat in the pass band, steep in the stop band
        Assert.Equal(0.0, AudioPreview.FilterMagnitudeDb(lowPass, 20), 1);
        Assert.Equal(0.0, AudioPreview.FilterMagnitudeDb(highPass, 20000), 1);
        Assert.InRange(AudioPreview.FilterMagnitudeDb(lowPass, 10000), -41, -39);
        Assert.InRange(AudioPreview.FilterMagnitudeDb(highPass, 100), -41, -39);
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioPreview.FilterMagnitudeDb(lowPass, 0));
    }

    /// <summary>
    ///     Validates that the band-pass model peaks at 0 dB at the cutoff and falls away on both sides.
    /// </summary>
    [Fact]
    public void AudioPreview_FilterMagnitude_BandPassPeaksAtCutoff()
    {
        // Arrange
        var bandPass = new FilterSettings("Band-pass", 1000);

        // Act
        var atCutoff = AudioPreview.FilterMagnitudeDb(bandPass, 1000);
        var below = AudioPreview.FilterMagnitudeDb(bandPass, 100);
        var above = AudioPreview.FilterMagnitudeDb(bandPass, 10000);

        // Assert
        Assert.Equal(0.0, atCutoff, 6);
        Assert.True(below < -15);
        Assert.Equal(below, above, 6);
    }

    /// <summary>
    ///     Validates the filter plot: a logarithmic 20 Hz to 20 kHz axis with labelled decades, a dB axis every 12 dB and
    ///     a cutoff marker.
    /// </summary>
    [Fact]
    public void AudioPreview_Filter_UsesLogFrequencyAxisWithCutoffMarker()
    {
        // Arrange
        var settings = FilterSettings.Default;

        // Act
        var plot = AudioPreview.Filter(settings);
        var clamped = AudioPreview.Filter(settings with { Cutoff = 5 });

        // Assert
        Assert.True(plot.XAxis.IsLogarithmic);
        Assert.Equal(20.0, plot.XAxis.Minimum);
        Assert.Equal(20000.0, plot.XAxis.Maximum);
        Assert.Equal(["20", "50", "100", "200", "500", "1k", "2k", "5k", "10k", "20k"], plot.XAxis.Ticks.Select(t => t.Label));
        Assert.Equal([-48.0, -36.0, -24.0, -12.0, 0.0], plot.YAxis.Ticks.Select(t => t.Value));
        Assert.Equal("Gain (dB)", plot.YAxis.Title);
        Assert.Equal(new PlotMarker(1200, "fc"), Assert.Single(plot.Markers));
        Assert.Equal(20.0, plot.Points[0].X, 6);
        Assert.Equal(20000.0, plot.Points[^1].X, 6);
        Assert.All(plot.Points, p => Assert.InRange(p.Y, AudioPreview.FilterFloorDb, 0.0 + Tolerance));
        Assert.Equal(20.0, Assert.Single(clamped.Markers).X);
    }

    /// <summary>
    ///     Validates that the settings readers accept numbers of any kind and invariant text, and fall back to the defaults
    ///     for missing, wrong or non-finite values and unknown choices.
    /// </summary>
    [Fact]
    public void AudioSettings_Read_MissingOrWrongValues_UseDefaults()
    {
        // Arrange
        var values = new Dictionary<string, object?>
        {
            [DemoNodeTypes.WaveformProperty] = "Noise",
            [DemoNodeTypes.FrequencyProperty] = "880.5",
            [DemoNodeTypes.AmplitudeProperty] = double.NaN,
            [DemoNodeTypes.PhaseProperty] = 45,
            [DemoNodeTypes.DcOffsetProperty] = true,
            [DemoNodeTypes.AttackProperty] = 25L,
            [DemoNodeTypes.SustainProperty] = "loud",
            [DemoNodeTypes.FilterTypeProperty] = "High-pass",
        };
        object? Reader(string name) => values.GetValueOrDefault(name);

        // Act
        var oscillator = OscillatorSettings.Read(Reader);
        var envelope = EnvelopeSettings.Read(Reader);
        var filter = FilterSettings.Read(Reader);
        var empty = OscillatorSettings.Read(_ => null);

        // Assert
        Assert.Equal(new OscillatorSettings("Sine", 880.5, 1.0, 45.0, 0.0), oscillator);
        Assert.Equal(new EnvelopeSettings(25, 120, 70, 300), envelope);
        Assert.Equal(new FilterSettings("High-pass", 1200), filter);
        Assert.Equal(OscillatorSettings.Default, empty);
        Assert.Throws<ArgumentNullException>(() => OscillatorSettings.Read(null!));
        Assert.Throws<ArgumentNullException>(() => OscillatorSettings.FromNode(null!));
    }
}
