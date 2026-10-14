using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Modbus.Communication.Common;

namespace Modbus.Communication.Services
{
    /// <summary>
    /// Thread-safe trafik loglama servisi
    /// Channel<T> kullanarak yüksek performanslı, thread-safe log yazar
    /// </summary>
    public class TrafficLogger : IDisposable
    {
        private readonly Channel<TrafficLogEntry> _channel;
        private readonly ChannelReader<TrafficLogEntry> _reader;
        private readonly ChannelWriter<TrafficLogEntry> _writer;
        private readonly CancellationTokenSource _cts;
        private readonly Task _processingTask;
        private readonly Action<TrafficLogEntry>? _onLogEntry;
        private readonly int _maxQueueSize;
        private bool _disposed;
        private int _droppedCount;

        /// <summary>
        /// Log kuyruğundaki eleman sayısı
        /// </summary>
        public int QueueCount => _reader.Count;

        /// <summary>
        /// Kuyruk doluluğu nedeniyle atılan log sayısı
        /// </summary>
        public int DroppedCount => _droppedCount;

        /// <summary>
        /// Trafik logger oluşturur
        /// </summary>
        /// <param name="onLogEntry">Her log entry için çağrılacak callback (UI güncellemesi vb.)</param>
        /// <param name="maxQueueSize">Maksimum kuyruk boyutu (varsayılan: 1000)</param>
        public TrafficLogger(Action<TrafficLogEntry>? onLogEntry = null, int maxQueueSize = 1000)
        {
            _onLogEntry = onLogEntry;
            _maxQueueSize = maxQueueSize;
            
            // Bounded channel oluştur - dolu olduğunda yazmayı beklet
            _channel = Channel.CreateBounded<TrafficLogEntry>(new BoundedChannelOptions(maxQueueSize)
            {
                FullMode = BoundedChannelFullMode.DropWrite, // Doluysa yeni logları at
                SingleReader = true,
                SingleWriter = false
            });
            
            _reader = _channel.Reader;
            _writer = _channel.Writer;
            _cts = new CancellationTokenSource();
            
            // Arka planda logları işleyen task
            _processingTask = ProcessLogsAsync(_cts.Token);
        }

        /// <summary>
        /// Log entry ekler
        /// </summary>
        public void Log(TrafficLogEntry entry)
        {
            if (_disposed)
                return;

            if (!_writer.TryWrite(entry))
            {
                // Kuyruk dolu, log atıldı
                Interlocked.Increment(ref _droppedCount);
            }
        }

        /// <summary>
        /// Request log'u ekler
        /// </summary>
        public void LogRequest(byte slaveId, byte functionCode, byte[] rawData)
        {
            Log(new TrafficLogEntry
            {
                Type = TrafficLogType.Request,
                SlaveId = slaveId,
                FunctionCode = functionCode,
                RawData = rawData,
                IsSuccess = true
            });
        }

        /// <summary>
        /// Response log'u ekler
        /// </summary>
        public void LogResponse(byte slaveId, byte functionCode, byte[] rawData, double? durationMs = null)
        {
            Log(new TrafficLogEntry
            {
                Type = TrafficLogType.Response,
                SlaveId = slaveId,
                FunctionCode = functionCode,
                RawData = rawData,
                DurationMs = durationMs,
                IsSuccess = true
            });
        }

        /// <summary>
        /// Hata log'u ekler
        /// </summary>
        public void LogError(byte slaveId, byte functionCode, string message, string? exceptionDetail = null)
        {
            Log(new TrafficLogEntry
            {
                Type = TrafficLogType.Error,
                SlaveId = slaveId,
                FunctionCode = functionCode,
                Message = message,
                ExceptionDetail = exceptionDetail,
                IsSuccess = false
            });
        }

        /// <summary>
        /// Bilgi log'u ekler
        /// </summary>
        public void LogInfo(string message, byte slaveId = 0, byte functionCode = 0)
        {
            Log(new TrafficLogEntry
            {
                Type = TrafficLogType.Info,
                SlaveId = slaveId,
                FunctionCode = functionCode,
                Message = message,
                IsSuccess = true
            });
        }

        /// <summary>
        /// Health check log'u ekler
        /// </summary>
        public void LogHealthCheck(byte slaveId, bool success, double durationMs)
        {
            Log(new TrafficLogEntry
            {
                Type = TrafficLogType.HealthCheck,
                SlaveId = slaveId,
                FunctionCode = 0,
                Message = success ? "Health check başarılı" : "Health check başarısız",
                DurationMs = durationMs,
                IsSuccess = success
            });
        }

        /// <summary>
        /// Arka planda logları işleyen task
        /// </summary>
        private async Task ProcessLogsAsync(CancellationToken token)
        {
            try
            {
                await foreach (var entry in _reader.ReadAllAsync(token))
                {
                    try
                    {
                        _onLogEntry?.Invoke(entry);
                    }
                    catch (Exception ex)
                    {
                        // Callback'de hata olursa logla ama devam et
                        Console.WriteLine($"TrafficLogger callback hatası: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal iptal durumu, loglama yapma
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TrafficLogger processing hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Tüm bekleyen logların işlenmesini bekler
        /// </summary>
        public async Task FlushAsync()
        {
            await _reader.WaitToReadAsync();
            while (_reader.Count > 0)
            {
                await Task.Delay(10);
            }
        }

        /// <summary>
        /// Servisi durdurur ve temizler
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            // Writer'ı kapat
            _writer.Complete();
            
            // İptal sinyali gönder
            _cts.Cancel();
            
            // Processing task'ın bitmesini bekle
            try
            {
                _processingTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Timeout durumunda force et
            }
            
            _cts.Dispose();
        }
    }
}
