using System.IO.Ports;

namespace Modbus.Communication.RTU
{
    /// <summary>
    /// Modbus RTU (seri port / COM / RS485) bağlantı ayarları.
    /// RTU kablo üzerinden haberleşir; bu yüzden ayarlar TCP'den farklıdır:
    /// port ismi, hız (baud rate), veri biti gibi seri port kavramları.
    /// 
    /// Modbus V1.02 §2.5.1:
    /// - Varsayılan parity: Even
    /// - No parity kullanılıyorsa 2 stop bit zorunlu
    /// - Varsayılan baud rate: 9600 (Regular class için 19200)
    /// </summary>
    public class RtuConnectionSettings
    {
        /// <summary>Seri port adı, örn. "COM3". Cihazın takılı olduğu port.</summary>
        public string PortName { get; set; } = "COM3";

        /// <summary>Baud rate = saniyedeki sinyal hızı. İki taraf AYNI olmalı (örn. 9600).</summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>Bir karakterdeki veri biti sayısı. Modbus RTU'da genelde 8.</summary>
        public int DataBits { get; set; } = 8;

        /// <summary>
        /// Parity kontrolü. Modbus standardına göre varsayılan "Even" olmalıdır.
        /// Modbus V1.02 §2.5.1: "Even parity is REQUIRED... The default parity mode MUST be even parity."
        /// </summary>
        public Parity Parity { get; set; } = Parity.Even;

        /// <summary>
        /// Stop bit sayısı. Modbus standardı:
        /// - Even/Odd parity ise: 1 stop bit (11 bit frame)
        /// - No parity ise: 2 stop bit ZORUNLU (11 bit frame korunur)
        /// </summary>
        public StopBits StopBits { get; set; } = StopBits.One;

        /// <summary>Zaman aşımı (ms). Cevap bu sürede gelmezse vazgeç.</summary>
        public int Timeout { get; set; } = 1000;

        /// <summary>
        /// Başarısız istek sonrası maximum yeniden deneme sayısı.
        /// Modbus V1.02 §2.4.1: "the maximum number of retries depends on the master set-up."
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Broadcast sonrası bekleme süresi (ms).
        /// Modbus V1.02 §2.4.1: "Turnaround delay... typically from 100ms to 200ms."
        /// </summary>
        public int TurnaroundDelayMs { get; set; } = 100;

        /// <summary>
        /// Handshake modu. RS485 2-wire için genelde None kullanılır.
        /// </summary>
        public Handshake Handshake { get; set; } = Handshake.None;

        /// <summary>
        /// RS485 yön kontrolü için RTS kullanılsın mı?
        /// Bazı USB-RS485 çeviriciler RTS sinyalini driver enable olarak kullanır.
        /// </summary>
        public bool RtsEnable { get; set; } = false;

        /// <summary>
        /// Parity ve StopBits uyumluluğunu doğrular.
        /// No parity + 1 stop bit = 10 bit frame (standart dışı).
        /// </summary>
        public void Validate()
        {
            if (Parity == Parity.None && StopBits != StopBits.Two)
            {
                // Modbus V1.02 §2.5.1: "the use of no parity requires 2 stop bits."
                // Otomatik düzelt ve uyar
                StopBits = StopBits.Two;
            }
        }
    }
}
