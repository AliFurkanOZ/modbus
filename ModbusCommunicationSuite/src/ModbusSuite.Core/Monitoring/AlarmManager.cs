using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusSuite.Core.Monitoring
{
    /// <summary>
    /// Alarm yönetimi ve tetikleme servisi.
    /// Register değerlerini önceden tanımlanmış limitlerle karşılaştırır ve alarm üretir.
    /// </summary>
    public class AlarmManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, AlarmDefinition> _definitions;
        private readonly ConcurrentQueue<AlarmEvent> _activeAlarms;
        private readonly ConcurrentQueue<AlarmEvent> _alarmHistory;
        private readonly object _lock = new();
        private readonly CancellationTokenSource _cts;
        private readonly Task _cleanupTask;
        private bool _disposed;

        /// <summary>
        /// Maksimum alarm geçmişi boyutu.
        /// </summary>
        public int MaxHistorySize { get; set; } = 1000;

        /// <summary>
        /// Aktif alarm sayısı.
        /// </summary>
        public int ActiveAlarmCount => _activeAlarms.Count;

        /// <summary>
        /// Toplam alarm sayısı (geçmiş dahil).
        /// </summary>
        public int TotalAlarmCount => _alarmHistory.Count;

        /// <summary>
        /// Alarm oluşturulduğunda tetiklenen event.
        /// </summary>
        public event EventHandler<AlarmEvent>? AlarmRaised;

        /// <summary>
        /// Alarm temizlendiğinde tetiklenen event.
        /// </summary>
        public event EventHandler<AlarmEvent>? AlarmCleared;

        /// <summary>
        /// AlarmManager oluşturur.
        /// </summary>
        public AlarmManager()
        {
            _definitions = new ConcurrentDictionary<string, AlarmDefinition>();
            _activeAlarms = new ConcurrentQueue<AlarmEvent>();
            _alarmHistory = new ConcurrentQueue<AlarmEvent>();
            _cts = new CancellationTokenSource();
            
            // Periyodik geçmiş temizleme
            _cleanupTask = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), _cts.Token);
                        CleanupHistory();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
        }

        /// <summary>
        /// Bir register için alarm tanımı ekler veya günceller.
        /// </summary>
        public void AddOrUpdateDefinition(string registerId, AlarmDefinition definition)
        {
            _definitions[registerId] = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        /// <summary>
        /// Bir register'ın alarm tanımını kaldırır.
        /// </summary>
        public void RemoveDefinition(string registerId)
        {
            _definitions.TryRemove(registerId, out _);
        }

        /// <summary>
        /// Tüm alarm tanımlarını döndürür.
        /// </summary>
        public IReadOnlyDictionary<string, AlarmDefinition> GetAllDefinitions()
        {
            return new Dictionary<string, AlarmDefinition>(_definitions);
        }

        /// <summary>
        /// Bir register için değer kontrolü yapar ve gerekli alarmları üretir.
        /// </summary>
        public void CheckValue(string registerId, double value, DateTime? timestamp = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AlarmManager));

            var ts = timestamp ?? DateTime.Now;

            if (!_definitions.TryGetValue(registerId, out var definition))
            {
                return; // Tanım yoksa kontrol etme
            }

            AlarmSeverity? severity = null;
            string? message = null;

            // Yüksek-Yüksek kontrolü
            if (definition.HighHighLimit.HasValue && value >= definition.HighHighLimit.Value)
            {
                severity = AlarmSeverity.Critical;
                message = $"Değer ({value:F2}), Yüksek-Yüksek limiti ({definition.HighHighLimit.Value:F2}) aştı.";
            }
            // Yüksek kontrolü
            else if (definition.HighLimit.HasValue && value >= definition.HighLimit.Value)
            {
                severity = AlarmSeverity.High;
                message = $"Değer ({value:F2}), Yüksek limiti ({definition.HighLimit.Value:F2}) aştı.";
            }
            // Düşük-Düşük kontrolü
            else if (definition.LowLowLimit.HasValue && value <= definition.LowLowLimit.Value)
            {
                severity = AlarmSeverity.Critical;
                message = $"Değer ({value:F2}), Düşük-Düşük limiti ({definition.LowLowLimit.Value:F2}) altına düştü.";
            }
            // Düşük kontrolü
            else if (definition.LowLimit.HasValue && value <= definition.LowLimit.Value)
            {
                severity = AlarmSeverity.Low;
                message = $"Değer ({value:F2}), Düşük limiti ({definition.LowLimit.Value:F2}) altına düştü.";
            }

            if (severity.HasValue)
            {
                RaiseAlarm(registerId, severity.Value, message!, value, ts);
            }
            else
            {
                // Değer normal sınırlara döndüyse aktif alarmları temizle
                ClearAlarm(registerId, ts);
            }
        }

        /// <summary>
        /// Toplu değer kontrolü.
        /// </summary>
        public void CheckValues(IDictionary<string, double> values, DateTime? timestamp = null)
        {
            foreach (var kvp in values)
            {
                CheckValue(kvp.Key, kvp.Value, timestamp);
            }
        }

        private void RaiseAlarm(string registerId, AlarmSeverity severity, string message, double value, DateTime timestamp)
        {
            // Aynı register için zaten aktif bir alarm var mı kontrol et
            var existingAlarm = _activeAlarms.FirstOrDefault(a => a.RegisterId == registerId && !a.IsCleared);
            
            if (existingAlarm != null)
            {
                // Aynı seviyede alarm zaten var, güncelle
                existingAlarm.LastOccurrence = timestamp;
                existingAlarm.OccurrenceCount++;
                return;
            }

            var alarm = new AlarmEvent
            {
                Id = Guid.NewGuid(),
                RegisterId = registerId,
                Severity = severity,
                Message = message,
                Value = value,
                Timestamp = timestamp,
                IsCleared = false,
                OccurrenceCount = 1,
                LastOccurrence = timestamp
            };

            _activeAlarms.Enqueue(alarm);
            _alarmHistory.Enqueue(alarm);

            AlarmRaised?.Invoke(this, alarm);

            // Geçmiş boyutunu kontrol et
            if (_alarmHistory.Count > MaxHistorySize)
            {
                _alarmHistory.TryDequeue(out _);
            }
        }

        private void ClearAlarm(string registerId, DateTime timestamp)
        {
            var activeAlarm = _activeAlarms.FirstOrDefault(a => a.RegisterId == registerId && !a.IsCleared);
            
            if (activeAlarm == null)
            {
                return; // Zaten temizlenmiş veya hiç alarm yok
            }

            activeAlarm.IsCleared = true;
            activeAlarm.ClearTimestamp = timestamp;

            AlarmCleared?.Invoke(this, activeAlarm);
        }

        /// <summary>
        /// Aktif alarmları döndürür.
        /// </summary>
        public IEnumerable<AlarmEvent> GetActiveAlarms()
        {
            return _activeAlarms.Where(a => !a.IsCleared);
        }

        /// <summary>
        /// Alarm geçmişini döndürür.
        /// </summary>
        public IEnumerable<AlarmEvent> GetAlarmHistory(int? count = null)
        {
            return count.HasValue 
                ? _alarmHistory.TakeLast(count.Value) 
                : _alarmHistory.AsEnumerable();
        }

        /// <summary>
        /// Belirtilen register'ın aktif alarmını manuel olarak temizler.
        /// </summary>
        public void AcknowledgeAlarm(string registerId)
        {
            var alarm = _activeAlarms.FirstOrDefault(a => a.RegisterId == registerId && !a.IsCleared);
            if (alarm != null)
            {
                alarm.IsCleared = true;
                alarm.ClearTimestamp = DateTime.Now;
                AlarmCleared?.Invoke(this, alarm);
            }
        }

        /// <summary>
        /// Tüm aktif alarmları temizler.
        /// </summary>
        public void AcknowledgeAllAlarms()
        {
            foreach (var alarm in _activeAlarms.Where(a => !a.IsCleared))
            {
                alarm.IsCleared = true;
                alarm.ClearTimestamp = DateTime.Now;
                AlarmCleared?.Invoke(this, alarm);
            }
        }

        /// <summary>
        /// Alarm geçmişini temizler.
        /// </summary>
        private void CleanupHistory()
        {
            while (_alarmHistory.Count > MaxHistorySize)
            {
                _alarmHistory.TryDequeue(out _);
            }
        }

        /// <summary>
        /// İstatistikleri sıfırlar.
        /// </summary>
        public void Reset()
        {
            _activeAlarms.Clear();
            _alarmHistory.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cts.Cancel();
            try
            {
                _cleanupTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Timeout durumunda ignore et
            }

            _cts.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Alarm tanımı.
    /// </summary>
    public class AlarmDefinition
    {
        /// <summary>
        /// Yüksek-Yüksek limit (Kritik).
        /// </summary>
        public double? HighHighLimit { get; set; }

        /// <summary>
        /// Yüksek limit (Uyarı).
        /// </summary>
        public double? HighLimit { get; set; }

        /// <summary>
        /// Düşük limit (Uyarı).
        /// </summary>
        public double? LowLimit { get; set; }

        /// <summary>
        /// Düşük-Düşük limit (Kritik).
        /// </summary>
        public double? LowLowLimit { get; set; }

        /// <summary>
        /// Alarm gecikmesi (saniye) - debounce için.
        /// </summary>
        public int DelaySeconds { get; set; } = 0;

        /// <summary>
        /// Özel mesaj şablonu.
        /// </summary>
        public string? CustomMessage { get; set; }

        /// <summary>
        /// Enable/Disable durumu.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Alarm ciddiyet seviyesi.
    /// </summary>
    public enum AlarmSeverity
    {
        Info = 0,
        Low = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Alarm olayı.
    /// </summary>
    public class AlarmEvent
    {
        public Guid Id { get; init; }
        public string RegisterId { get; init; } = string.Empty;
        public AlarmSeverity Severity { get; init; }
        public string Message { get; init; } = string.Empty;
        public double Value { get; init; }
        public DateTime Timestamp { get; init; }
        public bool IsCleared { get; set; }
        public DateTime? ClearTimestamp { get; set; }
        public int OccurrenceCount { get; set; }
        public DateTime LastOccurrence { get; set; }

        /// <summary>
        /// Alarm süresi (saniye).
        /// </summary>
        public double? DurationSeconds => IsCleared && ClearTimestamp.HasValue 
            ? (ClearTimestamp.Value - Timestamp).TotalSeconds 
            : null;

        public override string ToString()
        {
            var status = IsCleared ? "CLEARED" : "ACTIVE";
            return $"[{status}] {Severity}: {RegisterId} - {Message}";
        }
    }
}
