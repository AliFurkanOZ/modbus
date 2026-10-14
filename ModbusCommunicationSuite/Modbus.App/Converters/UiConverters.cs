using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Modbus.App.Converters
{
    /// <summary>
    /// Bool değerleri Visibility'a çevirir (True=Visible, False=Collapsed veya Hidden)
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Tersine çevirme için kullanılır (True=Collapsed, False=Visible)
        /// </summary>
        public bool Inverse { get; set; } = false;

        /// <summary>
        /// False durumunda kullanılacak Visibility değeri (Collapsed veya Hidden)
        /// </summary>
        public Visibility FalseVisibility { get; set; } = Visibility.Collapsed;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (Inverse)
                {
                    return boolValue ? FalseVisibility : Visibility.Visible;
                }
                return boolValue ? Visibility.Visible : FalseVisibility;
            }

            return FalseVisibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool result = visibility == Visibility.Visible;
                return Inverse ? !result : result;
            }

            return false;
        }
    }

    /// <summary>
    /// ConnectionState enum'ını renk koduna çevirir
    /// </summary>
    public class ConnectionStateToColorConverter : IValueConverter
    {
        public string ConnectedColor { get; set; } = "#4CAF50";
        public string DisconnectedColor { get; set; } = "#F44336";
        public string ConnectingColor { get; set; } = "#FF9800";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string state)
            {
                return state.ToLower().Contains("connected") || state.ToLower().Contains("başarılı")
                    ? ConnectedColor
                    : state.ToLower().Contains("connecting") || state.ToLower().Contains("bağlanıyor")
                        ? ConnectingColor
                        : DisconnectedColor;
            }

            return DisconnectedColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorStr)
            {
                if (colorStr.Equals(ConnectedColor, StringComparison.OrdinalIgnoreCase))
                    return "Connected";
                if (colorStr.Equals(ConnectingColor, StringComparison.OrdinalIgnoreCase))
                    return "Connecting";
                return "Disconnected";
            }
            
            throw new InvalidOperationException("Cannot convert color back to ConnectionState");
        }
    }

    /// <summary>
    /// Register türüne göre ikon veya renk döndürür
    /// </summary>
    public class RegisterTypeToColorConverter : IValueConverter
    {
        public string CoilsColor { get; set; } = "#2196F3";
        public string DiscreteInputsColor { get; set; } = "#00BCD4";
        public string InputRegistersColor { get; set; } = "#4CAF50";
        public string HoldingRegistersColor { get; set; } = "#FF9800";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string registerType)
            {
                return registerType.ToLower() switch
                {
                    "coil" or "coils" => CoilsColor,
                    "discreteinput" or "discrete inputs" => DiscreteInputsColor,
                    "inputregister" or "input registers" => InputRegistersColor,
                    "holdingregister" or "holding registers" => HoldingRegistersColor,
                    _ => "#9E9E9E"
                };
            }

            return "#9E9E9E";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorStr)
            {
                if (colorStr.Equals(CoilsColor, StringComparison.OrdinalIgnoreCase))
                    return "Coils";
                if (colorStr.Equals(DiscreteInputsColor, StringComparison.OrdinalIgnoreCase))
                    return "DiscreteInputs";
                if (colorStr.Equals(InputRegistersColor, StringComparison.OrdinalIgnoreCase))
                    return "InputRegisters";
                if (colorStr.Equals(HoldingRegistersColor, StringComparison.OrdinalIgnoreCase))
                    return "HoldingRegisters";
                return "Unknown";
            }
            
            throw new InvalidOperationException("Cannot convert color back to RegisterType");
        }
    }

    /// <summary>
    /// Alarm seviyesine göre renk döndürür
    /// </summary>
    public class AlarmLevelToColorConverter : IValueConverter
    {
        public string HighHighColor { get; set; } = "#D32F2F";
        public string HighColor { get; set; } = "#FF5722";
        public string LowColor { get; set; } = "#FFA726";
        public string LowLowColor { get; set; } = "#FDD835";
        public string NormalColor { get; set; } = "#4CAF50";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string level)
            {
                return level.ToLower() switch
                {
                    "highhigh" or "high high" or "hh" => HighHighColor,
                    "high" or "h" => HighColor,
                    "low" or "l" => LowColor,
                    "lowlow" or "low low" or "ll" => LowLowColor,
                    _ => NormalColor
                };
            }

            return NormalColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorStr)
            {
                if (colorStr.Equals(HighHighColor, StringComparison.OrdinalIgnoreCase))
                    return "HighHigh";
                if (colorStr.Equals(HighColor, StringComparison.OrdinalIgnoreCase))
                    return "High";
                if (colorStr.Equals(LowColor, StringComparison.OrdinalIgnoreCase))
                    return "Low";
                if (colorStr.Equals(LowLowColor, StringComparison.OrdinalIgnoreCase))
                    return "LowLow";
                return "Normal";
            }
            
            throw new InvalidOperationException("Cannot convert color back to AlarmLevel");
        }
    }

    /// <summary>
    /// Sayısal değerleri formatlar (örn: 2 ondalık basamak)
    /// </summary>
    public class NumericFormatConverter : IValueConverter
    {
        public string Format { get; set; } = "F2";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IFormattable formattable)
            {
                string format = parameter as string ?? Format;
                return formattable.ToString(format, culture);
            }

            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && double.TryParse(str, NumberStyles.Any, culture, out double result))
            {
                return result;
            }

            return 0.0;
        }
    }

    /// <summary>
    /// Null veya boş string'i Visibility.Collapsed'a çevirir
    /// </summary>
    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public bool Inverse { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEmpty = string.IsNullOrWhiteSpace(value as string);
            
            if (Inverse)
            {
                return isEmpty ? Visibility.Collapsed : Visibility.Visible;
            }

            return isEmpty ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible ? string.Empty : null;
            }
            
            throw new InvalidOperationException("Cannot convert Visibility back to string");
        }
    }

    /// <summary>
    /// Integer değeri 0 ise False, değilse True döndürür
    /// </summary>
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue != 0;
            }

            if (value is uint uintValue)
            {
                return uintValue != 0;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? 1 : 0;
            }

            return 0;
        }
    }
}
