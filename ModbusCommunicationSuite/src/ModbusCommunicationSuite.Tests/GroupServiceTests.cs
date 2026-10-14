namespace ModbusCommunicationSuite.Tests;

/// <summary>
/// GroupService için birim testleri
/// </summary>
public class GroupServiceTests : IDisposable
{
    private readonly GroupService _groupService;
    private readonly RegisterService _registerService;
    private readonly Mock<IModbusClient> _mockClient;

    public GroupServiceTests()
    {
        _mockClient = new Mock<IModbusClient>();
        _registerService = new RegisterService(_mockClient.Object);
        _groupService = new GroupService(_registerService);
    }

    public void Dispose()
    {
        _groupService.Dispose();
        _registerService.Dispose();
    }

    [Fact]
    public void CreateGroup_ValidName_GroupCreatedSuccessfully()
    {
        // Arrange
        var groupName = "TemperatureGroup";

        // Act
        var result = _groupService.CreateGroup(groupName);

        // Assert
        Assert.True(result);
        Assert.Single(_groupService.Groups);
        Assert.Equal(groupName, _groupService.Groups[0].Name);
    }

    [Fact]
    public void CreateGroup_DuplicateName_ReturnsFalse()
    {
        // Arrange
        var groupName = "PressureGroup";
        _groupService.CreateGroup(groupName);

        // Act
        var result = _groupService.CreateGroup(groupName);

        // Assert
        Assert.False(result);
        Assert.Single(_groupService.Groups);
    }

    [Fact]
    public void AddRegisterToGroup_ValidInput_RegisterAddedToGroup()
    {
        // Arrange
        var groupName = "TestGroup";
        _groupService.CreateGroup(groupName);

        var register = new RegisterDefinition
        {
            Name = "Temp1",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Float32
        };

        _registerService.AddRegister(register);

        // Act
        var result = _groupService.AddRegisterToGroup(groupName, 100, RegisterType.HoldingRegister);

        // Assert
        Assert.True(result);
        Assert.Single(_groupService.Groups[0].Registers);
    }

