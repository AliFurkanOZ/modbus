namespace ModbusCommunicationSuite.Tests;

/// <summary>
/// RegisterService için birim testleri
/// </summary>
public class RegisterServiceTests : IDisposable
{
    private readonly RegisterService _registerService;
    private readonly Mock<IModbusClient> _mockClient;

    public RegisterServiceTests()
    {
        _mockClient = new Mock<IModbusClient>();
        _registerService = new RegisterService(_mockClient.Object);
    }

    public void Dispose()
    {
        _registerService.Dispose();
    }

    [Fact]
    public void AddRegister_ValidInput_RegisterAddedSuccessfully()
    {
        // Arrange
        var register = new RegisterDefinition
        {
            Name = "Temperature",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Float32,
            ByteOrder = ByteOrder.ABCD
        };

        // Act
        var result = _registerService.AddRegister(register);

        // Assert
        Assert.True(result);
        Assert.Single(_registerService.Registers);
        Assert.Equal("Temperature", _registerService.Registers[0].Name);
    }

    [Fact]
    public void AddRegister_DuplicateAddress_ReturnsFalse()
    {
        // Arrange
        var register1 = new RegisterDefinition
        {
            Name = "Temp1",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Float32
        };

        var register2 = new RegisterDefinition
        {
            Name = "Temp2",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Float32
        };

        _registerService.AddRegister(register1);

        // Act
        var result = _registerService.AddRegister(register2);

        // Assert
        Assert.False(result);
        Assert.Single(_registerService.Registers);
    }

    [Fact]
    public void RemoveRegister_ExistingRegister_RegisterRemoved()
    {
        // Arrange
        var register = new RegisterDefinition
        {
            Name = "Pressure",
            Address = 200,
            RegisterType = RegisterType.InputRegister,
            DataType = DataType.Int16
        };

        _registerService.AddRegister(register);

        // Act
        var result = _registerService.RemoveRegister(200, RegisterType.InputRegister);

        // Assert
        Assert.True(result);
        Assert.Empty(_registerService.Registers);
    }

    [Fact]
    public void GetRegister_ExistingAddress_ReturnsCorrectRegister()
    {
        // Arrange
        var register = new RegisterDefinition
        {
            Name = "Flow",
            Address = 300,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.UInt32
        };

        _registerService.AddRegister(register);

        // Act
        var retrieved = _registerService.GetRegister(300, RegisterType.HoldingRegister);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Flow", retrieved.Name);
        Assert.Equal(300, retrieved.Address);
    }

    [Fact]
    public void ConvertRawToEngineering_Float32_CorrectConversion()
    {
        // Arrange
        var rawBytes = new byte[] { 0x42, 0xC8, 0x00, 0x00 }; // 100.0f in IEEE754

        // Act
        var value = _registerService.ConvertRawToEngineering(rawBytes, DataType.Float32, ByteOrder.ABCD, 1.0, 0.0);

        // Assert
        Assert.Equal(100.0, (double)value, precision: 2);
    }

    [Fact]
    public void ConvertEngineeringToRaw_Float32_CorrectConversion()
    {
        // Arrange
        double engineeringValue = 100.0;

        // Act
        var rawBytes = _registerService.ConvertEngineeringToRaw(engineeringValue, DataType.Float32, ByteOrder.ABCD, 1.0, 0.0);

        // Assert
        Assert.Equal(4, rawBytes.Length);
        // Verify by converting back
        var convertedBack = _registerService.ConvertRawToEngineering(rawBytes, DataType.Float32, ByteOrder.ABCD, 1.0, 0.0);
        Assert.Equal(100.0, (double)convertedBack, precision: 2);
    }

    [Fact]
    public void ApplyScaleAndOffset_CorrectCalculation()
    {
        // Arrange
        double rawValue = 50.0;
        double scale = 2.0;
        double offset = 10.0;
        double expected = (rawValue * scale) + offset; // 110.0

        // Act
        var result = _registerService.ApplyScaleAndOffset(rawValue, scale, offset);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExportToJson_ValidRegisters_ValidJsonOutput()
    {
        // Arrange
        var register1 = new RegisterDefinition
        {
            Name = "Temp1",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Float32
        };

        var register2 = new RegisterDefinition
        {
            Name = "Temp2",
            Address = 101,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Float32
        };

        _registerService.AddRegister(register1);
        _registerService.AddRegister(register2);

        // Act
        var json = _registerService.ExportToJson();

        // Assert
        Assert.NotNull(json);
        Assert.Contains("Temp1", json);
        Assert.Contains("Temp2", json);
        Assert.Contains("100", json);
    }

    [Fact]
    public void ImportFromJson_ValidJson_RegistersImported()
    {
        // Arrange
        var json = @"[
            {
                ""Name"": ""ImportedTemp"",
                ""Address"": 500,
                ""RegisterType"": 0,
                ""DataType"": 2,
                ""ByteOrder"": 0,
                ""Scale"": 1.0,
                ""Offset"": 0.0,
                ""Unit"": ""°C""
            }
        ]";

        // Act
        var result = _registerService.ImportFromJson(json);

        // Assert
        Assert.True(result);
        Assert.Single(_registerService.Registers);
        Assert.Equal("ImportedTemp", _registerService.Registers[0].Name);
        Assert.Equal(500, _registerService.Registers[0].Address);
    }

    [Fact]
    public void ReadAllRegisters_MockClient_ReturnsDataDictionary()
    {
        // Arrange
        var register = new RegisterDefinition
        {
            Name = "TestReg",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Int16
        };

        _registerService.AddRegister(register);

        var mockData = new Dictionary<int, ushort> { { 100, 1234 } };
        _mockClient.Setup(x => x.ReadHoldingRegistersAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ushort[] { 1234 });

        // Act
        var result = _registerService.ReadAllRegistersAsync().Result;

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Theory]
    [InlineData(DataType.Int16, 2)]
    [InlineData(DataType.UInt16, 2)]
    [InlineData(DataType.Int32, 4)]
    [InlineData(DataType.UInt32, 4)]
    [InlineData(DataType.Float32, 4)]
    [InlineData(DataType.Int64, 8)]
    [InlineData(DataType.UInt64, 8)]
    [InlineData(DataType.Float64, 8)]
    [InlineData(DataType.String, 2)]
    public void GetByteSizeForDataType_AllTypes_CorrectByteSize(DataType dataType, int expectedSize)
    {
        // Act
        var size = RegisterService.GetByteSizeForDataType(dataType);

        // Assert
        Assert.Equal(expectedSize, size);
    }
}
