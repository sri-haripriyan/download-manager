class DownloadService
{
    private readonly HttpClient _httpClient = new();

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

        await input.CopyToAsync(
            output,
            cancellationToken);

        Console.WriteLine("Download completed.");
    }
}