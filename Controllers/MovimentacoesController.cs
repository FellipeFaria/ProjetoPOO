using Microsoft.AspNetCore.Mvc;
using ProjetoPOO.Exceptions;
using ProjetoPOO.Models;
using ProjetoPOO.Models.Enums;
using ProjetoPOO.Repositories.Interfaces;

namespace ProjetoPOO.Controllers;

public class MovimentacoesController(
    IMovimentacaoRepository movimentacaoRepository,
    IProdutoRepository produtoRepository
) : Controller
{
    private readonly IMovimentacaoRepository _movimentacaoRepository =
        movimentacaoRepository ?? throw new ArgumentNullException(nameof(movimentacaoRepository));
    private readonly IProdutoRepository _produtoRepository =
        produtoRepository ?? throw new ArgumentNullException(nameof(produtoRepository));

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.Produtos = (await _produtoRepository.ObterTodosAsync()).ToList();
        var movimentacoes = await _movimentacaoRepository.ObterTodosAsync();
        return View(movimentacoes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(
        int produtoId,
        int quantidade,
        TipoMovimentacao tipo,
        string? motivo
    )
    {
        if (!ValidarEntrada())
            return RedirectToAction(nameof(Index));

        try
        {
            var movimentacao = new Movimentacao(
                id: 0,
                produtoId: produtoId,
                quantidade: quantidade,
                tipo: tipo,
                motivo: string.IsNullOrWhiteSpace(motivo) ? "Movimentação manual" : motivo
            );

            await _movimentacaoRepository.RegistrarComAtualizacaoEstoqueAsync(movimentacao);

            TempData["Sucesso"] = "Movimentação de estoque registrada.";
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
