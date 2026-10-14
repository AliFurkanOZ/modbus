using System;
using System.Collections.Generic;

using Modbus.Protocol.Packets;
using Modbus.Protocol.Helpers;

namespace Modbus.Protocol.Parsers
{
    /// <summary>
    /// Karşı cihazdan gelen ham yanıt baytlarını "anlamlı" hale getiren sınıf.
    ///
    /// İstek gönderdik, cihaz bize byte dizisi döndü. Ama bu baytlar kendiliğinden
    /// bir şey ifade etmez. Burada onları çözüyoruz (parse ediyoruz):
    ///  - hata var mı? (exception response)
    ///  - okunan register değerleri neler?
    ///  - CRC doğrulaması geçiyor mu?
    /// </summary>
    public class ResponseParser
    {
        /// <summary>
        /// TCP yanıtını çözer. TCP yanıtı da MBAP header ile başlar:
        /// [TxId Hi][Lo][Proto Hi][Lo][Len Hi][Lo][UnitId][FunctionCode][Veri...]
        /// Yani ilk 6 byte header, 7. byte UnitId, 8. byte fonksiyon kodudur.
        /// </summary>
        public ModbusPacket ParseTcpResponse(byte[] response)
        {
            // En az 8 byte olmalı (7 MBAP + en az 1 fonksiyon kodu). Yoksa bozuk.
            if (response == null || response.Length < 8)
                throw new ModbusFrameException("TCP yanıtı çok kısa / bozuk.");

            ModbusPacket packet = new();
            packet.RawData = response;

            packet.TransactionId = (ushort)((response[0] << 8) | response[1]);
            packet.ProtocolId = (ushort)((response[2] << 8) | response[3]);
            packet.Length = (ushort)((response[4] << 8) | response[5]);

            if (packet.ProtocolId != 0)
                throw new ModbusFrameException("Geçersiz Modbus TCP Protocol ID.");

            if (packet.Length != response.Length - 6)
                throw new ModbusFrameException("Modbus TCP Length alanı ile gerçek paket uzunluğu uyuşmuyor.");

            // MBAP'te UnitId 7. bytetır (index 6).
            packet.SlaveId = response[6];

            // Fonksiyon kodu 8. bytetır (index 7).
            packet.FunctionCode = response[7];

            // 8. byteten sonrası "veri" kısmıdır. Onu ayrı diziye kopyalıyoruz.
            int dataLength = response.Length - 8;
            packet.Data = new byte[dataLength];
            Array.Copy(response, 8, packet.Data, 0, dataLength);

            return packet;
        }

        /// <summary>
        /// RTU yanıtını çözer ve CRC doğrulaması yapar. RTU yanıtı:
        /// [SlaveId][FunctionCode][Veri...][CRC-Low][CRC-High]
        /// İlk byte adres, ikinci byte fonksiyon, sondaki 2 byte CRC.
        /// 
        /// Modbus V1.02 §2.5.1.2: "CRC field checks the contents of the entire message."
        /// </summary>
        /// <param name="response">Ham RTU yanıt byte dizisi (CRC dahil)</param>
        /// <param name="expectedSlaveId">Opsiyonel: beklenen Slave ID doğrulaması</param>
        /// <returns>Parse edilmiş ModbusPacket</returns>
        public ModbusPacket ParseRtuResponse(byte[] response, byte? expectedSlaveId = null)
        {
            // En az 4 byte: adres + fonksiyon + 2 byte CRC.
            if (response == null || response.Length < 4)
                throw new ModbusFrameException("RTU yanıtı çok kısa / bozuk. Minimum 4 byte gerekli (SlaveId + FC + CRC).");

            // ── CRC Doğrulaması (KRİTİK-01 düzeltmesi) ──
            // Modbus standardına göre alıcı taraf, gelen mesajın CRC'sini
            // hesaplayıp frame'deki CRC ile karşılaştırMALIDIR.
            if (!CRC16.Validate(response, out ushort calculatedCrc, out ushort receivedCrc))
            {
                throw new ModbusCrcException(calculatedCrc, receivedCrc);
            }

            ModbusPacket packet = new();
            packet.RawData = response;

            packet.SlaveId = response[0];
            packet.FunctionCode = response[1];

            // ── Slave ID Doğrulaması (MİMARİ-02 düzeltmesi) ──
            if (expectedSlaveId.HasValue && packet.SlaveId != expectedSlaveId.Value)
            {
                throw new ModbusFrameException(
                    $"Beklenmeyen Slave ID: Beklenen={expectedSlaveId.Value}, Gelen={packet.SlaveId}. " +
                    "Multi-slave ortamda yanlış cihazdan yanıt gelmiş olabilir.");
            }

            // Veri = baştan 2 (adres+fonksiyon), sondan 2 (CRC) çıkar.
            int dataLength = response.Length - 4;
            packet.Data = new byte[dataLength];
            Array.Copy(response, 2, packet.Data, 0, dataLength);

            return packet;
        }

