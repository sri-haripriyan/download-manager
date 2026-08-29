using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.VisualBasic;

class DownloadService
{
    private readonly HttpClient _httpClient = new();


    public async Task ShowProgress(Stream input, FileStream output, long existingBytes, long totalBytes, CancellationToken cancellationToken, IProgress<DownloadProgress> progress)
    {
        long sessionBytes = 0;
        Stopwatch stopwatch = Stopwatch.StartNew();
        byte[] bytes = new byte[50000];
        while (true)
        {
            int bytesRead = await input.ReadAsync(bytes, cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            await output.WriteAsync(bytes.AsMemory(0, bytesRead), cancellationToken);

            existingBytes += bytesRead;
            sessionBytes += bytesRead;
            double elapsedTime = stopwatch.Elapsed.TotalSeconds;
            double speed = sessionBytes / elapsedTime;

            if (totalBytes > 0 && speed > 0)
            {
                double percentage =
                    (double)existingBytes /
                    totalBytes * 100;

                long remainingBytes =
                    totalBytes - existingBytes;

                long etaSeconds = (long)Math.Ceiling(remainingBytes / speed);

                TimeSpan eta =
                    TimeSpan.FromSeconds(etaSeconds);

                progress.Report(new DownloadProgress
                {
                    TaskRunning = Thread.CurrentThread.Name,
                    Percentage = percentage,
                    Speed = speed,
                    Eta = eta
                });
            }
        }
    }

    public async Task DownloadAsync(
        string url,
        string destination,
        CancellationToken cancellationToken, IProgress<DownloadProgress> progress,
        SemaphoreSlim semaphoreSlim)
    {
        await semaphoreSlim.WaitAsync();
        try
        {
            int maxRetries = 3;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Console.WriteLine(
                    $"Download attempt {attempt + 1}");
                    await DownloadOnceAsync(
                    url,
                    destination,
                    cancellationToken,
                    progress);

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
                        Console.WriteLine(
                            "Maximum retry attempts reached.");

                        throw;
                    }

                    int delaySeconds =
                        (int)Math.Pow(2, attempt + 2);

                    Console.WriteLine(
                        $"Download failed: {ex.Message}");

                    Console.WriteLine(
                        $"Retrying in {delaySeconds} seconds...");

                    await Task.Delay(
                        TimeSpan.FromSeconds(delaySeconds),
                        cancellationToken);
                }
            }
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    public async Task DownloadOnceAsync(
        string url,
        string destination,
        CancellationToken cancellationToken, IProgress<DownloadProgress> progress)
    {


        long existingBytes = 0;
        if (File.Exists(destination))
        {
            existingBytes = new FileInfo(destination).Length;
        }
        if (existingBytes > 0)
            Console.WriteLine("Continuing download...");
        else
            Console.WriteLine("Starting download...");


        var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (existingBytes > 0)
            request.Headers.Range = new RangeHeaderValue(existingBytes, null);

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);


        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            var contentRange =
                response.Content.Headers.ContentRange;

            if (contentRange?.Length == null)
            {
                throw new Exception(
                    "Server did not provide the total file size.");
            }

            long totalBytes = contentRange.Length.Value;

            using Stream input =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken);

            using FileStream output =
                new(
                    destination,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.None);

            await ShowProgress(
                input,
                output,
                existingBytes,
                totalBytes,
                cancellationToken, progress);
        }
        else if (response.StatusCode == HttpStatusCode.OK)
        {
            Console.WriteLine(
                "Server does not support resume. Starting from beginning.");

            long totalBytes = response.Content.Headers.ContentLength ?? -1;

            using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);

            using FileStream output = new(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            await ShowProgress(input, output, 0, totalBytes, cancellationToken, progress);
        }
        else if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            var contentRange =
                response.Content.Headers.ContentRange;

            if (contentRange?.Length == existingBytes)
            {
                Console.WriteLine("File is already fully downloaded.");
                return;
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