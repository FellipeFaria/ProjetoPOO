using ProjetoPOO.Exceptions;
using ProjetoPOO.Models.Enums;

namespace ProjetoPOO.Models;

public class Venda
{
    private readonly List<ItemVenda> _itens = [];

    public int Id { get; private set; }
    public int ClienteId { get; private set; }
    public DateTime DataHora { get; private set; }
    public FormaPagamento FormaPagamento { get; private set; }
    public StatusVenda Status { get; private set; }

    public IReadOnlyCollection<ItemVenda> Itens => _itens.AsReadOnly();
    public decimal ValorTotal => _itens.Sum(item => item.Subtotal);

    private Venda() { }

    public Venda(
        int id,
        int clienteId,
        FormaPagamento formaPagamento,
        StatusVenda status = StatusVenda.ABERTA,
        DateTime? dataHora = null,
        IEnumerable<ItemVenda>? itens = null
    )
    {
        if (id < 0)
            throw new DomainException("O Id da venda não pode ser negativo.");

        if (clienteId <= 0)
            throw new DomainException("O ClienteId é obrigatório.");

        if (!Enum.IsDefined(formaPagamento))
            throw new DomainException("Forma de pagamento inválida.");

        if (!Enum.IsDefined(status))
            throw new DomainException("Status de venda inválido.");

        Id = id;
        ClienteId = clienteId;
        FormaPagamento = formaPagamento;
        Status = status;
        DataHora = dataHora ?? DateTime.Now;

        if (itens is not null)
        {
            foreach (var item in itens)
            {
                if (item is null)
                    throw new DomainException("Item de venda inválido");

                _itens.Add(item);
            }
        }
    }

    public void AdicionarItem(Produto produto, int quantidade)
    {
        GarantirVendaAberta();

        if (produto is null)
            throw new DomainException("O produto é obrigatório.");

        if (produto.Id <= 0)
            throw new DomainException("O produto precisa estar cadastrado antes de ser vendido.");

        if (quantidade <= 0)
            throw new DomainException("A quantidade deve ser maior que zero.");

        var itemExistente = _itens.FirstOrDefault(i => i.ProdutoId == produto.Id);
        var quantidadeExistente = itemExistente?.Quantidade ?? 0;
        if (quantidade > int.MaxValue - quantidadeExistente)
            throw new DomainException("A quantidade total do item excede o limite permitido.");

        var quantidadeTotalSolicitada = quantidadeExistente + quantidade;

        if (quantidadeTotalSolicitada > produto.Quantidade)
            throw new DomainException(
                $"Estoque insuficiente para '{produto.Nome}'. Disponível: {produto.Quantidade}, solicitado: {quantidadeTotalSolicitada}."
            );

        if (itemExistente is not null)
        {
            itemExistente.AumentarQuantidade(quantidade);
            return;
        }

        _itens.Add(
            new ItemVenda(
                id: 0,
                vendaId: Id,
                produtoId: produto.Id,
                quantidade: quantidade,
                precoUnitario: produto.Preco
            )
        );
    }

    public void RemoverItem(int produtoId)
    {
        GarantirVendaAberta();

        var item =
            _itens.FirstOrDefault(i => i.ProdutoId == produtoId)
            ?? throw new DomainException("Item não encontrado nesta venda.");

        _itens.Remove(item);
    }

    public void Concluir()
    {
        GarantirVendaAberta();

        if (_itens.Count == 0)
            throw new DomainException("Não é possível concluir uma venda sem itens.");

        Status = StatusVenda.CONCLUIDA;
    }

    public void Cancelar()
    {
        if (Status == StatusVenda.CANCELADA)
            throw new DomainException("A venda já está cancelada.");

        Status = StatusVenda.CANCELADA;
    }

    private void GarantirVendaAberta()
    {
        if (Status != StatusVenda.ABERTA)
            throw new DomainException(
                $"Operação não permitida porque a venda está com status '{Status}'."
            );
    }
}
