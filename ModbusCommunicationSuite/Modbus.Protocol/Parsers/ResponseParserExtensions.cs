using System;
using Modbus.Protocol.Packets;
using Modbus.Protocol.Enums;
using Modbus.Protocol.Functions;

namespace Modbus.Protocol.Parsers
{
    /// <summary>
    /// ResponseParser için extension metodlar
    /// Kolay kullanım için hazır parse fonksiyonları
    /// </summary>
    public static class ResponseParserExtensions
    {
        private static readonly ResponseParser _parser = new();

        /// <summary>
        /// TCP response'dan register değerlerini parse eder (FC=03, 04)
        /// </summary>
        public static ushort[] ParseRegistersResponse(byte[] response, byte expectedSlaveId, ModbusFunctionCode expectedFunctionCode)
        {
            var packet = _parser.ParseTcpResponse(response);
            
            if (packet.SlaveId != expectedSlaveId)
                throw new Exception($"Beklenen Slave ID: {expectedSlaveId}, gelen: {packet.SlaveId}");
            
            if (_parser.IsErrorResponse(packet))
            {
                byte exceptionCode = packet.Data.Length > 0 ? packet.Data[0] : (byte)0;
                throw new ModbusException(exceptionCode, $"Modbus exception: FC={(byte)expectedFunctionCode + 0x80}, Exception Code={exceptionCode}");
            }
            
            // Function code kontrolü
            if (packet.FunctionCode != (byte)expectedFunctionCode)
                throw new Exception($"Beklenen FC: {(byte)expectedFunctionCode}, gelen: {packet.FunctionCode}");
            
            return _parser.ReadRegisterValues(packet);
        }

        /// <summary>
        /// TCP response'dan coil değerlerini parse eder (FC=01)
        /// </summary>
        public static bool[] ParseCoilsResponse(byte[] response, byte expectedSlaveId)
        {
            var packet = _parser.ParseTcpResponse(response);
            
            if (packet.SlaveId != expectedSlaveId)
                throw new Exception($"Beklenen Slave ID: {expectedSlaveId}, gelen: {packet.SlaveId}");
            
            if (_parser.IsErrorResponse(packet))
            {
                byte exceptionCode = packet.Data.Length > 0 ? packet.Data[0] : (byte)0;
                throw new ModbusException(exceptionCode, $"Modbus exception: FC=0x81, Exception Code={exceptionCode}");
            }
            
            if (packet.FunctionCode != 0x01)
                throw new Exception($"Beklenen FC: 01, gelen: {packet.FunctionCode:X2}");
            
            // Data yapısı: [ByteCount][CoilData...]
            if (packet.Data.Length < 1)
                throw new Exception("Geçersiz Coil response: ByteCount eksik");
            
            int byteCount = packet.Data[0];
            int coilCount = packet.Data.Length - 1; // Gerçek veri byte sayısı
            
            // Her byte 8 coil içerir
            bool[] coils = new bool[byteCount * 8];
            int coilIndex = 0;
            
            for (int i = 1; i < packet.Data.Length && coilIndex < coils.Length; i++)
            {
                byte currentByte = packet.Data[i];
                for (int bit = 0; bit < 8 && coilIndex < coils.Length; bit++)
                {
                    coils[coilIndex++] = (currentByte & (1 << bit)) != 0;
                }
            }
            
            return coils;
        }

