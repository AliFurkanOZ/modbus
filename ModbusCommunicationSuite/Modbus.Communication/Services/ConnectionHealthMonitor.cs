using System;
using System.Threading;
using System.Threading.Tasks;
using Modbus.Communication.Common;
using Modbus.Core.Interfaces;
using Modbus.Protocol.Helpers;

namespace Modbus.Communication.Services
{
    /// <summary>
    /// Bağlantı sağlık kontrolü servisi
    /// Periyodik olarak probe göndererek bağlantı durumunu izler
    /// </summary>
    public class ConnectionHealthMonitor : IDisposable
    {
        private readonly IModbusClient _client;
        private readonly TrafficLogger _logger;
        private readonly byte _slaveId;
        private readonly TimeSpan _checkInterval;
        private readonly TimeSpan _timeout;
        private readonly int _maxFailedAttempts;
        private readonly bool _isRtu;
        private CancellationTokenSource? _cts;
        private Task? _monitoringTask;
        private bool _disposed;
        private int _consecutiveFailures;
        private DateTime _lastCheckTime;
        private DateTime _lastSuccessfulCheckTime;
        private ConnectionState _currentState;
        private readonly object _lock = new();

        /// <summary>
        /// Son sağlık kontrolü zamanı
        /// </summary>
        public DateTime LastCheckTime
        {
            get { lock (_lock) return _lastCheckTime; }
        }

        /// <summary>
        /// Son başarılı sağlık kontrolü zamanı
        /// </summary>
        public DateTime LastSuccessfulCheckTime
        {
            get { lock (_lock) return _lastSuccessfulCheckTime; }
        }

        /// <summary>
        /// Art arda başarısız deneme sayısı
        /// </summary>
        public int ConsecutiveFailures
        {
            get { lock (_lock) return _consecutiveFailures; }
        }

        /// <summary>
        /// Mevcut bağlantı durumu
        /// </summary>
        public ConnectionState CurrentState
        {
            get { lock (_lock) return _currentState; }
            private set { lock (_lock) _currentState = value; }
        }

        /// <summary>
        /// Bağlantı sağlıklı mı? (Son check başarılı ve threshold altında)
        /// </summary>
        public bool IsHealthy => CurrentState == ConnectionState.Connected && 
                                 _consecutiveFailures < _maxFailedAttempts;

        /// <summary>
        /// Monitoring çalışıyor mu?
        /// </summary>
        public bool IsMonitoring => _monitoringTask != null && !_monitoringTask.IsCompleted;

        /// <summary>
        /// Bağlantı sağlık kontrolü oluşturur
        /// </summary>
        /// <param name="client">Modbus client</param>
        /// <param name="logger">Trafik logger</param>
        /// <param name="slaveId">Slave ID</param>
        /// <param name="checkInterval">Kontrol sıklığı (varsayılan: 5 saniye)</param>
        /// <param name="timeout">Her check için timeout (varsayılan: 2 saniye)</param>
        /// <param name="maxFailedAttempts">Bağlantıyı kopuk saymadan önceki maksimum başarısız deneme (varsayılan: 3)</param>
        /// <param name="isRtu">RTU modu mu? (false = TCP). Probe paketi formatını belirler.</param>
        public ConnectionHealthMonitor(
            IModbusClient client,
            TrafficLogger logger,
            byte slaveId = 1,
            TimeSpan? checkInterval = null,
            TimeSpan? timeout = null,
            int maxFailedAttempts = 3,
            bool isRtu = false)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _slaveId = slaveId;
            _checkInterval = checkInterval ?? TimeSpan.FromSeconds(5);
            _timeout = timeout ?? TimeSpan.FromSeconds(2);
            _maxFailedAttempts = maxFailedAttempts;
            _isRtu = isRtu;
            CurrentState = ConnectionState.Disconnected;
        }

