public class DownloadProgress
{
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }
    public double Percentage { get; set; }
    public double Speed { get; set; }
    public TimeSpan Eta { get; set; }
}