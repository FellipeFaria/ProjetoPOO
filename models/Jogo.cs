using ProjetoPOO.Exceptions;

namespace ProjetoPOO.Models;

public class Jogo : Produto
{
    private readonly List<Plataforma> _plataformas = [];

    public string Desenvolvedor { get; private set; } = string.Empty;
    public int AnoLancamento { get; private set; }
    public IReadOnlyCollection<Plataforma> Plataformas => _plataformas.AsReadOnly();

    private Jogo() { }

    public Jogo(
        int id,
        string nome,
        decimal preco,
        int quantidade,
        string desenvolvedor,
        int anoLancamento,
        IEnumerable<Plataforma>? plataformas = null
    )
        : base(id, nome, preco, quantidade, "Jogo")
    {
        if (string.IsNullOrWhiteSpace(desenvolvedor))
            throw new DomainException("O desenvolvedor do jogo é obrigatório.");

        if (anoLancamento <= 0)
            throw new DomainException("O ano de lançamento deve ser maior que zero.");

        Desenvolvedor = desenvolvedor.Trim();
        AnoLancamento = anoLancamento;

        if (plataformas is not null)
        {
            foreach (var plataforma in plataformas)
            {
                AdicionarPlataforma(plataforma);
            }
        }
    }

    public void AdicionarPlataforma(Plataforma plataforma)
    {
        if (plataforma is null)
            throw new DomainException("A plataforma não pode ser nula.");

        if (
            !_plataformas.Any(p =>
                p.Id == plataforma.Id && plataforma.Id > 0
                || p.Nome.Equals(plataforma.Nome, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            _plataformas.Add(plataforma);
        }
    }

    public override IReadOnlyDictionary<string, string> ObterResumoEspecifico()
    {
        var plataformasTexto =
            Plataformas.Count > 0
                ? string.Join(", ", Plataformas.Select(p => p.Nome))
                : "Não informados";

        return new Dictionary<string, string>
        {
            ["Desenvolvedor"] = Desenvolvedor,
            ["Ano de Lançamento"] = AnoLancamento.ToString(),
            ["Plataformas"] = plataformasTexto,
        };
    }
}