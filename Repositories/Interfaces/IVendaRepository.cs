using ProjetoPOO.Models;
using ProjetoPOO.Models.Enums;

namespace ProjetoPOO.Repositories.Interfaces;

public interface IVendaRepository : IRepository<Venda>
{
    Task<int> RegistrarVendaCompletaAsync(Venda venda);
    Task<bool> AtualizarStatusAsync(int vendaId, StatusVenda novoStatus);
}