namespace PhotoWorld.App.Views;

public static class ToolbarExtensions
{
    /// <summary>
    /// Shows or hides a toolbar item by adding or removing it. ToolbarItem has no IsVisible on every MAUI version,
    /// and a disabled item would still take space in the navigation bar.
    /// </summary>
    public static void SetToolbarItemVisible(this Page page, ToolbarItem item, bool visible)
    {
        var shown = page.ToolbarItems.Contains(item);
        if (visible && !shown) page.ToolbarItems.Add(item);
        else if (!visible && shown) page.ToolbarItems.Remove(item);
    }
}
