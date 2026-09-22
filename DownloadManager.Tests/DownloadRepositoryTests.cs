using Microsoft.Data.Sqlite;

public class DownloadRepositoryTests : IDisposable
{
    private readonly string _databasePath;
    private readonly DownloadRepository _repository;

    public DownloadRepositoryTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"download-manager-test-{Guid.NewGuid()}.db"
        );

        var connectionString = $"Data Source={_databasePath},Pooling=false";

        _repository = new DownloadRepository(connectionString);
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    [Fact]
    public void Insert_ShouldAssignGeneratedId()
    {
        // Arrange
        var download = new DownloadItem("https://example.com/file.zip", "file.zip");

        // Act
        var id = _repository.Insert(download);

        // Assert
        Assert.True(id > 0);
        Assert.Equal(id, download.Id);
    }

    [Fact]
    public void GetAll_ShouldReturnInsertedDownload()
    {
        // Arrange
        var download = new DownloadItem("https://example.com/file.zip", "file.zip");

        var insertedId = _repository.Insert(download);

        // Act
        var downloads = _repository.GetAll();

        // Assert
        Assert.Single(downloads);

        var result = downloads[0];

        Assert.Equal(insertedId, result.Id);
        Assert.Equal(download.Url, result.Url);
        Assert.Equal(download.Destination, result.Destination);
        Assert.Equal(download.Status, result.Status);
        Assert.Equal(download.DownloadedBytes, result.DownloadedBytes);
        Assert.Equal(download.TotalBytes, result.TotalBytes);
    }

    [Fact]
    public void Update_ShouldPersistDownloadChanges()
    {
        // Arrange
        var download = new DownloadItem("https://example.com/file.zip", "file.zip");

        _repository.Insert(download);

        download.Status = DownloadStatus.Downloading;
        download.DownloadedBytes = 500;
        download.TotalBytes = 1000;

        // Act
        _repository.Update(download);

        var downloads = _repository.GetAll();

        // Assert
        Assert.Single(downloads);

        var result = downloads[0];

        Assert.Equal(download.Id, result.Id);
        Assert.Equal(DownloadStatus.Downloading, result.Status);
        Assert.Equal(500, result.DownloadedBytes);
        Assert.Equal(1000, result.TotalBytes);
    }

    [Fact]
    public void Delete_ShouldRemoveDownload()
    {
        // Arrange
        var download = new DownloadItem("https://example.com/file.zip", "file.zip");

        _repository.Insert(download);

        // Act
        _repository.Delete(download.Id);

        var downloads = _repository.GetAll();

        // Assert
        Assert.Empty(downloads);
    }
}
