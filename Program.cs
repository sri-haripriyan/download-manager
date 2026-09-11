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
        DownloadManager manager = new(new DownloadService(), 2);
        var url = "https://httptest.pp.ua/range/1048576";

        manager.AddDownload(url, "test/file1");
        var download2 = manager.AddDownload(url, "test/file2");
        var download3 = manager.AddDownload(url, "test/file3");
        manager.AddDownload(url, "test/file4");
        var download5 = manager.AddDownload(url, "test/file5");
        // manager.AddDownload(url, "file6");
        // manager.AddDownload(url, "file7");
        // manager.AddDownload(url, "file8");

        Task keyListener = Task.Run(() =>
        {
            while (true)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.P)
                {
                    Console.WriteLine("Downloader id: Status");
                    foreach (var download in manager.Downloads)
                    {
                        Console.Write($"{download.Id}:\t {download.Status}\n");
                    }
                }
                else
                    if (key.Key == ConsoleKey.X)
                    {
                        manager.PauseDownload(download5);
                    }
                    else
                        if (key.Key == ConsoleKey.C)
                        {
                            manager.CancelDownload(download3);
                        }
            }
        });
        manager.StartWorkers();
        _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                Console.WriteLine("\n[System] 5 seconds elapsed. Adding dynamic download...");

                try
                {
                    manager.AddDownload(url, "test/file_delayed");
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"Could not add download: {ex.Message}");
                }
            });

        Console.WriteLine("Press ENTER to shut down...");
        Console.ReadLine();

        await manager.ShutDown();

    }
}