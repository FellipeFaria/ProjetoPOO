using ProjetoPOO.Exceptions;

namespace ProjetoPOO.Models;

public abstract class Produto
{
    public int Id { get; protected set; }
    public string Nome { get; protected set; } = string.Empty;
    public decimal Preco { get; protected set; }
    public int Quantidade { get; protected set; }
    public string TipoProduto { get; protected set; } = string.Empty;

    protected Produto() { }

    protected Produto(int id, string nome, decimal preco, int quantidade, string tipoProduto)
    {
        if (id < 0)
            throw new DomainException("O Id do produto não pode ser negativo.");

        DefinirNome(nome);
        AlterarPreco(preco);
        DefinirQuantidadeInicial(quantidade);

        if (string.IsNullOrWhiteSpace(tipoProduto))
            throw new DomainException("O TipoProduto é obrigatório.");

        Id = id;
        TipoProduto = tipoProduto;
    }

    public void DefinirNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do produto é obrigatório");

        Nome = nome;
    }

    public void AlterarPreco(decimal novoPreco)
    {
        if (novoPreco < 0)
            throw new DomainException("O preço do produto não pode ser negativo");

        Preco = novoPreco;
    }

    public void DefinirQuantidadeInicial(int quantidade)
    {
        if (quantidade < 0)
            throw new DomainException("A quantidade inicial do produto não pode ser negativa.");

        Quantidade = quantidade;
    }

    public void AdicionarEstoque(int quantidade)
    {
        if (quantidade <= 0)
            throw new DomainException("A quantidade de entrada deve ser maior que zero");

        Quantidade += quantidade;
    }

    public void RemoverEstoque(int quantidade)
    {
        if (quantidade <= 0)
            throw new DomainException("A quantidade de saída deve ser maior que zero");

        if (quantidade > Quantidade)
            throw new DomainException(
                $"Estoque insuficiente para '{Nome}'. Disponivel: {Quantidade}, solicitado: {quantidade}"
            );
    }

    public virtual decimal CalcularValorTotalEstoque()
    {
        return Preco * Quantidade;
    }

    public abstract IReadOnlyDictionary<string, string> ObterResumoEspecifico();
}
