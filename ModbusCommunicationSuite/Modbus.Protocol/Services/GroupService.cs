using Modbus.Protocol.Models;
using Modbus.Protocol.Enums;
using System.Collections.ObjectModel;

namespace Modbus.Protocol.Services
{
    /// <summary>
    /// Register gruplarını yönetir, grup bazlı işlemleri koordine eder.
    /// Grup oluşturma, düzenleme, silme ve grup bazlı okuma/yazma işlemlerini sağlar.
    /// </summary>
    public class GroupService
    {
        private readonly ObservableCollection<RegisterGroup> _groups;
        private readonly RegisterService _registerService;
        private readonly object _lock = new();

        /// <summary>
        /// Tüm register grupları
        /// </summary>
        public IReadOnlyCollection<RegisterGroup> Groups => _groups;

        /// <summary>
        /// İlişkili register servisi
        /// </summary>
        public RegisterService RegisterService => _registerService;

        /// <summary>
        /// Grup servislerini başlatır
        /// </summary>
        public GroupService()
        {
            _groups = new ObservableCollection<RegisterGroup>();
            _registerService = new RegisterService();
        }

        /// <summary>
        /// Grup servislerini başlatır (mevcut register servisi ile)
        /// </summary>
        /// <param name="registerService">Mevcut register servisi</param>
        public GroupService(RegisterService registerService)
        {
            _registerService = registerService;
            _groups = new ObservableCollection<RegisterGroup>();
        }

        #region Grup Yönetimi

        /// <summary>
        /// Yeni grup oluşturur
        /// </summary>
        /// <param name="name">Grup adı</param>
        /// <param name="description">Grup açıklaması</param>
        /// <param name="colorCode">Grup rengi (opsiyonel)</param>
        /// <returns>Oluşturulan grup</returns>
        public RegisterGroup CreateGroup(string name, string? description = null, string? colorCode = null)
        {
            lock (_lock)
            {
                if (_groups.Any(g => g.Name == name))
                    throw new InvalidOperationException($"Group '{name}' already exists.");

                var group = new RegisterGroup
                {
                    Name = name,
                    Description = description,
                    ColorCode = colorCode
                };

                _groups.Add(group);
                return group;
            }
        }

        /// <summary>
        /// Gruba register ekler
        /// </summary>
        /// <param name="groupName">Grup adı</param>
        /// <param name="register">Eklenecek register tanımı</param>
        public void AddRegisterToGroup(string groupName, RegisterDefinition register)
        {
            lock (_lock)
            {
                var group = _groups.FirstOrDefault(g => g.Name == groupName);
                if (group == null)
                    throw new ArgumentException($"Group '{groupName}' not found.", nameof(groupName));

                // Register'ı ana servise de ekle
                _registerService.AddRegister(register);

                // Gruba da ekle
                group.AddRegister(register);
            }
        }

        /// <summary>
        /// Gruba birden fazla register ekler
        /// </summary>
        /// <param name="groupName">Grup adı</param>
        /// <param name="registers">Eklenecek register tanımları</param>
        public void AddRegistersToGroup(string groupName, IEnumerable<RegisterDefinition> registers)
        {
            lock (_lock)
            {
                var group = _groups.FirstOrDefault(g => g.Name == groupName);
                if (group == null)
                    throw new ArgumentException($"Group '{groupName}' not found.", nameof(groupName));

                foreach (var register in registers)
                {
                    _registerService.AddRegister(register);
                    group.AddRegister(register);
                }
            }
        }

        /// <summary>
        /// Gruptan register çıkarır (ana listeden silmez)
        /// </summary>
        /// <param name="groupName">Grup adı</param>
        /// <param name="register">Çıkarılacak register</param>
        public void RemoveRegisterFromGroup(string groupName, RegisterDefinition register)
        {
            lock (_lock)
            {
                var group = _groups.FirstOrDefault(g => g.Name == groupName);
                if (group == null)
                    throw new ArgumentException($"Group '{groupName}' not found.", nameof(groupName));

                group.RemoveRegister(register);
            }
        }

