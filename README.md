# GameStore

## Problema resolvido

Uma loja especializada em videogames, jogos e acessórios precisa administrar informações com características diferentes em um único negócio. Videogames possuem marca, modelo, armazenamento e memória RAM; jogos possuem desenvolvedor, ano de lançamento e plataformas; acessórios possuem marca, modelo e tipo. Ao mesmo tempo, o estoque, os clientes e as vendas precisam permanecer coerentes entre si.

Sem um sistema centralizado, essas informações podem ficar dispersas em registros independentes ou planilhas. Nesse cenário, são comuns erros como cadastrar o mesmo produto com dados inconsistentes, vender uma quantidade maior que a disponível, não registrar a saída do estoque, manter o histórico de movimentação incompleto e perder o vínculo entre venda, cliente e produtos comercializados.

O GameStore resolve esse problema propondo uma aplicação web única para o cadastro e a consulta do catálogo, o controle das entradas e saídas de estoque, a gestão dos dados de clientes e o registro das vendas concluídas. A implementação usa regras no domínio para impedir operações inválidas, como preço negativo, quantidade não positiva ou venda sem estoque suficiente. As informações são persistidas em SQLite e o histórico permite acompanhar que o estoque foi alterado por uma movimentação manual ou por uma venda.

## Proposta de valor

O GameStore organiza o processo da loja em um fluxo integrado: o catálogo é cadastrado por tipo de produto; o estoque acompanha a disponibilidade; o cliente é vinculado à venda; e cada venda registra os itens, o preço unitário, o pagamento e o valor total. Quando uma venda é registrada, o sistema baixa o estoque e cria as movimentações correspondentes, evitando que o cadastro comercial e o estoque sejam atualizados separadamente.

Além da aplicação prática, o projeto demonstra como os conceitos de Programação Orientada a Objetos podem estruturar um problema real: abstração e herança representam as categorias de produtos, encapsulamento protege o estado e as regras de negócio, polimorfismo permite tratar os produtos de forma uniforme e enums padronizam tipos, pagamentos e status.

## Integrantes

- Eduardo Henrique Vianna Silva — RA: `2840482511001`
- Fellipe Faria — RA: `2840482511023`
- Igor Pereira Alvez — RA: `2840482511040`
- Tiago Sanches Polo — RA: `2840482511016`

## Funcionalidades

O sistema cobre as principais etapas da operação da loja:

- **Produtos:** cadastro de videogames, jogos e acessórios; listagem do catálogo com especificações, estoque e preço; edição e exclusão.
- **Clientes:** cadastro, listagem e edição de nome, CPF, e-mail e telefone.
- **Estoque:** registro e consulta de movimentações de entrada e saída. As quantidades são atualizadas pelas movimentações e pelas vendas.
- **Vendas:** seleção de cliente e produto, definição da quantidade e da forma de pagamento; consulta do histórico, dos itens, do status e do total de vendas concluídas.
- **Validações de domínio:** regras para dados e operações, incluindo preço e quantidade não negativos, quantidade de movimentação positiva e verificação de estoque disponível para venda.

## Conceitos de POO no domínio

- **Abstração e herança:** `Produto` é uma classe abstrata. `Videogame`, `Jogo` e `Acessorio` herdam seus dados e comportamentos comuns e definem suas especificações.
- **Encapsulamento:** propriedades e alterações de estado são protegidas por acessores e métodos do domínio, como `AdicionarEstoque`, `RemoverEstoque` e `AtualizarDados`.
- **Polimorfismo:** os produtos implementam `ObterResumoEspecifico` para apresentar os detalhes próprios de cada tipo.
- **Composição e associações:** uma `Venda` reúne `ItemVenda`; itens referenciam produtos, e jogos podem estar associados a várias `Plataforma`.
- **Enums e exceção de domínio:** enums representam tipos de acessório, formas de pagamento, status da venda e tipos de movimentação. `DomainException` comunica violações das regras do domínio.

## Diagrama de classes UML

