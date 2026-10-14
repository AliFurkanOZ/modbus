using System.Collections.ObjectModel;

namespace Modbus.Protocol.Models
{
    /// <summary>
    /// Register tanımlarının gruplanmış halini temsil eder.
    /// Örneğin: "Sıcaklık Sensörleri", "Gerilim Ölçümleri", "Kontroller"
    /// </summary>
    public class RegisterGroup
    {
        /// <summary>
        /// Grup adı (örn. "Sıcaklık Sensörleri")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Grup açıklaması
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Bu gruptaki register tanımları
        /// </summary>
        public ObservableCollection<RegisterDefinition> Registers { get; set; } = new();

        /// <summary>
        /// Grup rengi (UI için)
        /// </summary>
        public string? ColorCode { get; set; }

        /// <summary>
        /// Grubun başlangıç adresi (opsiyonel, sıralama için)
        /// </summary>
        public ushort? StartAddress { get; set; }

        /// <summary>
        /// Grubun bitiş adresi (opsiyonel)
        /// </summary>
        public ushort? EndAddress { get; set; }

        /// <summary>
        /// Bu grubun okuma işlemi için optimize edilmiş adres aralığını hesaplar
        /// </summary>
        /// <returns>(startAddress, count) tuple</returns>
        public (ushort StartAddress, int Count) GetReadRange()
        {
            if (Registers.Count == 0)
                return (0, 0);

            var minAddr = Registers.Min(r => r.Address);
            var maxAddr = Registers.Max(r => r.Address);
            
            // En son register'ın kapladığı alanı hesaba kat
            var lastReg = Registers.First(r => r.Address == maxAddr);
            var endAddr = (ushort)(maxAddr + lastReg.GetRegisterCount() - 1);

            return ((ushort)minAddr, endAddr - minAddr + 1);
        }

        /// <summary>
        /// Adrese göre register bulur
        /// </summary>
        /// <param name="address">Register adresi</param>
        /// <returns>RegisterDefinition veya null</returns>
        public RegisterDefinition? FindByAddress(ushort address)
        {
            return Registers.FirstOrDefault(r => r.Address == address);
        }

        /// <summary>
        /// Register ekler
        /// </summary>
        /// <param name="register">Eklenecek register tanımı</param>
        public void AddRegister(RegisterDefinition register)
        {
            Registers.Add(register);
        }

        /// <summary>
        /// Register siler
        /// </summary>
        /// <param name="register">Silinecek register tanımı</param>
        public void RemoveRegister(RegisterDefinition register)
        {
            Registers.Remove(register);
        }

        /// <summary>
        /// Tüm registerları temizler
        /// </summary>
        public void Clear()
        {
            Registers.Clear();
        }

        /// <summary>
        /// Grubun toplam register sayısını döner
        /// </summary>
        /// <returns>Register sayısı</returns>
        public int TotalRegisterCount()
        {
            return Registers.Sum(r => r.GetRegisterCount());
        }
    }
}
