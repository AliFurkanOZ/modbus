namespace ModbusCommunicationSuite.Tests;

/// <summary>
/// Communication Layer için entegrasyon testleri
/// </summary>
public class CommunicationLayerTests : IDisposable
{
    private readonly ModbusTcpClient _tcpClient;
    private readonly ModbusRtuClient _rtuClient;

    public CommunicationLayerTests()
    {
        var tcpSettings = new TcpConnectionSettings
        {
            IpAddress = "127.0.0.1",
            Port = 502,
            ConnectionTimeout = TimeSpan.FromSeconds(2)
        };
        _tcpClient = new ModbusTcpClient(tcpSettings);

        var rtuSettings = new RtuConnectionSettings
        {
            PortName = "COM1",
            BaudRate = 9600,
            Parity = System.IO.Ports.Parity.None,
            DataBits = 8,
            StopBits = System.IO.Ports.StopBits.One
        };
        _rtuClient = new ModbusRtuClient(rtuSettings);
    }

    public void Dispose()
    {
        _tcpClient.Dispose();
        _rtuClient.Dispose();
    }

    [Fact]
    public async Task TcpClient_InitialState_IsNotConnected()
    {
        // Assert
        Assert.False(_tcpClient.IsConnected);
        Assert.Equal(ConnectionState.Disconnected, _tcpClient.ConnectionState);
    }

