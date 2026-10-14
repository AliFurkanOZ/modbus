using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusSuite.Core.Analysis
{
    /// <summary>
    /// Veri analizi ve istatistik hesaplama servisi.
    /// Gerçek zamanlı veri akışından trend, ortalama, min/max değerleri hesaplar.
    /// </summary>
    public class DataAnalyzer
    {
        private readonly Dictionary<string, RegisterStatistics> _statistics;
        private readonly object _lock = new();
        private readonly int _maxHistorySize;

        /// <summary>
        /// DataAnalyzer oluşturur.
        /// </summary>
        /// <param name="maxHistorySize">Her register için tutulacak maksimum geçmiş veri noktası sayısı.</param>
        public DataAnalyzer(int maxHistorySize = 1000)
        {
            _maxHistorySize = maxHistorySize;
            _statistics = new Dictionary<string, RegisterStatistics>();
        }

        /// <summary>
        /// Bir register için yeni bir veri noktası ekler ve istatistikleri günceller.
        /// </summary>
        /// <param name="registerId">Register kimliği (örn: "Holding_40001").</param>
        /// <param name="value">Okunan ham veya mühendislik değeri.</param>
        /// <param name="timestamp">Verinin zaman damgası (varsayılan: DateTime.Now).</param>
        public void AddDataPoint(string registerId, double value, DateTime? timestamp = null)
        {
            var ts = timestamp ?? DateTime.Now;

            lock (_lock)
            {
                if (!_statistics.ContainsKey(registerId))
                {
                    _statistics[registerId] = new RegisterStatistics(_maxHistorySize);
                }

                _statistics[registerId].Add(value, ts);
            }
        }

        /// <summary>
        /// Belirtilen register için mevcut istatistikleri döndürür.
        /// </summary>
        public RegisterStatistics? GetStatistics(string registerId)
        {
            lock (_lock)
            {
                return _statistics.TryGetValue(registerId, out var stats) ? stats : null;
            }
        }

        /// <summary>
        /// Tüm register'ların istatistiklerini döndürür.
        /// </summary>
        public IReadOnlyDictionary<string, RegisterStatistics> GetAllStatistics()
        {
            lock (_lock)
            {
                return new Dictionary<string, RegisterStatistics>(_statistics);
            }
        }

        /// <summary>
        /// Belirtilen register'ın istatistiklerini sıfırlar.
        /// </summary>
        public void ResetStatistics(string registerId)
        {
            lock (_lock)
            {
                if (_statistics.ContainsKey(registerId))
                {
                    _statistics[registerId] = new RegisterStatistics(_maxHistorySize);
                }
            }
        }

        /// <summary>
        /// Tüm istatistikleri sıfırlar.
        /// </summary>
        public void ResetAll()
        {
            lock (_lock)
            {
                _statistics.Clear();
            }
        }

        /// <summary>
        /// Birden fazla register için toplu veri ekleme (thread-safe).
        /// </summary>
        public void AddDataPoints(IDictionary<string, double> values, DateTime? timestamp = null)
        {
            var ts = timestamp ?? DateTime.Now;

            lock (_lock)
            {
                foreach (var kvp in values)
                {
                    if (!_statistics.ContainsKey(kvp.Key))
                    {
                        _statistics[kvp.Key] = new RegisterStatistics(_maxHistorySize);
                    }

                    _statistics[kvp.Key].Add(kvp.Value, ts);
                }
            }
        }
    }

    /// <summary>
    /// Tek bir register için istatistiksel verileri tutar.
    /// </summary>
    public class RegisterStatistics
    {
        private readonly LinkedList<double> _history;
        private readonly int _maxSize;
        private bool _isInitialized;

        public double Min { get; private set; }
        public double Max { get; private set; }
        public double Sum { get; private set; }
        public double Average => Count > 0 ? Sum / Count : 0;
        public int Count => _history.Count;
        public DateTime? FirstTimestamp { get; private set; }
        public DateTime? LastTimestamp { get; private set; }
        public double? StdDeviation { get; private set; }

        internal RegisterStatistics(int maxSize)
        {
            _maxSize = maxSize;
            _history = new LinkedList<double>();
            _isInitialized = false;
            Min = double.MaxValue;
            Max = double.MinValue;
            Sum = 0;
        }

        internal void Add(double value, DateTime timestamp)
        {
            // İlk veri noktası
            if (!_isInitialized)
            {
                Min = value;
                Max = value;
                Sum = value;
                FirstTimestamp = timestamp;
                _isInitialized = true;
            }
            else
            {
                if (value < Min) Min = value;
                if (value > Max) Max = value;
                Sum += value;
            }

            LastTimestamp = timestamp;
            _history.AddLast(value);

            // Maksimum boyutu aşarsa en eski veriyi çıkar
            if (_history.Count > _maxSize)
            {
                var oldest = _history.First!.Value;
                Sum -= oldest;
                _history.RemoveFirst();

                // Min/Max yeniden hesaplama gerekebilir (basit yaklaşım: tüm listeyi tara)
                RecalculateMinMax();
            }

            // Standart sapmayı hesapla
            CalculateStdDeviation();
        }

        private void RecalculateMinMax()
        {
            if (_history.Count == 0)
            {
                Min = 0;
                Max = 0;
                return;
            }

            Min = _history.Min();
            Max = _history.Max();
        }

        private void CalculateStdDeviation()
        {
            if (_history.Count < 2)
            {
                StdDeviation = null;
                return;
            }

            var avg = Average;
            var sumOfSquares = _history.Sum(x => Math.Pow(x - avg, 2));
            StdDeviation = Math.Sqrt(sumOfSquares / (_history.Count - 1));
        }

        /// <summary>
        /// Geçmiş veri noktalarını döndürür.
        /// </summary>
        public IEnumerable<double> GetHistory() => _history.AsEnumerable();

        /// <summary>
        /// Son N veri noktasını döndürür.
        /// </summary>
        public IEnumerable<double> GetRecentHistory(int count)
        {
            return _history.TakeLast(count);
        }
    }
}
