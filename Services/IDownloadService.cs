public interface IDownloadService
{
    Task DownloadAsync(
        DownloadItem download,
        CancellationToken cancellationToken,
        IProgress<DownloadProgress> progress
    );
}
