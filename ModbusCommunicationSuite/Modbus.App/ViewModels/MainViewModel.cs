using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

using Modbus.App.Commands;
using Modbus.App.Models;
using Modbus.Communication.RTU;
using Modbus.Communication.Server;
using Modbus.Communication.TCP;
using Modbus.Core.Interfaces;
using Modbus.Protocol.Builders;
using Modbus.Protocol.Functions;
using Modbus.Protocol.Packets;
using Modbus.Protocol.Parsers;

namespace Modbus.App.ViewModels
{
    /// <summary>
    /// Uygulama görünümleri arasında geçiş yapmak için kullanılır.
    /// </summary>
    public enum AppView
    {
        RoleSelection,
        Master,
        Slave
    }

    /// <summary>
    /// Ana ViewModel: Navigasyon yönetimi, bağımsız Client ve Server yönetimi,
    /// görünür register tabloları ve ham/çözümlenmiş paket takibi sağlar.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly PacketBuilder _builder = new();
        private readonly ResponseParser _parser = new();
        private readonly ModbusDataStore _dataStore = new();

        private IModbusClient? _client;
        private ModbusTcpServer? _server;
        private ushort _transactionId;

        public MainViewModel()
        {
            // Navigasyon komutları
            NavigateToMasterCommand = new RelayCommand(() => CurrentView = AppView.Master);
            NavigateToSlaveCommand = new RelayCommand(() => CurrentView = AppView.Slave);
            NavigateToRoleSelectionCommand = new RelayCommand(() => CurrentView = AppView.RoleSelection);

            // Mevcut iş komutları
            ConnectClientCommand = new RelayCommand(OnConnectClient);
            DisconnectClientCommand = new RelayCommand(OnDisconnectClient);
            StartServerCommand = new RelayCommand(OnStartServer);
            StopServerCommand = new RelayCommand(OnStopServer);
            SendCommand = new RelayCommand(OnSend);
            ClearLogCommand = new RelayCommand(() => LogEntries.Clear());

            CreateServerRegisterRows();
            _dataStore.HoldingRegisterChanged += OnHoldingRegisterChanged;

            AddLog("Uygulama hazır. Server'ı başlatın veya bir Modbus cihazına bağlanın.");
        }

        // ================ NAVİGASYON ================

