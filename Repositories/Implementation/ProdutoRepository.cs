using System.Data;
using Dapper;
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
                "SELECT Id, Nome, Preco, Quantidade, TipoProduto FROM Produtos ORDER BY Id"
            )
        ).ToList();

        var resultado = new List<Produto>();

        foreach (var baseRow in produtosBase)
        {
            var produto = await HidratarProdutoAsync(connection, baseRow);
            if (produto is not null)
                resultado.Add(produto);
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
            "SELECT Id, Nome, Preco, Quantidade, TipoProduto FROM Produtos WHERE Id = @Id",
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

        var ownsConnection = externalConnection is null;
        var connection = externalConnection ?? _connectionFactory();

        if (ownsConnection && connection.State != ConnectionState.Open)
            connection.Open();

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
                transaction
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
                        transaction
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
                        transaction
                    );

                    foreach (var plataforma in jogo.Plataformas)
                    {
                        var plataformaId = await ObterOuCriarPlataformaAsync(
                            connection,
                            transaction,
                            plataforma.Nome
                        );

                        await connection.ExecuteAsync(
                            @"
                            INSERT OR IGNORE INTO JogoPlataforma (JogoId, PlataformaId)
                            VALUES (@JogoId, @PlataformaId);",
                            new { JogoId = novoId, PlataformaId = plataformaId },
                            transaction
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
                        transaction
                    );
                    break;

                default:
                    throw new DomainException(
                        $"Tipo de produto nÃ£o suportado: {entidade.GetType().Name}"
                    );
            }

            return novoId;
        }
        finally
        {
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

        var ownsConnection = externalConnection is null;
        var connection = externalConnection ?? _connectionFactory();

        if (ownsConnection && connection.State != ConnectionState.Open)
            connection.Open();

        try
        {
            var linhasProduto = await connection.ExecuteAsync(
                @"
                UPDATE Produtos
                   SET Nome = @Nome,
                       Preco = @Preco,
                       Quantidade = @Quantidade
                 WHERE Id = @Id;",
                new
                {
                    entidade.Id,
                    entidade.Nome,
                    entidade.Preco,
                    entidade.Quantidade,
                },
                transaction
            );

            if (linhasProduto == 0)
                return false;

            switch (entidade)
            {
                case Videogame vg:
                    await connection.ExecuteAsync(
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
                        transaction
                    );
                    break;

                case Jogo jogo:
                    await connection.ExecuteAsync(
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
                        transaction
                    );

                    await connection.ExecuteAsync(
                        "DELETE FROM JogoPlataforma WHERE JogoId = @JogoId;",
                        new { JogoId = jogo.Id },
                        transaction
                    );

                    foreach (var plataforma in jogo.Plataformas)
                    {
                        var plataformaId = await ObterOuCriarPlataformaAsync(
                            connection,
                            transaction,
                            plataforma.Nome
                        );

                        await connection.ExecuteAsync(
                            @"
                            INSERT OR IGNORE INTO JogoPlataforma (JogoId, PlataformaId)
                            VALUES (@JogoId, @PlataformaId);",
                            new { JogoId = jogo.Id, PlataformaId = plataformaId },
                            transaction
                        );
                    }
                    break;

                case Acessorio acessorio:
                    await connection.ExecuteAsync(
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
                        transaction
                    );
                    break;
            }

            return true;
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
            await connection.ExecuteAsync(
                "DELETE FROM JogoPlataforma WHERE JogoId = @Id;",
                new { Id = id },
                transaction
            );
            await connection.ExecuteAsync(
                "DELETE FROM Videogames WHERE ProdutoId = @Id;",
                new { Id = id },
                transaction
            );
            await connection.ExecuteAsync(
                "DELETE FROM Jogos WHERE ProdutoId = @Id;",
                new { Id = id },
                transaction
            );
            await connection.ExecuteAsync(
                "DELETE FROM Acessorios WHERE ProdutoId = @Id;",
                new { Id = id },
                transaction
            );

            var afetadas = await connection.ExecuteAsync(
                "DELETE FROM Produtos WHERE Id = @Id;",
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

    private static async Task<Produto?> HidratarProdutoAsync(
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
                    return null;

                return new Videogame(
                    baseRow.Id,
                    baseRow.Nome,
                    baseRow.Preco,
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
                    return null;

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
                    baseRow.Preco,
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
                    return null;

                if (!Enum.TryParse<TipoAcessorio>(accRow.Tipo, ignoreCase: true, out var tipoEnum))
                    throw new DomainException(
                        $"Tipo de acessÃ³rio desconhecido no banco: '{accRow.Tipo}'."
                    );

                return new Acessorio(
                    baseRow.Id,
                    baseRow.Nome,
                    baseRow.Preco,
                    baseRow.Quantidade,
                    accRow.Marca,
                    accRow.Modelo,
                    tipoEnum
                );
            }

            default:
                return null;
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
        public decimal Preco { get; set; }
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