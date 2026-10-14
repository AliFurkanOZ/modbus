namespace Modbus.Protocol.Enums
{
    /// <summary>
    /// Register erişim izinleri.
    /// Bir register'ın hangi işlemlere izin verdiğini belirtir.
    /// </summary>
    public enum AccessType : byte
    {
        /// <summary>
        /// Sadece okuma (Read-Only)
        /// Örnek: Input Register, Discrete Input
        /// </summary>
        ReadOnly = 0,

        /// <summary>
        /// Sadece yazma (Write-Only)
        /// Nadir kullanılır
        /// </summary>
        WriteOnly = 1,

        /// <summary>
        /// Hem okuma hem yazma (Read/Write)
        /// Örnek: Holding Register, Coil
        /// </summary>
        ReadWrite = 2,

        /// <summary>
        /// Erişim yok (Disabled/Hidden)
        /// </summary>
        None = 3
    }
}
