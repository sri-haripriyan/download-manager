using System.Collections.Concurrent;
using System.Threading.Channels;

public class DownloadManager(DownloadService downloadService, int maxConcurrentDownloads)
{
    private readonly DownloadService _downloadService = downloadService;

    private readonly Channel<DownloadItem> DownloadQueue = Channel.CreateUnbounded<DownloadItem>();

    private readonly List<DownloadItem> _downloads = [];

    public IReadOnlyList<DownloadItem> Downloads => _downloads;

    private bool IsProcessing = false;

    private readonly List<Task> _workers = [];


    public DownloadItem AddDownload(string url, string destination)
    {
        var download = new DownloadItem(
            url,
            destination);
        DownloadQueue.Writer.TryWrite(download);
        _downloads.Add(download);

        return download;
    }

    public async Task StartDownloadAsync(DownloadItem download)
    {
        download.Status = DownloadStatus.Waiting;

        try
        {
            download.Status = DownloadStatus.Downloading;
            var progress = new Progress<DownloadProgress>(p =>
            {
                Console.Write(
                    $"\rProgress: {p.Percentage:F2}% | " +
                    $"Speed: {FileSizeFormatter.FormatBytes(p.Speed)}/s | " +
                    $"ETA: {p.Eta.TotalSeconds:F0}s" + $"Task: {p.TaskRunning}");
            });

            await _downloadService.DownloadAsync(
                download.Url,
                download.Destination,
                download.CancellationTokenSource.Token,
                progress);
            download.Status = DownloadStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            if (download.Status != DownloadStatus.Paused)
                download.Status = DownloadStatus.Cancelled;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error downloading {download.Destination}: {e.Message}");
            download.Status = DownloadStatus.Failed;
        }

    }

    public void CancelDownload(DownloadItem download)
    {
        download.CancellationTokenSource.Cancel();
        if (download.Status == DownloadStatus.Waiting)
        {
            download.Status = DownloadStatus.Cancelled;
        }
    }

    public void PauseDownload(DownloadItem download)
    {
        if (download.Status != DownloadStatus.Downloading)
            return;
        download.Status = DownloadStatus.Paused;
        download.CancellationTokenSource.Cancel();
    }

    public void ResumeDownloadAsync(DownloadItem download)
    {
        if (download.Status != DownloadStatus.Paused)
            return;

        download.CancellationTokenSource.Dispose();
        download.CancellationTokenSource = new CancellationTokenSource();

        download.Status = DownloadStatus.Waiting;

        DownloadQueue.Writer.TryWrite(download);

    }

    public void CancelAll()
    {
        foreach (var download in _downloads)
        {
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
        await foreach (var download in DownloadQueue.Reader.ReadAllAsync())
        {

            if (download.CancellationTokenSource.IsCancellationRequested)
            {
                download.Status = DownloadStatus.Cancelled;
                continue;
            }

            await StartDownloadAsync(download);
        }
    }

    public async Task StopWorkers()
    {
        DownloadQueue.Writer.Complete();
        try
        {
            await Task.WhenAll(_workers);
        }
        finally
        {
            IsProcessing = false;
        }
    }

}