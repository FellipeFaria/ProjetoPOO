using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using ProjetoPOO.Exceptions;
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

        ValidarConexaoTransacao(externalConnection, transaction);
        var ownsConnection = externalConnection is null && transaction is null;
        var connection = externalConnection ?? transaction?.Connection ?? _connectionFactory();

        if (connection.State != ConnectionState.Open)
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
        catch (SqliteException ex) when (
            ex.SqliteExtendedErrorCode == 2067
            && ex.Message.Contains("Clientes.CPF", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new DomainException("Já existe um cliente cadastrado com este CPF.", ex);
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

        ValidarConexaoTransacao(externalConnection, transaction);
        var ownsConnection = externalConnection is null && transaction is null;
        var connection = externalConnection ?? transaction?.Connection ?? _connectionFactory();

        if (connection.State != ConnectionState.Open)
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
        catch (SqliteException ex) when (
            ex.SqliteExtendedErrorCode == 2067
            && ex.Message.Contains("Clientes.CPF", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new DomainException("Já existe um cliente cadastrado com este CPF.", ex);
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

        ValidarConexaoTransacao(externalConnection, transaction);
        var ownsConnection = externalConnection is null && transaction is null;
        var connection = externalConnection ?? transaction?.Connection ?? _connectionFactory();

        if (connection.State != ConnectionState.Open)
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

    private static void ValidarConexaoTransacao(
        IDbConnection? externalConnection,
        IDbTransaction? transaction
    )
    {
        if (transaction is not null && transaction.Connection is null)
            throw new ArgumentException("A transação não está associada a uma conexão.", nameof(transaction));

        if (
            externalConnection is not null
            && transaction is not null
            && !ReferenceEquals(transaction.Connection, externalConnection)
        )
            throw new ArgumentException(
                "A transação deve pertencer à conexão informada.",
                nameof(transaction)
            );
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