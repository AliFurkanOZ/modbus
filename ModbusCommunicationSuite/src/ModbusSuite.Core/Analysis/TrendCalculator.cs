using System;
using System.Collections.Generic;
using System.Linq;

namespace ModbusSuite.Core.Analysis
{
    /// <summary>
    /// Trend hesaplama ve değişim analizi servisi.
    /// Veri noktaları arasındaki eğilimleri (artış, azalış, sabit) tespit eder.
    /// </summary>
    public class TrendCalculator
    {
        private readonly Dictionary<string, TrendData> _trendData;
        private readonly object _lock = new();

        /// <summary>
        /// TrendCalculator oluşturur.
        /// </summary>
        public TrendCalculator()
        {
            _trendData = new Dictionary<string, TrendData>();
        }

        /// <summary>
        /// Bir register için yeni veri noktası ekler ve trend hesaplar.
        /// </summary>
        public void AddDataPoint(string registerId, double value, DateTime timestamp)
        {
            lock (_lock)
            {
                if (!_trendData.ContainsKey(registerId))
                {
                    _trendData[registerId] = new TrendData();
                }

                _trendData[registerId].Add(value, timestamp);
            }
        }

        /// <summary>
        /// Belirtilen register için mevcut trend durumunu döndürür.
        /// </summary>
        public TrendInfo? GetTrend(string registerId)
        {
            lock (_lock)
            {
                return _trendData.TryGetValue(registerId, out var data) ? data.GetTrendInfo() : null;
            }
        }

        /// <summary>
        /// Tüm register'ların trend bilgilerini döndürür.
        /// </summary>
        public IReadOnlyDictionary<string, TrendInfo> GetAllTrends()
        {
            lock (_lock)
            {
                var result = new Dictionary<string, TrendInfo>();
                foreach (var kvp in _trendData)
                {
                    result[kvp.Key] = kvp.Value.GetTrendInfo();
                }
                return result;
            }
        }

        /// <summary>
        /// Belirtilen register'ın trend verisini sıfırlar.
        /// </summary>
        public void ResetTrend(string registerId)
        {
            lock (_lock)
            {
                if (_trendData.ContainsKey(registerId))
                {
                    _trendData[registerId] = new TrendData();
                }
            }
        }

        /// <summary>
        /// Tüm trend verilerini sıfırlar.
        /// </summary>
        public void ResetAll()
        {
            lock (_lock)
            {
                _trendData.Clear();
            }
        }

        /// <summary>
        /// Toplu veri ekleme.
        /// </summary>
        public void AddDataPoints(IDictionary<string, (double Value, DateTime Timestamp)> dataPoints)
        {
            lock (_lock)
            {
                foreach (var kvp in dataPoints)
                {
                    if (!_trendData.ContainsKey(kvp.Key))
                    {
                        _trendData[kvp.Key] = new TrendData();
                    }

                    _trendData[kvp.Key].Add(kvp.Value.Value, kvp.Value.Timestamp);
                }
            }
        }
    }

    /// <summary>
    /// Tek bir register için trend verilerini tutar.
    /// </summary>
    internal class TrendData
    {
        private double? _previousValue;
        private DateTime? _previousTimestamp;
        private double? _firstValue;
        private DateTime? _firstTimestamp;
        private int _consecutiveIncreases;
        private int _consecutiveDecreases;
        private double _totalChange;

        public void Add(double value, DateTime timestamp)
        {
            if (_firstValue == null)
            {
                _firstValue = value;
                _firstTimestamp = timestamp;
            }

            if (_previousValue.HasValue)
            {
                var diff = value - _previousValue.Value;
                _totalChange += diff;

                if (diff > 0)
                {
                    _consecutiveIncreases++;
                    _consecutiveDecreases = 0;
                }
                else if (diff < 0)
                {
                    _consecutiveDecreases++;
                    _consecutiveIncreases = 0;
                }
                else
                {
                    // Sabit değer
                    _consecutiveIncreases = 0;
                    _consecutiveDecreases = 0;
                }
            }

            _previousValue = value;
            _previousTimestamp = timestamp;
        }

        public TrendInfo GetTrendInfo()
        {
            if (_previousValue == null || _firstValue == null)
            {
                return new TrendInfo
                {
                    Direction = TrendDirection.Unknown,
                    Confidence = 0,
                    RateOfChange = 0,
                    TotalChange = 0,
                    ConsecutiveMoves = 0
                };
            }

            var totalChange = _previousValue.Value - _firstValue.Value;
            var timeSpan = _previousTimestamp!.Value - _firstTimestamp!.Value;
            var rateOfChange = timeSpan.TotalSeconds > 0 ? totalChange / timeSpan.TotalSeconds : 0;

            TrendDirection direction;
            int consecutiveMoves;
            double confidence;

            if (_consecutiveIncreases > _consecutiveDecreases)
            {
                direction = TrendDirection.Increasing;
                consecutiveMoves = _consecutiveIncreases;
                confidence = _consecutiveIncreases > 0 
                    ? (double)_consecutiveIncreases / (_consecutiveIncreases + _consecutiveDecreases + 1) 
                    : 0;
            }
            else if (_consecutiveDecreases > _consecutiveIncreases)
            {
                direction = TrendDirection.Decreasing;
                consecutiveMoves = _consecutiveDecreases;
                confidence = _consecutiveDecreases > 0 
                    ? (double)_consecutiveDecreases / (_consecutiveIncreases + _consecutiveDecreases + 1) 
                    : 0;
            }
            else
            {
                direction = TrendDirection.Stable;
                consecutiveMoves = 0;
                confidence = 1.0; // Tam stabilite
            }

            return new TrendInfo
            {
                Direction = direction,
                Confidence = Math.Min(1.0, confidence),
                RateOfChange = rateOfChange,
                TotalChange = totalChange,
                ConsecutiveMoves = consecutiveMoves,
                CurrentValue = _previousValue.Value,
                StartTime = _firstTimestamp!.Value,
                LastUpdateTime = _previousTimestamp!.Value
            };
        }
    }

    /// <summary>
    /// Trend yönünü belirtir.
    /// </summary>
    public enum TrendDirection
    {
        Unknown,
        Increasing,
        Decreasing,
        Stable
    }

    /// <summary>
    /// Trend bilgilerini içeren sınıf.
    /// </summary>
    public class TrendInfo
    {
        public TrendDirection Direction { get; init; }
        public double Confidence { get; init; } // 0-1 arası
        public double RateOfChange { get; init; } // Birim/saniye
        public double TotalChange { get; init; }
        public int ConsecutiveMoves { get; init; }
        public double? CurrentValue { get; init; }
        public DateTime? StartTime { get; init; }
        public DateTime? LastUpdateTime { get; init; }

        /// <summary>
        /// İnsan tarafından okunabilir trend açıklaması.
        /// </summary>
        public override string ToString()
        {
            return $"Trend: {Direction}, Güven: {Confidence:P1}, Değişim Hızı: {RateOfChange:F4}/s";
        }
    }
}
