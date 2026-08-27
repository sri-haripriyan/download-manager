class Program
{
    static async Task Main()
    {
        var service = new DownloadService();
        using CancellationTokenSource tokenSource = new();
        Task downloadTask = service.DownloadAsync(
            "https://cdn.truefilesize.com/test/test-1mb.bin",
            "downloaded-file",
            tokenSource.Token);

        Task listenForkey = Task.Run(() =>
        {
            while (true)
            {
                var c = Console.ReadKey();

                if (c.Key == ConsoleKey.C)
                {
                    tokenSource.Cancel();
                    break;
                }
            }
        });

        try
        {
            await downloadTask;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Download cancelled");
        }
    }
}