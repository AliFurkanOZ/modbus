namespace Modbus.Protocol.Enums
{
    /// <summary>
    /// Byte sıralama düzeni (Endianness).
    /// 32-bit ve 64-bit değerlerin byte'larının nasıl sıralanacağını belirler.
    /// Modbus cihazları farklı byte order kullanabilir, bu yüzden esneklik önemlidir.
    /// </summary>
    public enum ByteOrder : byte
    {
        /// <summary>
        /// Big Endian (Motorola): En anlamlı byte önce (ABCD)
        /// Örnek: 0x12345678 → [0x12, 0x34, 0x56, 0x78]
        /// Endüstri standardı
        /// </summary>
        BigEndian = 0,

        /// <summary>
        /// Little Endian (Intel): En az anlamlı byte önce (DCBA)
        /// Örnek: 0x12345678 → [0x78, 0x56, 0x34, 0x12]
        /// </summary>
        LittleEndian = 1,

        /// <summary>
        /// Mixed Big Endian (ABCD → CDAB): Word swap
        /// Örnek: 0x12345678 → [0x56, 0x78, 0x12, 0x34]
        /// Bazı PLC'lerde kullanılır
        /// </summary>
        MixedBigEndian = 2,

        /// <summary>
        /// Mixed Little Endian (DCBA → BADC): Byte swap içinde word swap
        /// Örnek: 0x12345678 → [0x34, 0x12, 0x78, 0x56]
        /// </summary>
        MixedLittleEndian = 3
    }
}
