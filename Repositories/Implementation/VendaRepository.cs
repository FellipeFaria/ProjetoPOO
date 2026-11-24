using System.Data;
using System.Globalization;
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

    public async Task<int> AdicionarAsync(
        Venda entidade,
        IDbConnection? externalConnection = null,
        IDbTransaction? transaction = null
    )
    {
        ArgumentNullException.ThrowIfNull(entidade);

        if (externalConnection is null && transaction is null)
            return await RegistrarVendaCompletaAsync(entidade);

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

        var connection = externalConnection ?? transaction?.Connection ?? _connectionFactory();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        var ownsTransaction = transaction is null;
        var tx = transaction ?? connection.BeginTransaction();

        try
        {
            var vendaId = await RegistrarVendaNaTransacaoAsync(entidade, connection, tx);
            if (ownsTransaction)
                tx.Commit();

            return vendaId;
        }
        catch
        {
            if (ownsTransaction)
                tx.Rollback();
            throw;
        }
        finally
        {
            if (ownsTransaction)
                tx.Dispose();
        }
    }

    public async Task<int> RegistrarVendaCompletaAsync(Venda venda)
    {
        ArgumentNullException.ThrowIfNull(venda);

        using var connection = _connectionFactory();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            var vendaId = await RegistrarVendaNaTransacaoAsync(venda, connection, transaction);

            transaction.Commit();
            return vendaId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<int> RegistrarVendaNaTransacaoAsync(
        Venda venda,
        IDbConnection connection,
        IDbTransaction transaction
    )
    {
        if (venda.Itens.Count == 0)
            throw new DomainException("Não é possível registrar uma venda sem itens.");

        if (venda.Status == StatusVenda.CANCELADA)
            throw new DomainException("Uma venda não pode ser cadastrada já cancelada.");

        var vendaId = await InserirVendaEItensAsync(venda, connection, transaction);

        if (venda.Status == StatusVenda.CONCLUIDA)
        {
            foreach (var item in venda.Itens)
            {
                await AplicarMovimentacaoDeVendaAsync(
                    connection,
                    transaction,
                    vendaId,
                    item,
                    TipoMovimentacao.SAIDA,
                    venda.DataHora
                );
            }
        }

        return vendaId;
    }

    public async Task<bool> AtualizarAsync(
        Venda entidade,
        IDbConnection? externalConnection = null,
        IDbTransaction? transaction = null
    )
    {
        ArgumentNullException.ThrowIfNull(entidade);

        if (entidade.Status != StatusVenda.ABERTA)
            throw new DomainException("Somente vendas abertas podem ser alteradas.");

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

        var ownsConnection = externalConnection is null && transaction is null;
        var connection = externalConnection ?? transaction?.Connection ?? _connectionFactory();

        if (connection.State != ConnectionState.Open)
            connection.Open();

        var ownsTransaction = transaction is null;
        var tx = transaction ?? connection.BeginTransaction();

        try
        {
            var afetadas = await connection.ExecuteAsync(
                @"
                UPDATE Vendas
                   SET ClienteId = @ClienteId,
                       DataHora = @DataHora,
                       FormaPagamento = @FormaPagamento,
                       Status = @Status
                 WHERE Id = @Id
                   AND Status = 'Aberta';",
                new
                {
                    entidade.Id,
                    entidade.ClienteId,
                    DataHora = FormatarDataHora(entidade.DataHora),
                    FormaPagamento = FormaPagamentoParaBanco(entidade.FormaPagamento),
                    Status = StatusVendaParaBanco(entidade.Status),
                },
                tx
            );

            if (afetadas == 0)
            {
                if (ownsTransaction)
                    tx.Rollback();
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
                tx.Commit();

            return true;
        }
        catch
        {
            if (ownsTransaction)
                tx.Rollback();
            throw;
        }
        finally
        {
            if (ownsTransaction)
                tx.Dispose();

            if (ownsConnection)
                connection.Dispose();
        }
    }

    public async Task<bool> AtualizarStatusAsync(int vendaId, StatusVenda novoStatus)
    {
        if (vendaId <= 0)
            return false;

        if (!Enum.IsDefined(novoStatus))
            throw new DomainException("Status de venda inválido.");

        using var connection = _connectionFactory();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            var statusAtualTexto = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT Status FROM Vendas WHERE Id = @Id;",
                new { Id = vendaId },
                transaction
            );

            if (statusAtualTexto is null)
                return false;

            var statusAtual = StatusVendaDoBanco(statusAtualTexto);
            if (statusAtual == novoStatus)
                return true;

            var deveConcluir = statusAtual == StatusVenda.ABERTA
                && novoStatus == StatusVenda.CONCLUIDA;
            var deveCancelarVendaConcluida = statusAtual == StatusVenda.CONCLUIDA
                && novoStatus == StatusVenda.CANCELADA;
            var deveCancelarVendaAberta = statusAtual == StatusVenda.ABERTA
                && novoStatus == StatusVenda.CANCELADA;

            if (!deveConcluir && !deveCancelarVendaConcluida && !deveCancelarVendaAberta)
                throw new DomainException(
                    $"Não é permitido alterar uma venda de '{statusAtual}' para '{novoStatus}'."
                );

            if (deveConcluir || deveCancelarVendaConcluida)
            {
                var itens = await CarregarItensDaVendaAsync(connection, vendaId, transaction);
                if (itens.Count == 0)
                    throw new DomainException(
                        "Não é possível concluir ou estornar uma venda sem itens."
                    );

                var tipo = deveConcluir
                    ? TipoMovimentacao.SAIDA
                    : TipoMovimentacao.ENTRADA;
                var dataHora = DateTime.Now;

                foreach (var item in itens)
                {
                    await AplicarMovimentacaoDeVendaAsync(
                        connection,
                        transaction,
                        vendaId,
                        item,
                        tipo,
                        dataHora
                    );
                }
            }

            var afetadas = await connection.ExecuteAsync(
                """
                UPDATE Vendas
                   SET Status = @NovoStatus
                 WHERE Id = @Id
                   AND Status = @StatusAtual;
                """,
                new
                {
                    Id = vendaId,
                    NovoStatus = StatusVendaParaBanco(novoStatus),
                    StatusAtual = StatusVendaParaBanco(statusAtual),
                },
                transaction
            );

            if (afetadas == 0)
                throw new DomainException("A venda foi alterada por outra operação.");

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
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

        var ownsConnection = externalConnection is null && transaction is null;
        var connection = externalConnection ?? transaction?.Connection ?? _connectionFactory();

        if (connection.State != ConnectionState.Open)
            connection.Open();

        var ownsTransaction = transaction is null;
        var tx = transaction ?? connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(
                """
                DELETE FROM ItensVenda
                 WHERE VendaId = @Id
                   AND EXISTS (
                       SELECT 1
                         FROM Vendas
                        WHERE Id = @Id
                          AND Status = 'Aberta'
                   );
                """,
                new { Id = id },
                tx
            );

            var afetadas = await connection.ExecuteAsync(
                "DELETE FROM Vendas WHERE Id = @Id AND Status = 'Aberta';",
                new { Id = id },
                tx
            );

            if (afetadas == 0)
            {
                if (ownsTransaction)
                    tx.Rollback();
                return false;
            }

            if (ownsTransaction)
                tx.Commit();

            return true;
        }
        catch
        {
            if (ownsTransaction)
                tx.Rollback();
            throw;
        }
        finally
        {
            if (ownsTransaction)
                tx.Dispose();

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
                DataHora = FormatarDataHora(venda.DataHora),
                FormaPagamento = FormaPagamentoParaBanco(venda.FormaPagamento),
                Status = StatusVendaParaBanco(venda.Status),
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
            SELECT Id, VendaId, ProdutoId, Quantidade, CAST(PrecoUnitario AS TEXT) AS PrecoUnitario
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
                decimal.Parse(r.PrecoUnitario, CultureInfo.InvariantCulture)
            )),
        ];
    }

    private static Venda MapearVendaParaDominio(VendaRow vRow, IEnumerable<ItemVenda> itens)
    {
        var formaPagamento = vRow.FormaPagamento switch
        {
            "Dinheiro" or "DINHEIRO" => FormaPagamento.DINHEIRO,
            "Pix" or "PIX" => FormaPagamento.PIX,
            "CartaoCredito" or "CARTAO_CREDITO" => FormaPagamento.CARTAO_CREDITO,
            "CartaoDebito" or "CARTAO_DEBITO" => FormaPagamento.CARTAO_DEBITO,
            _ => throw new DomainException(
                $"Forma de pagamento inválida no banco: '{vRow.FormaPagamento}'."
            ),
        };

        var statusVenda = StatusVendaDoBanco(vRow.Status);

        if (
            !DateTime.TryParse(
                vRow.DataHora,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var dataHoraConvertida
            )
        )
            throw new DomainException(
                $"Data/hora inválida no banco para a venda #{vRow.Id}: '{vRow.DataHora}'."
            );

        return new Venda(
            vRow.Id,
            vRow.ClienteId,
            formaPagamento,
            statusVenda,
            dataHoraConvertida,
            itens
        );
    }

    private static string FormatarDataHora(DateTime dataHora)
    {
        return dataHora.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string FormaPagamentoParaBanco(FormaPagamento formaPagamento)
    {
        return formaPagamento switch
        {
            FormaPagamento.DINHEIRO => "Dinheiro",
            FormaPagamento.PIX => "Pix",
            FormaPagamento.CARTAO_CREDITO => "CartaoCredito",
            FormaPagamento.CARTAO_DEBITO => "CartaoDebito",
            _ => throw new DomainException("Forma de pagamento inválida."),
        };
    }

    private static string StatusVendaParaBanco(StatusVenda status)
    {
        return status switch
        {
            StatusVenda.ABERTA => "Aberta",
            StatusVenda.CONCLUIDA => "Concluida",
            StatusVenda.CANCELADA => "Cancelada",
            _ => throw new DomainException("Status de venda inválido."),
        };
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

    private static StatusVenda StatusVendaDoBanco(string status)
    {
        if (
            !Enum.TryParse<StatusVenda>(status, ignoreCase: true, out var statusVenda)
            || !Enum.IsDefined(statusVenda)
        )
            throw new DomainException($"Status de venda inválido no banco: '{status}'.");

        return statusVenda;
    }

    private static async Task AplicarMovimentacaoDeVendaAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int vendaId,
        ItemVenda item,
        TipoMovimentacao tipo,
        DateTime dataHora
    )
    {
        var atualizadas = tipo switch
        {
            TipoMovimentacao.SAIDA => await connection.ExecuteAsync(
                """
                UPDATE Produtos
                   SET Quantidade = Quantidade - @Quantidade
                 WHERE Id = @ProdutoId
                   AND Quantidade >= @Quantidade;
                """,
                new { item.ProdutoId, item.Quantidade },
                transaction
            ),
            TipoMovimentacao.ENTRADA => await connection.ExecuteAsync(
                """
                UPDATE Produtos
                   SET Quantidade = Quantidade + @Quantidade
                 WHERE Id = @ProdutoId
                   AND Quantidade <= @Limite - @Quantidade;
                """,
                new
                {
                    item.ProdutoId,
                    item.Quantidade,
                    Limite = int.MaxValue,
                },
                transaction
            ),
            _ => throw new DomainException("Tipo de movimentação inválido."),
        };

        if (atualizadas == 0)
        {
            var estoqueAtual = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Quantidade FROM Produtos WHERE Id = @ProdutoId;",
                new { item.ProdutoId },
                transaction
            );

            if (!estoqueAtual.HasValue)
                throw new DomainException(
                    $"Produto #{item.ProdutoId} não encontrado no estoque."
                );

            if (tipo == TipoMovimentacao.SAIDA)
                throw new DomainException(
                    $"Estoque insuficiente para o produto #{item.ProdutoId}. Disponível: {estoqueAtual.Value}, solicitado: {item.Quantidade}."
                );

            throw new DomainException(
                $"O estorno da venda #{vendaId} excede o limite de estoque permitido."
            );
        }

        await connection.ExecuteAsync(
            """
            INSERT INTO Movimentacoes (ProdutoId, VendaId, Quantidade, Tipo, Motivo, DataHora)
            VALUES (@ProdutoId, @VendaId, @Quantidade, @Tipo, @Motivo, @DataHora);
            """,
            new
            {
                item.ProdutoId,
                VendaId = vendaId,
                item.Quantidade,
                Tipo = TipoMovimentacaoParaBanco(tipo),
                Motivo = tipo == TipoMovimentacao.ENTRADA
                    ? $"Estorno por cancelamento da Venda #{vendaId}"
                    : $"Venda #{vendaId}",
                DataHora = FormatarDataHora(dataHora),
            },
            transaction
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
        public string PrecoUnitario { get; set; } = "0";
    }
}