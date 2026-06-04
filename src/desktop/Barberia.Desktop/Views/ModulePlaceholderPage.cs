using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Barberia.Desktop.Views;

public abstract class ModulePlaceholderPage : Page
{
    protected ModulePlaceholderPage(ModulePageContent content)
    {
        Content = BuildContent(content);
    }

    private static UIElement BuildContent(ModulePageContent content)
    {
        var root = new ScrollViewer
        {
            Background = Brush(248, 249, 251),
            Content = new StackPanel
            {
                Padding = new Thickness(32, 28, 32, 32),
                Spacing = 18,
                Children =
                {
                    CreateHero(content),
                    CreateSectionGrid(content)
                }
            }
        };

        return root;
    }

    private static UIElement CreateHero(ModulePageContent content)
    {
        var hero = new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24),
            Child = new Grid()
        };

        var layout = (Grid)hero.Child;
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBox = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(8),
            Background = Brush(31, 119, 104),
            Child = new FontIcon
            {
                Glyph = content.IconGlyph,
                FontSize = 26,
                Foreground = Brush(255, 255, 255)
            }
        };

        var titleStack = new StackPanel
        {
            Margin = new Thickness(18, 2, 0, 0),
            Spacing = 5,
            Children =
            {
                new TextBlock
                {
                    Text = content.Title,
                    FontSize = 30,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(30, 31, 34)
                },
                new TextBlock
                {
                    Text = content.Status,
                    FontSize = 14,
                    Foreground = Brush(101, 108, 116)
                }
            }
        };

        var stageBadge = new Border
        {
            Background = Brush(255, 247, 232),
            BorderBrush = Brush(242, 181, 88),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                Children =
                {
                    new Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill = Brush(242, 181, 88),
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "Shell",
                        FontSize = 13,
                        Foreground = Brush(122, 82, 21)
                    }
                }
            }
        };

        Grid.SetColumn(titleStack, 1);
        Grid.SetColumn(stageBadge, 2);
        layout.Children.Add(iconBox);
        layout.Children.Add(titleStack);
        layout.Children.Add(stageBadge);

        return hero;
    }

    private static UIElement CreateSectionGrid(ModulePageContent content)
    {
        var grid = new Grid
        {
            ColumnSpacing = 12,
            RowSpacing = 12
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        for (var index = 0; index < content.Sections.Count; index++)
        {
            var section = CreateSectionCard(content.Sections[index]);
            Grid.SetColumn(section, index % 3);
            Grid.SetRow(section, index / 3);

            if (grid.RowDefinitions.Count <= index / 3)
            {
                grid.RowDefinitions.Add(new RowDefinition());
            }

            grid.Children.Add(section);
        }

        return grid;
    }

    private static FrameworkElement CreateSectionCard(ModuleSectionContent section)
    {
        return new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new Border
                    {
                        Width = 38,
                        Height = 38,
                        CornerRadius = new CornerRadius(8),
                        Background = Brush(235, 248, 244),
                        Child = new FontIcon
                        {
                            Glyph = section.IconGlyph,
                            FontSize = 18,
                            Foreground = Brush(31, 119, 104)
                        }
                    },
                    new TextBlock
                    {
                        Text = section.Title,
                        FontSize = 18,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(30, 31, 34)
                    },
                    new TextBlock
                    {
                        Text = section.Detail,
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brush(101, 108, 116)
                    }
                }
            }
        };
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
