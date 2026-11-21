using ProjetoPOO.Exceptions;

namespace ProjetoPOO.Models;

public class ItemVenda
{
    public int Id { get; private set; }
    public int VendaId { get; private set; }
    public int ProdutoId { get; private set; }
    public int Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }

    public decimal Subtotal => Quantidade * PrecoUnitario;

    private ItemVenda() { }

    public ItemVenda(int id, int vendaId, int produtoId, int quantidade, decimal precoUnitario)
    {
        if (id < 0)
            throw new DomainException("O Id do item da venda não pode ser negativo.");

        if (vendaId < 0)
            throw new DomainException("O VendaId não pode ser negativo.");

        if (produtoId <= 0)
            throw new DomainException("O ProdutoId é obrigatório.");

        if (quantidade <= 0)
            throw new DomainException("A quantidade do item deve ser maior que zero.");

        if (precoUnitario < 0)
            throw new DomainException("O preço unitário não pode ser negativo.");

        Id = id;
        VendaId = vendaId;
        ProdutoId = produtoId;
        Quantidade = quantidade;
        PrecoUnitario = precoUnitario;
    }

    internal void AumentarQuantidade(int quantidadeAdicional)
    {
        if (quantidadeAdicional <= 0)
            throw new DomainException("A quantidade adicional deve ser maior que zero.");

        Quantidade += quantidadeAdicional;
    }
}