    [Fact]
    public void AddRegisterToGroup_NonExistentGroup_ReturnsFalse()
    {
        // Arrange
        var register = new RegisterDefinition
        {
            Name = "Temp1",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Float32
        };

        _registerService.AddRegister(register);

        // Act
        var result = _groupService.AddRegisterToGroup("NonExistentGroup", 100, RegisterType.HoldingRegister);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RemoveRegisterFromGroup_ExistingRegister_RegisterRemoved()
    {
        // Arrange
        var groupName = "TestGroup";
        _groupService.CreateGroup(groupName);

        var register = new RegisterDefinition
        {
            Name = "Temp1",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Float32
        };

        _registerService.AddRegister(register);
        _groupService.AddRegisterToGroup(groupName, 100, RegisterType.HoldingRegister);

        // Act
        var result = _groupService.RemoveRegisterFromGroup(groupName, 100, RegisterType.HoldingRegister);

        // Assert
        Assert.True(result);
        Assert.Empty(_groupService.Groups[0].Registers);
    }

    [Fact]
    public void DeleteGroup_ExistingGroup_GroupDeleted()
    {
        // Arrange
        var groupName = "ToDeleteGroup";
        _groupService.CreateGroup(groupName);

        // Act
        var result = _groupService.DeleteGroup(groupName);

        // Assert
        Assert.True(result);
        Assert.Empty(_groupService.Groups);
    }

    [Fact]
    public void GetOptimizedReadGroups_AdjacentRegisters_SingleGroup()
    {
        // Arrange
        var groupName = "AdjacentGroup";
        _groupService.CreateGroup(groupName);

        var register1 = new RegisterDefinition
        {
            Name = "Temp1",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Int16
        };

        var register2 = new RegisterDefinition
        {
            Name = "Temp2",
            Address = 101,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Int16
        };

        var register3 = new RegisterDefinition
        {
            Name = "Temp3",
            Address = 102,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Int16
        };

        _registerService.AddRegister(register1);
        _registerService.AddRegister(register2);
        _registerService.AddRegister(register3);

        _groupService.AddRegisterToGroup(groupName, 100, RegisterType.HoldingRegister);
        _groupService.AddRegisterToGroup(groupName, 101, RegisterType.HoldingRegister);
        _groupService.AddRegisterToGroup(groupName, 102, RegisterType.HoldingRegister);

        // Act
        var optimizedGroups = _groupService.GetOptimizedReadGroups(groupName);

        // Assert
        Assert.NotNull(optimizedGroups);
        Assert.Single(optimizedGroups); // Bitişik registerlar tek grup olmalı
        Assert.Equal(100, optimizedGroups[0].StartAddress);
        Assert.Equal(3, optimizedGroups[0].Count);
    }

    [Fact]
    public void GetOptimizedReadGroups_NonAdjacentRegisters_MultipleGroups()
    {
        // Arrange
        var groupName = "NonAdjacentGroup";
        _groupService.CreateGroup(groupName);

        var register1 = new RegisterDefinition
        {
            Name = "Temp1",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Int16
        };

        var register2 = new RegisterDefinition
        {
            Name = "Temp2",
            Address = 200, // Farklı adres aralığı
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Int16
        };

        _registerService.AddRegister(register1);
        _registerService.AddRegister(register2);

        _groupService.AddRegisterToGroup(groupName, 100, RegisterType.HoldingRegister);
        _groupService.AddRegisterToGroup(groupName, 200, RegisterType.HoldingRegister);

        // Act
        var optimizedGroups = _groupService.GetOptimizedReadGroups(groupName);

        // Assert
        Assert.NotNull(optimizedGroups);
        Assert.Equal(2, optimizedGroups.Count); // İki ayrı grup olmalı
    }

    [Fact]
    public void ValidateGroup_ValidGroup_ReturnsTrue()
    {
        // Arrange
        var groupName = "ValidGroup";
        _groupService.CreateGroup(groupName);

        var register = new RegisterDefinition
        {
            Name = "Temp1",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Float32
        };

        _registerService.AddRegister(register);
        _groupService.AddRegisterToGroup(groupName, 100, RegisterType.HoldingRegister);

        // Act
        var (isValid, errors) = _groupService.ValidateGroup(groupName);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void GetGroupStatistics_ValidGroup_CorrectStatistics()
    {
        // Arrange
        var groupName = "StatsGroup";
        _groupService.CreateGroup(groupName);

        for (int i = 0; i < 5; i++)
        {
            var register = new RegisterDefinition
            {
                Name = $"Reg{i}",
                Address = 100 + i,
                RegisterType = RegisterType.HoldingRegister,
                DataType = DataType.Int16
            };
            _registerService.AddRegister(register);
            _groupService.AddRegisterToGroup(groupName, 100 + i, RegisterType.HoldingRegister);
        }

        // Act
        var stats = _groupService.GetGroupStatistics(groupName);

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(5, stats.RegisterCount);
        Assert.Equal(100, stats.MinAddress);
        Assert.Equal(104, stats.MaxAddress);
    }

    [Fact]
    public void ExportGroupToJson_ValidGroup_ValidJsonOutput()
    {
        // Arrange
        var groupName = "ExportGroup";
        _groupService.CreateGroup(groupName);

        var register = new RegisterDefinition
        {
            Name = "Temp1",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Float32
        };

        _registerService.AddRegister(register);
        _groupService.AddRegisterToGroup(groupName, 100, RegisterType.HoldingRegister);

        // Act
        var json = _groupService.ExportGroupToJson(groupName);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("ExportGroup", json);
        Assert.Contains("Temp1", json);
    }

    [Fact]
    public async Task ReadGroupAsync_MockClient_ReturnsDataDictionary()
    {
        // Arrange
        var groupName = "ReadGroup";
        _groupService.CreateGroup(groupName);

        var register = new RegisterDefinition
        {
            Name = "TestReg",
            Address = 100,
            RegisterType = RegisterType.HoldingRegister,
            DataType = DataType.Int16
        };

        _registerService.AddRegister(register);
        _groupService.AddRegisterToGroup(groupName, 100, RegisterType.HoldingRegister);

        _mockClient.Setup(x => x.ReadHoldingRegistersAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ushort[] { 1234 });

        // Act
        var result = await _groupService.ReadGroupAsync(groupName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}