        /// <summary>
        /// TCP response'dan discrete input değerlerini parse eder (FC=02)
        /// </summary>
        public static bool[] ParseDiscreteInputsResponse(byte[] response, byte expectedSlaveId)
        {
            var packet = _parser.ParseTcpResponse(response);
            
            if (packet.SlaveId != expectedSlaveId)
                throw new Exception($"Beklenen Slave ID: {expectedSlaveId}, gelen: {packet.SlaveId}");
            
            if (_parser.IsErrorResponse(packet))
            {
                byte exceptionCode = packet.Data.Length > 0 ? packet.Data[0] : (byte)0;
                throw new ModbusException(exceptionCode, $"Modbus exception: FC=0x82, Exception Code={exceptionCode}");
            }
            
            if (packet.FunctionCode != 0x02)
                throw new Exception($"Beklenen FC: 02, gelen: {packet.FunctionCode:X2}");
            
            // Data yapısı: [ByteCount][InputData...]
            if (packet.Data.Length < 1)
                throw new Exception("Geçersiz Discrete Inputs response: ByteCount eksik");
            
            int byteCount = packet.Data[0];
            
            // Her byte 8 input içerir
            bool[] inputs = new bool[byteCount * 8];
            int inputIndex = 0;
            
            for (int i = 1; i < packet.Data.Length && inputIndex < inputs.Length; i++)
            {
                byte currentByte = packet.Data[i];
                for (int bit = 0; bit < 8 && inputIndex < inputs.Length; bit++)
                {
                    inputs[inputIndex++] = (currentByte & (1 << bit)) != 0;
                }
            }
            
            return inputs;
        }

        /// <summary>
        /// Write Single Register/Coil response'unu doğrular
        /// </summary>
        public static void ValidateWriteSingleResponse(byte[] response, byte expectedSlaveId, byte expectedFunctionCode)
        {
            var packet = _parser.ParseTcpResponse(response);
            
            if (packet.SlaveId != expectedSlaveId)
                throw new Exception($"Beklenen Slave ID: {expectedSlaveId}, gelen: {packet.SlaveId}");
            
            if (_parser.IsErrorResponse(packet))
            {
                byte exceptionCode = packet.Data.Length > 0 ? packet.Data[0] : (byte)0;
                throw new ModbusException(exceptionCode, $"Modbus exception: FC={(uint)expectedFunctionCode + 0x80}, Exception Code={exceptionCode}");
            }
            
            if (packet.FunctionCode != expectedFunctionCode)
                throw new Exception($"Beklenen FC: {expectedFunctionCode:X2}, gelen: {packet.FunctionCode:X2}");
            
            // Write Single response'unda data echo edilir (adres + değer)
            if (packet.Data.Length != 4)
                throw new Exception($"Geçersiz Write Single response uzunluğu: {packet.Data.Length}");
        }

        /// <summary>
        /// Write Multiple response'unu doğrular
        /// </summary>
        public static void ValidateWriteMultipleResponse(byte[] response, byte expectedSlaveId, byte expectedFunctionCode, ushort expectedQuantity)
        {
            var packet = _parser.ParseTcpResponse(response);
            
            if (packet.SlaveId != expectedSlaveId)
                throw new Exception($"Beklenen Slave ID: {expectedSlaveId}, gelen: {packet.SlaveId}");
            
            if (_parser.IsErrorResponse(packet))
            {
                byte exceptionCode = packet.Data.Length > 0 ? packet.Data[0] : (byte)0;
                throw new ModbusException(exceptionCode, $"Modbus exception: FC={(uint)expectedFunctionCode + 0x80}, Exception Code={exceptionCode}");
            }
            
            if (packet.FunctionCode != expectedFunctionCode)
                throw new Exception($"Beklenen FC: {expectedFunctionCode:X2}, gelen: {packet.FunctionCode:X2}");
            
            // Write Multiple response: [StartAddr Hi][Lo][Quantity Hi][Lo]
            if (packet.Data.Length != 4)
                throw new Exception($"Geçersiz Write Multiple response uzunluğu: {packet.Data.Length}");
            
            ushort quantity = (ushort)((packet.Data[2] << 8) | packet.Data[3]);
            if (quantity != expectedQuantity)
                throw new Exception($"Beklenen Quantity: {expectedQuantity}, gelen: {quantity}");
        }
    }

    /// <summary>
    /// Modbus Exception hatası
    /// </summary>
    public class ModbusException : Exception
    {
        public byte ExceptionCode { get; }

        public ModbusException(byte exceptionCode, string message) : base(message)
        {
            ExceptionCode = exceptionCode;
        }

        public override string ToString()
        {
            string exceptionName = ((ModbusExceptionCode)ExceptionCode).ToString();
            return $"ModbusException: [{ExceptionCode}] {exceptionName} - {Message}";
        }
    }
}
