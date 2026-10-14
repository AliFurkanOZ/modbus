namespace Modbus.Communication.Common
{
    /// <summary>
    /// Trafik log kaydı
    /// Giden/gelen Modbus paketlerini ve olayları kaydeder
    /// </summary>
    public class TrafficLogEntry
    {
        /// <summary>
        /// Log zamanı
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Log türü (Request, Response, Error, Info)
        /// </summary>
        public TrafficLogType Type { get; set; }

        /// <summary>
        /// İşlem süresi (ms) - sadece Request-Response çiftleri için
        /// </summary>
        public double? DurationMs { get; set; }

        /// <summary>
        /// Slave/Unit ID
        /// </summary>
        public byte SlaveId { get; set; }

        /// <summary>
        /// Fonksiyon kodu
        /// </summary>
        public byte FunctionCode { get; set; }

        /// <summary>
        /// Ham byte dizisi (hex gösterim için)
        /// </summary>
        public byte[]? RawData { get; set; }

        /// <summary>
        /// Açıklama / hata mesajı
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Başarılı işlem mi?
        /// </summary>
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// Exception detayı (hata durumunda)
        /// </summary>
        public string? ExceptionDetail { get; set; }

        /// <summary>
        /// Hex string olarak ham veri
        /// </summary>
        /// <returns>Hex string</returns>
        public string ToHexString()
        {
            if (RawData == null || RawData.Length == 0)
                return string.Empty;

            return BitConverter.ToString(RawData).Replace("-", " ");
        }

        /// <summary>
        /// Log entry'yi string olarak formatlar
        /// </summary>
        /// <returns>Formatlı log string</returns>
        public override string ToString()
        {
            var typeStr = Type switch
            {
                TrafficLogType.Request => "TX",
                TrafficLogType.Response => "RX",
                TrafficLogType.Error => "ERR",
                TrafficLogType.Info => "INF",
                TrafficLogType.HealthCheck => "HCK",
                _ => Type.ToString()
            };

            var timeStr = Timestamp.ToString("HH:mm:ss.fff");
            var hexStr = ToHexString();
            
            if (!string.IsNullOrEmpty(Message))
                return $"[{timeStr}] {typeStr} (ID:{SlaveId}, FC:{FunctionCode}) - {Message}";
            
            if (!string.IsNullOrEmpty(hexStr))
                return $"[{timeStr}] {typeStr} (ID:{SlaveId}, FC:{FunctionCode}) [{hexStr}]";
            
            return $"[{timeStr}] {typeStr} (ID:{SlaveId}, FC:{FunctionCode})";
        }
    }
}
