namespace Modbus.Protocol.Enums
{
    /// <summary>
    /// Modbus exception (hata) kodları.
    /// Cihaz bir isteği işleyemediğinde bu kodlardan birini döner.
    /// </summary>
    public enum ModbusExceptionCode : byte
    {
        /// <summary>
        /// Exception yok (başarılı işlem)
        /// </summary>
        None = 0,

        /// <summary>
        /// Illegal Function (Fonksiyon Kodu 1):
        /// Alınan fonksiyon kodu tanınmadı veya desteklenmiyor.
        /// </summary>
        IllegalFunction = 1,

        /// <summary>
        /// Illegal Data Address (Fonksiyon Kodu 2):
        /// İstenen register adresi cihazda mevcut değil veya geçersiz.
        /// En yaygın hata kodu.
        /// </summary>
        IllegalDataAddress = 2,

        /// <summary>
        /// Illegal Data Value (Fonksiyon Kodu 3):
        /// İstenen veri değeri geçersiz veya izin verilen aralıkta değil.
        /// </summary>
        IllegalDataValue = 3,

        /// <summary>
        /// Slave Device Failure (Fonksiyon Kodu 4):
        /// Köle cihaz isteği işlerken iç hata oluştu.
        /// </summary>
        SlaveDeviceFailure = 4,

        /// <summary>
        /// Acknowledge (Fonksiyon Kodu 5):
        /// Cihaz uzun süreli bir işlemi onayladı, tamamlanmasını bekleyin.
        /// </summary>
        Acknowledge = 5,

        /// <summary>
        /// Slave Device Busy (Fonksiyon Kodu 6):
        /// Cihaz meşgul, isteği şu anda işleyemiyor.
        /// </summary>
        SlaveDeviceBusy = 6,

        /// <summary>
        /// Memory Parity Error (Fonksiyon Kodu 8):
        /// Cihaz hafızasında parity hatası tespit edildi.
        /// </summary>
        MemoryParityError = 8,

        /// <summary>
        /// Gateway Path Unavailable (Fonksiyon Kodu 10):
        /// Ağ geçidi yolu mevcut değil (gateway kullanıldığında).
        /// </summary>
        GatewayPathUnavailable = 10,

        /// <summary>
        /// Gateway Target Device Failed to Respond (Fonksiyon Kodu 11):
        /// Hedef cihaz yanıt vermedi (gateway kullanıldığında).
        /// </summary>
        GatewayTargetDeviceFailedToRespond = 11
    }
}
