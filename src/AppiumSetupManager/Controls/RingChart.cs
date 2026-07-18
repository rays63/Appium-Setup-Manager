using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AppiumSetupManager.Controls;

/// <summary>
/// One colored arc of a <see cref="RingChart"/>. <paramref name="Fraction"/> is 0..1 of the full
/// circle; segments are stacked in the order supplied, starting at 12 o'clock and sweeping clockwise.
/// This is a UI-layer-only presentation type — it intentionally does not live in Core, since an
/// <see cref="IBrush"/> is a rendering concern, not a domain concept.
/// </summary>
public sealed record RingSegment(double Fraction, IBrush Brush);

/// <summary>
/// A minimal donut/ring chart: a full-circle background track plus zero or more colored arc
/// segments stacked clockwise from 12 o'clock. Used for the Dashboard/Doctor/Storage hero cards.
///
/// Degenerate case: a single segment covering (almost) the entire circle is drawn as a filled ring
/// via <see cref="DrawingContext.DrawEllipse"/> rather than as an arc, since an arc whose start and
/// end points coincide (sweep ≈ 360°) does not render as a visible geometry.
/// </summary>
public sealed class RingChart : Control
{
    public static readonly StyledProperty<IEnumerable<RingSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<RingChart, IEnumerable<RingSegment>?>(nameof(Segments));

    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<RingChart, double>(nameof(Diameter), 120d);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<RingChart, double>(nameof(StrokeThickness), 12d);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<RingChart, IBrush?>(nameof(TrackBrush));

    static RingChart()
    {
        AffectsRender<RingChart>(SegmentsProperty, StrokeThicknessProperty, TrackBrushProperty);
        AffectsMeasure<RingChart>(DiameterProperty);
    }

    public IEnumerable<RingSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public double Diameter
    {
        get => GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var d = Math.Max(0, Diameter);
        return new Size(d, d);
    }

    public override void Render(DrawingContext context)
    {
        var diameter = Diameter;
        var thickness = StrokeThickness;
        var radius = (diameter - thickness) / 2.0;
        if (radius <= 0)
            return;

        var center = new Point(diameter / 2.0, diameter / 2.0);

        // Background track — always drawn first, full circle.
        var trackBrush = TrackBrush ?? Brushes.Transparent;
        var trackPen = new Pen(trackBrush, thickness);
        context.DrawEllipse(null, trackPen, center, radius, radius);

        var segments = Segments?.Where(s => s.Fraction > 0).ToList();
        if (segments is null || segments.Count == 0)
            return;

        double cumulative = 0;
        foreach (var segment in segments)
        {
            var fraction = Math.Clamp(segment.Fraction, 0, 1);
            DrawSegment(context, center, radius, thickness, cumulative, fraction, segment.Brush);
            cumulative += fraction;
        }
    }

    private static void DrawSegment(
        DrawingContext context,
        Point center,
        double radius,
        double thickness,
        double cumulativeStart,
        double fraction,
        IBrush brush)
    {
        const double fullCircleEpsilon = 0.999;

        // A single segment spanning (almost) the whole ring: an arc's start/end points would
        // coincide and render nothing, so draw a filled ring instead.
        if (fraction >= fullCircleEpsilon && cumulativeStart <= 1e-6)
        {
            context.DrawEllipse(null, new Pen(brush, thickness), center, radius, radius);
            return;
        }

        var startAngle = -90.0 + cumulativeStart * 360.0;
        var sweepAngle = fraction * 360.0;
        var endAngle = startAngle + sweepAngle;

        var startPoint = PointOnCircle(center, radius, startAngle);
        var endPoint = PointOnCircle(center, radius, endAngle);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(startPoint, false);
            ctx.ArcTo(endPoint, new Size(radius, radius), 0, sweepAngle > 180.0, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }

        var pen = new Pen(brush, thickness) { LineCap = PenLineCap.Round };
        context.DrawGeometry(null, pen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}
