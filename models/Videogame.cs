using ProjetoPOO.Models.Exceptions;

namespace ProjetoPOO.Models;

public class Videogame : Produto
{
    public string Marca { get; private set; } = string.Empty;
    public string Modelo { get; private set; } = string.Empty;
    public int Armazenamento { get; private set; }
    public int MemoriaRam { get; private set; }

    private Videogame() { }

    public Videogame(
        int id,
        string nome,
        decimal preco,
        int quantidade,
        string marca,
        string modelo,
        int armazenamento,
        int memoriaRam
    )
        : base(id, nome, preco, quantidade, "Videogame")
    {
        if (string.IsNullOrWhiteSpace(marca))
            throw new DomainException("A marca do videogame é obrigatória.");

        if (string.IsNullOrWhiteSpace(modelo))
            throw new DomainException("O modelo do videogame é obrigatório.");

        if (armazenamento < 0)
            throw new DomainException("O armazenamento não pode ser negativo.");

        if (memoriaRam < 0)
            throw new DomainException("A memória RAM não pode ser negativa.");

        Marca = marca.Trim();
        Modelo = modelo.Trim();
        Armazenamento = armazenamento;
        MemoriaRam = memoriaRam;
    }

    public override IReadOnlyDictionary<string, string> ObterResumoEspecifico()
    {
        return new Dictionary<string, string>
        {
            ["Marca"] = Marca,
            ["Modelo"] = Modelo,
            ["Armazenamento"] = $"{Armazenamento}",
            ["Memória RAM"] = $"{MemoriaRam}",
        };
    }
}