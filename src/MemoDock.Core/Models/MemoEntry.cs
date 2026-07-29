using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MemoDock.Core.Models;

public sealed class MemoEntry : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _body = string.Empty;
    private bool _isCompleted;

    public Guid Id { get; set; } = Guid.NewGuid();

    public MemoKind Kind { get; set; }

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string Body
    {
        get => _body;
        set => SetField(ref _body, value);
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetField(ref _isCompleted, value);
    }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
