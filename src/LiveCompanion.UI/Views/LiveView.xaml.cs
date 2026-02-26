using System.Windows.Controls;

namespace LiveCompanion.UI.Views;

public partial class LiveView : UserControl
{
    public LiveView()
    {
        InitializeComponent();
        Loaded += (_, _) => Focus();
    }
}
