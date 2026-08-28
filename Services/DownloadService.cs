using System.Net;
using System.Net.Http.Headers;

class DownloadService
{
    private readonly HttpClient _httpClient = new();

    public async Task ShowProgress(Stream input, FileStream output, long existingBytes, long totalBytes, CancellationToken cancellationToken)
    {
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

            if (totalBytes > 0)
            {
                double progress = Math.Round((double)existingBytes / totalBytes * 100, 2);
                Console.WriteLine($"Download progress: {progress} %");
            }
        }
    }

    public async Task DownloadAsync(
        string url,
        string destination,
        CancellationToken cancellationToken)
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

                cancellationToken);
        }
        else if (response.StatusCode == HttpStatusCode.OK)
        {
            Console.WriteLine(
                "Server does not support resume. Starting from beginning.");

            long totalBytes = response.Content.Headers.ContentLength ?? -1;

            using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);

            using FileStream output = new(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            await ShowProgress(input, output, 0, totalBytes, cancellationToken);
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
        Console.WriteLine("Download completed");
    }
}