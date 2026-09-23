public class FakeDownloadService : IDownloadService
{
    public bool DownloadCalled { get; private set; }

    public int DownloadCallCount { get; private set; }

    public bool ShouldFail { get; set; }

    public bool BlockDownload { get; set; }

    public async Task DownloadAsync(
        DownloadItem download,
        CancellationToken cancellationToken,
        IProgress<DownloadProgress> progress
    )
    {
        DownloadCalled = true;
        DownloadCallCount++;

        if (ShouldFail)
            throw new Exception("Simulated download failure.");

        if (BlockDownload)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return;
        }

        download.TotalBytes = 1000;
        download.DownloadedBytes = 1000;

        progress.Report(
            new DownloadProgress
            {
                Percentage = 100,
                Speed = 1000,
                Eta = TimeSpan.Zero,
            }
        );
    }
}
