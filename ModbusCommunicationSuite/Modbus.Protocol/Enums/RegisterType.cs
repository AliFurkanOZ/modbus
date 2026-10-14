namespace Modbus.Protocol.Enums
{
    /// <summary>
    /// Modbus register türleri.
    /// Hangi tür register alanına erişim yapılacağını belirler.
    /// </summary>
    public enum RegisterType : byte
    {
        /// <summary>
        /// Coil: 1-bit okuma/yazma (örn. röle kontrolü)
        /// Adres aralığı: 0xxxx (Fonksiyon kodu: 1, 5, 15)
        /// </summary>
        Coil = 0,

        /// <summary>
        /// Discrete Input: 1-bit salt okuma (örn. sensör durumu)
        /// Adres aralığı: 1xxxx (Fonksiyon kodu: 2)
        /// </summary>
        DiscreteInput = 1,

        /// <summary>
        /// Holding Register: 16-bit okuma/yazma (örn. parametre değerleri)
        /// Adres aralığı: 4xxxx (Fonksiyon kodu: 3, 6, 16)
        /// </summary>
        HoldingRegister = 2,

        /// <summary>
        /// Input Register: 16-bit salt okuma (örn. ölçüm değerleri)
        /// Adres aralığı: 3xxxx (Fonksiyon kodu: 4)
        /// </summary>
        InputRegister = 3
    }
}
