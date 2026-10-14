namespace Modbus.Communication.Common
{
    /// <summary>
    /// Trafik log kaydı türü
    /// </summary>
    public enum TrafficLogType
    {
        /// <summary>
        /// Giden istek (TX)
        /// </summary>
        Request = 0,

        /// <summary>
        /// Gelen yanıt (RX)
        /// </summary>
        Response = 1,

        /// <summary>
        /// Hata mesajı
        /// </summary>
        Error = 2,

        /// <summary>
        /// Bilgi mesajı (bağlantı durumu vb.)
        /// </summary>
        Info = 3,

        /// <summary>
        /// Health check probe
        /// </summary>
        HealthCheck = 4
    }
}
