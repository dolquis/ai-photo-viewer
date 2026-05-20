using AiPhotoViewer.UI.ViewModels;
using Avalonia.Controls;

namespace AiPhotoViewer.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
