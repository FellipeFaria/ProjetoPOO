using System.Data;
using Dapper;
using ProjetoPOO.Exceptions;
using ProjetoPOO.Models;
using ProjetoPOO.Models.Enums;
using ProjetoPOO.Repositories.Interfaces;

namespace ProjetoPOO.Repositories.Implementation;

public class MovimentacaoRepository(Func<IDbConnection> connectionFactory) : IMovimentacaoRepository
{
    private readonly Func<IDbConnection> _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<IEnumerable<Movimentacao>> ObterTodosAsync()
    {
        using var connection = _connectionFactory();
        connection.Open();

        var rows = await connection.QueryAsync<MovimentacaoRow>(
            @"
            SELECT Id, ProdutoId, VendaId, Quantidade, Tipo, Motivo, DataHora
              FROM Movimentacoes
             ORDER BY DataHora DESC, Id DESC;"
        );

        return rows.Select(MapearParaDominio);
    }

    public async Task<Movimentacao?> ObterPorIdAsync(int id)
    {
        if (id <= 0)
            return null;

        using var connection = _connectionFactory();
        connection.Open();

        var row = await connection.QueryFirstOrDefaultAsync<MovimentacaoRow>(
            @"
            SELECT Id, ProdutoId, VendaId, Quantidade, Tipo, Motivo, DataHora
              FROM Movimentacoes
             WHERE Id = @Id;",
            new { Id = id }
        );

        return row is null ? null : MapearParaDominio(row);
    }

    public async Task<IEnumerable<Movimentacao>> ObterPorProdutoIdAsync(int produtoId)
    {
        if (produtoId <= 0)
            return Enumerable.Empty<Movimentacao>();

        using var connection = _connectionFactory();
        connection.Open();

        var rows = await connection.QueryAsync<MovimentacaoRow>(
            @"
            SELECT Id, ProdutoId, VendaId, Quantidade, Tipo, Motivo, DataHora
              FROM Movimentacoes
             WHERE ProdutoId = @ProdutoId
             ORDER BY DataHora DESC, Id DESC;",
            new { ProdutoId = produtoId }
        );

        return rows.Select(MapearParaDominio);
    }

    public async Task<IEnumerable<Movimentacao>> ObterPorVendaIdAsync(int vendaId)
    {
        if (vendaId <= 0)
            return Enumerable.Empty<Movimentacao>();

        using var connection = _connectionFactory();
        connection.Open();

        var rows = await connection.QueryAsync<MovimentacaoRow>(
            @"
            SELECT Id, ProdutoId, VendaId, Quantidade, Tipo, Motivo, DataHora
              FROM Movimentacoes
             WHERE VendaId = @VendaId
             ORDER BY Id ASC;",
            new { VendaId = vendaId }
        );

        return rows.Select(MapearParaDominio);
    }

    public async Task<int> AdicionarAsync(
        Movimentacao entidade,
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
                INSERT INTO Movimentacoes (ProdutoId, VendaId, Quantidade, Tipo, Motivo, DataHora)
                VALUES (@ProdutoId, @VendaId, @Quantidade, @Tipo, @Motivo, @DataHora);
                SELECT last_insert_rowid();";

            return await connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    entidade.ProdutoId,
                    entidade.VendaId,
                    entidade.Quantidade,
                    Tipo = entidade.Tipo.ToString(),
                    entidade.Motivo,
                    DataHora = entidade.DataHora.ToString("yyyy-MM-dd HH:mm:ss"),
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
        Movimentacao entidade,
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
                UPDATE Movimentacoes
                   SET ProdutoId = @ProdutoId,
                       VendaId = @VendaId,
                       Quantidade = @Quantidade,
                       Tipo = @Tipo,
                       Motivo = @Motivo,
                       DataHora = @DataHora
                 WHERE Id = @Id;",
                new
                {
                    entidade.Id,
                    entidade.ProdutoId,
                    entidade.VendaId,
                    entidade.Quantidade,
                    Tipo = entidade.Tipo.ToString(),
                    entidade.Motivo,
                    DataHora = entidade.DataHora.ToString("yyyy-MM-dd HH:mm:ss"),
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
                "DELETE FROM Movimentacoes WHERE Id = @Id;",
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

    private static Movimentacao MapearParaDominio(MovimentacaoRow row)
    {
        if (!Enum.TryParse<TipoMovimentacao>(row.Tipo, ignoreCase: true, out var tipoEnum))
            throw new DomainException($"Tipo de movimentação inválido no banco: '{row.Tipo}'.");

        DateTime? dataHoraConvertida = DateTime.TryParse(row.DataHora, out var parsedDate)
            ? parsedDate
            : null;

        return new Movimentacao(
            row.Id,
            row.ProdutoId,
            row.Quantidade,
            tipoEnum,
            row.Motivo,
            row.VendaId,
            dataHoraConvertida
        );
    }

    private sealed class MovimentacaoRow
    {
        public int Id { get; set; }
        public int ProdutoId { get; set; }
        public int? VendaId { get; set; }
        public int Quantidade { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string? Motivo { get; set; }
        public string DataHora { get; set; } = string.Empty;
    }
}