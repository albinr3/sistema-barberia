using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Microsoft.UI.Text;
using Barberia.Desktop.Shell;

namespace Barberia.Desktop.Views;

public sealed partial class BarberPublicPage : Page
{
    private readonly LocalAdminService _service = new();
    private IReadOnlyList<Barber> _barbers = [];
    private IReadOnlyDictionary<Guid, DailyRotationEntry> _dailyRotationEntries = new Dictionary<Guid, DailyRotationEntry>();
    private int _currentPage = 1;
    private const int PageSize = 12;

    public event EventHandler? ShellMenuRequested;

    public BarberPublicPage()
    {
        InitializeComponent();
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args) => ShellMenuRequested?.Invoke(this, EventArgs.Empty);
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LoadData();
    }

    private void LoadData()
    {
        try
        {
            var snapshot = _service.Load();
            _barbers = snapshot.Barbers
                .Where(b => b.IsActive)
                .OrderBy(b => b.StationNumber ?? int.MaxValue)
                .ThenBy(b => b.DisplayName)
                .ToList();
            _dailyRotationEntries = snapshot.DailyRotationEntries.ToDictionary(entry => entry.BarberId);
            _currentPage = 1;
            UpdatePagination();
            SetStatus("", success: true);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, success: false);
        }
    }

    private void UpdatePagination()
    {
        var totalItems = _barbers.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));

        if (_currentPage > totalPages)
        {
            _currentPage = totalPages;
        }

        var pagedBarbers = _barbers.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();

        _rosterGrid.Children.Clear();

        for (int i = 0; i < pagedBarbers.Count; i++)
        {
            var barber = pagedBarbers[i];
            var card = CreateBarberCard(barber);
            Grid.SetRow(card, i / 4);
            Grid.SetColumn(card, i % 4);
            _rosterGrid.Children.Add(card);
        }

        var startIndex = totalItems == 0 ? 0 : (_currentPage - 1) * PageSize + 1;
        var endIndex = Math.Min(_currentPage * PageSize, totalItems);
        _footerText.Text = $"Showing {startIndex}-{endIndex} of {totalItems} barbers";

        _paginationPanel.Visibility = totalPages > 1 ? Visibility.Visible : Visibility.Collapsed;

        if (totalPages > 1)
        {
            RenderPaginationButtons(totalPages);
        }
    }

    private void RenderPaginationButtons(int totalPages)
    {
        _paginationPanel.Children.Clear();

        var prevButton = new Button
        {
            MinHeight = 34,
            MinWidth = 34,
            Content = "\uE76B",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            IsEnabled = _currentPage > 1
        };
        prevButton.Click += (_, _) => { _currentPage--; UpdatePagination(); };
        _paginationPanel.Children.Add(prevButton);

        for (int i = 1; i <= totalPages; i++)
        {
            var pageNum = i;
            var isCurrent = pageNum == _currentPage;
            var pageButton = new Button
            {
                MinHeight = 34,
                MinWidth = 34,
                Content = pageNum.ToString()
            };
            if (isCurrent)
            {
                pageButton.Background = Brush(223, 224, 255);
                pageButton.BorderBrush = Brush(0, 19, 135);
                pageButton.Foreground = Brush(0, 11, 98);
            }
            pageButton.Click += (_, _) => { _currentPage = pageNum; UpdatePagination(); };
            _paginationPanel.Children.Add(pageButton);
        }

        var nextButton = new Button
        {
            MinHeight = 34,
            MinWidth = 34,
            Content = "\uE76C",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            IsEnabled = _currentPage < totalPages
        };
        nextButton.Click += (_, _) => { _currentPage++; UpdatePagination(); };
        _paginationPanel.Children.Add(nextButton);
    }

    private FrameworkElement CreateBarberCard(Barber barber)
    {
        var card = new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(226, 226, 229),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };

        var container = new StackPanel
        {
            Spacing = 16
        };

        var identity = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        identity.Children.Add(CreateProfileAvatar(barber, 48));

        var details = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        details.Children.Add(new TextBlock
        {
            Text = barber.DisplayName,
            FontFamily = new FontFamily("Inter"),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(26, 28, 30),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        details.Children.Add(new TextBlock
        {
            Text = barber.StationCode ?? "Unassigned",
            FontFamily = new FontFamily("Inter"),
            FontSize = 13,
            Foreground = Brush(68, 70, 85)
        });
        details.Children.Add(new TextBlock
        {
            Text = FormatDailyRotationText(barber),
            FontFamily = new FontFamily("Inter"),
            FontSize = 12,
            Foreground = Brush(68, 70, 85),
            TextWrapping = TextWrapping.WrapWholeWords
        });

        identity.Children.Add(details);
        container.Children.Add(identity);

        var toggleContainer = new Grid();
        toggleContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toggleContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stateLabel = new TextBlock
        {
            Text = FormatBarberState(barber.State),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Inter"),
            FontSize = 14,
            Foreground = Brush(26, 28, 30)
        };
        toggleContainer.Children.Add(stateLabel);

        var stateSwitch = new ToggleSwitch
        {
            IsOn = barber.State != BarberState.Offline,
            OnContent = null,
            OffContent = null,
            IsEnabled = barber.State is not BarberState.Called and not BarberState.InService,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(stateSwitch, 1);

        stateSwitch.Toggled += (_, _) =>
        {
            try
            {
                if (stateSwitch.IsOn)
                {
                    _service.MarkBarberAvailable(barber.Id);
                }
                else
                {
                    _service.MarkBarberOffline(barber.Id);
                }
                LoadData();
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, success: false);
                stateSwitch.IsOn = barber.State != BarberState.Offline; // Revert visually
            }
        };

        toggleContainer.Children.Add(stateSwitch);
        container.Children.Add(toggleContainer);

        card.Child = container;
        return card;
    }

    private void SetStatus(string text, bool success)
    {
        _messageText.Text = text;
        _messageText.Foreground = success ? Brush(17, 105, 88) : Brush(154, 58, 47);
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
    }

    private static string FormatBarberState(BarberState state) => state switch
    {
        BarberState.Available => "Available",
        BarberState.Offline => "Offline",
        BarberState.Called => "Called",
        BarberState.InService => "In service",
        BarberState.NotCheckedIn => "Not checked in",
        _ => state.ToString()
    };

    private string FormatDailyRotationText(Barber barber)
    {
        return _dailyRotationEntries.TryGetValue(barber.Id, out var entry)
            ? $"Arrival #{entry.QueuePosition + 1} - {entry.ArrivedAt:hh:mm tt}"
            : "Not checked in today";
    }

    private static Grid CreateProfileAvatar(Barber barber, double size)
    {
        return CreateProfileAvatar(barber.DisplayName, barber.ProfileImagePath, size);
    }

    private static Grid CreateProfileAvatar(string displayName, string? relativeImagePath, double size)
    {
        var avatar = new Grid
        {
            Width = size,
            Height = size,
            VerticalAlignment = VerticalAlignment.Center
        };

        avatar.Children.Add(new Ellipse
        {
            Fill = Brush(243, 243, 246),
            Stroke = Brush(226, 230, 235),
            StrokeThickness = 1
        });

        avatar.Children.Add(new TextBlock
        {
            Text = GetInitials(displayName),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = size >= 50 ? 18 : 15,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(0, 19, 135)
        });

        var imagePath = ProfileImageCatalog.ResolveImagePath(relativeImagePath);
        if (imagePath is not null)
        {
            var imageBrush = new ImageBrush
            {
                Stretch = Stretch.UniformToFill
            };
            var imageCircle = new Ellipse
            {
                Fill = imageBrush,
                Stroke = Brush(255, 255, 255),
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed
            };
            avatar.Children.Add(imageCircle);
            _ = LoadProfileImageAsync(imageBrush, imageCircle, imagePath);
        }

        return avatar;
    }

    private static string GetInitials(string displayName)
    {
        var parts = displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]));

        var initials = string.Concat(parts);
        return string.IsNullOrWhiteSpace(initials) ? "?" : initials;
    }

    private static async Task LoadProfileImageAsync(ImageBrush imageBrush, UIElement imageElement, string fullPath)
    {
        try
        {
            await using var fileStream = File.OpenRead(fullPath);
            using var imageStream = fileStream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(imageStream);
            var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);
            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(bitmap);
            imageBrush.ImageSource = source;
            imageElement.Visibility = Visibility.Visible;
        }
        catch
        {
            imageBrush.ImageSource = null;
            imageElement.Visibility = Visibility.Collapsed;
        }
    }
}
