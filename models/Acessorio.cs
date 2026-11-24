using ProjetoPOO.Exceptions;
using ProjetoPOO.Models.Enums;

namespace ProjetoPOO.Models;

public class Acessorio : Produto
{
    public string Marca { get; private set; } = string.Empty;
    public string Modelo { get; private set; } = string.Empty;
    public TipoAcessorio Tipo { get; private set; }

    private Acessorio() { }

    public Acessorio(
        int id,
        string nome,
        decimal preco,
        int quantidade,
        string marca,
        string modelo,
        TipoAcessorio tipo
    )
        : base(id, nome, preco, quantidade, "Acessorio")
    {
        if (string.IsNullOrWhiteSpace(marca))
            throw new DomainException("A marca do acessório é obrigatória.");

        if (string.IsNullOrWhiteSpace(modelo))
            throw new DomainException("O modelo do acessório é obrigatório.");

        if (!Enum.IsDefined(tipo))
            throw new DomainException("Tipo de acessório inválido.");

        Marca = marca.Trim();
        Modelo = modelo.Trim();
        Tipo = tipo;
    }

    public override IReadOnlyDictionary<string, string> ObterResumoEspecifico()
    {
        return new Dictionary<string, string>
        {
            ["Tipo"] = Tipo.ToString(),
            ["Marca"] = Marca,
            ["Modelo"] = Modelo,
        };
    }
}
