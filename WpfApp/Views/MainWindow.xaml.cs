using FellowOakDicom;
using FellowOakDicom.Imaging;
using System.Windows;
using System.Windows.Input;
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
            new DicomSetupBuilder().RegisterServices(s => s.AddFellowOakDicom().AddImageManager<ImageSharpImageManager>()).Build();
        }
        private bool _isDragging = false;
        private Point _lastMousePosition;
        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as IInputElement;
            _lastMousePosition = e.GetPosition(element);
            _isDragging = true;

            // 捕获鼠标，防止拖出控件范围后失效
            if (sender is UIElement uiElement)
            {
                uiElement.CaptureMouse();
            }
        }

        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            if (sender is UIElement uiElement)
            {
                uiElement.ReleaseMouseCapture();
            }
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            var element = sender as IInputElement;
            var currentPosition = e.GetPosition(element);

            // 计算偏移量
            double deltaX = currentPosition.X - _lastMousePosition.X;
            double deltaY = currentPosition.Y - _lastMousePosition.Y;

            // 获取 ViewModel 并调用调整方法
            if (DataContext is MainViewModel vm)
            {
                // 注意：通常鼠标向下(Y增加)意味着降低亮度(WC增加)，还是反过来？
                // 常见的医学影像软件逻辑：
                // 水平向右 -> 增加窗宽 (图像变灰)
                // 垂直向下 -> 增加窗位 (图像变黑/数值变大)

                vm.AdjustWindowLevel(deltaX, deltaY);
            }

            _lastMousePosition = currentPosition;
        }
    }
}