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
        DownloadManager manager = new(new DownloadService(), 3);
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
                if (key.Key == ConsoleKey.X)
                {
                    manager.PauseDownload(download5);
                }
                if (key.Key == ConsoleKey.C)
                {
                    manager.CancelDownload(download3);
                }
            }
        });
        manager.StartWorkers();
        await manager.StopWorkers();

    }
}