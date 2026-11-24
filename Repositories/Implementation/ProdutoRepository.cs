using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using ProjetoPOO.Exceptions;
using ProjetoPOO.Models;
using ProjetoPOO.Models.Enums;
using ProjetoPOO.Repositories.Interfaces;

namespace ProjetoPOO.Repositories.Implementation;

public class ProdutoRepository(Func<IDbConnection> connectionFactory) : IProdutoRepository
{
    private readonly Func<IDbConnection> _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<IEnumerable<Produto>> ObterTodosAsync()
    {
        using var connection = _connectionFactory();
        connection.Open();

        var produtosBase = (
            await connection.QueryAsync<ProdutoBaseRow>(
                "SELECT Id, Nome, CAST(Preco AS TEXT) AS Preco, Quantidade, TipoProduto FROM Produtos ORDER BY Id"
            )
        ).ToList();

        var resultado = new List<Produto>();

        foreach (var baseRow in produtosBase)
        {
            resultado.Add(await HidratarProdutoAsync(connection, baseRow));
        }

        return resultado;
    }

    public async Task<Produto?> ObterPorIdAsync(int id)
    {
        if (id <= 0)
            return null;

        using var connection = _connectionFactory();
        connection.Open();

        var baseRow = await connection.QueryFirstOrDefaultAsync<ProdutoBaseRow>(
            "SELECT Id, Nome, CAST(Preco AS TEXT) AS Preco, Quantidade, TipoProduto FROM Produtos WHERE Id = @Id",
            new { Id = id }
        );

        if (baseRow is null)
            return null;

        return await HidratarProdutoAsync(connection, baseRow);
    }

