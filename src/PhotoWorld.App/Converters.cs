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

/// <summary>
/// A photo's review status as the badge on its tile ("Waiting for approval", "Not approved"), or null when approved.
/// Only owners are ever shown unapproved photos. Use with ConverterParameter="visible" for the badge's IsVisible.
/// </summary>
public sealed class ReviewBadgeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = (value as string) switch
        {
            PhotoWorld.Shared.Photos.PhotoReviewStatus.Pending => "Waiting for approval",
            PhotoWorld.Shared.Photos.PhotoReviewStatus.Rejected => "Not approved",
            _ => null,
        };
        return parameter as string == "visible" ? text is not null : text;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
