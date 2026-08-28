class Program
{
    static async Task Main()
    {
        var service = new DownloadService();
        var url = "https://httptest.pp.ua/range/1048576";
        var destination = "downloaded-file";
        using CancellationTokenSource tokenSource = new();
        Task downloadTask = service.DownloadAsync(url, destination, tokenSource.Token);

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
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine("Download cancelled");
        }
    }
}