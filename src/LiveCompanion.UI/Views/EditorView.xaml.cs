using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LiveCompanion.UI.ViewModels;

namespace LiveCompanion.UI.Views;

public partial class EditorView : UserControl
{
    private Point _dragStartPoint;
    private bool _isDragging;

    public EditorView()
    {
        InitializeComponent();
    }

    // ------------------------------------------------------------------ //
    // Drag & drop — Sections
    // ------------------------------------------------------------------ //

    private void SectionsListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private void SectionsListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (_isDragging) return;

        var listBox = (ListBox)sender;
        var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (item is null) return;

        var sectionVm = (SectionViewModel)listBox.ItemContainerGenerator.ItemFromContainer(item);
        _isDragging = true;
        DragDrop.DoDragDrop(item, sectionVm, DragDropEffects.Move);
        _isDragging = false;
    }

    private void SectionsListBox_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(SectionViewModel)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void SectionsListBox_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(SectionViewModel))) return;

        var droppedData = (SectionViewModel)e.Data.GetData(typeof(SectionViewModel))!;
        var listBox = (ListBox)sender;

        // Trouver l'item cible sous le curseur
        var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (targetItem is null) return;

        var targetVm = (SectionViewModel)listBox.ItemContainerGenerator.ItemFromContainer(targetItem);
        if (targetVm == droppedData) return;

        var vm = (EditorViewModel)DataContext;
        int fromIndex = vm.Sections.IndexOf(droppedData);
        int toIndex = vm.Sections.IndexOf(targetVm);

        if (fromIndex >= 0 && toIndex >= 0)
            vm.MoveSection(fromIndex, toIndex);
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