    [Fact]
    public async Task TcpClient_ConnectToInvalidHost_ConnectionFailed()
    {
        // Arrange
        var invalidSettings = new TcpConnectionSettings
        {
            IpAddress = "192.168.255.255",
            Port = 502,
            ConnectionTimeout = TimeSpan.FromMilliseconds(100)
        };

        using var client = new ModbusTcpClient(invalidSettings);

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await client.ConnectAsync();
        });
    }

    [Fact]
    public void TcpClient_Settings_ReturnsCorrectSettings()
    {
        // Assert
        Assert.Equal("127.0.0.1", _tcpClient.Settings.IpAddress);
        Assert.Equal(502, _tcpClient.Settings.Port);
    }

    [Fact]
    public void RtuClient_InitialState_IsNotConnected()
    {
        // Assert
        Assert.False(_rtuClient.IsConnected);
    }

    [Fact]
    public void RtuClient_Settings_ReturnsCorrectSettings()
    {
        // Assert
        Assert.Equal("COM1", _rtuClient.Settings.PortName);
        Assert.Equal(9600, _rtuClient.Settings.BaudRate);
    }

    [Fact]
    public async Task PacketBuilder_ReadHoldingRegisters_CorrectPacket()
    {
        // Arrange
        byte slaveId = 1;
        ushort startAddress = 100;
        ushort quantity = 10;

        // Act
        var packet = PacketBuilder.BuildReadHoldingRegisters(slaveId, startAddress, quantity);

        // Assert
        Assert.NotNull(packet);
        Assert.Equal(8, packet.Length); // SlaveId + FC + StartAddr(2) + Quantity(2) + CRC(2)
        Assert.Equal(slaveId, packet[0]);
        Assert.Equal(0x03, packet[1]); // FC 03
        Assert.Equal(100, BitConverter.ToUInt16(new[] { packet[3], packet[2] }, 0));
        Assert.Equal(10, BitConverter.ToUInt16(new[] { packet[5], packet[4] }, 0));
    }

    [Fact]
    public void PacketBuilder_WriteSingleRegister_CorrectPacket()
    {
        // Arrange
        byte slaveId = 2;
        ushort address = 200;
        ushort value = 1234;

        // Act
        var packet = PacketBuilder.BuildWriteSingleRegister(slaveId, address, value);

        // Assert
        Assert.NotNull(packet);
        Assert.Equal(8, packet.Length);
        Assert.Equal(slaveId, packet[0]);
        Assert.Equal(0x06, packet[1]); // FC 06
    }

    [Fact]
    public void ResponseParser_ParseReadHoldingRegisters_SuccessfulResponse()
    {
        // Arrange
        byte[] response = new byte[] { 0x01, 0x03, 0x04, 0x00, 0x64, 0x00, 0xC8, 0xB8, 0x44 };
        // SlaveId=1, FC=03, ByteCount=4, Data=[100, 200], CRC

        // Act
        var result = ResponseParser.ParseReadHoldingRegistersResponse(response);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Values.Length);
        Assert.Equal(100, result.Values[0]);
        Assert.Equal(200, result.Values[1]);
    }

    [Fact]
    public void ResponseParser_ParseModbusException_ExceptionDetected()
    {
        // Arrange
        byte[] exceptionResponse = new byte[] { 0x01, 0x83, 0x02, 0xD8, 0x44 };
        // SlaveId=1, FC=0x83 (error), ExceptionCode=02, CRC

        // Act & Assert
        var ex = Assert.Throws<ModbusException>(() =>
        {
            ResponseParser.ParseReadHoldingRegistersResponse(exceptionResponse);
        });

        Assert.Equal(0x02, ex.ExceptionCode);
    }

    [Fact]
    public async Task TrafficLogger_LogEntry_EntryLogged()
    {
        // Arrange
        using var logger = new TrafficLogger();
        var entries = new List<TrafficLogEntry>();

        logger.LogEntryReceived += (sender, entry) => entries.Add(entry);

        // Act
        logger.LogRequest(new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A }, "Test");

        // Assert
        Assert.Single(entries);
        Assert.Equal(TrafficLogType.Request, entries[0].Type);
        Assert.NotNull(entries[0].Data);
    }

    [Fact]
    public async Task ConnectionHealthMonitor_MonitoringStarted_MonitoringActive()
    {
        // Arrange
        using var monitor = new ConnectionHealthMonitor(_tcpClient, TimeSpan.FromSeconds(1));

        // Act
        monitor.StartMonitoring();

        // Assert
        Assert.True(monitor.IsMonitoring);
    }

    [Fact]
    public async Task ConnectionHealthMonitor_MonitoringStopped_MonitoringInactive()
    {
        // Arrange
        using var monitor = new ConnectionHealthMonitor(_tcpClient, TimeSpan.FromSeconds(1));
        monitor.StartMonitoring();

        // Act
        monitor.StopMonitoring();

        // Assert
        Assert.False(monitor.IsMonitoring);
    }

    [Fact]
    public void Crc16_Calculate_ValidCrcCalculated()
    {
        // Arrange
        byte[] data = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };

        // Act
        var crc = Crc16.Calculate(data);

        // Assert
        Assert.NotEqual(0, crc);
    }

    [Fact]
    public void Crc16_Verify_ValidData_ReturnsTrue()
    {
        // Arrange
        byte[] dataWithCrc = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x00 };
        // Son 2 byte'i gerçek CRC ile değiştir
        var crc = Crc16.Calculate(dataWithCrc.Take(6).ToArray());
        dataWithCrc[6] = (byte)(crc & 0xFF);
        dataWithCrc[7] = (byte)((crc >> 8) & 0xFF);

        // Act
        var isValid = Crc16.Verify(dataWithCrc);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ByteOrderConverter_SwapABCD_ToCDAB()
    {
        // Arrange
        byte[] input = new byte[] { 0x12, 0x34, 0x56, 0x78 };

        // Act
        var output = ByteOrderConverter.Swap(input, ByteOrder.ABCD, ByteOrder.CDAB);

        // Assert
        Assert.Equal(new byte[] { 0x56, 0x78, 0x12, 0x34 }, output);
    }

    [Fact]
    public void ModbusValueConverter_ToFloat32_ABCD_CorrectConversion()
    {
        // Arrange
        ushort[] registers = new ushort[] { 0x42C8, 0x0000 }; // 100.0f

        // Act
        var value = ModbusValueConverter.ToFloat32(registers, ByteOrder.ABCD);

        // Assert
        Assert.Equal(100.0, value, precision: 2);
    }

    [Fact]
    public void ModbusValueConverter_ToInt32_ABCD_CorrectConversion()
    {
        // Arrange
        ushort[] registers = new ushort[] { 0x0000, 0x03E8 }; // 1000

        // Act
        var value = ModbusValueConverter.ToInt32(registers, ByteOrder.ABCD);

        // Assert
        Assert.Equal(1000, value);
    }

    [Fact]
    public void ValueFormatter_ToHex_ValidHexString()
    {
        // Arrange
        byte[] data = new byte[] { 0x12, 0x34, 0xAB, 0xCD };

        // Act
        var hexString = ValueFormatter.ToHex(data);

        // Assert
        Assert.Equal("12 34 AB CD", hexString);
    }

    [Fact]
    public void ValueFormatter_ToBinary_ValidBinaryString()
    {
        // Arrange
        byte value = 0xA5;

        // Act
        var binaryString = ValueFormatter.ToBinary(value);

        // Assert
        Assert.Equal("10100101", binaryString);
    }
}
