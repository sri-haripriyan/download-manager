public class DownloadItem
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Url { get; }

    public string Destination { get; }

    public DownloadStatus Status { get; internal set; }

    public double Percentage { get; internal set; }

    public double Speed { get; internal set; }

    public TimeSpan Eta { get; internal set; }

    internal CancellationTokenSource CancellationTokenSource { get; set; }

    public DownloadItem(string url, string destination)
    {
        Url = url;
        Destination = destination;

        Status = DownloadStatus.Waiting;

        CancellationTokenSource = new CancellationTokenSource();
    }
}