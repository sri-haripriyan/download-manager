using Microsoft.Data.Sqlite;
public class DownloadRepository
{
    private readonly string _connectionString;
    public DownloadRepository(string connectionString = "Data Source=downloads.db")
    {
        _connectionString = connectionString;
        Initialize();
    }
    public void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
        CREATE TABLE IF NOT EXISTS Downloads (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Url TEXT NOT NULL,
            Destination TEXT NOT NULL,
            Status TEXT NOT NULL,
            DownloadedBytes INTEGER NOT NULL DEFAULT 0,
            TotalBytes INTEGER NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );
        CREATE TRIGGER IF NOT EXISTS UpdateDownloadsUpdatedAt
        AFTER UPDATE ON Downloads
        FOR EACH ROW
        BEGIN
            UPDATE Downloads 
            SET UpdatedAt = STRFTIME('%Y-%m-%dT%H:%M:%f', 'NOW', 'localtime')
            WHERE Id = OLD.Id;
        END;
        """;

        command.ExecuteNonQuery();
    }

    public int Insert(DownloadItem download)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
    INSERT INTO Downloads
    (
        Url,
        Destination,
        Status,
        DownloadedBytes,
        TotalBytes,
        CreatedAt,
        UpdatedAt
    )
    VALUES
    (
        @url,
        @destination,
        @status,
        @downloadedBytes,
        @totalBytes,
        @createdAt,
        @updatedAt
    );
    SELECT last_insert_rowid();
    """;

        command.Parameters.AddWithValue("@url", download.Url);
        command.Parameters.AddWithValue("@destination", download.Destination);
        command.Parameters.AddWithValue("@status", download.Status.ToString());
        command.Parameters.AddWithValue("@downloadedBytes", download.DownloadedBytes);

        command.Parameters.AddWithValue(
            "@totalBytes",
            download.TotalBytes.HasValue
                ? download.TotalBytes.Value
                : DBNull.Value);

        command.Parameters.AddWithValue("@createdAt", download.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@updatedAt", download.UpdatedAt.ToString("O"));

        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void Update(DownloadItem download)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
        UPDATE Downloads
        SET
            Status = $status,
            DownloadedBytes = $downloadedBytes,
            TotalBytes = $totalBytes,
            UpdatedAt = $updatedAt
        WHERE Id = $id;
        """;

        command.Parameters.AddWithValue("$id", download.Id);
        command.Parameters.AddWithValue("$status", download.Status.ToString());
        command.Parameters.AddWithValue("$downloadedBytes", download.DownloadedBytes);

        command.Parameters.AddWithValue(
            "$totalBytes",
            download.TotalBytes.HasValue
                ? download.TotalBytes.Value
                : DBNull.Value);

        command.Parameters.AddWithValue(
            "$updatedAt",
            download.UpdatedAt.ToString("O"));

        command.ExecuteNonQuery();
    }
}