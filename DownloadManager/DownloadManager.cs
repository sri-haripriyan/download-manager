using System.Collections.Concurrent;
using System.Threading.Channels;

public class DownloadManager(
    IDownloadService downloadService,
    IDownloadRepository downloadRepository,
    int maxConcurrentDownloads
)
{
    private readonly IDownloadService _downloadService = downloadService;

    private readonly IDownloadRepository _downloadRepository = downloadRepository;

    private readonly Channel<DownloadItem> DownloadQueue = Channel.CreateUnbounded<DownloadItem>();

    private readonly List<DownloadItem> _downloads = [];

    public IReadOnlyList<DownloadItem> Downloads => _downloads;

    private readonly CancellationTokenSource ShutdownCts = new();

    private bool IsProcessing = false;

    private readonly List<Task> _workers = [];

    public DownloadItem AddDownload(string url, string destination)
    {
        var download = new DownloadItem(url, destination) { Status = DownloadStatus.Waiting };
        if (!DownloadQueue.Writer.TryWrite(download))
        {
            throw new InvalidOperationException("Download manager is shutting down.");
        }
        download.Id = _downloadRepository.Insert(download);
        _downloads.Add(download);

        return download;
    }

    public async Task StartDownloadAsync(DownloadItem download)
    {
        download.Status = DownloadStatus.Waiting;
        using var persistenceCts = new CancellationTokenSource();

        var persistenceTask = PersistProgressPeriodicallyAsync(download, persistenceCts.Token);
        try
        {
            download.Status = DownloadStatus.Downloading;
            _downloadRepository.Update(download);
            var progress = new Progress<DownloadProgress>(p =>
            {
                Console.Write(
                    $"\rProgress: {p.Percentage:F2}% | "
                        + $"Speed: {FileSizeFormatter.FormatBytes(p.Speed)}/s | "
                        + $"ETA: {p.Eta.TotalSeconds:F0}s"
                        + $"Task: {p.TaskRunning}"
                );
            });

            await _downloadService.DownloadAsync(
                download,
                download.CancellationTokenSource.Token,
                progress
            );
            download.Status = DownloadStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            if (download.Status == DownloadStatus.Paused)
            {
                // PauseDownload already set the status.
                return;
            }

            if (download.Status == DownloadStatus.Removed)
            {
                // RemoveDownload already handled everything.
                return;
            }

            download.Status = DownloadStatus.Cancelled;
            download.UpdatedAt = DateTime.UtcNow;

            _downloadRepository.Update(download);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error downloading {download.Destination}: {e.Message}");
            download.Status = DownloadStatus.Failed;
        }
        finally
        {
            persistenceCts.Cancel();

            try
            {
                await persistenceTask;
            }
            catch (OperationCanceledException) { }
            _downloadRepository.Update(download);
        }
    }

    private async Task PersistProgressPeriodicallyAsync(
        DownloadItem download,
        CancellationToken cancellationToken
    )
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                download.UpdatedAt = DateTime.UtcNow;

                _downloadRepository.Update(download);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the download finishes/cancels.
        }
    }

    public void CancelDownload(DownloadItem download)
    {
        download.CancellationTokenSource.Cancel();
        download.Status = DownloadStatus.Cancelled;
        _downloadRepository.Update(download);
    }

    public void PauseDownload(DownloadItem download)
    {
        if (download.Status != DownloadStatus.Downloading)
            return;
        download.Status = DownloadStatus.Paused;
        download.CancellationTokenSource.Cancel();
        _downloadRepository.Update(download);
    }

    public void ResumeDownload(DownloadItem download)
    {
        if (download.Status != DownloadStatus.Paused)
            return;

        download.CancellationTokenSource.Dispose();
        download.CancellationTokenSource = new CancellationTokenSource();

        _downloadRepository.Update(download);
        download.Status = DownloadStatus.Waiting;

        if (!DownloadQueue.Writer.TryWrite(download))
        {
            download.Status = DownloadStatus.Paused;
            _downloadRepository.Update(download);

            throw new InvalidOperationException(
                "Unable to queue download because the manager is shutting down."
            );
        }
    }

    public void CancelAll()
    {
        foreach (var download in _downloads)
        {
            download.Status = DownloadStatus.Cancelled;
            _downloadRepository.Update(download);
            download.CancellationTokenSource.Cancel();
        }
    }

    public void StartWorkers()
    {
        if (IsProcessing)
            return;
        IsProcessing = true;

        for (int i = 0; i < maxConcurrentDownloads; i++)
        {
            _workers.Add(ProcessQueueAsync());
        }
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var download in DownloadQueue.Reader.ReadAllAsync(ShutdownCts.Token))
        {
            if (download.Status == DownloadStatus.Removed)
            {
                continue;
            }
            if (download.CancellationTokenSource.IsCancellationRequested)
            {
                download.Status = DownloadStatus.Cancelled;
                _downloadRepository.Update(download);

                continue;
            }

            await StartDownloadAsync(download);
        }
    }

    public async Task ShutDown()
    {
        if (!IsProcessing)
            return;

        DownloadQueue.Writer.Complete();

        ShutdownCts.Cancel();

        foreach (var download in _downloads)
        {
            if (
                download.Status == DownloadStatus.Downloading
                || download.Status == DownloadStatus.Waiting
            )
            {
                download.Status = DownloadStatus.Cancelled;
                download.CancellationTokenSource.Cancel();
            }
            _downloadRepository.Update(download);
        }

        try
        {
            await Task.WhenAll(_workers);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("All tasks cancelled and app shutdown gracefully");
        }

        IsProcessing = false;
    }

    public void RetryDownload(DownloadItem download)
    {
        if (download.Status != DownloadStatus.Failed)
            return;

        download.CancellationTokenSource.Dispose();
        download.CancellationTokenSource = new CancellationTokenSource();

        download.Status = DownloadStatus.Waiting;
        _downloadRepository.Update(download);

        if (!DownloadQueue.Writer.TryWrite(download))
        {
            download.Status = DownloadStatus.Failed;
            throw new InvalidOperationException("Unable to queue download for retry.");
        }
    }

    public void LoadDownloads()
    {
        var downloads = _downloadRepository.GetAll();

        foreach (var download in downloads)
        {
            if (download.Status == DownloadStatus.Downloading)
            {
                download.Status = DownloadStatus.Waiting;
                download.UpdatedAt = DateTime.UtcNow;

                _downloadRepository.Update(download);
            }

            _downloads.Add(download);

            if (
                download.Status == DownloadStatus.Waiting
                || download.Status == DownloadStatus.Paused
            )
            {
                DownloadQueue.Writer.TryWrite(download);
            }
        }
    }

    public void RemoveDownload(DownloadItem download, bool deleteFile = true)
    {
        if (download.Status == DownloadStatus.Removed)
            return;

        // Stop the download if it is currently running
        if (download.Status == DownloadStatus.Downloading)
        {
            download.Status = DownloadStatus.Removed;

            download.CancellationTokenSource.Cancel();
        }
        else
        {
            download.Status = DownloadStatus.Removed;
        }

        // Delete the downloaded file if requested
        if (deleteFile && File.Exists(download.Destination))
        {
            try
            {
                File.Delete(download.Destination);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Could not delete file: {ex.Message}");
            }
        }

        // Remove from in-memory collection
        _downloads.Remove(download);

        // Remove from database
        _downloadRepository.Delete(download.Id);
    }
}
