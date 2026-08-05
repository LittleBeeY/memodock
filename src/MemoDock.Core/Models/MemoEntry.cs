using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MemoDock.Core.Models;

/// <summary>一条备忘录记录（笔记或待办）。</summary>
public sealed class MemoEntry : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _body = string.Empty;
    private bool _isCompleted;

    /// <summary>记录唯一标识。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>记录类型：笔记或待办。</summary>
    public MemoKind Kind { get; set; }

    /// <summary>标题。</summary>
    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    /// <summary>正文内容。</summary>
    public string Body
    {
        get => _body;
        set => SetField(ref _body, value);
    }

    /// <summary>待办是否已完成。</summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetField(ref _isCompleted, value);
    }

    /// <summary>最后更新时间，用于排序与冲突合并。</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>属性变更通知。</summary>
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
