# Kütüphane Yazılım Mantığı

Bu doküman, gömülü sistemlerde yazılacak modüler kütüphaneler için genel yazılım geliştirme yaklaşımını tanımlar. Amaç, farklı projelerde yazılan kütüphanelerin aynı mimari disiplinle geliştirilmesini sağlamaktır.

---

## 1. Genel Yaklaşım

Kütüphane modüler, tekrar kullanılabilir ve farklı projelere kolay taşınabilir olacak şekilde yazılmalıdır. Kod tek bir donanıma, tek bir cihaza veya tek bir instance'a sabitlenmemelidir.

Temel yapı şu üç ana fonksiyon yaklaşımı üzerine kurulmalıdır:

```text
Init()
Control()
Callback()
```

Bu ayrım sayesinde kütüphane hem bare-metal sistemlerde hem de FreeRTOS gibi RTOS kullanılan yapılarda rahatça kullanılabilir.

---

## 2. Init Mantığı

`Init()` fonksiyonu yalnızca modülü hazırlar.

Görevleri:

```text
- Handle yapısını temizler.
- Kullanılacak donanım arayüzlerini atar.
- Varsayılan parametreleri yükler.
- State machine başlangıç durumunu ayarlar.
- Gerekli interrupt / receive mekanizmasını başlatır.
```

Önemli kural:

```text
Init fonksiyonu cihazı otomatik çalıştırmamalıdır.
Init sadece hazırlık yapmalıdır.
Gerçek çalışma ayrı bir Start / Request fonksiyonu ile başlatılmalıdır.
```

Mimari ayrım şu şekilde olmalıdır:

```text
Init  = hazırla
Start = çalıştır
Stop  = durdur
```

---

## 3. Control Mantığı

`Control()` fonksiyonu kütüphanenin ana çalışma fonksiyonudur.

Bu fonksiyon ana döngüde veya FreeRTOS task içinde periyodik olarak çağrılır.

Görevleri:

```text
- State machine'i yürütür.
- Event'leri işler.
- Timeout kontrolü yapar.
- Gerekli komutları gönderir.
- Gelen verileri işler.
- Hata / retry / recovery süreçlerini yönetir.
```

`Control()` fonksiyonu blocking olmamalıdır.

İçinde şu yapılar bulunmamalıdır:

```text
HAL_Delay()
while cevap bekle
while UART/SPI/I2C busy bekle
uzun süren işlem
```

Bunun yerine her işlem state machine ve zaman kontrolü ile yürütülmelidir.

---

## 4. Callback Mantığı

`Callback()` fonksiyonları ISR veya donanım callback tarafından çağrılır.

Callback içinde yalnızca minimum işlem yapılmalıdır.

Yapılacak işler:

```text
- Gelen byte / event alınır.
- İlgili handle bulunur.
- Buffer'a veri yazılır.
- Flag veya event set edilir.
```

Callback içinde yapılmaması gerekenler:

```text
- Paket parse etmek.
- Checksum hesaplamak.
- Uzun işlem yapmak.
- State machine'i doğrudan yürütmek.
- Blocking fonksiyon çağırmak.
```

Asıl işlem `Control()` fonksiyonu içinde yapılmalıdır.

---

## 5. State Machine Mantığı

Kütüphane state machine tabanlı yazılmalıdır.

Genel state yapısı şu şekilde olabilir:

```text
UNINIT
IDLE
STARTING
WAIT_RESPONSE
RUNNING
STOPPING
ERROR
RECOVERY
```

Her state yalnızca kendi sorumluluğunu yerine getirmelidir. State geçişleri açık, izlenebilir ve timeout kontrollü olmalıdır.

Örnek akış:

```text
STARTING
  ↓ komut gönder
WAIT_RESPONSE
  ↓ cevap geldiyse RUNNING
  ↓ timeout olduysa retry
  ↓ retry bitti ise ERROR
```

---

## 6. Event Driven Mantık

Donanımdan veya kullanıcıdan gelen olaylar event olarak ele alınmalıdır.

Örnek event tipleri:

```text
RX_READY
TX_DONE
TIMEOUT
START_REQUEST
STOP_REQUEST
ERROR_DETECTED
DATA_RECEIVED
```

Callback sadece event üretmelidir. Karar verme ve state geçişleri `Control()` fonksiyonunda yapılmalıdır.

---

## 7. Handle / Instance Mantığı

Kütüphane global değişkenlere bağımlı olmamalıdır.

Her cihaz veya modül için ayrı bir handle yapısı kullanılmalıdır:

```text
Module_Handle_t module1;
Module_Handle_t module2;
Module_Handle_t module3;
```

Bu sayede aynı kütüphane birden fazla cihaz için kullanılabilir.

Handle içinde genelde şu bilgiler bulunur:

```text
- Donanım arayüzü pointer'ı
- State bilgisi
- Event bilgisi
- TX/RX buffer
- Timeout zamanları
- Hata sayaçları
- Son alınan veri
- Çalışma durumu flag'leri
```

---

## 8. Haberleşme Mantığı

Non-blocking haberleşme tercih edilmelidir. TX/RX işlemleri interrupt veya DMA uyumlu düşünülmelidir.

Önemli kural:

```text
Non-blocking TX kullanılıyorsa lokal stack buffer gönderilmemelidir.
TX buffer handle içinde kalıcı olmalıdır.
```

Yanlış yaklaşım:

```text
Fonksiyon içinde lokal packet oluşturup non-blocking TX'e vermek.
```

Doğru yaklaşım:

```text
Handle içindeki kalıcı txBuffer hazırlanır ve gönderilir.
```

Bu kural, UART, SPI veya benzeri non-blocking haberleşmeler için önemlidir.

---

## 9. Hata Yönetimi

Kütüphane hata durumunda kilitlenmemelidir.

Temel hata mantığı:

```text
- Timeout kontrolü yapılır.
- Belirli sayıda retry yapılır.
- Retry limiti aşılırsa ERROR state'e geçilir.
- Recovery sonrası tekrar denenebilir.
```

Hata yönetimi state machine içinde görünür olmalıdır. Kütüphane sessizce başarısız olup çalışmaya devam etmemelidir.

---

## 10. FreeRTOS Uyumu

Kütüphane FreeRTOS'a doğrudan bağımlı olmak zorunda değildir; ancak FreeRTOS içinde rahat kullanılabilir olmalıdır.

Şu iki kullanım da mümkün olmalıdır:

```text
while(1) içinde Control çağırmak
```

veya

```text
FreeRTOS task içinde periyodik Control çağırmak
```

Kütüphane kendi içinde task oluşturmak zorunda değildir. Task yönetimi uygulama katmanına bırakılabilir.

---

## 11. Kısa Mimari Özet

Bu yazılım tarzında kütüphane şu prensiple yazılır:

```text
Init sadece hazırlar.
Start isteği ayrıca verilir.
Control tüm iş akışını yürütür.
Callback sadece event/flag üretir.
ISR içinde ağır işlem yapılmaz.
State machine kullanılır.
Her modül ayrı handle ile yönetilir.
Non-blocking çalışma tercih edilir.
Timeout, retry ve recovery mekanizması bulunur.
Kod donanıma aşırı gömülmez, taşınabilir kalır.
```

Bu yaklaşım korunursa farklı projelerde yazılan kütüphaneler aynı yazılım mimarisi ve kodlama disipliniyle geliştirilmiş olur.
