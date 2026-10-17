using System.Globalization;

namespace PhotoWorld.App;

/// <summary>True for anything that isn't null or an empty string. Use with ConverterParameter="not" to invert.</summary>
public sealed class HasValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var has = value is string s ? !string.IsNullOrWhiteSpace(s) : value is not null;
        return parameter as string == "not" ? !has : has;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
