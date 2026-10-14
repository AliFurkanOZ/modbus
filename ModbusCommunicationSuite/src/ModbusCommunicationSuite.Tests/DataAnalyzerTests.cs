namespace ModbusCommunicationSuite.Tests;

/// <summary>
/// DataAnalyzer için birim testleri
/// </summary>
public class DataAnalyzerTests : IDisposable
{
    private readonly DataAnalyzer _dataAnalyzer;

    public DataAnalyzerTests()
    {
        _dataAnalyzer = new DataAnalyzer(capacity: 100);
    }

    public void Dispose()
    {
        _dataAnalyzer.Dispose();
    }

    [Fact]
    public void AddValue_ValidValue_ValueAddedSuccessfully()
    {
        // Arrange
        double value = 50.0;

        // Act
        _dataAnalyzer.AddValue(value);

        // Assert
        Assert.Equal(1, _dataAnalyzer.Count);
    }

    [Fact]
    public void GetStatistics_MultipleValues_CorrectStatistics()
    {
        // Arrange
        var values = new double[] { 10, 20, 30, 40, 50 };
        foreach (var value in values)
        {
            _dataAnalyzer.AddValue(value);
        }

        // Act
        var stats = _dataAnalyzer.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(5, stats.Count);
        Assert.Equal(10, stats.Min);
        Assert.Equal(50, stats.Max);
        Assert.Equal(30, stats.Average, precision: 2);
    }

    [Fact]
    public void GetStatistics_EmptyData_ReturnsNull()
    {
        // Act
        var stats = _dataAnalyzer.GetStatistics();

        // Assert
        Assert.Null(stats);
    }

    [Fact]
    public void Clear_DataCleared_CountIsZero()
    {
        // Arrange
        _dataAnalyzer.AddValue(10);
        _dataAnalyzer.AddValue(20);
        Assert.Equal(2, _dataAnalyzer.Count);

        // Act
        _dataAnalyzer.Clear();

        // Assert
        Assert.Equal(0, _dataAnalyzer.Count);
    }

    [Fact]
    public void GetStandardDeviation_MultipleValues_CorrectStdDev()
    {
        // Arrange
        var values = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };
        foreach (var value in values)
        {
            _dataAnalyzer.AddValue(value);
        }

        // Act
        var stats = _dataAnalyzer.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        // Beklenen StdDev ≈ 2.0
        Assert.InRange(stats.StandardDeviation, 1.9, 2.1);
    }

    [Fact]
    public void CapacityLimit_ExceedsCapacity_OldestValuesRemoved()
    {
        // Arrange
        using var analyzer = new DataAnalyzer(capacity: 5);

        // Act - 10 değer ekle
        for (int i = 0; i < 10; i++)
        {
            analyzer.AddValue(i);
        }

        // Assert
        Assert.Equal(5, analyzer.Count);
        var stats = analyzer.GetStatistics();
        Assert.NotNull(stats);
        Assert.Equal(5, stats.Min); // İlk 5 değer silindi, min 5 olmalı
        Assert.Equal(9, stats.Max);
    }

    [Fact]
    public void GetPercentile_ValidData_CorrectPercentile()
    {
        // Arrange
        for (int i = 1; i <= 100; i++)
        {
            _dataAnalyzer.AddValue(i);
        }

        // Act
        var percentile90 = _dataAnalyzer.GetPercentile(90);

        // Assert
        Assert.NotNull(percentile90);
        Assert.InRange(percentile90.Value, 89, 91);
    }

    [Fact]
    public void GetPercentile_EmptyData_ReturnsNull()
    {
        // Act
        var percentile = _dataAnalyzer.GetPercentile(50);

        // Assert
        Assert.Null(percentile);
    }

    [Fact]
    public void GetMedian_OddCount_CorrectMedian()
    {
        // Arrange
        var values = new double[] { 1, 3, 5, 7, 9 };
        foreach (var value in values)
        {
            _dataAnalyzer.AddValue(value);
        }

        // Act
        var median = _dataAnalyzer.GetMedian();

        // Assert
        Assert.NotNull(median);
        Assert.Equal(5, median);
    }

    [Fact]
    public void GetMedian_EvenCount_CorrectMedian()
    {
        // Arrange
        var values = new double[] { 1, 2, 3, 4, 5, 6 };
        foreach (var value in values)
        {
            _dataAnalyzer.AddValue(value);
        }

        // Act
        var median = _dataAnalyzer.GetMedian();

        // Assert
        Assert.NotNull(median);
        Assert.Equal(3.5, median, precision: 2);
    }

    [Fact]
    public void GetAllValues_ReturnsAllValuesInOrder()
    {
        // Arrange
        var values = new double[] { 10, 20, 30, 40, 50 };
        foreach (var value in values)
        {
            _dataAnalyzer.AddValue(value);
        }

        // Act
        var allValues = _dataAnalyzer.GetAllValues();

        // Assert
        Assert.NotNull(allValues);
        Assert.Equal(values.Length, allValues.Count);
        Assert.Equal(values, allValues);
    }
}
