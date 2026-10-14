using System.Windows;
using Modbus.App.Helpers;

namespace Modbus.App
{
    /// <summary>
    /// Uygulamanın giriş noktası (App.xaml'in arka kod dosyası).
    ///
    /// DÜZELTME: Eskiden namespace "Modbus" idi ama App.xaml "Modbus.App" bekliyordu;
    /// bu uyumsuzluk derlemeyi bozuyordu. Artık namespace "Modbus.App" ve sınıf "App".
    ///
    /// "partial" nedir? Bu sınıfın diğer yarısı App.xaml'den otomatik üretilir
    /// (InitializeComponent, Main metodu vb.). İki parça birleşip tek sınıf olur.
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // UI Dispatcher'ı initialize et (Helpers için gerekli)
            UiDispatcherHelper.Initialize(Dispatcher);
        }
    }
}
