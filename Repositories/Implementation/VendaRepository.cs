using System.Data;
using Dapper;
using ProjetoPOO.Exceptions;
using ProjetoPOO.Models;
using ProjetoPOO.Models.Enums;
using ProjetoPOO.Repositories.Interfaces;

namespace ProjetoPOO.Repositories.Implementation;

public class VendaRepository(Func<IDbConnection> connectionFactory) : IVendaRepository
{
    private readonly Func<IDbConnection> _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<IEnumerable<Venda>> ObterTodosAsync()
    {
        using var connection = _connectionFactory();
        connection.Open();

        var vendaRows = (
            await connection.QueryAsync<VendaRow>(
                @"
            SELECT Id, ClienteId, DataHora, FormaPagamento, Status
              FROM Vendas
             ORDER BY DataHora DESC, Id DESC;"
            )
        ).ToList();

        var resultado = new List<Venda>();

        foreach (var vRow in vendaRows)
        {
            var itens = await CarregarItensDaVendaAsync(connection, vRow.Id, transaction: null);
            resultado.Add(MapearVendaParaDominio(vRow, itens));
        }

        return resultado;
    }

    public async Task<Venda?> ObterPorIdAsync(int id)
    {
        if (id <= 0)
            return null;

        using var connection = _connectionFactory();
        connection.Open();

        var vRow = await connection.QueryFirstOrDefaultAsync<VendaRow>(
            @"
            SELECT Id, ClienteId, DataHora, FormaPagamento, Status
              FROM Vendas
             WHERE Id = @Id;",
            new { Id = id }
        );

        if (vRow is null)
            return null;

        var itens = await CarregarItensDaVendaAsync(connection, vRow.Id, transaction: null);
        return MapearVendaParaDominio(vRow, itens);
    }

    public Task<int> AdicionarAsync(
        Venda entidade,
        IDbConnection? externalConnection = null,
        IDbTransaction? transaction = null
    )
    {
        ArgumentNullException.ThrowIfNull(entidade);

        if (externalConnection is not null && transaction is not null)
            return InserirVendaEItensAsync(entidade, externalConnection, transaction);

        return RegistrarVendaCompletaAsync(entidade);
    }

