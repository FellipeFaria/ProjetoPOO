using Microsoft.AspNetCore.Mvc;
using ProjetoPOO.Exceptions;
using ProjetoPOO.Models;
using ProjetoPOO.Repositories.Interfaces;

namespace ProjetoPOO.Controllers;

public class ClientesController(IClienteRepository clienteRepository) : Controller
{
    private readonly IClienteRepository _clienteRepository =
        clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var clientes = await _clienteRepository.ObterTodosAsync();
        return View(clientes);
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var cliente = await _clienteRepository.ObterPorIdAsync(id);
        return cliente is null ? NotFound() : View(cliente);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        int id,
        string? nome,
        string? cpf,
        string? email,
        string? telefone
    )
    {
        if (!ValidarEntrada())
            return RedirectToAction(nameof(Editar), new { id });

        try
        {
            var cliente = await _clienteRepository.ObterPorIdAsync(id);
            if (cliente is null)
            {
                TempData["Erro"] = "Cliente não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            cliente.AtualizarDados(nome ?? string.Empty, cpf, email, telefone);
            if (!await _clienteRepository.AtualizarAsync(cliente))
            {
                TempData["Erro"] = "Cliente não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Sucesso"] = "Cliente atualizado com sucesso.";
        }
        catch (DomainException ex)
        {
            TempData["Erro"] = ex.Message;
            return RedirectToAction(nameof(Editar), new { id });
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Criar(
        string nome,
        string? cpf,
        string? email,
        string? telefone
    )
    {
        if (!ValidarEntrada())
            return RedirectToAction(nameof(Index));

        try
        {
            var cliente = new Cliente(
                id: 0,
                nome: nome,
                cpf: cpf,
                email: email,
                telefone: telefone
            );
            await _clienteRepository.AdicionarAsync(cliente);
            TempData["Sucesso"] = "Cliente cadastrado com sucesso.";
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