        /// <summary>
        /// Grubu siler (registerlar ana listede kalır)
        /// </summary>
        /// <param name="groupName">Silinecek grup adı</param>
        /// <returns>Silindi mi?</returns>
        public bool DeleteGroup(string groupName)
        {
            lock (_lock)
            {
                var group = _groups.FirstOrDefault(g => g.Name == groupName);
                if (group == null)
                    return false;

                // Grup içindeki registerların referansını temizle ama ana listeden silme
                group.Clear();

                return _groups.Remove(group);
            }
        }

        /// <summary>
        /// Grubu ve içindeki tüm registerları siler
        /// </summary>
        /// <param name="groupName">Silinecek grup adı</param>
        /// <returns>Silindi mi?</returns>
        public bool DeleteGroupWithRegisters(string groupName)
        {
            lock (_lock)
            {
                var group = _groups.FirstOrDefault(g => g.Name == groupName);
                if (group == null)
                    return false;

                // Grubun tüm registerlarını ana listeden sil
                foreach (var register in group.Registers.ToList())
                {
                    _registerService.RemoveRegister(register);
                }

                group.Clear();
                return _groups.Remove(group);
            }
        }

        /// <summary>
        /// Ada göre grup bulur
        /// </summary>
        /// <param name="groupName">Grup adı</param>
        /// <returns>RegisterGroup veya null</returns>
        public RegisterGroup? FindByName(string groupName)
        {
            return _groups.FirstOrDefault(g => g.Name == groupName);
        }

        /// <summary>
        /// Adrese göre register'ın hangi grupta olduğunu bulur
        /// </summary>
        /// <param name="address">Register adresi</param>
        /// <param name="registerType">Register türü</param>
        /// <returns>Grup adı veya null</returns>
        public string? FindGroupByAddress(ushort address, RegisterType registerType = RegisterType.HoldingRegister)
        {
            foreach (var group in _groups)
            {
                if (group.Registers.Any(r => r.Address == address && r.RegisterType == registerType))
                    return group.Name;
            }
            return null;
        }

        #endregion

        #region Grup Bazlı İşlemler

        /// <summary>
        /// Grubun optimize edilmiş okuma aralığını hesaplar
        /// </summary>
        /// <param name="groupName">Grup adı</param>
        /// <returns>(startAddress, count) tuple</returns>
        public (ushort StartAddress, int Count) GetGroupReadRange(string groupName)
        {
            var group = FindByName(groupName) 
                ?? throw new ArgumentException($"Group '{groupName}' not found.", nameof(groupName));

            return group.GetReadRange();
        }

        /// <summary>
        /// Grubun tüm registerlarını döner
        /// </summary>
        /// <param name="groupName">Grup adı</param>
        /// <returns>Register tanımları</returns>
        public IEnumerable<RegisterDefinition> GetGroupRegisters(string groupName)
        {
            var group = FindByName(groupName)
                ?? throw new ArgumentException($"Group '{groupName}' not found.", nameof(groupName));

            return group.Registers;
        }

        /// <summary>
        /// Grubun toplam register alanını hesaplar
        /// </summary>
        /// <param name="groupName">Grup adı</param>
        /// <returns>Toplam 16-bit register sayısı</returns>
        public int GetGroupTotalRegisterCount(string groupName)
        {
            var group = FindByName(groupName)
                ?? throw new ArgumentException($"Group '{groupName}' not found.", nameof(groupName));

            return group.TotalRegisterCount();
        }

        /// <summary>
        /// Grubun tüm registerlarını adrese göre sıralar
        /// </summary>
        /// <param name="groupName">Grup adı</param>
        /// <returns>Sıralanmış register listesi</returns>
        public IEnumerable<RegisterDefinition> GetSortedGroupRegisters(string groupName)
        {
            var group = FindByName(groupName)
                ?? throw new ArgumentException($"Group '{groupName}' not found.", nameof(groupName));

            return group.Registers.OrderBy(r => r.Address);
        }

