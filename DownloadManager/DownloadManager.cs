using System.Collections.Concurrent;

public class DownloadManager(DownloadService downloadService, int maxConcurrentDownloads)
{
    private readonly DownloadService _downloadService = downloadService;

    private readonly Queue<DownloadItem> DownloadQueue = new();

    private readonly List<DownloadItem> _downloads = [];

    public IReadOnlyList<DownloadItem> Downloads => _downloads;


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

    public void CancelAll()
    {
        foreach (var download in _downloads)
        {
            download.CancellationTokenSource.Cancel();
        }
    }

    public async Task StartAllAsync()
    {
        var workers = new List<Task>();

        for (int i = 0; i < maxConcurrentDownloads; i++)
        {
            workers.Add(ProcessQueueAsync());
        }
        await Task.WhenAll(workers);
    }
    private async Task ProcessQueueAsync()
    {
        while (DownloadQueue.Count > 0)
        {
            DownloadItem download = DownloadQueue.Dequeue(); // cause indefinite behaviour in multithreading.. need to change

            if (download.CancellationTokenSource.IsCancellationRequested)
            {
                download.Status = DownloadStatus.Cancelled;
                continue;
            }

            await StartDownloadAsync(download);
        }
    }

}