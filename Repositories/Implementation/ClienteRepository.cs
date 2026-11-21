using System.Data;
using Dapper;
using ProjetoPOO.Models;
using ProjetoPOO.Repositories.Interfaces;

namespace ProjetoPOO.Repositories.Implementation;

public class ClienteRepository(Func<IDbConnection> connectionFactory) : IClienteRepository
{
    private readonly Func<IDbConnection> _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<IEnumerable<Cliente>> ObterTodosAsync()
    {
        using var connection = _connectionFactory();
        connection.Open();

        var rows = await connection.QueryAsync<ClienteRow>(
            "SELECT Id, Nome, CPF, Email, Telefone FROM Clientes ORDER BY Nome;"
        );

        return rows.Select(MapearParaDominio);
    }

    public async Task<Cliente?> ObterPorIdAsync(int id)
    {
        if (id <= 0)
            return null;

        using var connection = _connectionFactory();
        connection.Open();

        var row = await connection.QueryFirstOrDefaultAsync<ClienteRow>(
            "SELECT Id, Nome, CPF, Email, Telefone FROM Clientes WHERE Id = @Id;",
            new { Id = id }
        );

        return row is null ? null : MapearParaDominio(row);
    }

    public async Task<Cliente?> ObterPorCpfAsync(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return null;

        using var connection = _connectionFactory();
        connection.Open();

        var row = await connection.QueryFirstOrDefaultAsync<ClienteRow>(
            "SELECT Id, Nome, CPF, Email, Telefone FROM Clientes WHERE CPF = @Cpf;",
            new { Cpf = cpf.Trim() }
        );

        return row is null ? null : MapearParaDominio(row);
    }

    public async Task<int> AdicionarAsync(
        Cliente entidade,
        IDbConnection? externalConnection = null,
        IDbTransaction? transaction = null
    )
    {
        ArgumentNullException.ThrowIfNull(entidade);

        var ownsConnection = externalConnection is null;
        var connection = externalConnection ?? _connectionFactory();

        if (ownsConnection && connection.State != ConnectionState.Open)
            connection.Open();

        try
        {
            const string sql =
                @"
                INSERT INTO Clientes (Nome, CPF, Email, Telefone)
                VALUES (@Nome, @Cpf, @Email, @Telefone);
                SELECT last_insert_rowid();";

            return await connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    entidade.Nome,
                    entidade.Cpf,
                    entidade.Email,
                    entidade.Telefone,
                },
                transaction
            );
        }
        finally
        {
            if (ownsConnection)
                connection.Dispose();
        }
    }

    public async Task<bool> AtualizarAsync(
        Cliente entidade,
        IDbConnection? externalConnection = null,
        IDbTransaction? transaction = null
    )
    {
        ArgumentNullException.ThrowIfNull(entidade);

        var ownsConnection = externalConnection is null;
        var connection = externalConnection ?? _connectionFactory();

        if (ownsConnection && connection.State != ConnectionState.Open)
            connection.Open();

        try
        {
            var afetadas = await connection.ExecuteAsync(
                @"
                UPDATE Clientes
                   SET Nome = @Nome,
                       CPF = @Cpf,
                       Email = @Email,
                       Telefone = @Telefone
                 WHERE Id = @Id;",
                new
                {
                    entidade.Id,
                    entidade.Nome,
                    entidade.Cpf,
                    entidade.Email,
                    entidade.Telefone,
                },
                transaction
            );

            return afetadas > 0;
        }
        finally
        {
            if (ownsConnection)
                connection.Dispose();
        }
    }

    public async Task<bool> RemoverAsync(
        int id,
        IDbConnection? externalConnection = null,
        IDbTransaction? transaction = null
    )
    {
        if (id <= 0)
            return false;

        var ownsConnection = externalConnection is null;
        var connection = externalConnection ?? _connectionFactory();

        if (ownsConnection && connection.State != ConnectionState.Open)
            connection.Open();

        try
        {
            var afetadas = await connection.ExecuteAsync(
                "DELETE FROM Clientes WHERE Id = @Id;",
                new { Id = id },
                transaction
            );

            return afetadas > 0;
        }
        finally
        {
            if (ownsConnection)
                connection.Dispose();
        }
    }

    private static Cliente MapearParaDominio(ClienteRow row)
    {
        return new Cliente(row.Id, row.Nome, row.CPF, row.Email, row.Telefone);
    }

    private sealed class ClienteRow
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? CPF { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
    }
}