        /// <summary>
        /// Bir "Read Holding/Input Register" yanıtındaki register değerlerini döndürür.
        ///
        /// Bu yanıtın "Data" kısmının yapısı şöyledir:
        ///   [ByteCount][Reg1 Hi][Reg1 Lo][Reg2 Hi][Reg2 Lo]...
        /// İlk byte kaç byte veri geldiğini söyler; sonra her register 2 byte'tır.
        /// </summary>
        public ushort[] ReadRegisterValues(ModbusPacket packet)
        {
            // Data boşsa okunacak bir şey yok.
            if (packet.Data.Length < 1)
                return Array.Empty<ushort>();

            // İlk byte: toplam veri byte sayısı.
            int byteCount = packet.Data[0];

            // byteCount'un gerçek veriyle tutarlı olduğunu doğrula
            if (byteCount != packet.Data.Length - 1)
                throw new ModbusFrameException(
                    $"Register yanıtında ByteCount uyumsuzluğu: ByteCount={byteCount}, " +
                    $"gerçek veri uzunluğu={packet.Data.Length - 1}");

            // Her register 2 byte olduğu için register sayısı = byteCount / 2.
            int registerCount = byteCount / 2;
            ushort[] values = new ushort[registerCount];

            for (int i = 0; i < registerCount; i++)
            {
                // Data[0] byteCount olduğu için gerçek veriler index 1'den başlar.
                int hiIndex = 1 + (i * 2);     // yüksek byte
                int loIndex = hiIndex + 1;     // düşük byte

                // İki byte'ı 16 bitlik tek sayıya birleştir (Big-Endian):
                // yüksek byte'ı 8 bit sola kaydır, düşük byte'ı ekle.
                values[i] = (ushort)((packet.Data[hiIndex] << 8) | packet.Data[loIndex]);
            }

            return values;
        }

        /// <summary>
        /// Bir "Read Coils" veya "Read Discrete Inputs" yanıtındaki bit değerlerini döndürür.
        /// (KRİTİK-06 düzeltmesi — bu metot eksikti)
        ///
        /// Yanıtın "Data" kısmının yapısı:
        ///   [ByteCount][CoilData byte 1][CoilData byte 2]...
        /// Her byte 8 coil/input durumunu içerir (LSB-first: bit 0 = ilk coil).
        /// </summary>
        /// <param name="packet">Parse edilmiş Modbus paketi</param>
        /// <param name="quantity">İstenen coil/input sayısı (fazlalık bitleri kırpmak için)</param>
        /// <returns>Coil/input değerleri dizisi</returns>
        public bool[] ReadCoilValues(ModbusPacket packet, ushort quantity)
        {
            if (packet.Data.Length < 1)
                return Array.Empty<bool>();

            int byteCount = packet.Data[0];

            // Yeterli veri var mı kontrol et
            if (packet.Data.Length < 1 + byteCount)
                throw new ModbusFrameException(
                    $"Coil yanıtında veri eksik: ByteCount={byteCount}, " +
                    $"gerçek veri uzunluğu={packet.Data.Length - 1}");

            // Sonuç dizisini istenen quantity kadar oluştur (fazlalık bitleri kırp)
            bool[] values = new bool[quantity];
            int coilIndex = 0;

            for (int i = 1; i <= byteCount && coilIndex < quantity; i++)
            {
                byte currentByte = packet.Data[i];

                // Modbus standardı: LSB-first packing.
                // Bit 0 = ilk coil, Bit 7 = sekizinci coil.
                for (int bit = 0; bit < 8 && coilIndex < quantity; bit++)
                {
                    values[coilIndex++] = (currentByte & (1 << bit)) != 0;
                }
            }

            return values;
        }

        /// <summary>
        /// Yanıtın bir HATA yanıtı olup olmadığını söyler.
        /// Modbus'ta hata olduğunda cihaz, fonksiyon kodunun en yüksek bitini 1 yapar.
        /// Yani orijinal koda 0x80 eklenir (örn. 0x03 -> 0x83).
        /// </summary>
        public bool IsErrorResponse(ModbusPacket packet)
        {
            // 0x80 = 1000 0000. Bu bit set ise hata var demektir.
            return (packet.FunctionCode & 0x80) != 0;
        }
    }
}