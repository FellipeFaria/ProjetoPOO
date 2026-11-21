using ProjetoPOO.Exceptions;

namespace ProjetoPOO.Models;

public class Plataforma
{
    public int Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;

    private Plataforma() { }

    public Plataforma(int id, string nome)
    {
        if (id < 0)
            throw new DomainException("O Id da plataforma não pode ser negativo.");

        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome da plataforma é obrigatório");

        Id = id;
        Nome = nome.Trim();
    }
}
