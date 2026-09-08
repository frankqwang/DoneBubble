using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using DoneBubble.Models;
namespace DoneBubble.Services;
public sealed class DatabaseService
{
    private readonly string path;
    public DatabaseService(string? databasePath = null) => path = databasePath ?? Path.Combine(SettingsService.DataDirectory, "donebubble.db");
    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, DefaultTimeout = 2 }.ToString());
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE IF NOT EXISTS Records (Id INTEGER PRIMARY KEY AUTOINCREMENT, Content TEXT NOT NULL, CreatedAt TEXT NOT NULL); CREATE INDEX IF NOT EXISTS IX_Records_CreatedAt ON Records(CreatedAt);";
            command.ExecuteNonQuery();
            // Upgrade existing databases without changing any old records.
            bool hasCategory = false;
            command.CommandText = "PRAGMA table_info(Records)";
            using (var reader = command.ExecuteReader())
                while (reader.Read()) if (reader.GetString(1) == "Category") hasCategory = true;
            if (!hasCategory)
            {
                command.CommandText = "ALTER TABLE Records ADD COLUMN Category TEXT NULL";
                command.ExecuteNonQuery();
            }
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }
    // Store local wall-clock time: the recorded day stays the day the user experienced.
    public long Add(string content, DateTime? createdAt = null, string? category = null)
    {
        if (category != null && category != "轻" && category != "中" && category != "重") throw new ArgumentException("分类无效。");
        if (category == null && string.IsNullOrWhiteSpace(content)) throw new ArgumentException("内容不能为空。");
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Records(Content, CreatedAt, Category) VALUES ($content, $time, $category)";
        command.Parameters.AddWithValue("$content", content.Trim());
        command.Parameters.AddWithValue("$category", (object?)category ?? DBNull.Value);
        command.Parameters.AddWithValue("$time", (createdAt ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
        command.CommandText = "SELECT last_insert_rowid()";
        return (long)(command.ExecuteScalar() ?? 0L);
    }
    public List<RecordItem> GetToday(DateTime? now = null)
    {
        var day = (now ?? DateTime.Now).Date;
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Content, CreatedAt, Category FROM Records WHERE CreatedAt >= $start AND CreatedAt < $end ORDER BY CreatedAt DESC, Id DESC";
        command.Parameters.AddWithValue("$start", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$end", day.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        using var reader = command.ExecuteReader();
        var items = new List<RecordItem>();
        while (reader.Read()) items.Add(new(reader.GetInt64(0), reader.GetString(1), DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture), reader.IsDBNull(3) ? null : reader.GetString(3)));
        return items;
    }
    public void Delete(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Records WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }
    public void UpdateContent(long id, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Records SET Content = $content WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$content", content.Trim());
        command.ExecuteNonQuery();
    }
}
