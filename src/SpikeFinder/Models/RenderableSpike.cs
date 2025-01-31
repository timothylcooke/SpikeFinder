using System.Windows.Media;

namespace SpikeFinder.Models;

public record RenderableSpike(double MaxValue, double[] Spikes, Geometry[] Geometries, bool Used);
