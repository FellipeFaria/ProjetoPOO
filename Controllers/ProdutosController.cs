using Microsoft.AspNetCore.Mvc;
using ProjetoPOO.Exceptions;
using ProjetoPOO.Models;
using ProjetoPOO.Models.Enums;
using ProjetoPOO.Repositories.Interfaces;

namespace ProjetoPOO.Controllers;

public class ProdutosController(IProdutoRepository produtoRepository) : Controller
{
    private readonly IProdutoRepository _produtoRepository =
        produtoRepository ?? throw new ArgumentNullException(nameof(produtoRepository));

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var produtos = await _produtoRepository.ObterTodosAsync();

        return View(produtos);
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var produto = await _produtoRepository.ObterPorIdAsync(id);
        return produto is null ? NotFound() : View(produto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        int id,
        string? nome,
        decimal preco,
        string? marca,
        string? modelo,
        int armazenamento,
        int memoriaRam,
        string? desenvolvedor,
        int anoLancamento,
        string? plataformasCsv,
        TipoAcessorio tipo
    )
    {
        if (!ValidarEntrada())
            return RedirectToAction(nameof(Editar), new { id });

        try
        {
            var produtoExistente = await _produtoRepository.ObterPorIdAsync(id);
            if (produtoExistente is null)
            {
                TempData["Erro"] = "Produto não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            var plataformas = (plataformasCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(nomePlataforma => new Plataforma(0, nomePlataforma))
                .ToList();

            Produto produtoAtualizado = produtoExistente switch
            {
                Videogame videogame => new Videogame(
                    id,
                    nome ?? string.Empty,
                    preco,
                    videogame.Quantidade,
                    marca ?? string.Empty,
                    modelo ?? string.Empty,
                    armazenamento,
                    memoriaRam
                ),
                Jogo jogo => new Jogo(
                    id,
                    nome ?? string.Empty,
                    preco,
                    jogo.Quantidade,
                    desenvolvedor ?? string.Empty,
                    anoLancamento,
                    plataformas
                ),
                Acessorio acessorio => new Acessorio(
                    id,
                    nome ?? string.Empty,
                    preco,
                    acessorio.Quantidade,
                    marca ?? string.Empty,
                    modelo ?? string.Empty,
                    tipo
                ),
                _ => throw new DomainException("Tipo de produto não suportado para edição."),
            };

            if (!await _produtoRepository.AtualizarAsync(produtoAtualizado))
            {
                TempData["Erro"] = "Produto não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Sucesso"] = "Produto atualizado com sucesso.";
        }
        catch (DomainException ex)
        {
            TempData["Erro"] = ex.Message;
            return RedirectToAction(nameof(Editar), new { id });
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Criar()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarVideogame(
        string nome,
        decimal preco,
        int quantidade,
        string marca,
        string modelo,
        int armazenamento,
        int memoriaRam
    )
    {
        if (!ValidarEntrada())
            return RedirectToAction(nameof(Criar));

        try
        {
            var videogame = new Videogame(
                id: 0,
                nome: nome,
                preco: preco,
                quantidade: quantidade,
                marca: marca,
                modelo: modelo,
                armazenamento: armazenamento,
                memoriaRam: memoriaRam
            );

            await _produtoRepository.AdicionarAsync(videogame);
            TempData["Sucesso"] = "Videogame criado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainException ex)
        {
            TempData["Erro"] = ex.Message;
            return RedirectToAction(nameof(Criar));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarJogo(
        string nome,
        decimal preco,
        int quantidade,
        string desenvolvedor,
        int anoLancamento,
        string? plataformasCsv
    )
    {
        if (!ValidarEntrada())
            return RedirectToAction(nameof(Criar));

        try
        {
            var plataformas = (plataformasCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(p => new Plataforma(0, p))
                .ToList();

            var jogo = new Jogo(
                id: 0,
                nome: nome,
                preco: preco,
                quantidade: quantidade,
                desenvolvedor: desenvolvedor,
                anoLancamento: anoLancamento,
                plataformas: plataformas
            );

            await _produtoRepository.AdicionarAsync(jogo);
            TempData["Sucesso"] = "Jogo cadastrado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainException ex)
        {
            TempData["Erro"] = ex.Message;
            return RedirectToAction(nameof(Criar));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarAcessorio(
        string nome,
        decimal preco,
        int quantidade,
        string marca,
        string modelo,
        TipoAcessorio tipo
    )
    {
        if (!ValidarEntrada())
            return RedirectToAction(nameof(Criar));

        try
        {
            var acessorio = new Acessorio(
                id: 0,
                nome: nome,
                preco: preco,
                quantidade: quantidade,
                marca: marca,
                modelo: modelo,
                tipo: tipo
            );

            await _produtoRepository.AdicionarAsync(acessorio);
            TempData["Sucesso"] = "Acessório cadastrado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainException ex)
        {
            TempData["Erro"] = ex.Message;
            return RedirectToAction(nameof(Criar));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(int id)
    {
        try
        {
            var removido = await _produtoRepository.RemoverAsync(id);
            TempData[removido ? "Sucesso" : "Erro"] = removido
                ? "Produto removido do catálogo."
                : "Produto não encontrado.";
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
