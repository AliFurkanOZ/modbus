using System;
using System.Collections.Generic;
using Modbus.Protocol.Functions;
using Modbus.Protocol.Helpers;

namespace Modbus.Protocol.Builders
{
    /// <summary>
    /// PacketBuilder için extension metodlar
    /// Kolay kullanım için hazır paket oluşturma fonksiyonları.
    /// 
    /// Hem TCP hem RTU varyantlarını içerir.
    /// Quantity ve Slave ID validasyonları dahildir.
    /// </summary>
    public static class PacketBuilderExtensions
    {
        // MİMARİ-06 düzeltmesi: ushort yerine int kullanıyoruz
        // çünkü Interlocked.Increment ref int kabul eder.
        private static int _transactionId = 0;

        // İYİ-04 düzeltmesi: PacketBuilder her çağrıda yeniden oluşturulmaz
        private static readonly PacketBuilder _builder = new();

        /// <summary>
        /// Sonraki Transaction ID'yi döndürür (thread-safe)
        /// </summary>
        private static ushort GetNextTransactionId()
        {
            // MİMARİ-06 düzeltmesi: 65536 ile mod alarak 0-65535 aralığında kalır
            return (ushort)(System.Threading.Interlocked.Increment(ref _transactionId) % 65536);
        }

        // ================================================================
        // Validasyon Yardımcıları (MİMARİ-04, MİMARİ-05)
        // ================================================================

        /// <summary>
        /// Slave ID aralık kontrolü.
        /// Modbus V1.02 §2.2: Geçerli adresler 0-247, 248-255 reserved.
        /// </summary>
        private static void ValidateSlaveId(byte slaveId)
        {
            if (slaveId > 247)
                throw new ArgumentOutOfRangeException(nameof(slaveId),
                    $"Slave ID {slaveId} geçersiz. Geçerli aralık: 0-247 (248-255 reserved).");
        }

        /// <summary>
        /// Register okuma quantity kontrolü (FC=03, FC=04).
        /// Modbus standardı: 1-125 register.
        /// </summary>
        private static void ValidateRegisterReadQuantity(ushort quantity)
        {
            if (quantity < 1 || quantity > 125)
                throw new ArgumentOutOfRangeException(nameof(quantity),
                    $"Register okuma adedi {quantity} geçersiz. Geçerli aralık: 1-125.");
        }

        /// <summary>
        /// Coil/Discrete Input okuma quantity kontrolü (FC=01, FC=02).
        /// Modbus standardı: 1-2000 coil.
        /// </summary>
        private static void ValidateCoilReadQuantity(ushort quantity)
        {
            if (quantity < 1 || quantity > 2000)
                throw new ArgumentOutOfRangeException(nameof(quantity),
                    $"Coil okuma adedi {quantity} geçersiz. Geçerli aralık: 1-2000.");
        }

        /// <summary>
        /// Write Multiple Registers quantity kontrolü (FC=16).
        /// Modbus standardı: 1-123 register.
        /// </summary>
        private static void ValidateRegisterWriteQuantity(int quantity)
        {
            if (quantity < 1 || quantity > 123)
                throw new ArgumentOutOfRangeException(nameof(quantity),
                    $"Register yazma adedi {quantity} geçersiz. Geçerli aralık: 1-123.");
        }

        /// <summary>
        /// Write Multiple Coils quantity kontrolü (FC=15).
        /// Modbus standardı: 1-1968 coil.
        /// </summary>
        private static void ValidateCoilWriteQuantity(int quantity)
        {
            if (quantity < 1 || quantity > 1968)
                throw new ArgumentOutOfRangeException(nameof(quantity),
                    $"Coil yazma adedi {quantity} geçersiz. Geçerli aralık: 1-1968.");
        }

        // ================================================================
        // TCP Varyantları
        // ================================================================

        #region Read Functions (TCP)