    public async Task<int> AdicionarAsync(
        Produto entidade,
        IDbConnection? externalConnection = null,
        IDbTransaction? transaction = null
    )
    {
        ArgumentNullException.ThrowIfNull(entidade);

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
            const string sqlProduto =
                @"
                INSERT INTO Produtos (Nome, Preco, Quantidade, TipoProduto)
                VALUES (@Nome, @Preco, @Quantidade, @TipoProduto);
                SELECT last_insert_rowid();";

            var novoId = await connection.ExecuteScalarAsync<int>(
                sqlProduto,
                new
                {
                    entidade.Nome,
                    entidade.Preco,
                    entidade.Quantidade,
                    entidade.TipoProduto,
                },
                tx
            );

            switch (entidade)
            {
                case Videogame vg:
                    await connection.ExecuteAsync(
                        @"
                        INSERT INTO Videogames (ProdutoId, Marca, Modelo, Armazenamento, MemoriaRam)
                        VALUES (@ProdutoId, @Marca, @Modelo, @Armazenamento, @MemoriaRam);",
                        new
                        {
                            ProdutoId = novoId,
                            vg.Marca,
                            vg.Modelo,
                            vg.Armazenamento,
                            vg.MemoriaRam,
                        },
                        tx
                    );
                    break;

                case Jogo jogo:
                    await connection.ExecuteAsync(
                        @"
                        INSERT INTO Jogos (ProdutoId, Desenvolvedor, AnoLancamento)
                        VALUES (@ProdutoId, @Desenvolvedor, @AnoLancamento);",
                        new
                        {
                            ProdutoId = novoId,
                            jogo.Desenvolvedor,
                            jogo.AnoLancamento,
                        },
                        tx
                    );

                    foreach (var plataforma in jogo.Plataformas)
                    {
                        var plataformaId = await ObterOuCriarPlataformaAsync(
                            connection,
                            tx,
                            plataforma.Nome
                        );

                        await connection.ExecuteAsync(
                            @"
                            INSERT OR IGNORE INTO JogoPlataforma (JogoId, PlataformaId)
                            VALUES (@JogoId, @PlataformaId);",
                            new { JogoId = novoId, PlataformaId = plataformaId },
                            tx
                        );
                    }
                    break;

                case Acessorio acessorio:
                    await connection.ExecuteAsync(
                        @"
                        INSERT INTO Acessorios (ProdutoId, Marca, Modelo, Tipo)
                        VALUES (@ProdutoId, @Marca, @Modelo, @Tipo);",
                        new
                        {
                            ProdutoId = novoId,
                            acessorio.Marca,
                            acessorio.Modelo,
                            Tipo = acessorio.Tipo.ToString(),
                        },
                        tx
                    );
                    break;

                default:
                    throw new DomainException(
                        $"Tipo de produto não suportado: {entidade.GetType().Name}"
                    );
            }

            if (ownsTransaction)
                tx.Commit();

            return novoId;
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

    public async Task<bool> AtualizarAsync(
        Produto entidade,
        IDbConnection? externalConnection = null,
        IDbTransaction? transaction = null
    )
    {
        ArgumentNullException.ThrowIfNull(entidade);

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
            var linhasProduto = await connection.ExecuteAsync(
                @"
                UPDATE Produtos
                   SET Nome = @Nome,
                       Preco = @Preco
                 WHERE Id = @Id;",
                new
                {
                    entidade.Id,
                    entidade.Nome,
                    entidade.Preco,
                },
                tx
            );

            if (linhasProduto == 0)
            {
                if (ownsTransaction)
                    tx.Rollback();
                return false;
            }

            switch (entidade)
            {
                case Videogame vg:
                {
                    var linhasEspecificas = await connection.ExecuteAsync(
                        @"
                        UPDATE Videogames
                           SET Marca = @Marca,
                               Modelo = @Modelo,
                               Armazenamento = @Armazenamento,
                               MemoriaRam = @MemoriaRam
                         WHERE ProdutoId = @ProdutoId;",
                        new
                        {
                            ProdutoId = vg.Id,
                            vg.Marca,
                            vg.Modelo,
                            vg.Armazenamento,
                            vg.MemoriaRam,
                        },
                        tx
                    );

                    if (linhasEspecificas == 0)
                        throw new DomainException(
                            "As especificações do videogame não foram encontradas."
                        );
                    break;
                }

                case Jogo jogo:
                {
                    var linhasEspecificas = await connection.ExecuteAsync(
                        @"
                        UPDATE Jogos
                           SET Desenvolvedor = @Desenvolvedor,
                               AnoLancamento = @AnoLancamento
                         WHERE ProdutoId = @ProdutoId;",
                        new
                        {
                            ProdutoId = jogo.Id,
                            jogo.Desenvolvedor,
                            jogo.AnoLancamento,
                        },
                        tx
                    );

                    if (linhasEspecificas == 0)
                        throw new DomainException("As especificações do jogo não foram encontradas.");

                    await connection.ExecuteAsync(
                        "DELETE FROM JogoPlataforma WHERE JogoId = @JogoId;",
                        new { JogoId = jogo.Id },
                        tx
                    );

                    foreach (var plataforma in jogo.Plataformas)
                    {
                        var plataformaId = await ObterOuCriarPlataformaAsync(
                            connection,
                            tx,
                            plataforma.Nome
                        );

                        await connection.ExecuteAsync(
                            @"
                            INSERT OR IGNORE INTO JogoPlataforma (JogoId, PlataformaId)
                            VALUES (@JogoId, @PlataformaId);",
                            new { JogoId = jogo.Id, PlataformaId = plataformaId },
                            tx
                        );
                    }
                    break;
                }

                case Acessorio acessorio:
                {
                    var linhasEspecificas = await connection.ExecuteAsync(
                        @"
                        UPDATE Acessorios
                           SET Marca = @Marca,
                               Modelo = @Modelo,
                               Tipo = @Tipo
                         WHERE ProdutoId = @ProdutoId;",
                        new
                        {
                            ProdutoId = acessorio.Id,
                            acessorio.Marca,
                            acessorio.Modelo,
                            Tipo = acessorio.Tipo.ToString(),
                        },
                        tx
                    );

                    if (linhasEspecificas == 0)
                        throw new DomainException(
                            "As especificações do acessório não foram encontradas."
                        );
                    break;
                }

                default:
                    throw new DomainException(
                        $"Tipo de produto não suportado: {entidade.GetType().Name}"
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
                "DELETE FROM JogoPlataforma WHERE JogoId = @Id;",
                new { Id = id },
                tx
            );
            await connection.ExecuteAsync(
                "DELETE FROM Videogames WHERE ProdutoId = @Id;",
                new { Id = id },
                tx
            );
            await connection.ExecuteAsync(
                "DELETE FROM Jogos WHERE ProdutoId = @Id;",
                new { Id = id },
                tx
            );
            await connection.ExecuteAsync(
                "DELETE FROM Acessorios WHERE ProdutoId = @Id;",
                new { Id = id },
                tx
            );

            var afetadas = await connection.ExecuteAsync(
                "DELETE FROM Produtos WHERE Id = @Id;",
                new { Id = id },
                tx
            );

            if (ownsTransaction)
                tx.Commit();

            return afetadas > 0;
        }
        catch (SqliteException ex) when (
            ex.SqliteErrorCode == 19
            && ex.Message.Contains("FOREIGN KEY constraint failed", StringComparison.OrdinalIgnoreCase)
        )
        {
            if (ownsTransaction)
                tx.Rollback();

            throw new DomainException(
                "Não é possível excluir um produto associado a vendas ou movimentações de estoque.",
                ex
            );
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

    public async Task<bool> AtualizarEstoqueAsync(int produtoId, int novaQuantidade)
    {
        if (produtoId <= 0 || novaQuantidade < 0)
            return false;

        using var connection = _connectionFactory();
        connection.Open();

        var afetadas = await connection.ExecuteAsync(
            @"
            UPDATE Produtos
               SET Quantidade = @Quantidade
             WHERE Id = @Id;",
            new { Id = produtoId, Quantidade = novaQuantidade }
        );

        return afetadas > 0;
    }

    private static async Task<Produto> HidratarProdutoAsync(
        IDbConnection connection,
        ProdutoBaseRow baseRow
    )
    {
        switch (baseRow.TipoProduto.Trim())
        {
            case "Videogame":
            {
                var vgRow = await connection.QueryFirstOrDefaultAsync<VideogameRow>(
                    "SELECT Marca, Modelo, Armazenamento, MemoriaRam FROM Videogames WHERE ProdutoId = @Id",
                    new { baseRow.Id }
                );

                if (vgRow is null)
                    throw new DomainException(
                        $"Especificações do videogame #{baseRow.Id} não encontradas no banco."
                    );

                return new Videogame(
                    baseRow.Id,
                    baseRow.Nome,
                    decimal.Parse(baseRow.Preco, CultureInfo.InvariantCulture),
                    baseRow.Quantidade,
                    vgRow.Marca,
                    vgRow.Modelo,
                    vgRow.Armazenamento,
                    vgRow.MemoriaRam
                );
            }

            case "Jogo":
            {
                var jogoRow = await connection.QueryFirstOrDefaultAsync<JogoRow>(
                    "SELECT Desenvolvedor, AnoLancamento FROM Jogos WHERE ProdutoId = @Id",
                    new { baseRow.Id }
                );

                if (jogoRow is null)
                    throw new DomainException(
                        $"Especificações do jogo #{baseRow.Id} não encontradas no banco."
                    );

                var plataformas = (
                    await connection.QueryAsync<PlataformaRow>(
                        @"
                    SELECT p.Id, p.Nome
                      FROM Plataformas p
                     INNER JOIN JogoPlataforma jp ON jp.PlataformaId = p.Id
                     WHERE jp.JogoId = @Id
                     ORDER BY p.Nome;",
                        new { baseRow.Id }
                    )
                ).Select(p => new Plataforma(p.Id, p.Nome)).ToList();

                return new Jogo(
                    baseRow.Id,
                    baseRow.Nome,
                    decimal.Parse(baseRow.Preco, CultureInfo.InvariantCulture),
                    baseRow.Quantidade,
                    jogoRow.Desenvolvedor,
                    jogoRow.AnoLancamento,
                    plataformas
                );
            }

            case "Acessorio":
            {
                var accRow = await connection.QueryFirstOrDefaultAsync<AcessorioRow>(
                    "SELECT Marca, Modelo, Tipo FROM Acessorios WHERE ProdutoId = @Id",
                    new { baseRow.Id }
                );

                if (accRow is null)
                    throw new DomainException(
                        $"Especificações do acessório #{baseRow.Id} não encontradas no banco."
                    );

                if (!Enum.TryParse<TipoAcessorio>(accRow.Tipo, ignoreCase: true, out var tipoEnum))
                    throw new DomainException(
                        $"Tipo de acessório desconhecido no banco: '{accRow.Tipo}'."
                    );

                return new Acessorio(
                    baseRow.Id,
                    baseRow.Nome,
                    decimal.Parse(baseRow.Preco, CultureInfo.InvariantCulture),
                    baseRow.Quantidade,
                    accRow.Marca,
                    accRow.Modelo,
                    tipoEnum
                );
            }

            default:
                throw new DomainException(
                    $"Tipo de produto desconhecido no banco: '{baseRow.TipoProduto}'."
                );
        }
    }

    private static async Task<int> ObterOuCriarPlataformaAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        string nomePlataforma
    )
    {
        var nomeLimpo = nomePlataforma.Trim();

        var existenteId = await connection.QueryFirstOrDefaultAsync<int?>(
            "SELECT Id FROM Plataformas WHERE LOWER(Nome) = LOWER(@Nome);",
            new { Nome = nomeLimpo },
            transaction
        );

        if (existenteId.HasValue)
            return existenteId.Value;

        return await connection.ExecuteScalarAsync<int>(
            @"
            INSERT INTO Plataformas (Nome) VALUES (@Nome);
            SELECT last_insert_rowid();",
            new { Nome = nomeLimpo },
            transaction
        );
    }

    private sealed class ProdutoBaseRow
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Preco { get; set; } = "0";
        public int Quantidade { get; set; }
        public string TipoProduto { get; set; } = string.Empty;
    }

    private sealed class VideogameRow
    {
        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public int Armazenamento { get; set; }
        public int MemoriaRam { get; set; }
    }

    private sealed class JogoRow
    {
        public string Desenvolvedor { get; set; } = string.Empty;
        public int AnoLancamento { get; set; }
    }

    private sealed class PlataformaRow
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
    }

    private sealed class AcessorioRow
    {
        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
    }
}