using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Modbus.App.Models
{
    /// <summary>
    /// Ekranda bir Modbus register satırını temsil eder.
    /// Value değiştiğinde Decimal / HEX / Binary görünümleri otomatik yenilenir.
    /// </summary>
    public sealed class RegisterItem : INotifyPropertyChanged
    {
        private string _name;
        private ushort _value;
        private DateTime _lastUpdated;

        public RegisterItem(int address, string name, ushort value)
        {
            Address = address;
            _name = name;
            _value = value;
            _lastUpdated = DateTime.Now;
        }

        public int Address { get; }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        public ushort Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                _lastUpdated = DateTime.Now;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HexValue));
                OnPropertyChanged(nameof(BinaryValue));
                OnPropertyChanged(nameof(LastUpdated));
            }
        }

        public string HexValue => $"0x{Value:X4}";

        public string BinaryValue => Convert.ToString(Value, 2).PadLeft(16, '0');

        public string LastUpdated => _lastUpdated.ToString("HH:mm:ss.fff");

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}