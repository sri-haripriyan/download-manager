public class DownloadManager(DownloadService downloadService, int maxConcurrentDownloads)
{
    private readonly DownloadService _downloadService = downloadService;

    private readonly SemaphoreSlim _semaphore = new(maxConcurrentDownloads);

    private readonly List<DownloadItem> _downloads = [];

    public DownloadItem AddDownload(string url, string destination)
    {
        var download = new DownloadItem(
            url,
            destination);

        _downloads.Add(download);

        return download;
    }

    public async Task StartDownloadAsync(DownloadItem download)
    {
        download.Status = DownloadStatus.Waiting;
        await _semaphore.WaitAsync(download.CancellationTokenSource.Token);

        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                Console.Write(
                    $"\rProgress: {p.Percentage:F2}% | " +
                    $"Speed: {FileSizeFormatter.FormatBytes(p.Speed)}/s | " +
                    $"ETA: {p.Eta.TotalSeconds:F0}s" + $"Task: {p.TaskRunning}");
            });

            download.Status = DownloadStatus.Downloading;

            await _downloadService.DownloadAsync(
                download.Url,
                download.Destination,
                download.CancellationTokenSource.Token,
                progress);
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
        finally
        {
            _semaphore.Release();
        }
    }

    public void CancelDownload(DownloadItem download)
    {
        download.CancellationTokenSource.Cancel();
    }

    public void CancelAll()
    {
        foreach (var download in _downloads)
        {
            download.CancellationTokenSource.Cancel();
        }
    }
}