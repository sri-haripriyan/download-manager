using System.Collections.Concurrent;

public class DownloadManager(DownloadService downloadService, int maxConcurrentDownloads)
{
    private readonly DownloadService _downloadService = downloadService;

    private readonly ConcurrentQueue<DownloadItem> DownloadQueue = new();

    private readonly List<DownloadItem> _downloads = [];

    public IReadOnlyList<DownloadItem> Downloads => _downloads;

    private bool IsProcessing = false;


    public DownloadItem AddDownload(string url, string destination)
    {
        var download = new DownloadItem(
            url,
            destination);
        DownloadQueue.Enqueue(download);
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

    public async Task ResumeDownloadAsync(DownloadItem download)
    {
        if (download.Status != DownloadStatus.Paused)
            return;

        download.CancellationTokenSource.Dispose();
        download.CancellationTokenSource = new CancellationTokenSource();

        download.Status = DownloadStatus.Waiting;

        DownloadQueue.Enqueue(download);

        if (!IsProcessing)
        {
            await StartAllAsync();
        }

    }

    public void CancelAll()
    {
        foreach (var download in _downloads)
        {
            download.CancellationTokenSource.Cancel();
        }
    }

    public async Task StartAllAsync()
    {
        if (IsProcessing)
            return;
        IsProcessing = true;

        var workers = new List<Task>();

        for (int i = 0; i < maxConcurrentDownloads; i++)
        {
            workers.Add(ProcessQueueAsync());
        }
        await Task.WhenAll(workers);
    }
    private async Task ProcessQueueAsync()
    {
        while (DownloadQueue.TryDequeue(out DownloadItem? download))
        {

            if (download.CancellationTokenSource.IsCancellationRequested)
            {
                download.Status = DownloadStatus.Cancelled;
                continue;
            }

            await StartDownloadAsync(download);
        }
    }

}