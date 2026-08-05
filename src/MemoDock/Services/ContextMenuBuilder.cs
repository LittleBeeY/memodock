using System.Windows;
using MemoDock.Core.Models;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfMenuItem = System.Windows.Controls.MenuItem;

namespace MemoDock.Services;

/// <summary>构建主窗口中的深色上下文菜单，复用 App.xaml 定义的菜单样式。</summary>
internal static class ContextMenuBuilder
{
    private const string ContextMenuStyleKey = "DarkContextMenuStyle";
    private const string MenuItemStyleKey = "DarkMenuItemStyle";

    /// <summary>
    /// 构建"切换软件"菜单。
    /// </summary>
    /// <param name="placementTarget">菜单锚定元素。</param>
    /// <param name="notebooks">可切换的软件列表（已排除欢迎页等隐藏项）。</param>
    /// <param name="currentAppId">当前软件身份，用于勾选。</param>
    /// <param name="onSelected">选中某软件时的回调。</param>
    public static WpfContextMenu CreateAppSwitchMenu(
        FrameworkElement placementTarget,
        IReadOnlyList<AppNotebook> notebooks,
        string? currentAppId,
        Action<AppNotebook> onSelected)
    {
        var menu = new WpfContextMenu
        {
            PlacementTarget = placementTarget,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            VerticalOffset = 6,
            MinWidth = Math.Max(180, placementTarget.ActualWidth),
            MaxWidth = 280,
            Style = FindStyle(placementTarget, ContextMenuStyleKey)
        };

        if (notebooks.Count == 0)
        {
            menu.Items.Add(new WpfMenuItem
            {
                Header = "还没有可切换的软件",
                IsEnabled = false,
                Style = FindStyle(placementTarget, MenuItemStyleKey)
            });
        }

        foreach (var notebook in notebooks)
        {
            var item = new WpfMenuItem
            {
                Header = notebook.DisplayName,
                ToolTip = notebook.ExecutablePath,
                IsCheckable = true,
                IsChecked = string.Equals(
                    notebook.AppId,
                    currentAppId,
                    StringComparison.OrdinalIgnoreCase),
                Tag = notebook,
                Style = FindStyle(placementTarget, MenuItemStyleKey)
            };
            item.Click += (_, _) => onSelected(notebook);
            menu.Items.Add(item);
        }

        return menu;
    }

    /// <summary>
    /// 构建单条记录的卡片操作菜单（编辑/删除）。
    /// </summary>
    public static WpfContextMenu CreateCardMenu(
        FrameworkElement placementTarget,
        MemoEntry entry,
        Action<MemoEntry> onEdit,
        Action<MemoEntry> onDelete)
    {
        var menu = new WpfContextMenu
        {
            PlacementTarget = placementTarget,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            VerticalOffset = 4,
            MinWidth = 112,
            Style = FindStyle(placementTarget, ContextMenuStyleKey)
        };

        menu.Items.Add(new WpfMenuItem
        {
            Header = "编辑",
            Style = FindStyle(placementTarget, MenuItemStyleKey)
        }.WithClick(() => onEdit(entry)));

        menu.Items.Add(new WpfMenuItem
        {
            Header = "删除",
            Style = FindStyle(placementTarget, MenuItemStyleKey)
        }.WithClick(() => onDelete(entry)));

        return menu;
    }

    private static Style FindStyle(FrameworkElement element, string key)
    {
        return (Style)element.FindResource(key);
    }

    private static WpfMenuItem WithClick(this WpfMenuItem item, Action onClick)
    {
        item.Click += (_, _) => onClick();
        return item;
    }
}
