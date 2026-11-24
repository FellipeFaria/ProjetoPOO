using ProjetoPOO.Models;

namespace ProjetoPOO.Repositories.Interfaces;

public interface IProdutoRepository : IRepository<Produto>
{
    Task<bool> AtualizarEstoqueAsync(int produtoId, int novaQuantidade);
}