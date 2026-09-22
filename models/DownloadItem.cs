public class DownloadItem
{
    public int Id { get; set; }

    public string Url { get; }

    public string Destination { get; }

    public DownloadStatus Status { get; internal set; }

    public double Percentage { get; internal set; }

    public double Speed { get; internal set; }

    public TimeSpan Eta { get; internal set; }

    public long? TotalBytes { get; internal set; }
    public long DownloadedBytes { get; internal set; }

    internal CancellationTokenSource CancellationTokenSource { get; set; }

    public DownloadItem(string url, string destination)
    {
        Url = url;
        Destination = destination;

        CancellationTokenSource = new CancellationTokenSource();
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
