using Microsoft.Maui.Controls.Shapes;

namespace PhotoWorld.App.Views;

/// <summary>
/// A small line icon (one of <c>LineIconShapes</c>) in front of a line of text, both in the same theme colour: the app's
/// version of the website's <c>_LineIcon</c> beside a label, used where an emoji used to start the text.
/// </summary>
public class IconText : ContentView
{
    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(Geometry), typeof(IconText), propertyChanged: (b, _, v) => ((IconText)b)._path.Data = (Geometry?)v);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(IconText), propertyChanged: (b, _, v) => ((IconText)b)._label.Text = (string?)v);

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(IconText), 14.0, propertyChanged: (b, _, v) => ((IconText)b).Resize((double)v));

    /// <summary>The theme colour (a resource key) for both: "Muted" by default, "Accent" or "Text" otherwise.</summary>
    public static readonly BindableProperty ColorKeyProperty =
        BindableProperty.Create(nameof(ColorKey), typeof(string), typeof(IconText), "Muted", propertyChanged: (b, _, v) => ((IconText)b).Recolor((string)v));

    private readonly Microsoft.Maui.Controls.Shapes.Path _path = new()
    {
        StrokeThickness = 1.8, StrokeLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
        Fill = Colors.Transparent, Aspect = Stretch.Uniform, VerticalOptions = LayoutOptions.Center,
    };

    private readonly Label _label = new() { VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };

    public IconText()
    {
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 6 };
        grid.Add(_path, 0);
        grid.Add(_label, 1);
        Content = grid;
        Resize(FontSize);
        Recolor(ColorKey);
    }

    public Geometry? Icon { get => (Geometry?)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public string ColorKey { get => (string)GetValue(ColorKeyProperty); set => SetValue(ColorKeyProperty, value); }

    /// <summary>The icon is a little taller than the text, like an emoji was.</summary>
    private void Resize(double size)
    {
        _label.FontSize = size;
        _path.WidthRequest = _path.HeightRequest = Math.Round(size * 1.15);
    }

    private void Recolor(string key)
    {
        _label.SetDynamicResource(Label.TextColorProperty, key);
        _path.SetDynamicResource(Shape.StrokeProperty, key);
    }
}
