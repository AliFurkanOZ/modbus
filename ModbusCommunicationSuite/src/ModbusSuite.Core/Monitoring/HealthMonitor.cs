using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusSuite.Core.Monitoring
{
    /// <summary>
    /// Veri kalitesi ve bağlantı durumu izleme servisi.
    /// Communication health, data freshness ve connection status takibi yapar.
    /// </summary>
    public class HealthMonitor : IDisposable
    {
        private readonly Dictionary<string, ComponentHealth> _components;
        private readonly object _lock = new();
        private readonly CancellationTokenSource _cts;
        private readonly Task _monitorTask;
        private bool _disposed;

        /// <summary>
        /// Sağlık kontrolü periyodu (saniye).
        /// </summary>
        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Veri tazelik limiti (saniye) - bu süreden eski veri "stale" olarak işaretlenir.
        /// </summary>
        public TimeSpan FreshnessThreshold { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Genel sistem sağlığı.
        /// </summary>
        public OverallHealthStatus OverallStatus => GetOverallStatus();

        /// <summary>
        /// Bileşen sağlığı güncellendiğinde tetiklenen event.
        /// </summary>
        public event EventHandler<HealthUpdateEventArgs>? HealthUpdated;

        /// <summary>
        /// HealthMonitor oluşturur.
        /// </summary>
        public HealthMonitor()
        {
            _components = new Dictionary<string, ComponentHealth>();
            _cts = new CancellationTokenSource();

            _monitorTask = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(CheckInterval, _cts.Token);
                        PerformHealthCheck();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
        }

        /// <summary>
        /// Bir bileşen için sağlık durumu kaydeder veya günceller.
        /// </summary>
        public void RegisterComponent(string componentId, string componentType)
        {
            lock (_lock)
            {
                if (!_components.ContainsKey(componentId))
                {
                    _components[componentId] = new ComponentHealth
                    {
                        Id = componentId,
                        Type = componentType,
                        Status = HealthStatus.Unknown,
                        LastCheckTime = DateTime.Now
                    };
                }
            }
        }

        /// <summary>
        /// Bileşen sağlık durumunu günceller.
        /// </summary>
        public void UpdateComponentHealth(string componentId, HealthStatus status, string? message = null)
        {
            lock (_lock)
            {
                if (!_components.ContainsKey(componentId))
                {
                    _components[componentId] = new ComponentHealth
                    {
                        Id = componentId,
                        Type = "Unknown",
                        LastCheckTime = DateTime.Now
                    };
                }

                var component = _components[componentId];
                var oldStatus = component.Status;
                
                component.Status = status;
                component.LastCheckTime = DateTime.Now;
                component.Message = message;
                component.LastErrorTime = status == HealthStatus.Error ? DateTime.Now : component.LastErrorTime;

                if (oldStatus != status)
                {
                    HealthUpdated?.Invoke(this, new HealthUpdateEventArgs
                    {
                        ComponentId = componentId,
                        OldStatus = oldStatus,
                        NewStatus = status,
                        Timestamp = DateTime.Now
                    });
                }
            }
        }

        /// <summary>
        /// Veri noktası alındığında çağrılır - tazelik bilgisi güncellenir.
        /// </summary>
        public void RecordDataReceived(string componentId)
        {
            lock (_lock)
            {
                if (_components.TryGetValue(componentId, out var component))
                {
                    component.LastDataTime = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// Belirtilen bileşenin sağlık durumunu döndürür.
        /// </summary>
        public ComponentHealth? GetComponentHealth(string componentId)
        {
            lock (_lock)
            {
                return _components.TryGetValue(componentId, out var health) ? health : null;
            }
        }

        /// <summary>
        /// Tüm bileşenlerin sağlık durumlarını döndürür.
        /// </summary>
        public IReadOnlyDictionary<string, ComponentHealth> GetAllComponents()
        {
            lock (_lock)
            {
                return new Dictionary<string, ComponentHealth>(_components);
            }
        }

        /// <summary>
        /// Sağlıklı bileşenleri döndürür.
        /// </summary>
        public IEnumerable<ComponentHealth> GetHealthyComponents()
        {
            lock (_lock)
            {
                return _components.Values.Where(c => c.Status == HealthStatus.Healthy);
            }
        }

        /// <summary>
        /// Hatalı bileşenleri döndürür.
        /// </summary>
        public IEnumerable<ComponentHealth> GetUnhealthyComponents()
        {
            lock (_lock)
            {
                return _components.Values.Where(c => c.Status != HealthStatus.Healthy && c.Status != HealthStatus.Unknown);
            }
        }

        /// <summary>
        /// Periyodik sağlık kontrolü yapar.
        /// </summary>
        private void PerformHealthCheck()
        {
            var now = DateTime.Now;

            lock (_lock)
            {
                foreach (var component in _components.Values)
                {
                    // Veri tazelik kontrolü
                    if (component.LastDataTime.HasValue)
                    {
                        var timeSinceLastData = now - component.LastDataTime.Value;
                        
                        if (timeSinceLastData > FreshnessThreshold)
                        {
                            if (component.Status != HealthStatus.Degraded)
                            {
                                component.Status = HealthStatus.Degraded;
                                component.Message = $"Veri taze değil. Son veri: {timeSinceLastData.TotalSeconds:F1}s önce";
                                
                                HealthUpdated?.Invoke(this, new HealthUpdateEventArgs
                                {
                                    ComponentId = component.Id,
                                    OldStatus = HealthStatus.Healthy,
                                    NewStatus = HealthStatus.Degraded,
                                    Timestamp = now
                                });
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Genel sistem sağlığını hesaplar.
        /// </summary>
        private OverallHealthStatus GetOverallStatus()
        {
            lock (_lock)
            {
                if (_components.Count == 0)
                {
                    return new OverallHealthStatus
                    {
                        Status = HealthStatus.Unknown,
                        TotalComponents = 0,
                        HealthyCount = 0,
                        DegradedCount = 0,
                        ErrorCount = 0
                    };
                }

                var healthy = _components.Count(c => c.Value.Status == HealthStatus.Healthy);
                var degraded = _components.Count(c => c.Value.Status == HealthStatus.Degraded);
                var error = _components.Count(c => c.Value.Status == HealthStatus.Error);
                var unknown = _components.Count(c => c.Value.Status == HealthStatus.Unknown);

                HealthStatus overall;
                if (error > 0)
                    overall = HealthStatus.Error;
                else if (degraded > 0)
                    overall = HealthStatus.Degraded;
                else if (unknown == _components.Count)
                    overall = HealthStatus.Unknown;
                else
                    overall = HealthStatus.Healthy;

                return new OverallHealthStatus
                {
                    Status = overall,
                    TotalComponents = _components.Count,
                    HealthyCount = healthy,
                    DegradedCount = degraded,
                    ErrorCount = error,
                    UnknownCount = unknown,
                    LastUpdateTime = DateTime.Now
                };
            }
        }

        /// <summary>
        /// Bileşen sağlık bilgisini sıfırlar.
        /// </summary>
        public void ResetComponent(string componentId)
        {
            lock (_lock)
            {
                if (_components.ContainsKey(componentId))
                {
                    _components.Remove(componentId);
                }
            }
        }

        /// <summary>
        /// Tüm sağlık bilgilerini sıfırlar.
        /// </summary>
        public void ResetAll()
        {
            lock (_lock)
            {
                _components.Clear();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cts.Cancel();
            try
            {
                _monitorTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Timeout
            }

            _cts.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Bileşen sağlık durumu.
    /// </summary>
    public class ComponentHealth
    {
        public string Id { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public HealthStatus Status { get; set; }
        public string? Message { get; set; }
        public DateTime LastCheckTime { get; set; }
        public DateTime? LastDataTime { get; set; }
        public DateTime? LastErrorTime { get; set; }
        public int ConsecutiveErrors { get; set; }

        /// <summary>
        /// Verinin ne kadar süre önce alındığı.
        /// </summary>
        public TimeSpan? DataAge => LastDataTime.HasValue 
            ? DateTime.Now - LastDataTime.Value 
            : null;

        public override string ToString()
        {
            return $"{Id} ({Type}): {Status} - {Message ?? "OK"}";
        }
    }

    /// <summary>
    /// Sağlık durumu enum.
    /// </summary>
    public enum HealthStatus
    {
        Unknown = 0,
        Healthy = 1,
        Degraded = 2,
        Error = 3
    }

    /// <summary>
    /// Genel sağlık durumu özeti.
    /// </summary>
    public class OverallHealthStatus
    {
        public HealthStatus Status { get; init; }
        public int TotalComponents { get; init; }
        public int HealthyCount { get; init; }
        public int DegradedCount { get; init; }
        public int ErrorCount { get; init; }
        public int UnknownCount { get; init; }
        public DateTime? LastUpdateTime { get; init; }

        /// <summary>
        /// Sağlık yüzdesi.
        /// </summary>
        public double HealthPercentage => TotalComponents > 0 
            ? (double)HealthyCount / TotalComponents * 100 
            : 0;

        public override string ToString()
        {
            return $"Genel Durum: {Status}, Sağlık: {HealthPercentage:F1}%, Bileşenler: {TotalComponents}";
        }
    }

    /// <summary>
    /// Sağlık güncelleme event argümanları.
    /// </summary>
    public class HealthUpdateEventArgs : EventArgs
    {
        public string ComponentId { get; init; } = string.Empty;
        public HealthStatus OldStatus { get; init; }
        public HealthStatus NewStatus { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
