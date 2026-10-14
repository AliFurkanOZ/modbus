using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Modbus.Core.Interfaces;

namespace Modbus.Communication.Services
{
    /// <summary>
    /// Modbus Client bağlantı havuzu
    /// Aynı anda birden fazla cihazla iletişim için client yönetimi sağlar
    /// </summary>
    public class ModbusClientPool : IDisposable
    {
        private readonly ConcurrentDictionary<string, PooledClient> _clients;
        private readonly Func<string, IModbusClient> _clientFactory;
        private readonly TimeSpan _idleTimeout;
        private readonly int _maxPoolSize;
        private readonly Timer _cleanupTimer;
        private bool _disposed;
        private int _totalCreated;
        private int _totalBorrowed;
        private int _totalReturned;

        /// <summary>
        /// Havuzda bulunan toplam client sayısı
        /// </summary>
        public int TotalCount => _clients.Count;

        /// <summary>
        /// Kullanımda olan (borrowed) client sayısı
        /// </summary>
        public int InUseCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _clients)
                {
                    if (kvp.Value.IsBorrowed) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Boşta bekleyen client sayısı
        /// </summary>
        public int IdleCount => TotalCount - InUseCount;

        /// <summary>
        /// Şu ana kadar oluşturulan toplam client sayısı
        /// </summary>
        public int TotalCreated => _totalCreated;

        /// <summary>
        /// Şu ana kadar kaç kez client ödünç alındı
        /// </summary>
        public int TotalBorrowed => _totalBorrowed;

        /// <summary>
        /// Şu ana kadar kaç kez client geri verildi
        /// </summary>
        public int TotalReturned => _totalReturned;

        /// <summary>
        /// Client havuzu oluşturur
        /// </summary>
        /// <param name="clientFactory">Client oluşturma factory (connectionKey -> IModbusClient)</param>
        /// <param name="idleTimeout">Boştaki client'ın havuzda tutulma süresi (varsayılan: 5 dakika)</param>
        /// <param name="maxPoolSize">Maksimum havuz boyutu (varsayılan: 10)</param>
        public ModbusClientPool(
            Func<string, IModbusClient> clientFactory,
            TimeSpan? idleTimeout = null,
            int maxPoolSize = 10)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _idleTimeout = idleTimeout ?? TimeSpan.FromMinutes(5);
            _maxPoolSize = maxPoolSize;
            _clients = new ConcurrentDictionary<string, PooledClient>();
            
            // Periyodik olarak kullanılmayan client'ları temizle
            _cleanupTimer = new Timer(CleanupIdleClients, null, _idleTimeout, _idleTimeout);
        }