    public async Task<int> RegistrarVendaCompletaAsync(Venda venda)
    {
        ArgumentNullException.ThrowIfNull(venda);

        if (venda.Itens.Count == 0)
            throw new DomainException("Não é possível registrar uma venda sem itens.");

        using var connection = _connectionFactory();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            var vendaId = await InserirVendaEItensAsync(venda, connection, transaction);

            if (venda.Status == StatusVenda.CONCLUIDA)
            {
                foreach (var item in venda.Itens)
                {
                    var estoqueAtual = await connection.ExecuteScalarAsync<int?>(
                        "SELECT Quantidade FROM Produtos WHERE Id = @ProdutoId;",
                        new { item.ProdutoId },
                        transaction
                    );

                    if (!estoqueAtual.HasValue)
                        throw new DomainException(
                            $"Produto #{item.ProdutoId} não encontrado no estoque."
                        );

                    if (estoqueAtual.Value < item.Quantidade)
                        throw new DomainException(
                            $"Estoque insuficiente para o produto #{item.ProdutoId}. Disponível: {estoqueAtual.Value}, solicitado: {item.Quantidade}."
                        );

                    await connection.ExecuteAsync(
                        @"
                        UPDATE Produtos
                           SET Quantidade = Quantidade - @Quantidade
                         WHERE Id = @ProdutoId;",
                        new { item.Quantidade, item.ProdutoId },
                        transaction
                    );

                    await connection.ExecuteAsync(
                        @"
                        INSERT INTO Movimentacoes (ProdutoId, VendaId, Quantidade, Tipo, Motivo, DataHora)
                        VALUES (@ProdutoId, @VendaId, @Quantidade, @Tipo, @Motivo, @DataHora);",
                        new
                        {
                            item.ProdutoId,
                            VendaId = vendaId,
                            item.Quantidade,
                            Tipo = TipoMovimentacao.SAIDA.ToString(),
                            Motivo = $"Venda #{vendaId}",
                            DataHora = venda.DataHora.ToString("yyyy-MM-dd HH:mm:ss"),
                        },
                        transaction
                    );
                }
            }

            transaction.Commit();
            return vendaId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> AtualizarAsync(
        Venda entidade,
        IDbConnection? externalConnection = null,
        IDbTransaction? transaction = null
    )
    {
        ArgumentNullException.ThrowIfNull(entidade);

        var ownsConnection = externalConnection is null;
        var connection = externalConnection ?? _connectionFactory();
        var ownsTransaction = ownsConnection && transaction is null;

        if (ownsConnection && connection.State != ConnectionState.Open)
            connection.Open();

        var tx = transaction ?? (ownsTransaction ? connection.BeginTransaction() : null);

        try
        {
            var afetadas = await connection.ExecuteAsync(
                @"
                UPDATE Vendas
                   SET ClienteId = @ClienteId,
                       DataHora = @DataHora,
                       FormaPagamento = @FormaPagamento,
                       Status = @Status
                 WHERE Id = @Id;",
                new
                {
                    entidade.Id,
                    entidade.ClienteId,
                    DataHora = entidade.DataHora.ToString("yyyy-MM-dd HH:mm:ss"),
                    FormaPagamento = entidade.FormaPagamento.ToString(),
                    Status = entidade.Status.ToString(),
                },
                tx
            );

            if (afetadas == 0)
            {
                tx?.Rollback();
                return false;
            }

            await connection.ExecuteAsync(
                "DELETE FROM ItensVenda WHERE VendaId = @VendaId;",
                new { VendaId = entidade.Id },
                tx
            );

            foreach (var item in entidade.Itens)
            {
                await connection.ExecuteAsync(
                    @"
                    INSERT INTO ItensVenda (VendaId, ProdutoId, Quantidade, PrecoUnitario)
                    VALUES (@VendaId, @ProdutoId, @Quantidade, @PrecoUnitario);",
                    new
                    {
                        VendaId = entidade.Id,
                        item.ProdutoId,
                        item.Quantidade,
                        item.PrecoUnitario,
                    },
                    tx
                );
            }

            if (ownsTransaction)
                tx?.Commit();

            return true;
        }
        catch
        {
            if (ownsTransaction)
                tx?.Rollback();
            throw;
        }
        finally
        {
            if (ownsTransaction)
                tx?.Dispose();

            if (ownsConnection)
                connection.Dispose();
        }
    }

    public async Task<bool> AtualizarStatusAsync(int vendaId, StatusVenda novoStatus)
    {
        if (vendaId <= 0)
            return false;

        using var connection = _connectionFactory();
        connection.Open();

        var afetadas = await connection.ExecuteAsync(
            @"
            UPDATE Vendas
               SET Status = @Status
             WHERE Id = @Id;",
            new { Id = vendaId, Status = novoStatus.ToString() }
        );

        return afetadas > 0;
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
        var ownsTransaction = ownsConnection && transaction is null;

        if (ownsConnection && connection.State != ConnectionState.Open)
            connection.Open();

        var tx = transaction ?? (ownsTransaction ? connection.BeginTransaction() : null);

        try
        {
            await connection.ExecuteAsync(
                "DELETE FROM ItensVenda WHERE VendaId = @Id;",
                new { Id = id },
                tx
            );

            var afetadas = await connection.ExecuteAsync(
                "DELETE FROM Vendas WHERE Id = @Id;",
                new { Id = id },
                tx
            );

            if (ownsTransaction)
                tx?.Commit();

            return afetadas > 0;
        }
        catch
        {
            if (ownsTransaction)
                tx?.Rollback();
            throw;
        }
        finally
        {
            if (ownsTransaction)
                tx?.Dispose();

            if (ownsConnection)
                connection.Dispose();
        }
    }

    private static async Task<int> InserirVendaEItensAsync(
        Venda venda,
        IDbConnection connection,
        IDbTransaction transaction
    )
    {
        const string sqlVenda =
            @"
            INSERT INTO Vendas (ClienteId, DataHora, FormaPagamento, Status)
            VALUES (@ClienteId, @DataHora, @FormaPagamento, @Status);
            SELECT last_insert_rowid();";

        var vendaId = await connection.ExecuteScalarAsync<int>(
            sqlVenda,
            new
            {
                venda.ClienteId,
                DataHora = venda.DataHora.ToString("yyyy-MM-dd HH:mm:ss"),
                FormaPagamento = venda.FormaPagamento.ToString(),
                Status = venda.Status.ToString(),
            },
            transaction
        );

        foreach (var item in venda.Itens)
        {
            await connection.ExecuteAsync(
                @"
                INSERT INTO ItensVenda (VendaId, ProdutoId, Quantidade, PrecoUnitario)
                VALUES (@VendaId, @ProdutoId, @Quantidade, @PrecoUnitario);",
                new
                {
                    VendaId = vendaId,
                    item.ProdutoId,
                    item.Quantidade,
                    item.PrecoUnitario,
                },
                transaction
            );
        }

        return vendaId;
    }

    private static async Task<List<ItemVenda>> CarregarItensDaVendaAsync(
        IDbConnection connection,
        int vendaId,
        IDbTransaction? transaction
    )
    {
        var itemRows = await connection.QueryAsync<ItemVendaRow>(
            @"
            SELECT Id, VendaId, ProdutoId, Quantidade, PrecoUnitario
              FROM ItensVenda
             WHERE VendaId = @VendaId
             ORDER BY Id ASC;",
            new { VendaId = vendaId },
            transaction
        );

        return
        [
            .. itemRows.Select(r => new ItemVenda(
                r.Id,
                r.VendaId,
                r.ProdutoId,
                r.Quantidade,
                r.PrecoUnitario
            )),
        ];
    }

    private static Venda MapearVendaParaDominio(VendaRow vRow, IEnumerable<ItemVenda> itens)
    {
        if (
            !Enum.TryParse<FormaPagamento>(
                vRow.FormaPagamento,
                ignoreCase: true,
                out var formaPagamento
            )
        )
            throw new DomainException(
                $"Forma de pagamento inválida no banco: '{vRow.FormaPagamento}'."
            );

        if (!Enum.TryParse<StatusVenda>(vRow.Status, ignoreCase: true, out var statusVenda))
            throw new DomainException($"Status de venda inválido no banco: '{vRow.Status}'.");

        DateTime? dataHoraConvertida = DateTime.TryParse(vRow.DataHora, out var parsedDate)
            ? parsedDate
            : null;

        return new Venda(
            vRow.Id,
            vRow.ClienteId,
            formaPagamento,
            statusVenda,
            dataHoraConvertida,
            itens
        );
    }

    private sealed class VendaRow
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public string DataHora { get; set; } = string.Empty;
        public string FormaPagamento { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private sealed class ItemVendaRow
    {
        public int Id { get; set; }
        public int VendaId { get; set; }
        public int ProdutoId { get; set; }
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }
    }
}