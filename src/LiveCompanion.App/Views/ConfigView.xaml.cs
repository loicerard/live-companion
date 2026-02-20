using System.Windows;
using System.Windows.Controls;
using LiveCompanion.App.ViewModels;

namespace LiveCompanion.App.Views;

public partial class ConfigView : UserControl
{
    public ConfigView()
    {
        InitializeComponent();
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is ConfigViewModel vm)
            vm.SelectedNode = e.NewValue;
    }
}
