using ProjetoPOO.Exceptions;

namespace ProjetoPOO.Models;

public class Cliente
{
    public int Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Cpf { get; private set; }
    public string? Email { get; private set; }
    public string? Telefone { get; private set; }

    private Cliente() { }

    public Cliente(
        int id,
        string nome,
        string? cpf = null,
        string? email = null,
        string? telefone = null
    )
    {
        if (id < 0)
            throw new DomainException("O Id do cliente não pode ser negativo");

        Id = id;
        AtualizarDados(nome, cpf, email, telefone);
    }

    public void AtualizarDados(
        string nome,
        string? cpf = null,
        string? email = null,
        string? telefone = null
    )
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do cliente é obrigatório");

        Nome = nome.Trim();
        Cpf = string.IsNullOrWhiteSpace(cpf) ? null : cpf.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim();
    }
}
