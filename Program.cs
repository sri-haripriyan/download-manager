class Program
{
    private string FormatBytes(double bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024 * 1024):F2} MB";

        if (bytes >= 1024)
            return $"{bytes / 1024:F2} KB";

        return $"{bytes:F2} B";
    }
    static async Task Main()
    {
        var service = new DownloadService();
        var url = "https://httptest.pp.ua/range/1048576";
        var destination = "downloaded-file";
        using CancellationTokenSource tokenSource = new();
        var progress = new Progress<DownloadProgress>(p =>
        {
            Console.Write(
    $"\rProgress: {p.Percentage:F2}% | " +
    $"Speed: {Util.FormatBytes(p.Speed)}/s | " +
    $"ETA: {p.Eta.TotalSeconds:F0}s");
        });
        Task downloadTask = service.DownloadAsync(url, destination, tokenSource.Token, progress);




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
            Console.WriteLine();
            Console.WriteLine("Download cancelled.");
        }
        catch (Exception e)
        {
            Console.WriteLine();
            Console.WriteLine($"Download failed: {e.Message}");
        }
    }
}