        #endregion

        #region Toplu Grup İşlemleri

        /// <summary>
        /// Tüm grupların optimize edilmiş okuma planını oluşturur
        /// </summary>
        /// <returns>Her grup için (groupName, startAddress, count)</returns>
        public Dictionary<string, (ushort StartAddress, int Count)> CreateReadPlan()
        {
            lock (_lock)
            {
                var plan = new Dictionary<string, (ushort, int)>();

                foreach (var group in _groups)
                {
                    if (group.Registers.Count > 0)
                    {
                        plan[group.Name] = group.GetReadRange();
                    }
                }

                return plan;
            }
        }

        /// <summary>
        /// Grupları export eder (JSON formatında)
        /// </summary>
        /// <returns>JSON string</returns>
        public string ExportGroupsToJson()
        {
            return System.Text.Json.JsonSerializer.Serialize(_groups, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        /// <summary>
        /// Grupları JSON'dan import eder
        /// </summary>
        /// <param name="json">JSON string</param>
        public void ImportGroupsFromJson(string json)
        {
            var imported = System.Text.Json.JsonSerializer.Deserialize<List<RegisterGroup>>(json);
            if (imported != null)
            {
                lock (_lock)
                {
                    _groups.Clear();
                    foreach (var group in imported)
                    {
                        _groups.Add(group);

                        // Registerları da ana servise ekle
                        foreach (var register in group.Registers)
                        {
                            try
                            {
                                _registerService.AddRegister(register);
                            }
                            catch
                            {
                                // Zaten varsa atla
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region İstatistikler

        /// <summary>
        /// Toplam grup sayısı
        /// </summary>
        public int TotalGroupCount => _groups.Count;

        /// <summary>
        /// Gruplardaki toplam register sayısı
        /// </summary>
        public int TotalGroupedRegisters => _groups.Sum(g => g.Registers.Count);

        /// <summary>
        /// Grup bilgilerini özetler
        /// </summary>
        /// <returns>Grup istatistikleri</returns>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["TotalGroups"] = _groups.Count,
                ["TotalGroupedRegisters"] = _groups.Sum(g => g.Registers.Count),
                ["TotalRegisterSpace"] = _groups.Sum(g => g.TotalRegisterCount()),
                ["UngroupedRegisters"] = _registerService.TotalCount - _groups.Sum(g => g.Registers.Count),
                ["Groups"] = _groups.Select(g => new
                {
                    g.Name,
                    RegisterCount = g.Registers.Count,
                    TotalSpace = g.TotalRegisterCount(),
                    ReadRange = g.GetReadRange()
                }).ToList()
            };
        }

        #endregion

        #region Validasyon

        /// <summary>
        /// Grup tutarlılığını doğrular
        /// </summary>
        /// <returns>Validasyon hataları</returns>
        public List<string> ValidateGroups()
        {
            var errors = new List<string>();

            lock (_lock)
            {
                // Her grubun registerlarını kontrol et
                foreach (var group in _groups)
                {
                    foreach (var register in group.Registers)
                    {
                        // Register ana listede var mı?
                        if (!_registerService.Registers.Contains(register))
                        {
                            errors.Add($"Register '{register.Name}' in group '{group.Name}' not found in main registry.");
                        }
                    }
                }

                // Duplicate adres kontrolü (her grup içinde)
                foreach (var group in _groups)
                {
                    var duplicates = group.Registers
                        .GroupBy(r => r.Address)
                        .Where(g => g.Count() > 1)
                        .SelectMany(g => g.Skip(1));

                    foreach (var dup in duplicates)
                    {
                        errors.Add($"Duplicate address {dup.Address} in group '{group.Name}' for register '{dup.Name}'.");
                    }
                }
            }

            return errors;
        }

        #endregion
    }
}
