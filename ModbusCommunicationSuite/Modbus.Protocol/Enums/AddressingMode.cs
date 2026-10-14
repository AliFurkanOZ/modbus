namespace Modbus.Protocol.Enums
{
    /// <summary>
    /// Modbus adresleme modu.
    /// Cihazların farklı adresleme convention'ları kullanabilmesi için.
    /// </summary>
    public enum AddressingMode : byte
    {
        /// <summary>
        /// Sıfır-tabanlı adresleme (0-indexed)
        /// İlk register adresi 0'dır.
        /// Modern cihazlar ve kütüphaneler bunu kullanır.
        /// </summary>
        ZeroBased = 0,

        /// <summary>
        /// Bir-tabanlı adresleme (1-indexed)
        /// İlk register adresi 1'dir.
        /// Bazı eski cihazlar ve PLC'ler bunu kullanır.
        /// </summary>
        OneBased = 1
    }
}
