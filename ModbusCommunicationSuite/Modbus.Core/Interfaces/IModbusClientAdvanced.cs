using System;
using System.Threading.Tasks;

namespace Modbus.Core.Interfaces
{
    /// <summary>
    /// Gelişmiş Modbus Client arayüzü
    /// Yüksek seviyeli Modbus fonksiyonlarını içerir
    /// </summary>
    public interface IModbusClientAdvanced : IModbusClient
    {
        /// <summary>
        /// Holding Register okur (FC=03)
        /// </summary>
        Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort quantity);

        /// <summary>
        /// Input Register okur (FC=04)
        /// </summary>
        Task<ushort[]> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort quantity);

        /// <summary>
        /// Coil okur (FC=01)
        /// </summary>
        Task<bool[]> ReadCoilsAsync(byte slaveId, ushort startAddress, ushort quantity);

        /// <summary>
        /// Discrete Input okur (FC=02)
        /// </summary>
        Task<bool[]> ReadDiscreteInputsAsync(byte slaveId, ushort startAddress, ushort quantity);

        /// <summary>
        /// Single Coil yazar (FC=05)
        /// </summary>
        Task WriteSingleCoilAsync(byte slaveId, ushort address, bool value);

        /// <summary>
        /// Single Register yazar (FC=06)
        /// </summary>
        Task WriteSingleRegisterAsync(byte slaveId, ushort address, ushort value);

        /// <summary>
        /// Multiple Coils yazar (FC=15)
        /// </summary>
        Task WriteMultipleCoilsAsync(byte slaveId, ushort startAddress, bool[] values);

        /// <summary>
        /// Multiple Registers yazar (FC=16)
        /// </summary>
        Task WriteMultipleRegistersAsync(byte slaveId, ushort startAddress, ushort[] values);

        /// <summary>
        /// Son işlem süresi (ms)
        /// </summary>
        double LastTransactionTimeMs { get; }

        /// <summary>
        /// Toplam başarılı işlem sayısı
        /// </summary>
        int SuccessfulTransactionCount { get; }

        /// <summary>
        /// Toplam hata sayısı
        /// </summary>
        int ErrorCount { get; }
    }
}
