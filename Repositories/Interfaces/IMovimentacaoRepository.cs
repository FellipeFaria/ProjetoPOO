using ProjetoPOO.Models;

namespace ProjetoPOO.Repositories.Interfaces;

public interface IMovimentacaoRepository : IRepository<Movimentacao>
{
    Task<int> RegistrarComAtualizacaoEstoqueAsync(Movimentacao movimentacao);
    Task<IEnumerable<Movimentacao>> ObterPorProdutoIdAsync(int produtoId);
    Task<IEnumerable<Movimentacao>> ObterPorVendaIdAsync(int vendaId);
}