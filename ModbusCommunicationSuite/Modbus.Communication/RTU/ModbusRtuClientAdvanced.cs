using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Modbus.Core.Interfaces;
using Modbus.Protocol.Builders;
using Modbus.Protocol.Parsers;
using Modbus.Protocol.Enums;
using Modbus.Communication.Services;

namespace Modbus.Communication.RTU
{
    /// <summary>
    /// Gelişmiş Modbus RTU Client
    /// Yüksek seviyeli Modbus fonksiyonlarını destekler (Serial Port).
    /// 
    /// KRİTİK-07 düzeltmesi: Tüm Write fonksiyonlarında yanıt doğrulaması yapılır.
    /// Tüm fonksiyonlarda Slave ID doğrulaması dahildir.
    /// </summary>
    public class ModbusRtuClientAdvanced : ModbusRtuClient, IModbusClientAdvanced
    {
        private readonly TrafficLogger? _logger;
        private double _lastTransactionTimeMs;
        private int _successfulTransactionCount;
        private int _errorCount;

        /// <summary>
        /// Son işlem süresi (ms)
        /// </summary>
        public double LastTransactionTimeMs => _lastTransactionTimeMs;

        /// <summary>
        /// Toplam başarılı işlem sayısı
        /// </summary>
        public int SuccessfulTransactionCount => _successfulTransactionCount;

        /// <summary>
        /// Toplam hata sayısı
        /// </summary>
        public int ErrorCount => _errorCount;

        /// <summary>
        /// Gelişmiş Modbus RTU Client oluşturur
        /// </summary>
        public ModbusRtuClientAdvanced(RtuConnectionSettings settings, TrafficLogger? logger = null)
            : base(settings)
        {
            _logger = logger;
        }

        /// <summary>
        /// Holding Register okur (FC=03)
        /// </summary>
        public async Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort quantity)
        {
            var packet = PacketBuilderExtensions.BuildReadHoldingRegistersRtu(slaveId, startAddress, quantity);
            var response = await SendWithTimingAsync(packet);
            
            // RTU response parse et (CRC doğrulaması + Slave ID kontrolü dahil)
            var parser = new ResponseParser();
            var pkt = parser.ParseRtuResponse(response, slaveId);
            
            if (parser.IsErrorResponse(pkt))
                throw new ModbusException(pkt.Data[0], $"RTU exception response (FC=0x{pkt.FunctionCode:X2})");
            
            _successfulTransactionCount++;
            return parser.ReadRegisterValues(pkt);
        }

