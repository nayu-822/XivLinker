using System.Windows;
using XivLinker.App.ViewModels;

namespace XivLinker.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
