using System.Windows.Media;

namespace SpikeFinder.Models;

public record RenderableSpike(double MaxValue, double[] Original, double[] Spikes, bool Used);
