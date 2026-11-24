using ProjetoPOO.Models;

namespace ProjetoPOO.Repositories.Interfaces;

public interface IClienteRepository : IRepository<Cliente>
{
    Task<Cliente?> ObterPorCpfAsync(string cpf);
}