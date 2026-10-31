using Microsoft.Maui.Controls.Shapes;
using PhotoWorld.Shared.Design;

namespace PhotoWorld.App.Services;

/// <summary>
/// <see cref="LineIcons"/> as MAUI geometry, for a <c>Path</c> stroked with the theme's accent (so it follows light
/// and dark like the website's, rather than being a black image). A new geometry each time: a Path owns its Data.
/// </summary>
public static class LineIconShapes
{
    private static readonly PathGeometryConverter Converter = new();

    public static Geometry Clubs => Parse(LineIcons.Clubs);
    public static Geometry Walks => Parse(LineIcons.Walks);
    public static Geometry Workshops => Parse(LineIcons.Workshops);
    public static Geometry Collabs => Parse(LineIcons.Collabs);
    public static Geometry Challenges => Parse(LineIcons.Challenges);
    public static Geometry Pin => Parse(LineIcons.Pin);
    public static Geometry People => Parse(LineIcons.People);
    public static Geometry Sunrise => Parse(LineIcons.Sunrise);

    private static Geometry Parse(string data) => (Geometry)Converter.ConvertFromInvariantString(data)!;
}