        /// <summary>
        /// Input Register okur (FC=04)
        /// </summary>
        public async Task<ushort[]> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort quantity)
        {
            var packet = PacketBuilderExtensions.BuildReadInputRegistersRtu(slaveId, startAddress, quantity);
            var response = await SendWithTimingAsync(packet);
            
            var parser = new ResponseParser();
            var pkt = parser.ParseRtuResponse(response, slaveId);
            
            if (parser.IsErrorResponse(pkt))
                throw new ModbusException(pkt.Data[0], $"RTU exception response (FC=0x{pkt.FunctionCode:X2})");
            
            _successfulTransactionCount++;
            return parser.ReadRegisterValues(pkt);
        }

        /// <summary>
        /// Coil okur (FC=01)
        /// </summary>
        public async Task<bool[]> ReadCoilsAsync(byte slaveId, ushort startAddress, ushort quantity)
        {
            var packet = PacketBuilderExtensions.BuildReadCoilsRtu(slaveId, startAddress, quantity);
            var response = await SendWithTimingAsync(packet);
            
            var parser = new ResponseParser();
            var pkt = parser.ParseRtuResponse(response, slaveId);
            
            if (parser.IsErrorResponse(pkt))
                throw new ModbusException(pkt.Data[0], $"RTU exception response (FC=0x{pkt.FunctionCode:X2})");
            
            _successfulTransactionCount++;
            return parser.ReadCoilValues(pkt, quantity);
        }

        /// <summary>
        /// Discrete Input okur (FC=02)
        /// </summary>
        public async Task<bool[]> ReadDiscreteInputsAsync(byte slaveId, ushort startAddress, ushort quantity)
        {
            var packet = PacketBuilderExtensions.BuildReadDiscreteInputsRtu(slaveId, startAddress, quantity);
            var response = await SendWithTimingAsync(packet);
            
            var parser = new ResponseParser();
            var pkt = parser.ParseRtuResponse(response, slaveId);
            
            if (parser.IsErrorResponse(pkt))
                throw new ModbusException(pkt.Data[0], $"RTU exception response (FC=0x{pkt.FunctionCode:X2})");
            
            _successfulTransactionCount++;
            return parser.ReadCoilValues(pkt, quantity);
        }

        /// <summary>
        /// Single Coil yazar (FC=05).
        /// KRİTİK-07 düzeltmesi: Yanıt doğrulaması eklendi.
        /// FC=05 yanıtı request'in echo'sudur: [SlaveId][0x05][Adres Hi][Lo][Değer Hi][Lo][CRC]
        /// </summary>
        public async Task WriteSingleCoilAsync(byte slaveId, ushort address, bool value)
        {
            var packet = PacketBuilderExtensions.BuildWriteSingleCoilRtu(slaveId, address, value);
            var response = await SendWithTimingAsync(packet);
            
            // Yanıt doğrulaması
            var parser = new ResponseParser();
            var pkt = parser.ParseRtuResponse(response, slaveId);
            
            if (parser.IsErrorResponse(pkt))
                throw new ModbusException(pkt.Data[0], $"RTU exception response (FC=0x{pkt.FunctionCode:X2})");
            
            // Echo doğrulaması: Data = [Adres Hi][Lo][Değer Hi][Lo] = 4 byte
            if (pkt.Data.Length != 4)
                throw new ModbusFrameException($"Write Single Coil yanıt uzunluğu geçersiz: {pkt.Data.Length} (beklenen: 4)");
            
            _successfulTransactionCount++;
        }

        /// <summary>
        /// Single Register yazar (FC=06).
        /// KRİTİK-07 düzeltmesi: Yanıt doğrulaması eklendi.
        /// FC=06 yanıtı request'in echo'sudur: [SlaveId][0x06][Adres Hi][Lo][Değer Hi][Lo][CRC]
        /// </summary>
        public async Task WriteSingleRegisterAsync(byte slaveId, ushort address, ushort value)
        {
            var packet = PacketBuilderExtensions.BuildWriteSingleRegisterRtu(slaveId, address, value);
            var response = await SendWithTimingAsync(packet);
            
            // Yanıt doğrulaması
            var parser = new ResponseParser();
            var pkt = parser.ParseRtuResponse(response, slaveId);
            
            if (parser.IsErrorResponse(pkt))
                throw new ModbusException(pkt.Data[0], $"RTU exception response (FC=0x{pkt.FunctionCode:X2})");
            
            // Echo doğrulaması: Data = [Adres Hi][Lo][Değer Hi][Lo] = 4 byte
            if (pkt.Data.Length != 4)
                throw new ModbusFrameException($"Write Single Register yanıt uzunluğu geçersiz: {pkt.Data.Length} (beklenen: 4)");
            
            _successfulTransactionCount++;
        }

        /// <summary>
        /// Multiple Coils yazar (FC=15).
        /// KRİTİK-07 düzeltmesi: Yanıt doğrulaması eklendi.
        /// FC=15 yanıtı: [SlaveId][0x0F][BaşlangıçAdresi Hi][Lo][Quantity Hi][Lo][CRC]
        /// </summary>
        public async Task WriteMultipleCoilsAsync(byte slaveId, ushort startAddress, bool[] values)
        {
            var packet = PacketBuilderExtensions.BuildWriteMultipleCoilsRtu(slaveId, startAddress, values);
            var response = await SendWithTimingAsync(packet);
            
            // Yanıt doğrulaması
            var parser = new ResponseParser();
            var pkt = parser.ParseRtuResponse(response, slaveId);
            
            if (parser.IsErrorResponse(pkt))
                throw new ModbusException(pkt.Data[0], $"RTU exception response (FC=0x{pkt.FunctionCode:X2})");
            
            // Yanıt doğrulaması: Data = [StartAddr Hi][Lo][Quantity Hi][Lo] = 4 byte
            if (pkt.Data.Length != 4)
                throw new ModbusFrameException($"Write Multiple Coils yanıt uzunluğu geçersiz: {pkt.Data.Length} (beklenen: 4)");
            
            // Quantity doğrulaması
            ushort responseQuantity = (ushort)((pkt.Data[2] << 8) | pkt.Data[3]);
            if (responseQuantity != values.Length)
                throw new ModbusFrameException($"Write Multiple Coils quantity uyumsuzluğu: Beklenen={values.Length}, Gelen={responseQuantity}");
            
            _successfulTransactionCount++;
        }

        /// <summary>
        /// Multiple Registers yazar (FC=16).
        /// KRİTİK-07 düzeltmesi: Yanıt doğrulaması eklendi.
        /// FC=16 yanıtı: [SlaveId][0x10][BaşlangıçAdresi Hi][Lo][Quantity Hi][Lo][CRC]
        /// </summary>
        public async Task WriteMultipleRegistersAsync(byte slaveId, ushort startAddress, ushort[] values)
        {
            var packet = PacketBuilderExtensions.BuildWriteMultipleRegistersRtu(slaveId, startAddress, values);
            var response = await SendWithTimingAsync(packet);
            
            // Yanıt doğrulaması
            var parser = new ResponseParser();
            var pkt = parser.ParseRtuResponse(response, slaveId);
            
            if (parser.IsErrorResponse(pkt))
                throw new ModbusException(pkt.Data[0], $"RTU exception response (FC=0x{pkt.FunctionCode:X2})");
            
            // Yanıt doğrulaması: Data = [StartAddr Hi][Lo][Quantity Hi][Lo] = 4 byte
            if (pkt.Data.Length != 4)
                throw new ModbusFrameException($"Write Multiple Registers yanıt uzunluğu geçersiz: {pkt.Data.Length} (beklenen: 4)");
            
            // Quantity doğrulaması
            ushort responseQuantity = (ushort)((pkt.Data[2] << 8) | pkt.Data[3]);
            if (responseQuantity != values.Length)
                throw new ModbusFrameException($"Write Multiple Registers quantity uyumsuzluğu: Beklenen={values.Length}, Gelen={responseQuantity}");
            
            _successfulTransactionCount++;
        }

        /// <summary>
        /// Zamanlama ile paket gönderir ve loglama yapar
        /// </summary>
        private async Task<byte[]> SendWithTimingAsync(byte[] packet)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Request log
                byte slaveId = packet[0]; // RTU'da ilk byte Slave ID
                byte functionCode = packet[1];
                _logger?.LogRequest(slaveId, functionCode, packet);

                var response = await SendAsync(packet);
                
                stopwatch.Stop();
                _lastTransactionTimeMs = stopwatch.Elapsed.TotalMilliseconds;

                // Response log
                if (response.Length >= 2)
                    _logger?.LogResponse(slaveId, response[1], response, _lastTransactionTimeMs);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _lastTransactionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                _errorCount++;
                
                byte slaveId = packet.Length > 0 ? packet[0] : (byte)0;
                byte functionCode = packet.Length > 1 ? packet[1] : (byte)0;
                _logger?.LogError(slaveId, functionCode, ex.Message, ex.ToString());
                
                throw;
            }
        }
    }
}
