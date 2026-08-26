class Program
{
    static async Task Main()
    {
        var service = new DownloadService();

        await service.DownloadAsync(
            "https://cdn.truefilesize.com/test/test-1mb.bin",
            "downloaded-file",
            CancellationToken.None);
    }
}