        private AppView _currentView = AppView.RoleSelection;
        /// <summary>Aktif görünüm (RoleSelection, Master veya Slave).</summary>
        public AppView CurrentView
        {
            get => _currentView;
            set
            {
                if (_currentView == value) return;
                _currentView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRoleSelection));
                OnPropertyChanged(nameof(IsMasterMode));
                OnPropertyChanged(nameof(IsSlaveMode));
            }
        }

        public bool IsRoleSelection => CurrentView == AppView.RoleSelection;
        public bool IsMasterMode => CurrentView == AppView.Master;
        public bool IsSlaveMode => CurrentView == AppView.Slave;

        /// <summary>Server'ın şu an çalışıp çalışmadığını gösterir.</summary>
        private bool _isServerRunning;
        public bool IsServerRunning
        {
            get => _isServerRunning;
            set { _isServerRunning = value; OnPropertyChanged(); }
        }

        public ICommand NavigateToMasterCommand { get; }
        public ICommand NavigateToSlaveCommand { get; }
        public ICommand NavigateToRoleSelectionCommand { get; }

        // ---------------- CLIENT AYARLARI ----------------
        private int _clientProtocolIndex;
        public int ClientProtocolIndex
        {
            get => _clientProtocolIndex;
            set { _clientProtocolIndex = value; OnPropertyChanged(); }
        }

        private string _clientIpAddress = "127.0.0.1";
        public string ClientIpAddress
        {
            get => _clientIpAddress;
            set { _clientIpAddress = value; OnPropertyChanged(); }
        }

        private string _clientPort = "1502";
        public string ClientPort
        {
            get => _clientPort;
            set { _clientPort = value; OnPropertyChanged(); }
        }

        private string _clientComPort = "COM3";
        public string ClientComPort
        {
            get => _clientComPort;
            set { _clientComPort = value; OnPropertyChanged(); }
        }

        private string _clientBaudRate = "9600";
        public string ClientBaudRate
        {
            get => _clientBaudRate;
            set { _clientBaudRate = value; OnPropertyChanged(); }
        }

        private string _clientStatus = "Bağlı değil";
        public string ClientStatus
        {
            get => _clientStatus;
            set { _clientStatus = value; OnPropertyChanged(); }
        }

        // ---------------- SERVER AYARLARI ----------------
        private string _serverPort = "1502";
        public string ServerPort
        {
            get => _serverPort;
            set { _serverPort = value; OnPropertyChanged(); }
        }

        private string _serverStatus = "Durduruldu";
        public string ServerStatus
        {
            get => _serverStatus;
            set { _serverStatus = value; OnPropertyChanged(); }
        }

        // ---------------- İSTEK AYARLARI ----------------
        private string _slaveId = "1";
        public string SlaveId
        {
            get => _slaveId;
            set { _slaveId = value; OnPropertyChanged(); }
        }

        // 0=FC01, 1=FC03, 2=FC06
        private int _functionIndex = 1;
        public int FunctionIndex
        {
            get => _functionIndex;
            set { _functionIndex = value; OnPropertyChanged(); }
        }

        private string _address = "0";
        public string Address
        {
            get => _address;
            set { _address = value; OnPropertyChanged(); }
        }

        private string _quantity = "3";
        public string Quantity
        {
            get => _quantity;
            set { _quantity = value; OnPropertyChanged(); }
        }

        private string _writeValue = "0";
        public string WriteValue
        {
            get => _writeValue;
            set { _writeValue = value; OnPropertyChanged(); }
        }

        private string _lastRequestSummary = "Henüz istek gönderilmedi.";
        public string LastRequestSummary
        {
            get => _lastRequestSummary;
            set { _lastRequestSummary = value; OnPropertyChanged(); }
        }

        private string _lastResponseSummary = "Henüz cevap alınmadı.";
        public string LastResponseSummary
        {
            get => _lastResponseSummary;
            set { _lastResponseSummary = value; OnPropertyChanged(); }
        }

        public ObservableCollection<RegisterItem> ServerRegisters { get; } = new();
        public ObservableCollection<RegisterItem> ClientRegisters { get; } = new();
        public ObservableCollection<PacketFieldItem> RequestDetails { get; } = new();
        public ObservableCollection<PacketFieldItem> ResponseDetails { get; } = new();
        public ObservableCollection<string> LogEntries { get; } = new();

        public ICommand ConnectClientCommand { get; }
        public ICommand DisconnectClientCommand { get; }
        public ICommand StartServerCommand { get; }
        public ICommand StopServerCommand { get; }
        public ICommand SendCommand { get; }
        public ICommand ClearLogCommand { get; }

        private async void OnConnectClient()
        {
            try
            {
                if (_client != null)
                    await _client.DisconnectAsync();

                if (ClientProtocolIndex == 0)
                {
                    int port = ParseIntInRange(ClientPort, 1, 65535, "Client portu");
                    var settings = new TcpConnectionSettings
                    {
                        IpAddress = ClientIpAddress.Trim(),
                        Port = port,
                        Timeout = 5000
                    };

                    _client = new ModbusTcpClient(settings);
                    AddLog($"TCP client bağlanıyor: {settings.IpAddress}:{settings.Port} ...");
                }
                else
                {
                    int baudRate = ParseIntInRange(ClientBaudRate, 1, 2_000_000, "Baud rate");
                    var settings = new RtuConnectionSettings
                    {
                        PortName = ClientComPort.Trim(),
                        BaudRate = baudRate,
                        Timeout = 3000
                    };

                    _client = new ModbusRtuClient(settings);
                    AddLog($"RTU client bağlanıyor: {settings.PortName} @ {settings.BaudRate} ...");
                }

                await _client.ConnectAsync();
                ClientStatus = "Bağlandı";
                AddLog("Client bağlantısı başarılı.");
            }
            catch (Exception ex)
            {
                ClientStatus = "Hata";
                AddLog("HATA (Client Connect): " + ex.Message);
            }
        }

        private async void OnDisconnectClient()
        {
            try
            {
                if (_client != null)
                {
                    await _client.DisconnectAsync();
                    _client = null;
                }

                ClientStatus = "Bağlı değil";
                AddLog("Client bağlantısı kapatıldı.");
            }
            catch (Exception ex)
            {
                AddLog("HATA (Client Disconnect): " + ex.Message);
            }
        }

        private void OnStartServer()
        {
            try
            {
                if (_server != null && _server.IsRunning)
                {
                    AddLog("Server zaten çalışıyor.");
                    return;
                }

                int port = ParseIntInRange(ServerPort, 1, 65535, "Server portu");
                _server = new ModbusTcpServer(_dataStore, port);
                _server.OnLog += AddLog;
                _server.Start();

                ServerStatus = $"Çalışıyor :{port}";
                IsServerRunning = true;
            }
            catch (Exception ex)
            {
                ServerStatus = "Hata";
                AddLog("HATA (Server Start): " + ex.Message);
            }
        }

        private void OnStopServer()
        {
            try
            {
                if (_server != null)
                {
                    _server.Stop();
                    _server.OnLog -= AddLog;
                    _server = null;
                }

                ServerStatus = "Durduruldu";
                IsServerRunning = false;
            }
            catch (Exception ex)
            {
                AddLog("HATA (Server Stop): " + ex.Message);
            }
        }

        private async void OnSend()
        {
            if (_client == null || !_client.IsConnected)
            {
                AddLog("Önce Client bölümünden bağlantı kurun.");
                return;
            }

            try
            {
                byte slaveId = (byte)ParseIntInRange(SlaveId, 0, 247, "Unit ID");
                ushort address = (ushort)ParseIntInRange(Address, 0, 65535, "Adres");
                ushort quantity = (ushort)ParseIntInRange(Quantity, 1, 2000, "Miktar");
                ushort writeValue = (ushort)ParseIntInRange(WriteValue, 0, 65535, "Yazma değeri");

                ModbusFunctionCode function = FunctionIndex switch
                {
                    0 => ModbusFunctionCode.ReadCoils,
                    1 => ModbusFunctionCode.ReadHoldingRegisters,
                    2 => ModbusFunctionCode.WriteSingleRegister,
                    _ => ModbusFunctionCode.ReadHoldingRegisters
                };

                if (function == ModbusFunctionCode.ReadHoldingRegisters && quantity > 125)
                    throw new ArgumentException("FC03 için Quantity 1 ile 125 arasında olmalıdır.");

                byte[] pdu = function switch
                {
                    ModbusFunctionCode.ReadCoils => _builder.BuildReadPdu(function, address, quantity),
                    ModbusFunctionCode.ReadHoldingRegisters => _builder.BuildReadPdu(function, address, quantity),
                    ModbusFunctionCode.WriteSingleRegister => _builder.BuildWriteSinglePdu(function, address, writeValue),
                    _ => throw new InvalidOperationException("Desteklenmeyen function code.")
                };

                bool isTcp = ClientProtocolIndex == 0;
                byte[] request;

                if (isTcp)
                {
                    _transactionId++;
                    request = _builder.WrapTcp(_transactionId, slaveId, pdu);
                }
                else
                {
                    request = _builder.WrapRtu(slaveId, pdu);
                }

                FillRequestDetails(request, isTcp, function, address, quantity, writeValue);
                AddLog("[Client TX] " + ToHex(request));

                byte[] response = await _client.SendAsync(request);
                AddLog("[Client RX] " + ToHex(response));

                ModbusPacket packet = isTcp
                    ? _parser.ParseTcpResponse(response)
                    : _parser.ParseRtuResponse(response);

                FillResponseDetails(packet, function, address);

                if (_parser.IsErrorResponse(packet))
                {
                    byte errorCode = packet.Data.Length > 0 ? packet.Data[0] : (byte)0;
                    LastResponseSummary = $"Modbus hata cevabı: FC 0x{packet.FunctionCode:X2}, hata 0x{errorCode:X2}";
                    AddLog(LastResponseSummary);
                    return;
                }

                if (function == ModbusFunctionCode.ReadHoldingRegisters)
                {
                    ushort[] values = _parser.ReadRegisterValues(packet);
                    UpdateClientRegisterRows(address, values);
                    LastResponseSummary = $"{values.Length} register okundu: {string.Join(", ", values)}";
                    AddLog("Okunan register değerleri: " + string.Join(", ", values));
                }
                else if (function == ModbusFunctionCode.WriteSingleRegister)
                {
                    LastResponseSummary = $"Register {address} için yazma onaylandı. Değer: {writeValue}";
                    UpdateSingleClientRegister(address, writeValue);
                    AddLog(LastResponseSummary);
                }
                else
                {
                    LastResponseSummary = "Coil okuma cevabı alındı. Ham bit verisi paket ayrıntılarında gösteriliyor.";
                }
            }
            catch (Exception ex)
            {
                LastResponseSummary = "Hata: " + ex.Message;
                AddLog("HATA (Send): " + ex.Message);
            }
        }

        private void CreateServerRegisterRows()
        {
            int visibleRegisterCount = Math.Min(20, _dataStore.HoldingRegisters.Length);

            for (int address = 0; address < visibleRegisterCount; address++)
            {
                RegisterItem item = new(address, GetRegisterName(address), _dataStore.HoldingRegisters[address]);
                item.PropertyChanged += OnServerRegisterRowChanged;
                ServerRegisters.Add(item);
            }
        }

        private void OnServerRegisterRowChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(RegisterItem.Value) || sender is not RegisterItem item)
                return;

            try
            {
                if (_dataStore.HoldingRegisters[item.Address] == item.Value)
                    return;

                _dataStore.SetHoldingRegister(item.Address, item.Value);
                AddLog($"[UI] Server Register[{item.Address}] = {item.Value} olarak değiştirildi.");
            }
            catch (Exception ex)
            {
                AddLog("Register güncelleme hatası: " + ex.Message);
            }
        }

        private void OnHoldingRegisterChanged(int address, ushort value)
        {
            RunOnUiThread(() =>
            {
                if (address >= 0 && address < ServerRegisters.Count)
                    ServerRegisters[address].Value = value;
            });
        }

        private void UpdateClientRegisterRows(ushort startAddress, ushort[] values)
        {
            ClientRegisters.Clear();

            for (int i = 0; i < values.Length; i++)
            {
                int address = startAddress + i;
                ClientRegisters.Add(new RegisterItem(address, GetRegisterName(address), values[i]));
            }
        }

        private void UpdateSingleClientRegister(ushort address, ushort value)
        {
            RegisterItem? existing = ClientRegisters.FirstOrDefault(x => x.Address == address);
            if (existing != null)
            {
                existing.Value = value;
                return;
            }

            ClientRegisters.Add(new RegisterItem(address, GetRegisterName(address), value));
        }

        private void FillRequestDetails(
            byte[] request,
            bool isTcp,
            ModbusFunctionCode function,
            ushort address,
            ushort quantity,
            ushort writeValue)
        {
            RequestDetails.Clear();

            if (isTcp)
            {
                AddPacketField(RequestDetails, "Transaction ID", ReadUInt16(request, 0).ToString(), "İstek ile cevabı eşleştiren numara");
                AddPacketField(RequestDetails, "Protocol ID", ReadUInt16(request, 2).ToString(), "Modbus TCP için 0 olmalıdır");
                AddPacketField(RequestDetails, "Length", ReadUInt16(request, 4).ToString(), "Unit ID ve PDU uzunluğu");
                AddPacketField(RequestDetails, "Unit ID", request[6].ToString(), "Hedef cihaz numarası");
                AddPacketField(RequestDetails, "Function", $"0x{request[7]:X2}", FunctionDescription(request[7]));
            }
            else
            {
                AddPacketField(RequestDetails, "Unit ID", request[0].ToString(), "Hedef cihaz numarası");
                AddPacketField(RequestDetails, "Function", $"0x{request[1]:X2}", FunctionDescription(request[1]));
                AddPacketField(RequestDetails, "CRC", $"{request[^2]:X2} {request[^1]:X2}", "RTU hata kontrolü");
            }

            AddPacketField(RequestDetails, "Start Address", address.ToString(), "İşlemin başladığı register/coil adresi");

            if (function == ModbusFunctionCode.WriteSingleRegister)
                AddPacketField(RequestDetails, "Write Value", writeValue.ToString(), $"0x{writeValue:X4}");
            else
                AddPacketField(RequestDetails, "Quantity", quantity.ToString(), "Okunacak eleman sayısı");

            AddPacketField(RequestDetails, "Raw Frame", ToHex(request), "Ağdan/kablodan gönderilen ham paket");
            LastRequestSummary = $"{FunctionDescription((byte)function)} — adres {address}" +
                                 (function == ModbusFunctionCode.WriteSingleRegister
                                     ? $", değer {writeValue}"
                                     : $", adet {quantity}");
        }

        private void FillResponseDetails(ModbusPacket packet, ModbusFunctionCode requestedFunction, ushort startAddress)
        {
            ResponseDetails.Clear();

            if (ClientProtocolIndex == 0)
            {
                AddPacketField(ResponseDetails, "Transaction ID", packet.TransactionId.ToString(), "İstekle aynı olmalı");
                AddPacketField(ResponseDetails, "Protocol ID", packet.ProtocolId.ToString(), "Modbus TCP için 0");
                AddPacketField(ResponseDetails, "Length", packet.Length.ToString(), "Cevap uzunluğu");
            }

            AddPacketField(ResponseDetails, "Unit ID", packet.SlaveId.ToString(), "Cevap veren cihaz");
            AddPacketField(ResponseDetails, "Function", $"0x{packet.FunctionCode:X2}", FunctionDescription(packet.FunctionCode));

            if ((packet.FunctionCode & 0x80) != 0)
            {
                string exception = packet.Data.Length > 0 ? $"0x{packet.Data[0]:X2}" : "Yok";
                AddPacketField(ResponseDetails, "Exception Code", exception, ExceptionDescription(packet.Data.FirstOrDefault()));
            }
            else if (requestedFunction == ModbusFunctionCode.ReadHoldingRegisters && packet.Data.Length > 0)
            {
                AddPacketField(ResponseDetails, "Byte Count", packet.Data[0].ToString(), "Register verilerinin byte sayısı");
                ushort[] values = _parser.ReadRegisterValues(packet);
                for (int i = 0; i < values.Length; i++)
                {
                    AddPacketField(
                        ResponseDetails,
                        $"Register {startAddress + i}",
                        values[i].ToString(),
                        $"HEX 0x{values[i]:X4} / Binary {Convert.ToString(values[i], 2).PadLeft(16, '0')}");
                }
            }
            else if (requestedFunction == ModbusFunctionCode.WriteSingleRegister && packet.Data.Length >= 4)
            {
                ushort address = ReadUInt16(packet.Data, 0);
                ushort value = ReadUInt16(packet.Data, 2);
                AddPacketField(ResponseDetails, "Written Address", address.ToString(), "Server'ın onayladığı adres");
                AddPacketField(ResponseDetails, "Written Value", value.ToString(), $"0x{value:X4}");
            }
            else if (requestedFunction == ModbusFunctionCode.ReadCoils && packet.Data.Length > 0)
            {
                AddPacketField(ResponseDetails, "Byte Count", packet.Data[0].ToString(), "Coil bitlerinin kapladığı byte sayısı");
                AddPacketField(ResponseDetails, "Coil Data", ToHex(packet.Data.Skip(1).ToArray()), "Bit paketli açık/kapalı değerleri");
            }

            AddPacketField(ResponseDetails, "Raw Frame", ToHex(packet.RawData), "Karşı taraftan gelen ham paket");
        }

        private static void AddPacketField(
            ObservableCollection<PacketFieldItem> collection,
            string field,
            string value,
            string description)
        {
            collection.Add(new PacketFieldItem
            {
                Field = field,
                Value = value,
                Description = description
            });
        }

        private static string GetRegisterName(int address)
        {
            return address switch
            {
                0 => "Test Değeri 1",
                1 => "Test Değeri 2",
                2 => "Test Değeri 3",
                _ => $"Register {address}"
            };
        }

        private static string FunctionDescription(byte functionCode)
        {
            byte normalCode = (byte)(functionCode & 0x7F);
            string name = normalCode switch
            {
                0x01 => "Read Coils — açık/kapalı değerleri oku",
                0x03 => "Read Holding Registers — sayısal değerleri oku",
                0x05 => "Write Single Coil — tek açık/kapalı değer yaz",
                0x06 => "Write Single Register — tek sayısal değer yaz",
                _ => "Bilinmeyen veya desteklenmeyen işlem"
            };

            return (functionCode & 0x80) != 0 ? "Hata cevabı: " + name : name;
        }

        private static string ExceptionDescription(byte code)
        {
            return code switch
            {
                0x01 => "Illegal Function — işlem desteklenmiyor",
                0x02 => "Illegal Data Address — adres geçersiz",
                0x03 => "Illegal Data Value — miktar/değer geçersiz",
                0x04 => "Server Device Failure — server işlemi tamamlayamadı",
                _ => "Bilinmeyen hata kodu"
            };
        }

        private static int ParseIntInRange(string text, int minimum, int maximum, string fieldName)
        {
            if (!int.TryParse(text, out int value))
                throw new ArgumentException($"{fieldName} sayı olmalıdır.");

            if (value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(fieldName, $"{fieldName} {minimum} ile {maximum} arasında olmalıdır.");

            return value;
        }

        private static ushort ReadUInt16(byte[] data, int index)
        {
            return (ushort)((data[index] << 8) | data[index + 1]);
        }

        private static string ToHex(byte[] data)
        {
            return data.Length == 0 ? "(boş)" : BitConverter.ToString(data).Replace("-", " ");
        }

        private void AddLog(string message)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff}  {message}";
            RunOnUiThread(() =>
            {
                LogEntries.Add(line);
                while (LogEntries.Count > 500)
                    LogEntries.RemoveAt(0);
            });
        }

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.Invoke(action);
            else
                action();
        }
    }
}