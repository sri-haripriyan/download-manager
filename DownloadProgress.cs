public class DownloadProgress
{
    public string? TaskRunning { get; set; }
    public double Percentage { get; set; }
    public double Speed { get; set; }
    public TimeSpan Eta { get; set; }
}