        /// <summary>
        /// Havuzdan bir client ödünç alır
        /// </summary>
        /// <param name="connectionKey">Bağlantı anahtarı (IP:Port veya SerialPort adı)</param>
        /// <returns>PooledClient wrapper</returns>
        public async Task<PooledClient> BorrowAsync(string connectionKey)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ModbusClientPool));

            // Mevcut client var mı kontrol et
            if (_clients.TryGetValue(connectionKey, out var pooledClient))
            {
                if (!pooledClient.IsBorrowed && pooledClient.Client.IsConnected)
                {
                    // Var ve kullanılabilir
                    pooledClient.IsBorrowed = true;
                    pooledClient.LastUsedTime = DateTime.Now;
                    Interlocked.Increment(ref _totalBorrowed);
                    return pooledClient;
                }
                else if (!pooledClient.Client.IsConnected)
                {
                    // Bağlantı kopmuş, yeniden bağlan
                    try
                    {
                        await pooledClient.Client.ConnectAsync();
                        pooledClient.IsBorrowed = true;
                        pooledClient.LastUsedTime = DateTime.Now;
                        Interlocked.Increment(ref _totalBorrowed);
                        return pooledClient;
                    }
                    catch
                    {
                        // Bağlanamadı, client'ı havuzdan çıkar
                        _clients.TryRemove(connectionKey, out _);
                        pooledClient.Dispose();
                    }
                }
            }

            // Yeni client oluştur
            if (_clients.Count >= _maxPoolSize)
            {
                // Havuz dolu, en eski boş client'ı bul ve kaldır
                var oldestIdle = GetOldestIdleClient();
                if (oldestIdle.HasValue)
                {
                    _clients.TryRemove(oldestIdle.Value.Key, out _);
                    oldestIdle.Value.Value.Dispose();
                }
            }

            var newClient = _clientFactory(connectionKey);
            await newClient.ConnectAsync();
            
            var newPooledClient = new PooledClient(newClient, connectionKey);
            newPooledClient.IsBorrowed = true;
            newPooledClient.LastUsedTime = DateTime.Now;
            
            _clients[connectionKey] = newPooledClient;
            Interlocked.Increment(ref _totalCreated);
            Interlocked.Increment(ref _totalBorrowed);
            
            return newPooledClient;
        }

        /// <summary>
        /// Client'ı havuza geri verir
        /// </summary>
        /// <param name="pooledClient">Geri verilecek client</param>
        public void Return(PooledClient pooledClient)
        {
            if (pooledClient == null || _disposed)
                return;

            if (_clients.TryGetValue(pooledClient.ConnectionKey, out var existing))
            {
                if (existing == pooledClient)
                {
                    pooledClient.IsBorrowed = false;
                    pooledClient.LastUsedTime = DateTime.Now;
                    Interlocked.Increment(ref _totalReturned);
                }
            }
        }

        /// <summary>
        /// Belirli bir bağlantıyı havuzdan tamamen kaldırır
        /// </summary>
        public async Task RemoveAsync(string connectionKey)
        {
            if (_clients.TryRemove(connectionKey, out var pooledClient))
            {
                try
                {
                    await pooledClient.Client.DisconnectAsync();
                }
                catch
                {
                    // Disconnect hatası loglanabilir ama yutulur
                }
                pooledClient.Dispose();
            }
        }

        /// <summary>
        /// Tüm bağlantıları havuzdan kaldırır
        /// </summary>
        public async Task ClearAsync()
        {
            foreach (var kvp in _clients)
            {
                try
                {
                    await kvp.Value.Client.DisconnectAsync();
                }
                catch
                {
                    // Ignore disconnect errors
                }
                kvp.Value.Dispose();
            }
            _clients.Clear();
        }

        /// <summary>
        /// En eski boş client'ı bulur
        /// </summary>
        private KeyValuePair<string, PooledClient>? GetOldestIdleClient()
        {
            DateTime oldestTime = DateTime.MaxValue;
            KeyValuePair<string, PooledClient>? oldest = null;

            foreach (var kvp in _clients)
            {
                if (!kvp.Value.IsBorrowed && kvp.Value.LastUsedTime < oldestTime)
                {
                    oldestTime = kvp.Value.LastUsedTime;
                    oldest = kvp;
                }
            }

            return oldest;
        }

        /// <summary>
        /// Boşta bekleyen ve timeout'a ulaşan client'ları temizler
        /// </summary>
        private async void CleanupIdleClients(object? state)
        {
            if (_disposed)
                return;

            var now = DateTime.Now;
            foreach (var kvp in _clients)
            {
                if (!kvp.Value.IsBorrowed && 
                    (now - kvp.Value.LastUsedTime) > _idleTimeout)
                {
                    if (_clients.TryRemove(kvp.Key, out var removed))
                    {
                        try
                        {
                            await removed.Client.DisconnectAsync();
                        }
                        catch
                        {
                            // Ignore
                        }
                        removed.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Havuzu temizler ve tüm client'ları kapatır
        /// </summary>
        public async Task DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cleanupTimer.Dispose();
            
            await ClearAsync();
        }

        /// <summary>
        /// Havuzu temizler
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().Wait(TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Havuzdaki bir client'ı temsil eder
        /// </summary>
        public class PooledClient : IDisposable
        {
            private bool _disposed;

            public IModbusClient Client { get; }
            public string ConnectionKey { get; }
            public bool IsBorrowed { get; set; }
            public DateTime LastUsedTime { get; set; }

            public PooledClient(IModbusClient client, string connectionKey)
            {
                Client = client ?? throw new ArgumentNullException(nameof(client));
                ConnectionKey = connectionKey ?? throw new ArgumentNullException(nameof(connectionKey));
                LastUsedTime = DateTime.Now;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                
                if (Client is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
