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

        var download1 = manager.AddDownload(url, "file1");
        var download2 = manager.AddDownload(url, "file2");

        Task task1 = manager.StartDownloadAsync(download1);
        Task task2 = manager.StartDownloadAsync(download2);

        Task keyListener = Task.Run(() =>
        {
            while (true)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.C)
                {
                    manager.CancelDownload(download1);
                    break;
                }
            }
        });

        await Task.WhenAll(task1, task2);
    }
}