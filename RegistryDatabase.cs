using Microsoft.Data.Sqlite;
using System.IO;

namespace OpenFaceRegistry;

public sealed class RegistryDatabase
{
    private readonly string _connectionString;

    public RegistryDatabase()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenFaceRegistry");
        Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "registry.db") }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS People (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FullName TEXT NOT NULL,
                Phone TEXT NOT NULL DEFAULT '',
                Email TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE TABLE IF NOT EXISTS FaceSamples (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PersonId INTEGER NOT NULL REFERENCES People(Id) ON DELETE CASCADE,
                ImageBytes BLOB NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    public List<PersonRecord> Search(string search = "")
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.Id, p.FullName, p.Phone, p.Email, p.Notes, COUNT(s.Id)
            FROM People p LEFT JOIN FaceSamples s ON s.PersonId = p.Id
            WHERE $search = '' OR p.FullName LIKE $pattern OR p.Phone LIKE $pattern OR p.Email LIKE $pattern
            GROUP BY p.Id ORDER BY p.FullName COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$search", search);
        command.Parameters.AddWithValue("$pattern", $"%{search}%");
        using var reader = command.ExecuteReader();
        var results = new List<PersonRecord>();
        while (reader.Read()) results.Add(new PersonRecord { Id = reader.GetInt64(0), FullName = reader.GetString(1), Phone = reader.GetString(2), Email = reader.GetString(3), Notes = reader.GetString(4), SampleCount = reader.GetInt32(5) });
        return results;
    }

    public PersonRecord? Get(long id) => Search().FirstOrDefault(p => p.Id == id);

    public long Save(PersonRecord person, IReadOnlyList<byte[]> newSamples)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            if (person.Id == 0)
            {
                command.CommandText = "INSERT INTO People(FullName,Phone,Email,Notes) VALUES($name,$phone,$email,$notes); SELECT last_insert_rowid();";
            }
            else
            {
                command.CommandText = "UPDATE People SET FullName=$name,Phone=$phone,Email=$email,Notes=$notes WHERE Id=$id; SELECT $id;";
                command.Parameters.AddWithValue("$id", person.Id);
            }
            command.Parameters.AddWithValue("$name", person.FullName);
            command.Parameters.AddWithValue("$phone", person.Phone);
            command.Parameters.AddWithValue("$email", person.Email);
            command.Parameters.AddWithValue("$notes", person.Notes);
            person.Id = (long)command.ExecuteScalar()!;
        }
        foreach (var bytes in newSamples)
        {
            using var sample = connection.CreateCommand();
            sample.Transaction = transaction;
            sample.CommandText = "INSERT INTO FaceSamples(PersonId,ImageBytes) VALUES($id,$bytes);";
            sample.Parameters.AddWithValue("$id", person.Id);
            sample.Parameters.Add("$bytes", SqliteType.Blob).Value = bytes;
            sample.ExecuteNonQuery();
        }
        transaction.Commit();
        return person.Id;
    }

    public void Delete(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM People WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public List<FaceSample> GetSamples()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT PersonId,ImageBytes FROM FaceSamples ORDER BY PersonId;";
        using var reader = command.ExecuteReader();
        var results = new List<FaceSample>();
        while (reader.Read()) results.Add(new FaceSample(reader.GetInt64(0), (byte[])reader[1]));
        return results;
    }
}
