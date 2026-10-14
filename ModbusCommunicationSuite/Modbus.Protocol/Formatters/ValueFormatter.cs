namespace Modbus.Protocol.Formatters
{
    /// <summary>
    /// Modbus register değerlerini farklı formatlarda string olarak gösterir.
    /// Hex, Binary, ASCII gibi formatları destekler.
    /// </summary>
    public static class ValueFormatter
    {
        /// <summary>
        /// 16-bit değeri hex string olarak formatlar
        /// </summary>
        /// <param name="value">16-bit değer</param>
        /// <param name="prefix">"0x" öneki eklenecek mi?</param>
        /// <param name="upperCase">Büyük harf kullanılsın mı?</param>
        /// <returns>Hex string (örn: "0x1A2B" veya "1a2b")</returns>
        public static string ToHex(ushort value, bool prefix = true, bool upperCase = true)
        {
            string format = upperCase ? "X4" : "x4";
            string hex = value.ToString(format);
            return prefix ? $"0x{hex}" : hex;
        }

        /// <summary>
        /// 32-bit değeri hex string olarak formatlar
        /// </summary>
        public static string ToHex(uint value, bool prefix = true, bool upperCase = true)
        {
            string format = upperCase ? "X8" : "x8";
            string hex = value.ToString(format);
            return prefix ? $"0x{hex}" : hex;
        }

        /// <summary>
        /// 64-bit değeri hex string olarak formatlar
        /// </summary>
        public static string ToHex(ulong value, bool prefix = true, bool upperCase = true)
        {
            string format = upperCase ? "X16" : "x16";
            string hex = value.ToString(format);
            return prefix ? $"0x{hex}" : hex;
        }

        /// <summary>
        /// 16-bit değeri binary string olarak formatlar
        /// </summary>
        /// <param name="value">16-bit değer</param>
        /// <param name="groupSize">Bitleri gruplamak için boyut (örn: 4 için "0001 1010 0010 1011")</param>
        /// <returns>Binary string</returns>
        public static string ToBinary(ushort value, int? groupSize = null)
        {
            string binary = Convert.ToString(value, 2).PadLeft(16, '0');
            
            if (groupSize.HasValue && groupSize > 0)
            {
                var groups = new List<string>();
                for (int i = 0; i < binary.Length; i += groupSize.Value)
                {
                    int len = Math.Min(groupSize.Value, binary.Length - i);
                    groups.Add(binary.Substring(i, len));
                }
                return string.Join(" ", groups);
            }
            
            return binary;
        }

        /// <summary>
        /// 32-bit değeri binary string olarak formatlar
        /// </summary>
        public static string ToBinary(uint value, int? groupSize = null)
        {
            string binary = Convert.ToString(value, 2).PadLeft(32, '0');
            
            if (groupSize.HasValue && groupSize > 0)
            {
                var groups = new List<string>();
                for (int i = 0; i < binary.Length; i += groupSize.Value)
                {
                    int len = Math.Min(groupSize.Value, binary.Length - i);
                    groups.Add(binary.Substring(i, len));
                }
                return string.Join(" ", groups);
            }
            
            return binary;
        }

        /// <summary>
        /// İki 16-bit register'dan ASCII string oluşturur
        /// Her register 2 ASCII karakter içerir
        /// </summary>
        /// <param name="reg1">İlk register</param>
        /// <param name="reg2">İkinci register</param>
        /// <returns>ASCII string (maksimum 4 karakter)</returns>
        public static string ToAscii(ushort reg1, ushort reg2)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(reg1 >> 8);
            bytes[1] = (byte)(reg1 & 0xFF);
            bytes[2] = (byte)(reg2 >> 8);
            bytes[3] = (byte)(reg2 & 0xFF);
            
            // Null-terminated string ise sonunu kes
            int length = Array.IndexOf(bytes, (byte)0);
            if (length >= 0)
                return System.Text.Encoding.ASCII.GetString(bytes, 0, length);
            
            return System.Text.Encoding.ASCII.GetString(bytes);
        }

        /// <summary>
        /// Register dizisinden ASCII string oluşturur
        /// </summary>
        /// <param name="registers">Register dizisi</param>
        /// <param name="maxLength">Maksimum karakter sayısı (null = tüm registerlar)</param>
        /// <returns>ASCII string</returns>
        public static string ToAscii(ushort[] registers, int? maxLength = null)
        {
            int charCount = maxLength ?? registers.Length * 2;
            byte[] bytes = new byte[charCount];
            
            int byteIndex = 0;
            foreach (var reg in registers)
            {
                if (byteIndex >= charCount) break;
                
                bytes[byteIndex++] = (byte)(reg >> 8);
                if (byteIndex >= charCount) break;
                
                bytes[byteIndex++] = (byte)(reg & 0xFF);
            }
            
            // Null-terminated string ise sonunu kes
            int nullIndex = Array.IndexOf(bytes, (byte)0);
            if (nullIndex >= 0 && nullIndex < charCount)
                return System.Text.Encoding.ASCII.GetString(bytes, 0, nullIndex);
            
            return System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(byteIndex, charCount));
        }

        /// <summary>
        /// Değeri formatlı string olarak gösterir (birim ile birlikte)
        /// </summary>
        /// <param name="value">Değer</param>
        /// <param name="unit">Birim (örn: "°C", "V")</param>
        /// <param name="decimalPlaces">Ondalık basamak sayısı</param>
        /// <returns>Formatlı string (örn: "25.50 °C")</returns>
        public static string WithUnit(double value, string unit, int decimalPlaces = 2)
        {
            if (string.IsNullOrEmpty(unit))
                return value.ToString($"F{decimalPlaces}");
            
            return $"{value.ToString($"F{decimalPlaces}")} {unit}";
        }

        /// <summary>
        /// Float değeri bilimsel gösterimde formatlar
        /// </summary>
        public static string ToScientific(double value, int decimalPlaces = 2)
        {
            return value.ToString($"E{decimalPlaces}");
        }
    }
}
