using Microsoft.AspNetCore.Mvc;
using ProjetoPOO.Exceptions;
using ProjetoPOO.Models;
using ProjetoPOO.Models.Enums;
using ProjetoPOO.Repositories.Interfaces;

namespace ProjetoPOO.Controllers;

public class VendasController(
    IVendaRepository vendaRepository,
    IClienteRepository clienteRepository,
    IProdutoRepository produtoRepository
) : Controller
{
    private readonly IVendaRepository _vendaRepository =
        vendaRepository ?? throw new ArgumentNullException(nameof(vendaRepository));
    private readonly IClienteRepository _clienteRepository =
        clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));
    private readonly IProdutoRepository _produtoRepository =
        produtoRepository ?? throw new ArgumentNullException(nameof(produtoRepository));

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.Clientes = (await _clienteRepository.ObterTodosAsync()).ToList();
        ViewBag.Produtos = (await _produtoRepository.ObterTodosAsync()).ToList();
        var vendas = await _vendaRepository.ObterTodosAsync();
        return View(vendas);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Criar(
        int clienteId,
        FormaPagamento formaPagamento,
        int produtoId,
        int quantidade
    )
    {
        if (!ValidarEntrada())
            return RedirectToAction(nameof(Index));

        try
        {
            _ = await _clienteRepository.ObterPorIdAsync(clienteId)
                ?? throw new DomainException("Cliente selecionado não encontrado.");

            var produto =
                await _produtoRepository.ObterPorIdAsync(produtoId)
                ?? throw new DomainException("Produto selecionado não encontrado.");

            var venda = new Venda(id: 0, clienteId: clienteId, formaPagamento: formaPagamento);
            venda.AdicionarItem(produto, quantidade);
            venda.Concluir();

            await _vendaRepository.RegistrarVendaCompletaAsync(venda);
            TempData["Sucesso"] = "Venda concluída e estoque atualizado.";
        }
        catch (DomainException ex)
        {
            TempData["Erro"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private bool ValidarEntrada()
    {
        if (ModelState.IsValid)
            return true;

        TempData["Erro"] = string.Join(
            " ",
            ModelState.Values.SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
        );
        if (TempData["Erro"] is not string { Length: > 0 })
            TempData["Erro"] = "Os dados informados são inválidos.";

        return false;
    }
}
