using System.Windows;
using System.Windows.Controls;

namespace Nova.Desktop;

internal sealed class ConversationPickerWindow : Window
{
    private readonly ListBox _conversations;
    private readonly TextBlock _preview;

    public ConversationPickerWindow(IReadOnlyList<ConversationRecord> conversations, string query)
    {
        Title = "Frühere Unterhaltung auswählen";
        Width = 620;
        Height = 560;
        MinWidth = 480;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ThemeManager.TrackWindow(this);

        _conversations = new ListBox
        {
            ItemsSource = conversations,
            DisplayMemberPath = nameof(ConversationRecord.Title),
            Margin = new Thickness(0, 12, 0, 12)
        };
        _conversations.SetResourceReference(Control.BackgroundProperty, "InputBrush");
        _conversations.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        _conversations.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        _conversations.SelectionChanged += (_, _) => UpdatePreview();
        _preview = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        _preview.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");

        var content = new Grid { Margin = new Thickness(22) };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "Nur nach deiner Freigabe",
            FontSize = 21,
            FontWeight = FontWeights.SemiBold
        });
        heading.Children.Add(new TextBlock
        {
            Text = $"NOVA fragt nach einer früheren Unterhaltung zu: {query}",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 7, 0, 0)
        });
        content.Children.Add(heading);
        Grid.SetRow(_conversations, 1);
        content.Children.Add(_conversations);
        Grid.SetRow(_preview, 2);
        content.Children.Add(_preview);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var cancel = new Button { Content = "Abbrechen", MinWidth = 95, Margin = new Thickness(0, 0, 9, 0), Padding = new Thickness(12, 7, 12, 7) };
        cancel.Click += (_, _) => DialogResult = false;
        var select = new Button
        {
            Content = "Unterhaltung auswählen",
            MinWidth = 165,
            Padding = new Thickness(12, 7, 12, 7)
        };
        select.SetResourceReference(Control.BackgroundProperty, "AccentBrush");
        select.SetResourceReference(Control.ForegroundProperty, "AccentTextBrush");
        select.Click += (_, _) =>
        {
            if (SelectedConversation is not null)
                DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(select);
        Grid.SetRow(buttons, 3);
        content.Children.Add(buttons);
        Content = content;

        if (conversations.Count > 0)
            _conversations.SelectedIndex = 0;
    }

    public ConversationRecord? SelectedConversation => _conversations.SelectedItem as ConversationRecord;

    private void UpdatePreview()
    {
        if (SelectedConversation is not { } selected)
        {
            _preview.Text = "Keine Unterhaltungen verfügbar.";
            return;
        }

        var excerpt = string.Join(
            Environment.NewLine,
            selected.Messages.TakeLast(4).Select(message => $"{(message.Role == "user" ? "Du" : "NOVA")}: {message.Content}"));
        _preview.Text = $"{selected.UpdatedUtc.ToLocalTime():g} · {selected.Messages.Count} Nachrichten"
            + Environment.NewLine + Environment.NewLine + excerpt;
    }
}
