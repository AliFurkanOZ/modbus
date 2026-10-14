using System;

namespace Modbus.Protocol.Parsers
{
    /// <summary>
    /// Modbus CRC doğrulama hatası.
    /// RTU frame'inde hesaplanan CRC ile alınan CRC eşleşmediğinde fırlatılır.
    /// </summary>
    public class ModbusCrcException : ModbusException
    {
        /// <summary>
        /// Hesaplanan CRC değeri
        /// </summary>
        public ushort CalculatedCrc { get; }

        /// <summary>
        /// Alınan (frame'deki) CRC değeri
        /// </summary>
        public ushort ReceivedCrc { get; }

        public ModbusCrcException(ushort calculatedCrc, ushort receivedCrc)
            : base(0, $"CRC doğrulama hatası: Hesaplanan=0x{calculatedCrc:X4}, Alınan=0x{receivedCrc:X4}")
        {
            CalculatedCrc = calculatedCrc;
            ReceivedCrc = receivedCrc;
        }

        public override string ToString()
        {
            return $"ModbusCrcException: Hesaplanan CRC=0x{CalculatedCrc:X4}, Alınan CRC=0x{ReceivedCrc:X4}";
        }
    }

    /// <summary>
    /// Modbus timeout hatası.
    /// Yanıt beklenen süre içinde gelmediğinde fırlatılır.
    /// </summary>
    public class ModbusTimeoutException : ModbusException
    {
        /// <summary>
        /// Beklenen timeout süresi (ms)
        /// </summary>
        public int TimeoutMs { get; }

        public ModbusTimeoutException(int timeoutMs)
            : base(0, $"Modbus yanıt timeout: {timeoutMs}ms içinde yanıt alınamadı")
        {
            TimeoutMs = timeoutMs;
        }
    }

    /// <summary>
    /// Modbus frame hatası.
    /// Alınan frame yapısı bozuk olduğunda fırlatılır (eksik byte, geçersiz uzunluk vb.).
    /// </summary>
    public class ModbusFrameException : ModbusException
    {
        public ModbusFrameException(string message)
            : base(0, message)
        {
        }
    }
}
