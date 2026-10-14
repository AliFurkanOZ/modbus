namespace Modbus.Protocol.Models
{
    /// <summary>
    /// Bir Modbus register'ının tanımını temsil eder.
    /// Register adresi, veri tipi, byte order, erişim türü gibi metadata içerir.
    /// </summary>
    public class RegisterDefinition
    {
        /// <summary>
        /// Register adı (örn. "Sıcaklık Sensörü 1")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Register adresi (cihaz protokolüne göre 0-based veya 1-based olabilir)
        /// </summary>
        public ushort Address { get; set; }

        /// <summary>
        /// Register türü (Coil, Discrete Input, Holding Register, Input Register)
        /// </summary>
        public Enums.RegisterType RegisterType { get; set; } = Enums.RegisterType.HoldingRegister;

        /// <summary>
        /// Veri tipi (UInt16, Int32, Float32, vb.)
        /// </summary>
        public Enums.DataType DataType { get; set; } = Enums.DataType.UInt16;

        /// <summary>
        /// Byte sıralama düzeni (endianness)
        /// Sadece 32-bit ve 64-bit tipler için geçerlidir
        /// </summary>
        public Enums.ByteOrder ByteOrder { get; set; } = Enums.ByteOrder.BigEndian;

        /// <summary>
        /// Erişim izni (ReadOnly, ReadWrite, vb.)
        /// </summary>
        public Enums.AccessType AccessType { get; set; } = Enums.AccessType.ReadWrite;

        /// <summary>
        /// Değer ölçeklendirme çarpanı
        /// Örnek: Cihaz 2500 döndüğünde gerçek değer 25.0°C olsun (ScaleFactor = 0.01)
        /// </summary>
        public double ScaleFactor { get; set; } = 1.0;

        /// <summary>
        /// Değer ofset değeri
        /// Örnek: Ölçeklendirilmiş değere eklenecek ofset
        /// </summary>
        public double Offset { get; set; } = 0.0;

        /// <summary>
        /// Birim (örn. "°C", "V", "A", "Hz")
        /// </summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// Minimum izin verilen değer
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Maksimum izin verilen değer
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Açıklama / Not alanı
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Bu register'ın kaç adet 16-bit register kapladığını hesaplar
        /// </summary>
        /// <returns>Required register count</returns>
        public int GetRegisterCount()
        {
            return DataType switch
            {
                Enums.DataType.UInt16 or Enums.DataType.Int16 or Enums.DataType.Raw => 1,
                Enums.DataType.UInt32 or Enums.DataType.Int32 or Enums.DataType.Float32 => 2,
                Enums.DataType.UInt64 or Enums.DataType.Int64 or Enums.DataType.Float64 => 4,
                Enums.DataType.String => 1, // String uzunluğu ayrı belirlenir
                _ => 1
            };
        }

        /// <summary>
        /// Adresleme moduna göre gerçek cihaz adresini hesaplar
        /// </summary>
        /// <param name="mode">Adresleme modu</param>
        /// <returns>Cihaz adresi</returns>
        public ushort GetDeviceAddress(Enums.AddressingMode mode)
        {
            return mode switch
            {
                Enums.AddressingMode.ZeroBased => Address,
                Enums.AddressingMode.OneBased => (ushort)(Address + 1),
                _ => Address
            };
        }

        /// <summary>
        /// Ham değeri ölçeklendirilmiş ve ofsetlenmiş gerçek değere dönüştürür
        /// </summary>
        /// <param name="rawValue">Cihazdan okunan ham değer</param>
        /// <returns>Ölçeklendirilmiş gerçek değer</returns>
        public double ConvertToEngineeringValue(double rawValue)
        {
            return (rawValue * ScaleFactor) + Offset;
        }

        /// <summary>
        /// Gerçek değeri ham cihaz değerine dönüştürür (yazma işlemleri için)
        /// </summary>
        /// <param name="engineeringValue">Gerçek değer</param>
        /// <returns>Cihaza yazılacak ham değer</returns>
        public double ConvertToRawValue(double engineeringValue)
        {
            return (engineeringValue - Offset) / ScaleFactor;
        }
    }
}
