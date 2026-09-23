public class FakeDownloadRepository : IDownloadRepository
{
    private readonly List<DownloadItem> _downloads = [];
    private int _nextId = 1;

    public int Insert(DownloadItem download)
    {
        download.Id = _nextId++;
        _downloads.Add(download);

        return download.Id;
    }

    public void Update(DownloadItem download)
    {
        var existing = _downloads.FirstOrDefault(d => d.Id == download.Id);

        if (existing == null)
            return;

        existing.Status = download.Status;
        existing.DownloadedBytes = download.DownloadedBytes;
        existing.TotalBytes = download.TotalBytes;
        existing.UpdatedAt = download.UpdatedAt;
    }

    public void Delete(int id)
    {
        _downloads.RemoveAll(d => d.Id == id);
    }

    public List<DownloadItem> GetAll()
    {
        return _downloads.ToList();
    }

    public DownloadItem? Find(int id)
    {
        return _downloads.FirstOrDefault(d => d.Id == id);
    }
}
