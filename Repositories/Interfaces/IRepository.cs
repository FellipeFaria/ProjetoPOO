using System.Data;

namespace ProjetoPOO.Repositories.Interfaces;

public interface IRepository<T>
    where T : class
{
    Task<IEnumerable<T>> ObterTodosAsync();
    Task<T?> ObterPorIdAsync(int id);
    Task<int> AdicionarAsync(
        T entidade,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null
    );
    Task<bool> AtualizarAsync(
        T entidade,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null
    );
    Task<bool> RemoverAsync(
        int id,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null
    );
}