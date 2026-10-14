using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Modbus.Core.Interfaces;
using Modbus.Protocol.Builders;
using Modbus.Protocol.Parsers;
using Modbus.Protocol.Enums;
using Modbus.Protocol.Functions;
using Modbus.Communication.Common;
using Modbus.Communication.Services;

namespace Modbus.Communication.TCP
{
    /// <summary>
    /// Gelişmiş Modbus TCP Client
    /// Yüksek seviyeli Modbus fonksiyonlarını destekler
    /// </summary>
    public class ModbusTcpClientAdvanced : ModbusTcpClient, IModbusClientAdvanced
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
        /// Gelişmiş Modbus TCP Client oluşturur
        /// </summary>
        public ModbusTcpClientAdvanced(TcpConnectionSettings settings, TrafficLogger? logger = null)
            : base(settings)
        {
            _logger = logger;
        }

        /// <summary>
        /// Holding Register okur (FC=03)
        /// </summary>
        public async Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort quantity)
        {
            return await ExecuteReadAsync(slaveId, startAddress, quantity, ModbusFunctionCode.ReadHoldingRegisters);
        }

        /// <summary>
        /// Input Register okur (FC=04)
        /// </summary>
        public async Task<ushort[]> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort quantity)
        {
            return await ExecuteReadAsync(slaveId, startAddress, quantity, ModbusFunctionCode.ReadInputRegisters);
        }

        /// <summary>
        /// Coil okur (FC=01)
        /// </summary>
        public async Task<bool[]> ReadCoilsAsync(byte slaveId, ushort startAddress, ushort quantity)
        {
            var packet = PacketBuilderExtensions.BuildReadCoils(slaveId, startAddress, quantity);
            var response = await SendWithTimingAsync(packet);
            
            var values = ResponseParserExtensions.ParseCoilsResponse(response, slaveId);
            _successfulTransactionCount++;
            return values;
        }

        /// <summary>
        /// Discrete Input okur (FC=02)
        /// </summary>
        public async Task<bool[]> ReadDiscreteInputsAsync(byte slaveId, ushort startAddress, ushort quantity)
        {
            var packet = PacketBuilderExtensions.BuildReadDiscreteInputs(slaveId, startAddress, quantity);
            var response = await SendWithTimingAsync(packet);
            
            var values = ResponseParserExtensions.ParseDiscreteInputsResponse(response, slaveId);
            _successfulTransactionCount++;
            return values;
        }

        /// <summary>
        /// Single Coil yazar (FC=05)
        /// </summary>
        public async Task WriteSingleCoilAsync(byte slaveId, ushort address, bool value)
        {
            var packet = PacketBuilderExtensions.BuildWriteSingleCoil(slaveId, address, value);
            await SendWithTimingAsync(packet);
            _successfulTransactionCount++;
        }

        /// <summary>
        /// Single Register yazar (FC=06)
        /// </summary>
        public async Task WriteSingleRegisterAsync(byte slaveId, ushort address, ushort value)
        {
            var packet = PacketBuilderExtensions.BuildWriteSingleRegister(slaveId, address, value);
            await SendWithTimingAsync(packet);
            _successfulTransactionCount++;
        }

        /// <summary>
        /// Multiple Coils yazar (FC=15)
        /// </summary>
        public async Task WriteMultipleCoilsAsync(byte slaveId, ushort startAddress, bool[] values)
        {
            var packet = PacketBuilderExtensions.BuildWriteMultipleCoils(slaveId, startAddress, values);
            await SendWithTimingAsync(packet);
            _successfulTransactionCount++;
        }

        /// <summary>
        /// Multiple Registers yazar (FC=16)
        /// </summary>
        public async Task WriteMultipleRegistersAsync(byte slaveId, ushort startAddress, ushort[] values)
        {
            var packet = PacketBuilderExtensions.BuildWriteMultipleRegisters(slaveId, startAddress, values);
            await SendWithTimingAsync(packet);
            _successfulTransactionCount++;
        }

        /// <summary>
        /// Generic okuma işlemi (Register tipleri için)
        /// </summary>
        private async Task<ushort[]> ExecuteReadAsync(byte slaveId, ushort startAddress, ushort quantity, ModbusFunctionCode functionCode)
        {
            byte[] packet = functionCode switch
            {
                ModbusFunctionCode.ReadHoldingRegisters => PacketBuilderExtensions.BuildReadHoldingRegisters(slaveId, startAddress, quantity),
                ModbusFunctionCode.ReadInputRegisters => PacketBuilderExtensions.BuildReadInputRegisters(slaveId, startAddress, quantity),
                _ => throw new ArgumentException($"Desteklenmeyen fonksiyon kodu: {functionCode}")
            };

            var response = await SendWithTimingAsync(packet);
            var values = ResponseParserExtensions.ParseRegistersResponse(response, slaveId, functionCode);
            _successfulTransactionCount++;
            return values;
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
                byte slaveId = packet[6]; // TCP header'dan sonra Unit ID
                byte functionCode = packet[7];
                _logger?.LogRequest(slaveId, functionCode, packet);

                var response = await SendAsync(packet);
                
                stopwatch.Stop();
                _lastTransactionTimeMs = stopwatch.Elapsed.TotalMilliseconds;

                // Response log
                _logger?.LogResponse(slaveId, response[7], response, _lastTransactionTimeMs);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _lastTransactionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                _errorCount++;
                
                byte slaveId = packet[6];
                byte functionCode = packet[7];
                _logger?.LogError(slaveId, functionCode, ex.Message, ex.ToString());
                
                throw;
            }
        }
    }
}
