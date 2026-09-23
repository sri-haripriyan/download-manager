public interface IDownloadRepository
{
    int Insert(DownloadItem download);
    void Update(DownloadItem download);
    void Delete(int id);
    List<DownloadItem> GetAll();
}
