using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.Views;

/// <summary>
/// A single-series column chart of one metric per day (views or likes; never both on one scale). Columns are at most
/// 24px with 4px rounded tops and a square foot, a hairline baseline, and a few clean ticks. Tapping or dragging over a
/// column shows its day and value in <see cref="Selected"/>.
/// </summary>
public class ColumnChart : GraphicsView, IDrawable
{
    public static readonly BindableProperty DaysProperty = BindableProperty.Create(nameof(Days), typeof(IReadOnlyList<InsightDayDto>),
        typeof(ColumnChart), null, propertyChanged: (b, _, _) => ((ColumnChart)b).Changed());

    public static readonly BindableProperty MetricProperty = BindableProperty.Create(nameof(Metric), typeof(string),
        typeof(ColumnChart), "views", propertyChanged: (b, _, _) => ((ColumnChart)b).Changed());

    public static readonly BindableProperty SelectedProperty = BindableProperty.Create(nameof(Selected), typeof(string),
        typeof(ColumnChart), "", BindingMode.OneWayToSource);

    private const float Left = 30, Top = 8, Bottom = 20;
    private int _selected = -1;

    public ColumnChart()
    {
        Drawable = this;
        HeightRequest = 170;
        StartInteraction += (_, e) => Select(e.Touches[0].X);
        DragInteraction += (_, e) => Select(e.Touches[0].X);
        Application.Current!.RequestedThemeChanged += (_, _) => Invalidate();
    }

    public IReadOnlyList<InsightDayDto>? Days { get => (IReadOnlyList<InsightDayDto>?)GetValue(DaysProperty); set => SetValue(DaysProperty, value); }
    public string Metric { get => (string)GetValue(MetricProperty); set => SetValue(MetricProperty, value); }
    public string Selected { get => (string)GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }

    private int Value(InsightDayDto d) => Metric == "likes" ? d.Likes : d.Views;

    private void Changed()
    {
        _selected = -1;
        Selected = Summary();
        SemanticProperties.SetDescription(this, Summary());
        Invalidate();
    }

    private string Summary()
    {
        if (Days is not { Count: > 0 } days) return "";
        var total = days.Sum(Value);
        var best = days.MaxBy(Value)!;
        return total == 0 ? $"No {Metric} in the last 30 days" : $"{total:N0} {Metric} · busiest {best.Date:ddd d MMM} ({Value(best):N0})";
    }

    private void Select(float x)
    {
        if (Days is not { Count: > 0 } days) return;
        var slot = ((float)Width - Left) / days.Count;
        var i = Math.Clamp((int)((x - Left) / slot), 0, days.Count - 1);
        if (i == _selected) return;
        _selected = i;
        Selected = $"{days[i].Date:ddd d MMM}: {Value(days[i]):N0} {Metric}";
        Invalidate();
    }

    private static Color Resource(string key) =>
        Application.Current!.Resources.TryGetValue(key, out var v) && v is Color c ? c : Colors.Gray;

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (Days is not { Count: > 0 } days) return;
        var values = days.Select(Value).ToList();
        var (top, step) = NiceMax(values.Max());
        var plotH = rect.Height - Top - Bottom;
        var plotW = rect.Width - Left;
        float Y(int v) => Top + plotH - v / (float)top * plotH;

        canvas.FontSize = 10;
        canvas.FontColor = Resource("Muted");
        for (var t = 0; t <= top; t += step)
        {
            canvas.StrokeColor = t == 0 ? Resource("Muted") : Resource("Line");
            canvas.StrokeSize = 1;
            canvas.DrawLine(Left, Y(t), rect.Width, Y(t));
            canvas.DrawString(t >= 1000 ? $"{t / 1000.0:0.#}K" : t.ToString(), 0, Y(t) - 6, Left - 6, 12,
                HorizontalAlignment.Right, VerticalAlignment.Center);
        }

        var slot = plotW / values.Count;
        var barW = Math.Min(24f, slot - 2);
        for (var i = 0; i < values.Count; i++)
        {
            var x = Left + i * slot + (slot - barW) / 2;
            if (values[i] > 0)
            {
                var h = Top + plotH - Y(values[i]);
                var r = Math.Min(4f, Math.Min(barW / 2, h));
                canvas.FillColor = i == _selected ? Resource("Text") : Resource("Chart");
                var path = new PathF();
                path.MoveTo(x, Top + plotH);
                path.LineTo(x, Y(values[i]) + r);
                path.QuadTo(x, Y(values[i]), x + r, Y(values[i]));
                path.LineTo(x + barW - r, Y(values[i]));
                path.QuadTo(x + barW, Y(values[i]), x + barW, Y(values[i]) + r);
                path.LineTo(x + barW, Top + plotH);
                path.Close();
                canvas.FillPath(path);
            }
            if (i % 7 == 0 || i == values.Count - 1)
                canvas.DrawString(days[i].Date.ToString("d MMM"), Left + i * slot + slot / 2 - 30, rect.Height - Bottom + 4, 60, 14,
                    HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }

    /// <summary>A clean axis top: steps of 1, 2 or 5 × 10^n, about four ticks.</summary>
    private static (int Top, int Step) NiceMax(int max)
    {
        if (max <= 4) return (4, 1);
        var raw = max / 4.0;
        var mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var step = (int)new[] { 1, 2, 5, 10 }.Select(m => m * mag).First(s => s >= raw);
        return ((int)Math.Ceiling(max / (double)step) * step, step);
    }
}
