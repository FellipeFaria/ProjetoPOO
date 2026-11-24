using System.Data;
using System.Globalization;
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

        ValidarConexaoTransacao(externalConnection, transaction);
        var ownsConnection = externalConnection is null && transaction is null;
        var connection = externalConnection ?? transaction?.Connection ?? _connectionFactory();

        if (connection.State != ConnectionState.Open)
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
                    Tipo = TipoMovimentacaoParaBanco(entidade.Tipo),
                    Motivo = entidade.Motivo ?? "Movimentação manual",
                    DataHora = FormatarDataHora(entidade.DataHora),
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

    public async Task<int> RegistrarComAtualizacaoEstoqueAsync(Movimentacao movimentacao)
    {
        ArgumentNullException.ThrowIfNull(movimentacao);

        using var connection = _connectionFactory();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            var atualizadas = movimentacao.Tipo switch
            {
                TipoMovimentacao.ENTRADA => await connection.ExecuteAsync(
                        """
                        UPDATE Produtos
                           SET Quantidade = Quantidade + @Quantidade
                         WHERE Id = @ProdutoId
                           AND Quantidade <= @Limite - @Quantidade;
                        """,
                        new
                        {
                            movimentacao.ProdutoId,
                            movimentacao.Quantidade,
                            Limite = int.MaxValue,
                        },
                        transaction
                ),
                TipoMovimentacao.SAIDA => await connection.ExecuteAsync(
                        """
                        UPDATE Produtos
                           SET Quantidade = Quantidade - @Quantidade
                         WHERE Id = @ProdutoId
                           AND Quantidade >= @Quantidade;
                        """,
                        new { movimentacao.ProdutoId, movimentacao.Quantidade },
                        transaction
                ),
                _ => throw new DomainException("Tipo de movimentação inválido."),
            };

            if (atualizadas == 0)
            {
                var estoqueAtual = await connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT Quantidade FROM Produtos WHERE Id = @ProdutoId;",
                    new { movimentacao.ProdutoId },
                    transaction
                );

                if (!estoqueAtual.HasValue)
                    throw new DomainException("Produto selecionado não encontrado.");

                if (movimentacao.Tipo == TipoMovimentacao.ENTRADA)
                    throw new DomainException("A quantidade de estoque excede o limite permitido.");

                throw new DomainException(
                    $"Estoque insuficiente. Disponível: {estoqueAtual.Value}, solicitado: {movimentacao.Quantidade}."
                );
            }

            const string sql =
                """
                INSERT INTO Movimentacoes (ProdutoId, VendaId, Quantidade, Tipo, Motivo, DataHora)
                VALUES (@ProdutoId, @VendaId, @Quantidade, @Tipo, @Motivo, @DataHora);
                SELECT last_insert_rowid();
                """;

            var id = await connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    movimentacao.ProdutoId,
                    movimentacao.VendaId,
                    movimentacao.Quantidade,
                    Tipo = TipoMovimentacaoParaBanco(movimentacao.Tipo),
                    Motivo = movimentacao.Motivo ?? "Movimentação manual",
                    DataHora = FormatarDataHora(movimentacao.DataHora),
                },
                transaction
            );

            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> AtualizarAsync(
        Movimentacao entidade,
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
                    Tipo = TipoMovimentacaoParaBanco(entidade.Tipo),
                    Motivo = entidade.Motivo ?? "Movimentação manual",
                    DataHora = FormatarDataHora(entidade.DataHora),
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

        ValidarConexaoTransacao(externalConnection, transaction);
        var ownsConnection = externalConnection is null && transaction is null;
        var connection = externalConnection ?? transaction?.Connection ?? _connectionFactory();

        if (connection.State != ConnectionState.Open)
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

        if (
            !DateTime.TryParse(
                row.DataHora,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var dataHoraConvertida
            )
        )
            throw new DomainException(
                $"Data/hora inválida no banco para a movimentação #{row.Id}: '{row.DataHora}'."
            );

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

    private static string FormatarDataHora(DateTime dataHora)
    {
        return dataHora.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string TipoMovimentacaoParaBanco(TipoMovimentacao tipo)
    {
        return tipo switch
        {
            TipoMovimentacao.ENTRADA => "Entrada",
            TipoMovimentacao.SAIDA => "Saida",
            _ => throw new DomainException("Tipo de movimentação inválido."),
        };
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