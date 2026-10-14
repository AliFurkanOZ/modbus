using Modbus.Protocol.Models;
using Modbus.Protocol.Enums;
using Modbus.Protocol.Converters;
using System.Collections.ObjectModel;

namespace Modbus.Protocol.Services
{
    /// <summary>
    /// Register tanımlarını yönetir, okuma/yazma işlemlerini koordine eder.
    /// Register gruplarını oluşturur, veri dönüşümlerini yapar.
    /// </summary>
    public class RegisterService
    {
        private readonly ObservableCollection<RegisterDefinition> _registers;
        private readonly object _lock = new();

        /// <summary>
        /// Tüm register tanımları
        /// </summary>
        public IReadOnlyCollection<RegisterDefinition> Registers => _registers;

        /// <summary>
        /// Register servisleri başlatır
        /// </summary>
        public RegisterService()
        {
            _registers = new ObservableCollection<RegisterDefinition>();
        }

        /// <summary>
        /// Register servisleri başlatır (başlangıç registerları ile)
        /// </summary>
        /// <param name="initialRegisters">Başlangıç register tanımları</param>
        public RegisterService(IEnumerable<RegisterDefinition> initialRegisters)
        {
            _registers = new ObservableCollection<RegisterDefinition>(initialRegisters);
        }

        #region Register Yönetimi

        /// <summary>
        /// Yeni register ekler
        /// </summary>
        /// <param name="register">Eklenecek register tanımı</param>
        public void AddRegister(RegisterDefinition register)
        {
            lock (_lock)
            {
                // Aynı adreste aynı tipte register var mı kontrol et
                var existing = _registers.FirstOrDefault(r => 
                    r.Address == register.Address && 
                    r.RegisterType == register.RegisterType);

                if (existing != null)
                    throw new InvalidOperationException($"Address {register.Address} ({register.RegisterType}) already exists.");

                _registers.Add(register);
            }
        }

        /// <summary>
        /// Birden fazla register ekler
        /// </summary>
        /// <param name="registers">Eklenecek register tanımları</param>
        public void AddRegisters(IEnumerable<RegisterDefinition> registers)
        {
            lock (_lock)
            {
                foreach (var register in registers)
                {
                    AddRegister(register);
                }
            }
        }

        /// <summary>
        /// Register siler
        /// </summary>
        /// <param name="register">Silinecek register tanımı</param>
        public void RemoveRegister(RegisterDefinition register)
        {
            lock (_lock)
            {
                _registers.Remove(register);
            }
        }

        /// <summary>
        /// Adrese göre register siler
        /// </summary>
        /// <param name="address">Silinecek register adresi</param>
        /// <param name="registerType">Register türü</param>
        /// <returns>Silindi mi?</returns>
        public bool RemoveRegisterByAddress(ushort address, RegisterType registerType = RegisterType.HoldingRegister)
        {
            lock (_lock)
            {
                var register = _registers.FirstOrDefault(r => 
                    r.Address == address && r.RegisterType == registerType);

                if (register == null)
                    return false;

                return _registers.Remove(register);
            }
        }

