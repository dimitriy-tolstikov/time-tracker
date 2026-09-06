using System.Windows;
using System.Windows.Controls;

namespace TimeTracker
{
    /// <summary>
    /// Вкладка «Проекты и правила» главного окна. DataContext (ProjectsViewModel)
    /// устанавливает MainWindow — у контрола свой репозиторий нет, поэтому
    /// конструктор без параметров (нужен для XAML).
    /// </summary>
    public partial class ProjectsView : UserControl
    {
        public ProjectsView()
        {
            InitializeComponent();
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            // Popup живёт в отдельном визуальном дереве — DataContext пробрасываем вручную.
            ColorPicker.DataContext = DataContext;
            ColorPopup.IsOpen = true;
        }
    }
}