        /// <summary>
        /// Read Coils paketi oluşturur (FC=01) — TCP
        /// </summary>
        public static byte[] BuildReadCoils(byte slaveId, ushort startAddress, ushort quantity)
        {
            ValidateSlaveId(slaveId);
            ValidateCoilReadQuantity(quantity);
            var pdu = _builder.BuildReadPdu(ModbusFunctionCode.ReadCoils, startAddress, quantity);
            return _builder.WrapTcp(GetNextTransactionId(), slaveId, pdu);
        }

        /// <summary>
        /// Read Discrete Inputs paketi oluşturur (FC=02) — TCP
        /// </summary>
        public static byte[] BuildReadDiscreteInputs(byte slaveId, ushort startAddress, ushort quantity)
        {
            ValidateSlaveId(slaveId);
            ValidateCoilReadQuantity(quantity);
            var pdu = _builder.BuildReadPdu(ModbusFunctionCode.ReadDiscreteInputs, startAddress, quantity);
            return _builder.WrapTcp(GetNextTransactionId(), slaveId, pdu);
        }

        /// <summary>
        /// Read Holding Registers paketi oluşturur (FC=03) — TCP
        /// </summary>
        public static byte[] BuildReadHoldingRegisters(byte slaveId, ushort startAddress, ushort quantity)
        {
            ValidateSlaveId(slaveId);
            ValidateRegisterReadQuantity(quantity);
            var pdu = _builder.BuildReadPdu(ModbusFunctionCode.ReadHoldingRegisters, startAddress, quantity);
            return _builder.WrapTcp(GetNextTransactionId(), slaveId, pdu);
        }

        /// <summary>
        /// Read Input Registers paketi oluşturur (FC=04) — TCP
        /// </summary>
        public static byte[] BuildReadInputRegisters(byte slaveId, ushort startAddress, ushort quantity)
        {
            ValidateSlaveId(slaveId);
            ValidateRegisterReadQuantity(quantity);
            var pdu = _builder.BuildReadPdu(ModbusFunctionCode.ReadInputRegisters, startAddress, quantity);
            return _builder.WrapTcp(GetNextTransactionId(), slaveId, pdu);
        }

        #endregion

        #region Write Single Functions (TCP)

        /// <summary>
        /// Write Single Coil paketi oluşturur (FC=05) — TCP
        /// </summary>
        public static byte[] BuildWriteSingleCoil(byte slaveId, ushort address, bool value)
        {
            ValidateSlaveId(slaveId);
            ushort coilValue = value ? (ushort)0xFF00 : (ushort)0x0000;
            var pdu = _builder.BuildWriteSinglePdu(ModbusFunctionCode.WriteSingleCoil, address, coilValue);
            return _builder.WrapTcp(GetNextTransactionId(), slaveId, pdu);
        }

        /// <summary>
        /// Write Single Register paketi oluşturur (FC=06) — TCP
        /// </summary>
        public static byte[] BuildWriteSingleRegister(byte slaveId, ushort address, ushort value)
        {
            ValidateSlaveId(slaveId);
            var pdu = _builder.BuildWriteSinglePdu(ModbusFunctionCode.WriteSingleRegister, address, value);
            return _builder.WrapTcp(GetNextTransactionId(), slaveId, pdu);
        }

        #endregion

        #region Write Multiple Functions (TCP)

        /// <summary>
        /// Write Multiple Coils paketi oluşturur (FC=15) — TCP
        /// </summary>
        public static byte[] BuildWriteMultipleCoils(byte slaveId, ushort startAddress, bool[] values)
        {
            ValidateSlaveId(slaveId);
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values dizisi boş olamaz", nameof(values));
            ValidateCoilWriteQuantity(values.Length);

            byte[] pdu = BuildWriteMultipleCoilsPdu(startAddress, values);
            return _builder.WrapTcp(GetNextTransactionId(), slaveId, pdu);
        }

