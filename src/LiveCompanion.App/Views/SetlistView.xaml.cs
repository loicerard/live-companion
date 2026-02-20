using System.Windows.Controls;
using System.Windows.Input;
using LiveCompanion.App.ViewModels;

namespace LiveCompanion.App.Views;

public partial class SetlistView : UserControl
{
    public SetlistView()
    {
        InitializeComponent();
    }

    private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SetlistViewModel vm)
            vm.OpenSongCommand.Execute(vm.SelectedSong);
    }
}
