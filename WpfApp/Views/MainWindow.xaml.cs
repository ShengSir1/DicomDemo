using FellowOakDicom;
using FellowOakDicom.Imaging;
using System.Windows;
using WpfApp.ViewModels;

namespace WpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom().AddImageManager<ImageSharpImageManager>())
.Build();
        }
    }
}