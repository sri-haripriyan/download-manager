using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

public class DownloadService
{
    private readonly HttpClient _httpClient = new();

    public async Task ShowProgress(Stream input, FileStream output, DownloadItem download, CancellationToken cancellationToken, IProgress<DownloadProgress> progress)
    {
        long sessionBytes = 0;
        Stopwatch stopwatch = Stopwatch.StartNew();

        byte[] buffer = new byte[50000];

        while (true)
        {
            int bytesRead = await input.ReadAsync(buffer, cancellationToken);

            if (bytesRead == 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

            // Update the DownloadItem
            download.DownloadedBytes += bytesRead;
            sessionBytes += bytesRead;

            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

            if (elapsedSeconds <= 0)
                continue;

            double speed = sessionBytes / elapsedSeconds;

            double percentage = 0;
            TimeSpan eta = TimeSpan.Zero;

            if (download.TotalBytes.HasValue &&
                download.TotalBytes.Value > 0)
            {
                percentage = (double)download.DownloadedBytes / download.TotalBytes.Value * 100;

                long remainingBytes = Math.Max(0, download.TotalBytes.Value - download.DownloadedBytes);

                if (speed > 0)
                {
                    eta = TimeSpan.FromSeconds(remainingBytes / speed);
                }
            }

            progress.Report(new DownloadProgress
            {
                TaskRunning = Thread.CurrentThread.Name,
                DownloadedBytes = download.DownloadedBytes,
                TotalBytes = download.TotalBytes ?? 0,
                Percentage = percentage,
                Speed = speed,
                Eta = eta
            });
        }
    }

    public async Task DownloadAsync(DownloadItem download, CancellationToken cancellationToken, IProgress<DownloadProgress> progress)
    {
        int maxRetries = 3;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"Download attempt {attempt + 1}");
                await DownloadOnceAsync(download, cancellationToken, progress);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    Console.WriteLine("Maximum retry attempts reached.");

                    throw;
                }

                int delaySeconds = (int)Math.Pow(2, attempt + 2);

                Console.WriteLine($"Download failed: {ex.Message}");

                Console.WriteLine($"Retrying in {delaySeconds} seconds...");

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }
    }

    public async Task DownloadOnceAsync(
        DownloadItem download,
        CancellationToken cancellationToken,
        IProgress<DownloadProgress> progress)
    {
        long existingBytes = 0;

        if (File.Exists(download.Destination))
        {
            existingBytes = new FileInfo(download.Destination).Length;
        }

        // Keep the model in sync with the actual file.
        download.DownloadedBytes = existingBytes;

        if (existingBytes > 0)
            Console.WriteLine("Continuing download...");
        else
            Console.WriteLine("Starting download...");

        using var request = new HttpRequestMessage(HttpMethod.Get, download.Url);

        if (existingBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingBytes, null);
        }

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            var contentRange = response.Content.Headers.ContentRange;

            if (contentRange?.Length == null)
            {
                throw new Exception("Server did not provide the total file size.");
            }

            long totalBytes = contentRange.Length.Value;

            download.TotalBytes = totalBytes;

            using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);

            using FileStream output = new(
                    download.Destination,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.None);

            await ShowProgress(
                input,
                output,
                download,
                cancellationToken,
                progress);
        }
        else if (response.StatusCode == HttpStatusCode.OK)
        {
            Console.WriteLine("Server does not support resume. Starting from beginning.");

            long totalBytes = response.Content.Headers.ContentLength ?? -1;

            download.TotalBytes = totalBytes > 0 ? totalBytes : null;

            download.DownloadedBytes = 0;

            using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);

            using FileStream output = new(
                    download.Destination,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None);

            await ShowProgress(
                input,
                output,
                download,
                cancellationToken,
                progress);
        }
        else if (
            response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            var contentRange =
                response.Content.Headers.ContentRange;

            // Server tells us the actual total size through:
            // Content-Range: bytes */1048576
            if (contentRange?.Length != null)
            {
                download.TotalBytes =
                    contentRange.Length.Value;

                if (download.DownloadedBytes ==
                    download.TotalBytes.Value)
                {
                    Console.WriteLine(
                        "File is already fully downloaded.");

                    return;
                }
            }

            throw new Exception(
                "Requested range is not satisfiable.");
        }
        else
        {
            response.EnsureSuccessStatusCode();
        }

        Console.WriteLine();
        Console.WriteLine("Download completed");
    }
}