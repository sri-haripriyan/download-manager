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
        var destination1 = "downloaded-file1";
        var destination2 = "downloaded-file2";
        var destination3 = "downloaded-file3";

        SemaphoreSlim semaphoreSlim = new(2);

        using CancellationTokenSource tokenSource = new();
        var progress = new Progress<DownloadProgress>(p =>
        {
            Console.Write(
    $"\rProgress: {p.Percentage:F2}% | " +
    $"Speed: {Util.FormatBytes(p.Speed)}/s | " +
    $"ETA: {p.Eta.TotalSeconds:F0}s" + $"Task: {p.TaskRunning}");
        });


        Task task1 = service.DownloadAsync(url, destination1, tokenSource.Token, progress, semaphoreSlim);
        Task task2 = service.DownloadAsync(url, destination2, tokenSource.Token, progress, semaphoreSlim);
        Task task3 = service.DownloadAsync(url, destination3, tokenSource.Token, progress, semaphoreSlim);

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
            await Task.WhenAll(task1, task2, task3);
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