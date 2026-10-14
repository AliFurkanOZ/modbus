using System.Buffers.Binary;

namespace Modbus.Protocol.Converters
{
    /// <summary>
    /// Modbus register verilerini .NET veri tiplerine dönüştürür.
    /// IEEE 754 float, Int32, UInt32, Int64, UInt64 dönüşümlerini destekler.
    /// </summary>
    public static class ModbusValueConverter
    {
        /// <summary>
        /// İki 16-bit register'dan 32-bit Float (IEEE 754) değeri oluşturur
        /// </summary>
        /// <param name="highRegister">Yüksek register</param>
        /// <param name="lowRegister">Düşük register</param>
        /// <param name="byteOrder">Byte sıralama düzeni</param>
        /// <returns>Float değer</returns>
        public static float ToFloat32(ushort highRegister, ushort lowRegister, Enums.ByteOrder byteOrder = Enums.ByteOrder.BigEndian)
        {
            byte[] bytes = ByteOrderConverter.CombineRegisters(highRegister, lowRegister, byteOrder);
            
            if (byteOrder == Enums.ByteOrder.BigEndian || byteOrder == Enums.ByteOrder.MixedBigEndian)
                return BinaryPrimitives.ReadSingleBigEndian(bytes);
            else
                return BinaryPrimitives.ReadSingleLittleEndian(bytes);
        }

        /// <summary>
        /// Dört 16-bit register'dan 64-bit Double (IEEE 754) değeri oluşturur
        /// </summary>
        public static double ToFloat64(ushort reg1, ushort reg2, ushort reg3, ushort reg4, Enums.ByteOrder byteOrder = Enums.ByteOrder.BigEndian)
        {
            byte[] bytes = ByteOrderConverter.CombineRegisters(reg1, reg2, reg3, reg4, byteOrder);
            
            if (byteOrder == Enums.ByteOrder.BigEndian || byteOrder == Enums.ByteOrder.MixedBigEndian)
                return BinaryPrimitives.ReadDoubleBigEndian(bytes);
            else
                return BinaryPrimitives.ReadDoubleLittleEndian(bytes);
        }

        /// <summary>
        /// İki 16-bit register'dan 32-bit imzalı tamsayı oluşturur
        /// </summary>
        public static int ToInt32(ushort highRegister, ushort lowRegister, Enums.ByteOrder byteOrder = Enums.ByteOrder.BigEndian)
        {
            byte[] bytes = ByteOrderConverter.CombineRegisters(highRegister, lowRegister, byteOrder);
            
            if (byteOrder == Enums.ByteOrder.BigEndian || byteOrder == Enums.ByteOrder.MixedBigEndian)
                return BinaryPrimitives.ReadInt32BigEndian(bytes);
            else
                return BinaryPrimitives.ReadInt32LittleEndian(bytes);
        }

        /// <summary>
        /// İki 16-bit register'dan 32-bit imzasız tamsayı oluşturur
        /// </summary>
        public static uint ToUInt32(ushort highRegister, ushort lowRegister, Enums.ByteOrder byteOrder = Enums.ByteOrder.BigEndian)
        {
            byte[] bytes = ByteOrderConverter.CombineRegisters(highRegister, lowRegister, byteOrder);
            
            if (byteOrder == Enums.ByteOrder.BigEndian || byteOrder == Enums.ByteOrder.MixedBigEndian)
                return BinaryPrimitives.ReadUInt32BigEndian(bytes);
            else
                return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        }

        /// <summary>
        /// Dört 16-bit register'dan 64-bit imzalı tamsayı oluşturur
        /// </summary>
        public static long ToInt64(ushort reg1, ushort reg2, ushort reg3, ushort reg4, Enums.ByteOrder byteOrder = Enums.ByteOrder.BigEndian)
        {
            byte[] bytes = ByteOrderConverter.CombineRegisters(reg1, reg2, reg3, reg4, byteOrder);
            
            if (byteOrder == Enums.ByteOrder.BigEndian || byteOrder == Enums.ByteOrder.MixedBigEndian)
                return BinaryPrimitives.ReadInt64BigEndian(bytes);
            else
                return BinaryPrimitives.ReadInt64LittleEndian(bytes);
        }

        /// <summary>
        /// Dört 16-bit register'dan 64-bit imzasız tamsayı oluşturur
        /// </summary>
        public static ulong ToUInt64(ushort reg1, ushort reg2, ushort reg3, ushort reg4, Enums.ByteOrder byteOrder = Enums.ByteOrder.BigEndian)
        {
            byte[] bytes = ByteOrderConverter.CombineRegisters(reg1, reg2, reg3, reg4, byteOrder);
            
            if (byteOrder == Enums.ByteOrder.BigEndian || byteOrder == Enums.ByteOrder.MixedBigEndian)
                return BinaryPrimitives.ReadUInt64BigEndian(bytes);
            else
                return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        }

        /// <summary>
        /// Tek 16-bit register'dan imzalı tamsayı oluşturur
        /// </summary>
        public static short ToInt16(ushort register)
        {
            return unchecked((short)register);
        }

        /// <summary>
        /// Float değeri iki 16-bit register'a böler
        /// </summary>
        /// <param name="value">Float değer</param>
        /// <param name="byteOrder">Byte order</param>
        /// <returns>(highRegister, lowRegister) tuple</returns>
        public static (ushort High, ushort Low) FromFloat32(float value, Enums.ByteOrder byteOrder = Enums.ByteOrder.BigEndian)
        {
            byte[] bytes = new byte[4];
            
            if (byteOrder == Enums.ByteOrder.BigEndian || byteOrder == Enums.ByteOrder.MixedBigEndian)
                BinaryPrimitives.WriteSingleBigEndian(bytes, value);
            else
                BinaryPrimitives.WriteSingleLittleEndian(bytes, value);

            // Byte order'a göre yeniden sırala (big endian varsayımıyla okumak için)
            bytes = ByteOrderConverter.ConvertUInt32(bytes, Enums.ByteOrder.BigEndian);

            ushort high = (ushort)((bytes[0] << 8) | bytes[1]);
            ushort low = (ushort)((bytes[2] << 8) | bytes[3]);

            return (high, low);
        }

        /// <summary>
        /// Int32 değeri iki 16-bit register'a böler
        /// </summary>
        public static (ushort High, ushort Low) FromInt32(int value, Enums.ByteOrder byteOrder = Enums.ByteOrder.BigEndian)
        {
            byte[] bytes = new byte[4];
            
            if (byteOrder == Enums.ByteOrder.BigEndian || byteOrder == Enums.ByteOrder.MixedBigEndian)
                BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            else
                BinaryPrimitives.WriteInt32LittleEndian(bytes, value);

            bytes = ByteOrderConverter.ConvertUInt32(bytes, Enums.ByteOrder.BigEndian);

            ushort high = (ushort)((bytes[0] << 8) | bytes[1]);
            ushort low = (ushort)((bytes[2] << 8) | bytes[3]);

            return (high, low);
        }
    }
}