```mermaid
classDiagram
    class Produto {
        <<abstract>>
        +int Id
        +string Nome
        +decimal Preco
        +int Quantidade
        +DefinirNome(nome)
        +AlterarPreco(novoPreco)
        +AdicionarEstoque(quantidade)
        +RemoverEstoque(quantidade)
        +CalcularValorTotalEstoque()
        +ObterResumoEspecifico()
    }

    class Videogame {
        +string Marca
        +string Modelo
        +int Armazenamento
        +int MemoriaRam
        +ObterResumoEspecifico()
    }

    class Jogo {
        +string Desenvolvedor
        +int AnoLancamento
        +IReadOnlyCollection~Plataforma~ Plataformas
        +AdicionarPlataforma(plataforma)
        +ObterResumoEspecifico()
    }

    class Acessorio {
        +string Marca
        +string Modelo
        +TipoAcessorio Tipo
        +ObterResumoEspecifico()
    }

    class Plataforma {
        +int Id
        +string Nome
    }

    class Cliente {
        +int Id
        +string Nome
        +string? Cpf
        +string? Email
        +string? Telefone
        +AtualizarDados(nome, cpf, email, telefone)
    }

    class Venda {
        +int Id
        +int ClienteId
        +DateTime DataHora
        +FormaPagamento FormaPagamento
        +StatusVenda Status
        +IReadOnlyCollection~ItemVenda~ Itens
        +decimal ValorTotal
        +AdicionarItem(produto, quantidade)
        +RemoverItem(produtoId)
        +Concluir()
        +Cancelar()
    }

    class ItemVenda {
        +int Id
        +int VendaId
        +int ProdutoId
        +int Quantidade
        +decimal PrecoUnitario
        +decimal Subtotal
    }

    class Movimentacao {
        +int Id
        +int ProdutoId
        +int? VendaId
        +int Quantidade
        +TipoMovimentacao Tipo
        +string? Motivo
        +DateTime DataHora
    }

    class TipoAcessorio {
        <<enumeration>>
        Controle
        Headset
        Teclado
        Mouse
        Carregador
        Cabo
        Outros
    }

    class FormaPagamento {
        <<enumeration>>
        DINHEIRO
        PIX
        CARTAO_CREDITO
        CARTAO_DEBITO
    }

    class StatusVenda {
        <<enumeration>>
        ABERTA
        CONCLUIDA
        CANCELADA
    }

    class TipoMovimentacao {
        <<enumeration>>
        ENTRADA
        SAIDA
    }

    class DomainException {
        +DomainException(message)
    }

    Produto <|-- Videogame
    Produto <|-- Jogo
    Produto <|-- Acessorio
    Jogo "0..*" -- "0..*" Plataforma : plataformas
    Cliente "1" -- "0..*" Venda : cliente
    Venda "1" *-- "0..*" ItemVenda : itens
    ItemVenda "*" --> "1" Produto : produto
    Produto "1" <-- "0..*" Movimentacao : produto
    Venda "0..1" <-- "0..*" Movimentacao : venda relacionada
    Acessorio --> TipoAcessorio
    Venda --> FormaPagamento
    Venda --> StatusVenda
    Movimentacao --> TipoMovimentacao
    Produto ..> DomainException : valida regras
    Cliente ..> DomainException : valida regras
    Venda ..> DomainException : valida regras
    Movimentacao ..> DomainException : valida regras
```

## Tecnologias

- ASP.NET Core MVC com Razor Views
- C# e .NET 10 (`net10.0`)
- SQLite por meio do pacote `Microsoft.Data.Sqlite`
- Dapper para acesso a dados

## Configuração e execução

### Pré-requisitos

- .NET SDK 10 instalado.
- Terminal aberto na raiz do projeto, onde está `ProjetoPOO.csproj`.

### Connection String e banco de dados

Em `appsettings.json`, a conexão usada pela aplicação é `ConnectionStrings:DefaultConnection`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=gamestore.db"
}
```

O valor padrão usa SQLite e aponta para `gamestore.db`. Ajuste o `Data Source` nessa configuração para mudar o arquivo do banco. `appsettings.Development.json` contém apenas configurações de log e não substitui a connection string padrão.

Na inicialização, `Program.cs` chama `DbInitializer`, que verifica se já existe a tabela `Produtos`. Se não existir, executa `Database/Script.sql`, que cria as tabelas, índices, gatilhos e insere dados de exemplo. Portanto, em uma instalação nova não é necessário executar o script manualmente. Se a tabela `Produtos` já existir, o inicializador não executa o script novamente.

### Restaurar, compilar e iniciar

No terminal, a partir da raiz do projeto, execute:

```powershell
dotnet restore
dotnet build
dotnet run
```

Quando a aplicação iniciar, o terminal exibirá os endereços locais de escuta, normalmente no formato `http://localhost:<porta>` e/ou `https://localhost:<porta>`. Abra no navegador o endereço informado pelo próprio `dotnet run`.

## Navegação da aplicação

O menu principal dá acesso às páginas **Produtos**, **Clientes**, **Estoque** e **Vendas**. A rota inicial da aplicação também abre o catálogo de produtos.