        /// <summary>
        /// Sağlık kontrolünü başlatır
        /// </summary>
        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConnectionHealthMonitor));

            if (IsMonitoring)
                return; // Zaten çalışıyor

            _cts = new CancellationTokenSource();
            _monitoringTask = MonitorLoopAsync(_cts.Token);
            
            _logger.LogInfo($"Health monitor başlatıldı - Interval: {_checkInterval.TotalSeconds}s", _slaveId);
        }

        /// <summary>
        /// Sağlık kontrolünü durdurur
        /// </summary>
        public async Task StopAsync()
        {
            if (_cts == null || !IsMonitoring)
                return;

            _cts.Cancel();
            
            if (_monitoringTask != null)
            {
                try
                {
                    await _monitoringTask.WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    // Timeout durumunda task'ı force et
                }
            }

            _cts?.Dispose();
            _cts = null;
            _monitoringTask = null;
            
            _logger.LogInfo("Health monitor durduruldu", _slaveId);
        }

        /// <summary>
        /// Arka planda periyodik sağlık kontrolü yapan döngü
        /// </summary>
        private async Task MonitorLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Bekle
                    try
                    {
                        await Task.Delay(_checkInterval, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    // Health check yap
                    await PerformHealthCheckAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(_slaveId, 0, $"Health monitor döngü hatası: {ex.Message}", ex.ToString());
            }
        }

        /// <summary>
        /// Tek bir sağlık kontrolü gerçekleştirir
        /// </summary>
        private async Task PerformHealthCheckAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool success = false;

            try
            {
                // Client bağlı değilse health check yapma
                if (!_client.IsConnected)
                {
                    UpdateState(ConnectionState.Disconnected);
                    return;
                }

                // MİMARİ-03 düzeltmesi: RTU/TCP'ye uygun probe paketi oluştur
                byte[] probePacket = _isRtu 
                    ? BuildReadHoldingRegistersProbeRtu(_slaveId, 0, 1)
                    : BuildReadHoldingRegistersProbe(_slaveId, 0, 1);
                
                using var cts = new CancellationTokenSource(_timeout);
                byte[] response = await _client.SendAsync(probePacket).WaitAsync(cts.Token);
                
                stopwatch.Stop();
                
                // Response geçerli mi? RTU ve TCP için farklı minimum boyut kontrolü
                int minResponseSize = _isRtu ? 5 : 9; // RTU: addr+fc+1data+2crc, TCP: 7mbap+fc+1data
                if (response != null && response.Length >= minResponseSize)
                {
                    byte functionCode;
                    
                    if (_isRtu)
                    {
                        // RTU: CRC doğrulaması yap
                        if (!CRC16.Validate(response))
                        {
                            _logger.LogInfo("Health check: CRC doğrulaması başarısız", _slaveId);
                        }
                        else
                        {
                            functionCode = response[1]; // RTU'da 2. byte FC
                            success = true;
                            if ((functionCode & 0x80) == 0x80)
                                _logger.LogInfo($"Health check: Cihaz exception FC=0x{functionCode:X2} döndürdü", _slaveId);
                        }
                    }
                    else
                    {
                        functionCode = response[7]; // TCP'de 8. byte FC
                        success = true;
                        if ((functionCode & 0x80) == 0x80)
                            _logger.LogInfo($"Health check: Cihaz exception FC=0x{functionCode:X2} döndürdü", _slaveId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                _logger.LogError(_slaveId, 0, "Health check timeout", null);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(_slaveId, 0, $"Health check hatası: {ex.Message}", ex.ToString());
            }

            // Durumu güncelle
            double durationMs = stopwatch.Elapsed.TotalMilliseconds;
            UpdateState(success ? ConnectionState.Connected : ConnectionState.Error);
            
            if (success)
            {
                _consecutiveFailures = 0;
                lock (_lock)
                {
                    _lastSuccessfulCheckTime = DateTime.Now;
                }
            }
            else
            {
                _consecutiveFailures++;
            }

            lock (_lock)
            {
                _lastCheckTime = DateTime.Now;
            }

            // Log yaz
            _logger.LogHealthCheck(_slaveId, success, durationMs);

            // Eğer çok fazla ardışık hata varsa bağlantıyı kes
            if (_consecutiveFailures >= _maxFailedAttempts && _client.IsConnected)
            {
                _logger.LogInfo($"Art arda {_maxFailedAttempts} başarısız health check. Bağlantı kesiliyor...", _slaveId);
                try
                {
                    await _client.DisconnectAsync();
                    UpdateState(ConnectionState.Disconnected);
                }
                catch (Exception ex)
                {
                    _logger.LogError(_slaveId, 0, $"Bağlantı kesme hatası: {ex.Message}", null);
                }
            }
        }

        /// <summary>
        /// Read Holding Registers (FC=03) probe paketi oluşturur
        /// </summary>
        private static byte[] BuildReadHoldingRegistersProbe(byte slaveId, ushort startAddress, ushort quantity)
        {
            // Modbus TCP: Transaction ID (2) + Protocol ID (2) + Length (2) + Unit ID (1) + FC (1) + Addr (2) + Qty (2)
            return new byte[]
            {
                0x00, 0x01,             // Transaction ID
                0x00, 0x00,             // Protocol ID (Modbus)
                0x00, 0x06,             // Length (Unit ID + FC + Addr + Qty = 6)
                slaveId,                // Unit ID
                0x03,                   // Function Code (Read Holding Registers)
                (byte)(startAddress >> 8),
                (byte)(startAddress & 0xFF),
                (byte)(quantity >> 8),
                (byte)(quantity & 0xFF)
            };
        }

        /// <summary>
        /// MİMARİ-03 düzeltmesi: RTU formatında probe paketi oluşturur (CRC dahil).
        /// Modbus RTU: [SlaveId][FC=0x03][Addr Hi][Lo][Qty Hi][Lo][CRC Lo][CRC Hi]
        /// </summary>
        private static byte[] BuildReadHoldingRegistersProbeRtu(byte slaveId, ushort startAddress, ushort quantity)
        {
            byte[] frame = new byte[]
            {
                slaveId,
                0x03,                   // Function Code (Read Holding Registers)
                (byte)(startAddress >> 8),
                (byte)(startAddress & 0xFF),
                (byte)(quantity >> 8),
                (byte)(quantity & 0xFF)
            };

            // CRC hesapla ve ekle
            ushort crc = CRC16.Calculate(frame);
            byte[] packet = new byte[frame.Length + 2];
            Array.Copy(frame, packet, frame.Length);
            packet[frame.Length] = (byte)(crc & 0xFF);       // CRC Low
            packet[frame.Length + 1] = (byte)(crc >> 8);     // CRC High

            return packet;
        }

        /// <summary>
        /// Bağlantı durumunu günceller
        /// </summary>
        private void UpdateState(ConnectionState newState)
        {
            if (CurrentState != newState)
            {
                CurrentState = newState;
                _logger.LogInfo($"Bağlantı durumu değişti: {newState}", _slaveId);
            }
        }

        /// <summary>
        /// Servisi temizler
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            // Önce monitoring'i durdur
            if (IsMonitoring)
            {
                StopAsync().Wait(TimeSpan.FromSeconds(2));
            }
        }
    }
}
