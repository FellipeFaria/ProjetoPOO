using ProjetoPOO.Exceptions;
using ProjetoPOO.Models.Enums;

namespace ProjetoPOO.Models;

public class Movimentacao
{
    public int Id { get; private set; }
    public int ProdutoId { get; private set; }
    public int? VendaId { get; private set; }
    public int Quantidade { get; private set; }
    public TipoMovimentacao Tipo { get; private set; }
    public string? Motivo { get; private set; }
    public DateTime DataHora { get; private set; }

    private Movimentacao() { }

    public Movimentacao(
        int id,
        int produtoId,
        int quantidade,
        TipoMovimentacao tipo,
        string? motivo = null,
        int? vendaId = null,
        DateTime? dataHora = null
    )
    {
        if (id < 0)
            throw new DomainException("O Id da movimentação não pode ser negativo.");

        if (produtoId <= 0)
            throw new DomainException("O ProdutoId é obrigatório.");

        if (quantidade <= 0)
            throw new DomainException("A quantidade da movimentação deve ser maior que zero.");

        if (!Enum.IsDefined(tipo))
            throw new DomainException("Tipo de movimentação inválido.");

        if (vendaId.HasValue && vendaId.Value <= 0)
            throw new DomainException("O VendaId, quando informado, deve ser maior que zero.");

        Id = id;
        ProdutoId = produtoId;
        Quantidade = quantidade;
        Tipo = tipo;
        Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        VendaId = vendaId;
        DataHora = dataHora ?? DateTime.Now;
    }
}
