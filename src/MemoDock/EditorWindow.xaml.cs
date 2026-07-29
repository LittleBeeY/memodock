using System.Windows;
using System.Windows.Input;
using MemoDock.Core.Models;

namespace MemoDock;

public partial class EditorWindow : Window
{
    public EditorWindow(MemoKind kind, MemoEntry? existing)
    {
        InitializeComponent();

        Heading.Text = existing is null
            ? kind == MemoKind.Todo ? "新建待办" : "新建笔记"
            : kind == MemoKind.Todo ? "编辑待办" : "编辑笔记";

        if (existing is not null)
        {
            TitleBox.Text = existing.Title;
            BodyBox.Text = existing.Body;
        }

        Loaded += (_, _) =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        };
    }

    public string EntryTitle { get; private set; } = string.Empty;

    public string EntryBody { get; private set; } = string.Empty;

    private void Save()
    {
        var title = TitleBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ValidationText.Text = "请输入标题。";
            ValidationText.Visibility = Visibility.Visible;
            TitleBox.Focus();
            return;
        }

        EntryTitle = title;
        EntryBody = BodyBox.Text.Trim();
        DialogResult = true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Save();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Save();
            e.Handled = true;
        }
    }
}