        /// <summary>
        /// Tüm registerları temizler
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _registers.Clear();
            }
        }

        /// <summary>
        /// Adrese göre register bulur
        /// </summary>
        /// <param name="address">Register adresi</param>
        /// <param name="registerType">Register türü</param>
        /// <returns>RegisterDefinition veya null</returns>
        public RegisterDefinition? FindByAddress(ushort address, RegisterType registerType = RegisterType.HoldingRegister)
        {
            return _registers.FirstOrDefault(r => 
                r.Address == address && r.RegisterType == registerType);
        }

        /// <summary>
        /// Ada göre register bulur (kısmi eşleşme)
        /// </summary>
        /// <param name="namePattern">Aranacak isim pattern'i</param>
        /// <returns>Eşleşen registerlar</returns>
        public IEnumerable<RegisterDefinition> FindByName(string namePattern)
        {
            return _registers.Where(r => 
                r.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Register türüne göre filtreleme
        /// </summary>
        /// <param name="registerType">Register türü</param>
        /// <returns>Filtrelenmiş registerlar</returns>
        public IEnumerable<RegisterDefinition> FilterByType(RegisterType registerType)
        {
            return _registers.Where(r => r.RegisterType == registerType);
        }

        /// <summary>
        /// Adres aralığına göre filtreleme
        /// </summary>
        /// <param name="startAddress">Başlangıç adresi</param>
        /// <param name="endAddress">Bitiş adresi</param>
        /// <param name="registerType">Register türü</param>
        /// <returns>Filtrelenmiş registerlar</returns>
        public IEnumerable<RegisterDefinition> FilterByAddressRange(
            ushort startAddress, 
            ushort endAddress, 
            RegisterType registerType = RegisterType.HoldingRegister)
        {
            return _registers.Where(r => 
                r.RegisterType == registerType &&
                r.Address >= startAddress && 
                r.Address <= endAddress);
        }

        #endregion

        #region Veri Dönüşümü

        /// <summary>
        /// Ham byte dizisini register tanımına göre dönüştürür
        /// </summary>
        /// <param name="data">Ham byte verisi</param>
        /// <param name="register">Register tanımı</param>
        /// <returns>Dönüştürülmüş değer</returns>
        public object ConvertRawData(byte[] data, RegisterDefinition register)
        {
            if (data.Length < 2)
                throw new ArgumentException("Data must be at least 2 bytes.", nameof(data));

            return register.DataType switch
            {
                DataType.UInt16 => BitConverter.ToUInt16(data, 0),
                DataType.Int16 => BitConverter.ToInt16(data, 0),
                DataType.UInt32 => ModbusValueConverter.ToUInt32(
                    BitConverter.ToUInt16(data, 0),
                    BitConverter.ToUInt16(data, 2),
                    register.ByteOrder),
                DataType.Int32 => ModbusValueConverter.ToInt32(
                    BitConverter.ToUInt16(data, 0),
                    BitConverter.ToUInt16(data, 2),
                    register.ByteOrder),
                DataType.Float32 => ModbusValueConverter.ToFloat32(
                    BitConverter.ToUInt16(data, 0),
                    BitConverter.ToUInt16(data, 2),
                    register.ByteOrder),
                DataType.Float64 => ModbusValueConverter.ToFloat64(
                    BitConverter.ToUInt16(data, 0),
                    BitConverter.ToUInt16(data, 2),
                    BitConverter.ToUInt16(data, 4),
                    BitConverter.ToUInt16(data, 6),
                    register.ByteOrder),
                DataType.UInt64 => ModbusValueConverter.ToUInt64(
                    BitConverter.ToUInt16(data, 0),
                    BitConverter.ToUInt16(data, 2),
                    BitConverter.ToUInt16(data, 4),
                    BitConverter.ToUInt16(data, 6),
                    register.ByteOrder),
                DataType.Int64 => ModbusValueConverter.ToInt64(
                    BitConverter.ToUInt16(data, 0),
                    BitConverter.ToUInt16(data, 2),
                    BitConverter.ToUInt16(data, 4),
                    BitConverter.ToUInt16(data, 6),
                    register.ByteOrder),
                DataType.String => System.Text.Encoding.ASCII.GetString(data).TrimEnd('\0'),
                _ => BitConverter.ToUInt16(data, 0)
            };
        }

        /// <summary>
        /// Ham değeri mühendislik birimine dönüştürür (ölçeklendirme + ofset)
        /// </summary>
        /// <param name="rawValue">Ham değer</param>
        /// <param name="register">Register tanımı</param>
        /// <returns>Mühendislik birimindeki değer</returns>
        public double ConvertToEngineeringValue(object rawValue, RegisterDefinition register)
        {
            var doubleValue = Convert.ToDouble(rawValue);
            return register.ConvertToEngineeringValue(doubleValue);
        }

        /// <summary>
        /// Mühendislik değerini ham değere dönüştürür (yazma işlemleri için)
        /// </summary>
        /// <param name="engineeringValue">Mühendislik değeri</param>
        /// <param name="register">Register tanımı</param>
        /// <returns>Ham cihaz değeri</returns>
        public double ConvertToRawValue(double engineeringValue, RegisterDefinition register)
        {
            return register.ConvertToRawValue(engineeringValue);
        }

        /// <summary>
        /// Değeri string formatında döndürür (birim ile birlikte)
        /// </summary>
        /// <param name="value">Değer</param>
        /// <param name="register">Register tanımı</param>
        /// <param name="format">Format string (örn. "F2", "X4")</param>
        /// <returns>Formatlanmış string</returns>
        public string FormatValue(object value, RegisterDefinition register, string format = "F2")
        {
            var engineeringValue = ConvertToEngineeringValue(value, register);
            
            if (!string.IsNullOrEmpty(register.Unit))
                return $"{engineeringValue.ToString(format)} {register.Unit}";
            
            return engineeringValue.ToString(format);
        }

        #endregion

        #region Toplu İşlemler

        /// <summary>
        /// Registerları adreslerine göre sıralar
        /// </summary>
        /// <returns>Sıralanmış register listesi</returns>
        public IEnumerable<RegisterDefinition> GetSortedByAddress()
        {
            return _registers.OrderBy(r => r.Address);
        }

        /// <summary>
        /// Registerları register türüne göre gruplar
        /// </summary>
        /// <returns>Grouped registers</returns>
        public ILookup<RegisterType, RegisterDefinition> GroupByType()
        {
            return _registers.ToLookup(r => r.RegisterType);
        }

        /// <summary>
        /// Optimize edilmiş okuma grupları oluşturur (bitişik registerları birleştirir)
        /// </summary>
        /// <param name="registerType">Register türü</param>
        /// <param name="maxGap">Maksimum izin verilen adres boşluğu</param>
        /// <returns>Okuma grupları (startAddress, count)</returns>
        public List<(ushort StartAddress, int Count)> CreateOptimizedReadGroups(
            RegisterType registerType = RegisterType.HoldingRegister,
            int maxGap = 1)
        {
            var filtered = FilterByType(registerType)
                .OrderBy(r => r.Address)
                .ToList();

            if (filtered.Count == 0)
                return new List<(ushort, int)>();

            var groups = new List<(ushort StartAddress, int Count)>();
            var currentStart = filtered[0].Address;
            var currentEnd = (ushort)(currentStart + filtered[0].GetRegisterCount() - 1);

            for (int i = 1; i < filtered.Count; i++)
            {
                var reg = filtered[i];
                var nextStart = reg.Address;
                var gap = nextStart - currentEnd - 1;

                if (gap <= maxGap)
                {
                    // Bitişik veya yakın, grubu genişlet
                    currentEnd = (ushort)(nextStart + reg.GetRegisterCount() - 1);
                }
                else
                {
                    // Yeni grup başlat
                    groups.Add((currentStart, currentEnd - currentStart + 1));
                    currentStart = nextStart;
                    currentEnd = (ushort)(nextStart + reg.GetRegisterCount() - 1);
                }
            }

            // Son grubu ekle
            groups.Add((currentStart, currentEnd - currentStart + 1));

            return groups;
        }

        /// <summary>
        /// Export register tanımlarını JSON formatında
        /// </summary>
        /// <returns>JSON string</returns>
        public string ExportToJson()
        {
            return System.Text.Json.JsonSerializer.Serialize(_registers, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        /// <summary>
        /// Import register tanımlarını JSON formatından
        /// </summary>
        /// <param name="json">JSON string</param>
        public void ImportFromJson(string json)
        {
            var imported = System.Text.Json.JsonSerializer.Deserialize<List<RegisterDefinition>>(json);
            if (imported != null)
            {
                Clear();
                AddRegisters(imported);
            }
        }

        #endregion

        #region İstatistikler

        /// <summary>
        /// Toplam register sayısı
        /// </summary>
        public int TotalCount => _registers.Count;

        /// <summary>
        /// Toplam kaplanan register alanı (16-bit birimleri)
        /// </summary>
        public int TotalRegisterSpace => _registers.Sum(r => r.GetRegisterCount());

        /// <summary>
        /// Register türlerine göre dağılım
        /// </summary>
        public Dictionary<RegisterType, int> DistributionByType => _registers
            .GroupBy(r => r.RegisterType)
            .ToDictionary(g => g.Key, g => g.Count());

        #endregion
    }
}
