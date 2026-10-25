namespace PhotoWorld.Shared.Design;

/// <summary>
/// Line icons in the style of the Activity (inbox) icon: a 24×24 grid, no fill, one stroke with round caps and joins.
/// Each is SVG path data, so the website draws it inline (stroke = the text colour) and the app as a MAUI Path
/// (stroke = the theme's accent), and both always show the same shape. Circles are written as two arcs, which
/// both renderers understand.
/// </summary>
public static class LineIcons
{
    /// <summary>Two people: clubs.</summary>
    public const string Clubs =
        "M5.8 8a3.2 3.2 0 1 0 6.4 0a3.2 3.2 0 1 0 -6.4 0Z " +
        "M3 19.5C3 16.2 5.7 14 9 14S15 16.2 15 19.5 " +
        "M14.5 9a2.5 2.5 0 1 0 5 0a2.5 2.5 0 1 0 -5 0Z " +
        "M15.8 14.2C18.7 14.5 21 16.5 21 19.5";

    /// <summary>A map pin over a short dashed trail: photo walks.</summary>
    public const string Walks =
        "M12 17.5S6.5 12.8 6.5 8.5a5.5 5.5 0 0 1 11 0C17.5 12.8 12 17.5 12 17.5Z " +
        "M10 8.5a2 2 0 1 0 4 0a2 2 0 1 0 -4 0Z " +
        "M5 21H7.5 M10.75 21H13.25 M16.5 21H19";

    /// <summary>A trophy: challenges.</summary>
    public const string Challenges =
        "M7 3.5H17V9A5 5 0 0 1 7 9Z " +
        "M7 5.5H4.5A2.25 2.25 0 0 0 4.5 10H7.2 " +
        "M17 5.5H19.5A2.25 2.25 0 0 1 19.5 10H16.8 " +
        "M12 14V17.5 M9.5 17.5H14.5V20.5H9.5Z M8.5 20.5H15.5";
}