        /// <summary>
        /// Write Multiple Registers paketi oluşturur (FC=16) — TCP
        /// </summary>
        public static byte[] BuildWriteMultipleRegisters(byte slaveId, ushort startAddress, ushort[] values)
        {
            ValidateSlaveId(slaveId);
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values dizisi boş olamaz", nameof(values));
            ValidateRegisterWriteQuantity(values.Length);

            byte[] pdu = BuildWriteMultipleRegistersPdu(startAddress, values);
            return _builder.WrapTcp(GetNextTransactionId(), slaveId, pdu);
        }

        #endregion

        // ================================================================
        // RTU Varyantları (KRİTİK-05 düzeltmesi — eksik 6 metot eklendi)
        // ================================================================

        #region Read Functions (RTU)

        /// <summary>
        /// Read Coils RTU paketi oluşturur (FC=01)
        /// </summary>
        public static byte[] BuildReadCoilsRtu(byte slaveId, ushort startAddress, ushort quantity)
        {
            ValidateSlaveId(slaveId);
            ValidateCoilReadQuantity(quantity);
            var pdu = _builder.BuildReadPdu(ModbusFunctionCode.ReadCoils, startAddress, quantity);
            return _builder.WrapRtu(slaveId, pdu);
        }

        /// <summary>
        /// Read Discrete Inputs RTU paketi oluşturur (FC=02)
        /// </summary>
        public static byte[] BuildReadDiscreteInputsRtu(byte slaveId, ushort startAddress, ushort quantity)
        {
            ValidateSlaveId(slaveId);
            ValidateCoilReadQuantity(quantity);
            var pdu = _builder.BuildReadPdu(ModbusFunctionCode.ReadDiscreteInputs, startAddress, quantity);
            return _builder.WrapRtu(slaveId, pdu);
        }

        /// <summary>
        /// Read Holding Registers RTU paketi oluşturur (FC=03)
        /// </summary>
        public static byte[] BuildReadHoldingRegistersRtu(byte slaveId, ushort startAddress, ushort quantity)
        {
            ValidateSlaveId(slaveId);
            ValidateRegisterReadQuantity(quantity);
            var pdu = _builder.BuildReadPdu(ModbusFunctionCode.ReadHoldingRegisters, startAddress, quantity);
            return _builder.WrapRtu(slaveId, pdu);
        }

        /// <summary>
        /// Read Input Registers RTU paketi oluşturur (FC=04)
        /// </summary>
        public static byte[] BuildReadInputRegistersRtu(byte slaveId, ushort startAddress, ushort quantity)
        {
            ValidateSlaveId(slaveId);
            ValidateRegisterReadQuantity(quantity);
            var pdu = _builder.BuildReadPdu(ModbusFunctionCode.ReadInputRegisters, startAddress, quantity);
            return _builder.WrapRtu(slaveId, pdu);
        }

        #endregion

        #region Write Single Functions (RTU)

        /// <summary>
        /// Write Single Coil RTU paketi oluşturur (FC=05)
        /// </summary>
        public static byte[] BuildWriteSingleCoilRtu(byte slaveId, ushort address, bool value)
        {
            ValidateSlaveId(slaveId);
            ushort coilValue = value ? (ushort)0xFF00 : (ushort)0x0000;
            var pdu = _builder.BuildWriteSinglePdu(ModbusFunctionCode.WriteSingleCoil, address, coilValue);
            return _builder.WrapRtu(slaveId, pdu);
        }

        /// <summary>
        /// Write Single Register RTU paketi oluşturur (FC=06)
        /// </summary>
        public static byte[] BuildWriteSingleRegisterRtu(byte slaveId, ushort address, ushort value)
        {
            ValidateSlaveId(slaveId);
            var pdu = _builder.BuildWriteSinglePdu(ModbusFunctionCode.WriteSingleRegister, address, value);
            return _builder.WrapRtu(slaveId, pdu);
        }

        #endregion

        #region Write Multiple Functions (RTU)

