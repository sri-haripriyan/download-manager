class DownloadService
{
    private readonly HttpClient _httpClient = new();

    public async Task ShowProgress(Stream input, HttpResponseMessage response, FileStream output, CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[50000];
        long downloadedBytes = 0;
        long totalBytes = response.Content.Headers.ContentLength ?? -1;
        while (true)
        {
            int bytesRead = await input.ReadAsync(bytes, cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            await output.WriteAsync(bytes.AsMemory(0, bytesRead), cancellationToken);

            downloadedBytes += bytesRead;

            if (totalBytes > 0)
            {
                double progress = Math.Round((double)downloadedBytes / totalBytes * 100, 2);
                Console.WriteLine($"Download progress: {progress} %");
            }
        }
    }

    public async Task DownloadAsync(
        string url,
        string destination,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("Starting download...");

        using HttpResponseMessage response =
            await _httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        using Stream input =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using FileStream output =
            new(
                destination,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

        await ShowProgress(input, response, output, cancellationToken);

        Console.WriteLine("Download completed.");
    }
}