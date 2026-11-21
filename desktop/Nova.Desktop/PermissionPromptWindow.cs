using System.Windows;
using System.Windows.Controls;

namespace Nova.Desktop;

internal sealed class PermissionPromptWindow : Window
{
    public PermissionPromptWindow(string title, string details)
    {
        Title = "NOVA · Berechtigung";
        Width = 440;
        Height = 320;
        MinWidth = 440;
        MinHeight = 320;
        MaxWidth = 440;
        MaxHeight = 320;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ThemeManager.TrackWindow(this);

        var layout = new Grid { Margin = new Thickness(20) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(heading, 0);
        layout.Children.Add(heading);

        var detailsText = new TextBlock
        {
            Text = details,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
        };
        detailsText.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        var detailsScroll = new ScrollViewer
        {
            Content = detailsText,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 14)
        };
        Grid.SetRow(detailsScroll, 1);
        layout.Children.Add(detailsScroll);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var skip = new Button
        {
            Content = "Überspringen",
            MinWidth = 105,
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };
        skip.Click += (_, _) => DialogResult = false;
        var allow = new Button
        {
            Content = "Ja",
            MinWidth = 75,
            Padding = new Thickness(12, 7, 12, 7),
            IsDefault = true
        };
        allow.SetResourceReference(Control.BackgroundProperty, "AccentBrush");
        allow.SetResourceReference(Control.ForegroundProperty, "AccentTextBrush");
        allow.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(skip);
        buttons.Children.Add(allow);
        Grid.SetRow(buttons, 2);
        layout.Children.Add(buttons);

        var dialogBorder = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = layout
        };
        dialogBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        Content = dialogBorder;
    }
}