        /// <summary>
        /// Write Multiple Coils RTU paketi oluşturur (FC=15)
        /// </summary>
        public static byte[] BuildWriteMultipleCoilsRtu(byte slaveId, ushort startAddress, bool[] values)
        {
            ValidateSlaveId(slaveId);
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values dizisi boş olamaz", nameof(values));
            ValidateCoilWriteQuantity(values.Length);

            byte[] pdu = BuildWriteMultipleCoilsPdu(startAddress, values);
            return _builder.WrapRtu(slaveId, pdu);
        }

        /// <summary>
        /// Write Multiple Registers RTU paketi oluşturur (FC=16)
        /// </summary>
        public static byte[] BuildWriteMultipleRegistersRtu(byte slaveId, ushort startAddress, ushort[] values)
        {
            ValidateSlaveId(slaveId);
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values dizisi boş olamaz", nameof(values));
            ValidateRegisterWriteQuantity(values.Length);

            byte[] pdu = BuildWriteMultipleRegistersPdu(startAddress, values);
            return _builder.WrapRtu(slaveId, pdu);
        }

        #endregion

        // ================================================================
        // Ortak PDU Oluşturucular
        // ================================================================

        /// <summary>
        /// Write Multiple Coils (FC=15) PDU oluşturur.
        /// KRİTİK-08 düzeltmesi: Bit packing artık LSB-first (standart uyumlu).
        /// Modbus standardı: İlk coil → byte'ın bit 0'ı (en düşük bit).
        /// </summary>
        private static byte[] BuildWriteMultipleCoilsPdu(ushort startAddress, bool[] values)
        {
            List<byte> pdu = new();

            // Function code
            pdu.Add((byte)ModbusFunctionCode.WriteMultipleCoils);

            // Start address (2 bytes, big-endian)
            pdu.Add((byte)(startAddress >> 8));
            pdu.Add((byte)(startAddress & 0xFF));

            // Quantity of outputs (2 bytes, big-endian)
            pdu.Add((byte)(values.Length >> 8));
            pdu.Add((byte)(values.Length & 0xFF));

            // Byte count
            byte byteCount = (byte)((values.Length + 7) / 8);
            pdu.Add(byteCount);

            // Values — KRİTİK-08 DÜZELTMESİ: LSB-first packing
            // Modbus standardı: İlk coil byte'ın BIT 0'ına, ikinci coil BIT 1'ine yerleşir.
            // Eski (yanlış): currentByte |= (byte)(0x80 >> bit);  // MSB-first
            // Yeni (doğru):  currentByte |= (byte)(1 << bit);     // LSB-first
            for (int i = 0; i < values.Length; i += 8)
            {
                byte currentByte = 0;
                for (int bit = 0; bit < 8 && (i + bit) < values.Length; bit++)
                {
                    if (values[i + bit])
                        currentByte |= (byte)(1 << bit); // LSB-first — Standart uyumlu
                }
                pdu.Add(currentByte);
            }

            return pdu.ToArray();
        }

        /// <summary>
        /// Write Multiple Registers (FC=16) PDU oluşturur.
        /// </summary>
        private static byte[] BuildWriteMultipleRegistersPdu(ushort startAddress, ushort[] values)
        {
            List<byte> pdu = new();

            // Function code
            pdu.Add((byte)ModbusFunctionCode.WriteMultipleRegisters);

            // Start address (2 bytes, big-endian)
            pdu.Add((byte)(startAddress >> 8));
            pdu.Add((byte)(startAddress & 0xFF));

            // Quantity of registers (2 bytes, big-endian)
            pdu.Add((byte)(values.Length >> 8));
            pdu.Add((byte)(values.Length & 0xFF));

            // Byte count
            byte byteCount = (byte)(values.Length * 2);
            pdu.Add(byteCount);

            // Values (each register is 2 bytes, big-endian)
            foreach (var value in values)
            {
                pdu.Add((byte)(value >> 8));
                pdu.Add((byte)(value & 0xFF));
            }

            return pdu.ToArray();
        }
    }
}
