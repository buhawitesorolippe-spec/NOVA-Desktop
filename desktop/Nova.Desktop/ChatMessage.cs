using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nova.Desktop;

public sealed class ChatMessage(string role, string content, bool canRetry = false) : INotifyPropertyChanged
{
    public string Role { get; } = role;
    public bool CanRetry { get; private set; } = canRetry;
    private string _content = content;
    public string Content
    {
        get => _content;
        set
        {
            if (_content == value)
                return;
            _content = value;
            OnPropertyChanged();
        }
    }
    public bool IsUser => Role == "user";
    public string Speaker => IsUser ? "DU" : "NOVA";

    public void DisableRetry()
    {
        if (!CanRetry)
            return;
        CanRetry = false;
        OnPropertyChanged(nameof(CanRetry));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
