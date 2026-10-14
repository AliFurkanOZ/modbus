using System;
using System.Windows.Threading;

namespace Modbus.App.Helpers
{
    /// <summary>
    /// UI thread'de güvenli işlem yapmaya yardımcı olan statik sınıf
    /// </summary>
    public static class UiDispatcherHelper
    {
        private static Dispatcher? _uiDispatcher;

        /// <summary>
        /// UI Dispatcher'ı ayarlar (App.xaml.cs'ten çağrılmalı)
        /// </summary>
        public static void Initialize(Dispatcher dispatcher)
        {
            _uiDispatcher = dispatcher;
        }

        /// <summary>
        /// UI thread'de asenkron olarak çalıştırır
        /// </summary>
        public static void BeginInvoke(Action action, DispatcherPriority priority = DispatcherPriority.Background)
        {
            if (_uiDispatcher == null)
            {
                throw new InvalidOperationException("UI Dispatcher initialize edilmemiş. App.xaml.cs'te UiDispatcherHelper.Initialize() çağrılmalı.");
            }

            _uiDispatcher.BeginInvoke(action, priority);
        }

        /// <summary>
        /// UI thread'de senkron olarak çalıştırır (dikkatli kullanın, deadlock riski var)
        /// </summary>
        public static void Invoke(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            if (_uiDispatcher == null)
            {
                throw new InvalidOperationException("UI Dispatcher initialize edilmemiş.");
            }

            _uiDispatcher.Invoke(action, priority);
        }

        /// <summary>
        /// UI thread'de olup olmadığını kontrol eder
        /// </summary>
        public static bool IsOnUiThread()
        {
            return _uiDispatcher?.CheckAccess() == true;
        }
    }
}
