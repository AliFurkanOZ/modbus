using System;

namespace Modbus.Protocol.Helpers
{
    /// <summary>
    /// Modbus RTU çerçevelerinde kullanılan CRC-16 hesaplayıcı.
    /// CRC = "Cyclic Redundancy Check" (Döngüsel Artıklık Denetimi).
    /// Amacı: gönderilen paketin yolda bozulup bozulmadığını anlamak.
    /// Gönderen taraf CRC hesaplar ve pakete ekler; alan taraf aynı hesabı
    /// yapar, sonuç aynıysa paket sağlamdır.
    ///
    /// "static" sınıf: nesne oluşturmadan CRC16.Calculate(...) diye çağırılır.
    /// </summary>
    public static class CRC16
    {
        /// <summary>
        /// Verilen byte dizisi için Modbus CRC-16 değerini hesaplar.
        /// </summary>
        public static ushort Calculate(byte[] data)
        {
            return Calculate(data, 0, data.Length);
        }

        /// <summary>
        /// Verilen byte dizisinin belirli bir bölümü için Modbus CRC-16 değerini hesaplar.
        /// </summary>
        /// <param name="data">Veri dizisi</param>
        /// <param name="offset">Başlangıç indeksi</param>
        /// <param name="count">Hesaplanacak byte sayısı</param>
        public static ushort Calculate(byte[] data, int offset, int count)
        {
            // Modbus standardı CRC'yi 0xFFFF (tüm bitler 1) ile başlatmayı söyler.
            ushort crc = 0xFFFF;

            // Paketteki her byte'ı tek tek işliyoruz.
            for (int j = offset; j < offset + count; j++)
            {
                // XOR: byte'ı CRC'nin alt 8 bitine karıştır.
                crc ^= data[j];

                // Her byte için 8 bit boyunca kaydırma yapıyoruz.
                for (int i = 0; i < 8; i++)
                {
                    // En düşük bit (LSB) 1 mi? (crc & 1)
                    if ((crc & 1) != 0)
                    {
                        // 1 ise: bir bit sağa kaydır, sonra Modbus polinomu 0xA001 ile XOR'la.
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        // 0 ise: sadece bir bit sağa kaydır.
                        crc >>= 1;
                    }
                }
            }

            // Sonuç 16 bitlik CRC. Modbus RTU'da paketin sonuna
            // önce düşük byte (Low), sonra yüksek byte (High) olarak eklenir.
            return crc;
        }

        /// <summary>
        /// Gelen RTU frame'inin CRC doğrulamasını yapar.
        /// Frame yapısı: [SlaveId][FunctionCode][Data...][CRC-Low][CRC-High]
        /// Son 2 byte CRC'dir; geri kalan byte'lar üzerinden CRC hesaplanır ve karşılaştırılır.
        /// </summary>
        /// <param name="frame">Tam RTU frame (CRC dahil)</param>
        /// <returns>CRC geçerli ise true</returns>
        public static bool Validate(byte[] frame)
        {
            if (frame == null || frame.Length < 4)
                return false;

            // Frame'in son 2 byte'ı alınan CRC (Low + High)
            ushort receivedCrc = (ushort)(frame[frame.Length - 2] | (frame[frame.Length - 1] << 8));

            // Geri kalan byte'lar üzerinden CRC hesapla
            ushort calculatedCrc = Calculate(frame, 0, frame.Length - 2);

            return receivedCrc == calculatedCrc;
        }

        /// <summary>
        /// Gelen RTU frame'inin CRC doğrulamasını yapar.
        /// Eşleşmezse hesaplanan ve alınan CRC değerlerini out parametrelerinde döndürür.
        /// </summary>
        public static bool Validate(byte[] frame, out ushort calculatedCrc, out ushort receivedCrc)
        {
            calculatedCrc = 0;
            receivedCrc = 0;

            if (frame == null || frame.Length < 4)
                return false;

            receivedCrc = (ushort)(frame[frame.Length - 2] | (frame[frame.Length - 1] << 8));
            calculatedCrc = Calculate(frame, 0, frame.Length - 2);

            return receivedCrc == calculatedCrc;
        }
    }
}
