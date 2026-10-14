using System;

namespace Modbus.Communication.Server
{
    /// <summary>
    /// Server tarafının register ve coil hafızası.
    /// Ekran ve Modbus server aynı veri deposunu kullandığı için bir tarafta yapılan
    /// değişiklik diğer tarafta anında görünür.
    /// </summary>
    public class ModbusDataStore
    {
        public ushort[] HoldingRegisters { get; }
        public bool[] Coils { get; }

        public event Action<int, ushort>? HoldingRegisterChanged;
        public event Action<int, bool>? CoilChanged;

        public ModbusDataStore(int registerCount = 100, int coilCount = 100)
        {
            if (registerCount <= 0) throw new ArgumentOutOfRangeException(nameof(registerCount));
            if (coilCount <= 0) throw new ArgumentOutOfRangeException(nameof(coilCount));

            HoldingRegisters = new ushort[registerCount];
            Coils = new bool[coilCount];

            // Uygulamanın ilk çalışmasında görülecek örnek test değerleri.
            if (registerCount >= 3)
            {
                HoldingRegisters[0] = 1234;
                HoldingRegisters[1] = 5678;
                HoldingRegisters[2] = 42;
            }
        }

        public ushort GetHoldingRegister(int address)
        {
            ValidateRegisterAddress(address);
            return HoldingRegisters[address];
        }

        public void SetHoldingRegister(int address, ushort value)
        {
            ValidateRegisterAddress(address);
            if (HoldingRegisters[address] == value) return;

            HoldingRegisters[address] = value;
            HoldingRegisterChanged?.Invoke(address, value);
        }

        public bool GetCoil(int address)
        {
            ValidateCoilAddress(address);
            return Coils[address];
        }

        public void SetCoil(int address, bool value)
        {
            ValidateCoilAddress(address);
            if (Coils[address] == value) return;

            Coils[address] = value;
            CoilChanged?.Invoke(address, value);
        }

        public bool IsValidRegisterRange(int startAddress, int quantity)
        {
            return startAddress >= 0 && quantity > 0 && startAddress + quantity <= HoldingRegisters.Length;
        }

        public bool IsValidCoilRange(int startAddress, int quantity)
        {
            return startAddress >= 0 && quantity > 0 && startAddress + quantity <= Coils.Length;
        }

        private void ValidateRegisterAddress(int address)
        {
            if (address < 0 || address >= HoldingRegisters.Length)
                throw new ArgumentOutOfRangeException(nameof(address), "Register adresi veri deposu sınırları dışında.");
        }

        private void ValidateCoilAddress(int address)
        {
            if (address < 0 || address >= Coils.Length)
                throw new ArgumentOutOfRangeException(nameof(address), "Coil adresi veri deposu sınırları dışında.");
        }
    }
}