using Dapper;
using Microsoft.Data.Sqlite;

namespace ProjetoPOO.Database;

public class DbInitializer(string connectionString, string scriptPath)
{
    private readonly string _connectionString = connectionString;
    private readonly string _scriptPath = scriptPath;

    public void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        connection.Execute("PRAGMA foreign_keys = ON");

        var tabelaExiste = connection.ExecuteScalar<int>(
            """
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type = 'table' AND name = 'Produtos';
            """
        );

        if (tabelaExiste > 0)
            return;

        if (!File.Exists(_scriptPath))
            throw new FileNotFoundException($"Script SQL não encontrado em {_scriptPath}");

        var sqlScript = File.ReadAllText(_scriptPath);
        connection.Execute(sqlScript);
    }
}
