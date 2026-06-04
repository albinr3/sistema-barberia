using Microsoft.UI.Xaml.Controls;

namespace Barberia.Desktop.Shell;

public sealed class ShellModuleDefinition
{
    public ShellModuleDefinition(
        ShellModuleKey key,
        string title,
        string subtitle,
        string iconGlyph,
        Type pageType)
    {
        if (!typeof(Page).IsAssignableFrom(pageType))
        {
            throw new ArgumentException("Shell module page types must derive from WinUI Page.", nameof(pageType));
        }

        Key = key;
        Title = title;
        Subtitle = subtitle;
        IconGlyph = iconGlyph;
        PageType = pageType;
    }

    public ShellModuleKey Key { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string IconGlyph { get; }

    public Type PageType { get; }
}
