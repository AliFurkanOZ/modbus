namespace Modbus.Protocol.Converters
{
    /// <summary>
    /// Byte sıralama düzeni (endianness) dönüşümlerini gerçekleştirir.
    /// 32-bit ve 64-bit değerlerin byte'larını farklı endianness formatları arasında dönüştürür.
    /// </summary>
    public static class ByteOrderConverter
    {
        /// <summary>
        /// 32-bit (4 byte) veriyi belirtilen byte order'a göre yeniden sıralar
        /// </summary>
        /// <param name="bytes">4 byte'lık veri</param>
        /// <param name="order">Hedef byte order</param>
        /// <returns>Yeniden sıralanmış byte dizisi</returns>
        /// <exception cref="ArgumentException">Byte dizisi 4 elemanlı değilse</exception>
        public static byte[] ConvertUInt32(byte[] bytes, Enums.ByteOrder order)
        {
            if (bytes.Length != 4)
                throw new ArgumentException("UInt32 dönüşümü için 4 byte gereklidir.", nameof(bytes));

            return order switch
            {
                Enums.ByteOrder.BigEndian => new[] { bytes[0], bytes[1], bytes[2], bytes[3] }, // ABCD
                Enums.ByteOrder.LittleEndian => new[] { bytes[3], bytes[2], bytes[1], bytes[0] }, // DCBA
                Enums.ByteOrder.MixedBigEndian => new[] { bytes[2], bytes[3], bytes[0], bytes[1] }, // CDAB
                Enums.ByteOrder.MixedLittleEndian => new[] { bytes[1], bytes[0], bytes[3], bytes[2] }, // BADC
                _ => bytes
            };
        }

        /// <summary>
        /// 64-bit (8 byte) veriyi belirtilen byte order'a göre yeniden sıralar
        /// </summary>
        /// <param name="bytes">8 byte'lık veri</param>
        /// <param name="order">Hedef byte order</param>
        /// <returns>Yeniden sıralanmış byte dizisi</returns>
        /// <exception cref="ArgumentException">Byte dizisi 8 elemanlı değilse</exception>
        public static byte[] ConvertUInt64(byte[] bytes, Enums.ByteOrder order)
        {
            if (bytes.Length != 8)
                throw new ArgumentException("UInt64 dönüşümü için 8 byte gereklidir.", nameof(bytes));

            return order switch
            {
                Enums.ByteOrder.BigEndian => new[] 
                { 
                    bytes[0], bytes[1], bytes[2], bytes[3],
                    bytes[4], bytes[5], bytes[6], bytes[7] 
                }, // ABCDEFGH
                Enums.ByteOrder.LittleEndian => new[] 
                { 
                    bytes[7], bytes[6], bytes[5], bytes[4],
                    bytes[3], bytes[2], bytes[1], bytes[0] 
                }, // HGFEDCBA
                Enums.ByteOrder.MixedBigEndian => new[] 
                { 
                    bytes[4], bytes[5], bytes[6], bytes[7],
                    bytes[0], bytes[1], bytes[2], bytes[3] 
                }, // EFGHABCD
                Enums.ByteOrder.MixedLittleEndian => new[] 
                { 
                    bytes[3], bytes[2], bytes[1], bytes[0],
                    bytes[7], bytes[6], bytes[5], bytes[4] 
                }, // DCBAHGFE
                _ => bytes
            };
        }

        /// <summary>
        /// İki 16-bit register'ı birleştirip 32-bit değere çevirirken byte order uygular
        /// </summary>
        /// <param name="highRegister">Yüksek register (ilk okunan)</param>
        /// <param name="lowRegister">Düşük register (ikinci okunan)</param>
        /// <param name="order">Byte order</param>
        /// <returns>4 byte'lık dizi</returns>
        public static byte[] CombineRegisters(ushort highRegister, ushort lowRegister, Enums.ByteOrder order)
        {
            byte[] bytes = new byte[4];
            
            // Önce register'ları byte'lara ayır (big endian varsayımıyla)
            bytes[0] = (byte)(highRegister >> 8);
            bytes[1] = (byte)(highRegister & 0xFF);
            bytes[2] = (byte)(lowRegister >> 8);
            bytes[3] = (byte)(lowRegister & 0xFF);

            // Şimdi byte order'a göre yeniden sırala
            return ConvertUInt32(bytes, order);
        }

        /// <summary>
        /// Dört 16-bit register'ı birleştirip 64-bit değere çevirirken byte order uygular
        /// </summary>
        public static byte[] CombineRegisters(ushort reg1, ushort reg2, ushort reg3, ushort reg4, Enums.ByteOrder order)
        {
            byte[] bytes = new byte[8];
            
            bytes[0] = (byte)(reg1 >> 8);
            bytes[1] = (byte)(reg1 & 0xFF);
            bytes[2] = (byte)(reg2 >> 8);
            bytes[3] = (byte)(reg2 & 0xFF);
            bytes[4] = (byte)(reg3 >> 8);
            bytes[5] = (byte)(reg3 & 0xFF);
            bytes[6] = (byte)(reg4 >> 8);
            bytes[7] = (byte)(reg4 & 0xFF);

            return ConvertUInt64(bytes, order);
        }
    }
}
