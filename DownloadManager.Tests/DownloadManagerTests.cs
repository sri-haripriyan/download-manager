public class DownloadManagerTests
{
    private readonly FakeDownloadRepository _repository;
    private readonly FakeDownloadService _service;
    private readonly DownloadManager _manager;

    public DownloadManagerTests()
    {
        _repository = new FakeDownloadRepository();
        _service = new FakeDownloadService();

        _manager = new DownloadManager(_service, _repository, 2);
    }

    [Fact]
    public void AddDownload_ShouldCreateWaitingDownload()
    {
        // Act
        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        // Assert
        Assert.NotNull(download);

        Assert.Equal(DownloadStatus.Waiting, download.Status);

        Assert.Equal("https://example.com/file.zip", download.Url);

        Assert.Equal("file.zip", download.Destination);

        Assert.True(download.Id > 0);

        Assert.Contains(download, _manager.Downloads);
    }

    [Fact]
    public void CancelDownload_ShouldCancelDownload()
    {
        // Arrange
        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        // Act
        _manager.CancelDownload(download);

        // Assert
        Assert.Equal(DownloadStatus.Cancelled, download.Status);

        Assert.True(download.CancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public void PauseDownload_ShouldPauseRunningDownload()
    {
        // Arrange
        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        download.Status = DownloadStatus.Downloading;

        // Act
        _manager.PauseDownload(download);

        // Assert
        Assert.Equal(DownloadStatus.Paused, download.Status);

        Assert.True(download.CancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public void PauseDownload_ShouldDoNothing_WhenNotDownloading()
    {
        // Arrange
        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        // Act
        _manager.PauseDownload(download);

        // Assert
        Assert.Equal(DownloadStatus.Waiting, download.Status);

        Assert.False(download.CancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public void ResumeDownload_ShouldCreateNewCancellationToken()
    {
        // Arrange
        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        download.Status = DownloadStatus.Paused;

        var oldTokenSource = download.CancellationTokenSource;

        // Act
        _manager.ResumeDownload(download);

        // Assert
        Assert.Equal(DownloadStatus.Waiting, download.Status);

        Assert.NotSame(oldTokenSource, download.CancellationTokenSource);

        Assert.False(download.CancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public void RetryDownload_ShouldResetFailedDownload()
    {
        // Arrange
        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        download.Status = DownloadStatus.Failed;

        var oldTokenSource = download.CancellationTokenSource;

        // Act
        _manager.RetryDownload(download);

        // Assert
        Assert.Equal(DownloadStatus.Waiting, download.Status);

        Assert.NotSame(oldTokenSource, download.CancellationTokenSource);

        Assert.False(download.CancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public void RetryDownload_ShouldDoNothing_WhenDownloadIsNotFailed()
    {
        // Arrange
        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        // Act
        _manager.RetryDownload(download);

        // Assert
        Assert.Equal(DownloadStatus.Waiting, download.Status);
    }

    [Fact]
    public void RemoveDownload_ShouldRemoveFromManagerAndRepository()
    {
        // Arrange
        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        // Act
        _manager.RemoveDownload(download, deleteFile: false);

        // Assert
        Assert.Equal(DownloadStatus.Removed, download.Status);

        Assert.DoesNotContain(download, _manager.Downloads);

        Assert.Null(_repository.Find(download.Id));
    }

    [Fact]
    public void RemoveDownload_ShouldCancelActiveDownload()
    {
        // Arrange
        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        download.Status = DownloadStatus.Downloading;

        // Act
        _manager.RemoveDownload(download, deleteFile: false);

        // Assert
        Assert.Equal(DownloadStatus.Removed, download.Status);

        Assert.True(download.CancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public async Task StartDownloadAsync_ShouldMarkDownloadCompleted()
    {
        // Arrange
        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        // Act
        await _manager.StartDownloadAsync(download);

        // Assert
        Assert.Equal(DownloadStatus.Completed, download.Status);

        Assert.True(_service.DownloadCalled);
    }

    [Fact]
    public async Task StartDownloadAsync_ShouldMarkDownloadFailed()
    {
        // Arrange
        _service.ShouldFail = true;

        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        // Act
        await _manager.StartDownloadAsync(download);

        // Assert
        Assert.Equal(DownloadStatus.Failed, download.Status);
    }

    [Fact]
    public async Task StartWorkers_ShouldProcessQueuedDownload()
    {
        // Arrange
        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        // Act
        _manager.StartWorkers();

        // Give worker enough time to process the item.
        var timeout = Task.Delay(2000);

        while (download.Status != DownloadStatus.Completed && !timeout.IsCompleted)
        {
            await Task.Delay(20);
        }

        // Assert
        Assert.Equal(DownloadStatus.Completed, download.Status);

        Assert.True(_service.DownloadCalled);
    }

    [Fact]
    public void LoadDownloads_ShouldRecoverWaitingAndPausedDownloads()
    {
        // Arrange
        var waiting = new DownloadItem("https://example.com/a.zip", "a.zip")
        {
            Status = DownloadStatus.Waiting,
        };

        var paused = new DownloadItem("https://example.com/b.zip", "b.zip")
        {
            Status = DownloadStatus.Paused,
        };

        var downloading = new DownloadItem("https://example.com/c.zip", "c.zip")
        {
            Status = DownloadStatus.Downloading,
        };

        _repository.Insert(waiting);
        _repository.Insert(paused);
        _repository.Insert(downloading);

        // Act
        _manager.LoadDownloads();

        // Assert
        Assert.Equal(3, _manager.Downloads.Count);

        Assert.Equal(DownloadStatus.Waiting, downloading.Status);
    }

    [Fact]
    public void CancelAll_ShouldCancelAllDownloads()
    {
        // Arrange
        var download1 = _manager.AddDownload("https://example.com/a.zip", "a.zip");

        var download2 = _manager.AddDownload("https://example.com/b.zip", "b.zip");

        // Act
        _manager.CancelAll();

        // Assert
        Assert.Equal(DownloadStatus.Cancelled, download1.Status);

        Assert.Equal(DownloadStatus.Cancelled, download2.Status);

        Assert.True(download1.CancellationTokenSource.IsCancellationRequested);

        Assert.True(download2.CancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public async Task ShutDown_ShouldCancelRunningDownloads()
    {
        // Arrange
        _service.BlockDownload = true;

        var download = _manager.AddDownload("https://example.com/file.zip", "file.zip");

        _manager.StartWorkers();

        // Wait until the fake service actually starts.
        var timeout = DateTime.UtcNow.AddSeconds(2);

        while (!_service.DownloadCalled && DateTime.UtcNow < timeout)
        {
            await Task.Delay(20);
        }

        Assert.True(_service.DownloadCalled);

        // Act
        await _manager.ShutDown();

        // Assert
        Assert.True(download.CancellationTokenSource.IsCancellationRequested);
    }
}
