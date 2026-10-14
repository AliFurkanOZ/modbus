namespace ModbusCommunicationSuite.Tests;

/// <summary>
/// AlarmManager için birim testleri
/// </summary>
public class AlarmManagerTests : IDisposable
{
    private readonly AlarmManager _alarmManager;

    public AlarmManagerTests()
    {
        _alarmManager = new AlarmManager();
    }

    public void Dispose()
    {
        _alarmManager.Dispose();
    }

    [Fact]
    public void SetAlarmLimits_ValidLimits_LimitsSetSuccessfully()
    {
        // Arrange
        string alarmId = "TempAlarm";

        // Act
        var result = _alarmManager.SetAlarmLimits(alarmId, 90.0, 80.0, 20.0, 10.0);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CheckAlarm_NoLimitsSet_ReturnsNoAlarm()
    {
        // Arrange
        string alarmId = "NoLimitsAlarm";
        double value = 50.0;

        // Act
        var alarmState = _alarmManager.CheckAlarm(alarmId, value);

        // Assert
        Assert.Equal(AlarmState.Normal, alarmState);
    }

    [Fact]
    public void CheckAlarm_ValueAboveHighHigh_HighHighAlarmTriggered()
    {
        // Arrange
        string alarmId = "HighHighAlarm";
        _alarmManager.SetAlarmLimits(alarmId, 90.0, 80.0, 20.0, 10.0);
        double value = 95.0; // HH üzerinde

        // Act
        var alarmState = _alarmManager.CheckAlarm(alarmId, value);

        // Assert
        Assert.Equal(AlarmState.HighHigh, alarmState);
    }

    [Fact]
    public void CheckAlarm_ValueAboveHigh_HighAlarmTriggered()
    {
        // Arrange
        string alarmId = "HighAlarm";
        _alarmManager.SetAlarmLimits(alarmId, 90.0, 80.0, 20.0, 10.0);
        double value = 85.0; // H ve HH arasında

        // Act
        var alarmState = _alarmManager.CheckAlarm(alarmId, value);

        // Assert
        Assert.Equal(AlarmState.High, alarmState);
    }

    [Fact]
    public void CheckAlarm_ValueBelowLowLow_LowLowAlarmTriggered()
    {
        // Arrange
        string alarmId = "LowLowAlarm";
        _alarmManager.SetAlarmLimits(alarmId, 90.0, 80.0, 20.0, 10.0);
        double value = 5.0; // LL altında

        // Act
        var alarmState = _alarmManager.CheckAlarm(alarmId, value);

        // Assert
        Assert.Equal(AlarmState.LowLow, alarmState);
    }

    [Fact]
    public void CheckAlarm_ValueBelowLow_LowAlarmTriggered()
    {
        // Arrange
        string alarmId = "LowAlarm";
        _alarmManager.SetAlarmLimits(alarmId, 90.0, 80.0, 20.0, 10.0);
        double value = 15.0; // L ve LL arasında

        // Act
        var alarmState = _alarmManager.CheckAlarm(alarmId, value);

        // Assert
        Assert.Equal(AlarmState.Low, alarmState);
    }

    [Fact]
    public void CheckAlarm_ValueInRange_NormalState()
    {
        // Arrange
        string alarmId = "NormalAlarm";
        _alarmManager.SetAlarmLimits(alarmId, 90.0, 80.0, 20.0, 10.0);
        double value = 50.0; // Normal aralıkta

        // Act
        var alarmState = _alarmManager.CheckAlarm(alarmId, value);

        // Assert
        Assert.Equal(AlarmState.Normal, alarmState);
    }

    [Fact]
    public void GetActiveAlarms_WithActiveAlarms_ReturnsActiveAlarmsList()
    {
        // Arrange
        string alarmId1 = "Alarm1";
        string alarmId2 = "Alarm2";

        _alarmManager.SetAlarmLimits(alarmId1, 90.0, 80.0, 20.0, 10.0);
        _alarmManager.SetAlarmLimits(alarmId2, 90.0, 80.0, 20.0, 10.0);

        _alarmManager.CheckAlarm(alarmId1, 95.0); // HH alarm
        _alarmManager.CheckAlarm(alarmId2, 50.0); // Normal

        // Act
        var activeAlarms = _alarmManager.GetActiveAlarms();

        // Assert
        Assert.NotNull(activeAlarms);
        Assert.Single(activeAlarms);
        Assert.Equal(alarmId1, activeAlarms[0].AlarmId);
    }

    [Fact]
    public void AcknowledgeAlarm_ExistingAlarm_AlarmAcknowledged()
    {
        // Arrange
        string alarmId = "AckAlarm";
        _alarmManager.SetAlarmLimits(alarmId, 90.0, 80.0, 20.0, 10.0);
        _alarmManager.CheckAlarm(alarmId, 95.0); // HH alarm

        // Act
        var result = _alarmManager.AcknowledgeAlarm(alarmId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AcknowledgeAlarm_NonExistentAlarm_ReturnsFalse()
    {
        // Act
        var result = _alarmManager.AcknowledgeAlarm("NonExistentAlarm");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RemoveAlarmLimits_ExistingAlarm_LimitsRemoved()
    {
        // Arrange
        string alarmId = "RemoveAlarm";
        _alarmManager.SetAlarmLimits(alarmId, 90.0, 80.0, 20.0, 10.0);

        // Act
        var result = _alarmManager.RemoveAlarmLimits(alarmId);

        // Assert
        Assert.True(result);
        var alarmState = _alarmManager.CheckAlarm(alarmId, 50.0);
        Assert.Equal(AlarmState.Normal, alarmState);
    }

    [Fact]
    public void GetAlarmHistory_AfterMultipleChecks_HistoryContainsEntries()
    {
        // Arrange
        string alarmId = "HistoryAlarm";
        _alarmManager.SetAlarmLimits(alarmId, 90.0, 80.0, 20.0, 10.0);

        _alarmManager.CheckAlarm(alarmId, 95.0); // HH
        Thread.Sleep(10);
        _alarmManager.CheckAlarm(alarmId, 50.0); // Normal
        Thread.Sleep(10);
        _alarmManager.CheckAlarm(alarmId, 5.0); // LL

        // Act
        var history = _alarmManager.GetAlarmHistory(alarmId);

        // Assert
        Assert.NotNull(history);
        Assert.NotEmpty(history);
    }

    [Fact]
    public void ClearAlarmHistory_ExistingHistory_HistoryCleared()
    {
        // Arrange
        string alarmId = "ClearHistoryAlarm";
        _alarmManager.SetAlarmLimits(alarmId, 90.0, 80.0, 20.0, 10.0);

        _alarmManager.CheckAlarm(alarmId, 95.0);
        _alarmManager.CheckAlarm(alarmId, 50.0);

        // Act
        var result = _alarmManager.ClearAlarmHistory(alarmId);

        // Assert
        Assert.True(result);
        var history = _alarmManager.GetAlarmHistory(alarmId);
        Assert.Empty(history);
    }

    [Fact]
    public void AlarmEvent_Triggered_EventRaised()
    {
        // Arrange
        string alarmId = "EventAlarm";
        _alarmManager.SetAlarmLimits(alarmId, 90.0, 80.0, 20.0, 10.0);

        AlarmEventArgs? receivedArgs = null;
        _alarmManager.AlarmChanged += (sender, args) => receivedArgs = args;

        // Act
        _alarmManager.CheckAlarm(alarmId, 95.0);

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Equal(alarmId, receivedArgs.AlarmId);
        Assert.Equal(AlarmState.HighHigh, receivedArgs.NewState);
    }

    [Fact]
    public void GetAllAlarmStates_MultipleAlarms_AllStatesReturned()
    {
        // Arrange
        string alarmId1 = "StateAlarm1";
        string alarmId2 = "StateAlarm2";

        _alarmManager.SetAlarmLimits(alarmId1, 90.0, 80.0, 20.0, 10.0);
        _alarmManager.SetAlarmLimits(alarmId2, 90.0, 80.0, 20.0, 10.0);

        _alarmManager.CheckAlarm(alarmId1, 95.0);
        _alarmManager.CheckAlarm(alarmId2, 50.0);

        // Act
        var allStates = _alarmManager.GetAllAlarmStates();

        // Assert
        Assert.NotNull(allStates);
        Assert.Equal(2, allStates.Count);
    }
}
