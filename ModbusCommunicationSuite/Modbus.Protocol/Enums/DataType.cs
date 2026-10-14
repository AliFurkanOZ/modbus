namespace Modbus.Protocol.Enums
{
    /// <summary>
    /// Modbus register veri tipleri.
    /// Okunan register değerlerinin nasıl yorumlanacağını belirler.
    /// </summary>
    public enum DataType : byte
    {
        /// <summary>
        /// 16-bit imzasız tamsayı (0 - 65535)
        /// </summary>
        UInt16 = 0,

        /// <summary>
        /// 16-bit imzalı tamsayı (-32768 - 32767)
        /// </summary>
        Int16 = 1,

        /// <summary>
        /// 32-bit imzasız tamsayı (0 - 4294967295)
        /// İki register kullanır (High + Low)
        /// </summary>
        UInt32 = 2,

        /// <summary>
        /// 32-bit imzalı tamsayı (-2147483648 - 2147483647)
        /// İki register kullanır (High + Low)
        /// </summary>
        Int32 = 3,

        /// <summary>
        /// 32-bit IEEE 754 tek hassasiyetli kayan nokta
        /// İki register kullanır
        /// </summary>
        Float32 = 4,

        /// <summary>
        /// 64-bit IEEE 754 çift hassasiyetli kayan nokta
        /// Dört register kullanır
        /// </summary>
        Float64 = 5,

        /// <summary>
        /// 64-bit imzasız tamsayı
        /// Dört register kullanır
        /// </summary>
        UInt64 = 6,

        /// <summary>
        /// 64-bit imzalı tamsayı
        /// Dört register kullanır
        /// </summary>
        Int64 = 7,

        /// <summary>
        /// ASCII string karakterleri
        /// Her register 2 ASCII karakter içerir
        /// </summary>
        String = 8,

        /// <summary>
        /// Ham 16-bit değer (hex gösterim için)
        /// </summary>
        Raw = 9
    }
}
