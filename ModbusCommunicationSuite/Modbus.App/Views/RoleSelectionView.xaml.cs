using System.Windows.Controls;
using System.Windows.Input;

namespace Modbus.App.Views
{
    /// <summary>
    /// Uygulama açılışında gösterilen rol seçim ekranı.
    /// Kullanıcı Master veya Slave modunu seçer.
    /// </summary>
    public partial class RoleSelectionView : UserControl
    {
        public RoleSelectionView()
        {
            InitializeComponent();
        }

        private void MasterCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.NavigateToMasterCommand.Execute(null);
        }

        private void SlaveCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.NavigateToSlaveCommand.Execute(null);
        }
    }
}
