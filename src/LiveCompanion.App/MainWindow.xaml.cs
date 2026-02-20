using System.Windows;
using LiveCompanion.App.ViewModels;

namespace LiveCompanion.App;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
