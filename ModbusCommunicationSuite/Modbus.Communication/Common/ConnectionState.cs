namespace Modbus.Communication.Common
{
    /// <summary>
    /// Bağlantı durumunu temsil eder
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// Bağlantı kapalı
        /// </summary>
        Disconnected = 0,

        /// <summary>
        /// Bağlanıyor
        /// </summary>
        Connecting = 1,

        /// <summary>
        /// Bağlı
        /// </summary>
        Connected = 2,

        /// <summary>
        /// Bağlantı kesiliyor
        /// </summary>
        Disconnecting = 3,

        /// <summary>
        /// Hata durumu
        /// </summary>
        Error = 4
    }
}
