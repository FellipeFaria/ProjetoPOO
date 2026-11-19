PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;


CREATE TABLE IF NOT EXISTS Produtos (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Nome TEXT NOT NULL,
    Preco NUMERIC NOT NULL CHECK (Preco >= 0),
    Quantidade INTEGER NOT NULL DEFAULT 0 CHECK (Quantidade >= 0),
    TipoProduto TEXT NOT NULL CHECK (
        TipoProduto IN ('Videogame', 'Jogo', 'Acessorio')
    )
);

CREATE TABLE IF NOT EXISTS Videogames (
    ProdutoId INTEGER PRIMARY KEY,
    Marca TEXT NOT NULL,
    Modelo TEXT NOT NULL,
    Armazenamento INTEGER NOT NULL CHECK (Armazenamento >= 0),
    MemoriaRam INTEGER NOT NULL CHECK (MemoriaRam >= 0),

    FOREIGN KEY (ProdutoId) REFERENCES Produtos(Id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS Jogos (
    ProdutoId INTEGER PRIMARY KEY,
    Desenvolvedor TEXT NOT NULL,
    AnoLancamento INTEGER NOT NULL CHECK (AnoLancamento > 0),

    FOREIGN KEY (ProdutoId) REFERENCES Produtos(Id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS Acessorios (
    ProdutoId INTEGER PRIMARY KEY,
    Marca TEXT NOT NULL,
    Modelo TEXT NOT NULL,
    Tipo TEXT NOT NULL CHECK (
        Tipo IN (
            'Controle',
            'Headset',
            'Teclado',
            'Mouse',
            'Carregador',
            'Cabo',
            'Outros'
        )
    ),

    FOREIGN KEY (ProdutoId) REFERENCES Produtos(Id) ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS Plataformas (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Nome TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS JogoPlataforma (
    JogoId INTEGER NOT NULL,
    PlataformaId INTEGER NOT NULL,

    PRIMARY KEY (JogoId, PlataformaId),

    FOREIGN KEY (JogoId) REFERENCES Jogos(ProdutoId) ON DELETE CASCADE,
    FOREIGN KEY (PlataformaId) REFERENCES Plataformas(Id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS Clientes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Nome TEXT NOT NULL,
    CPF TEXT UNIQUE,
    Email TEXT,
    Telefone TEXT
);

CREATE TABLE IF NOT EXISTS Vendas (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ClienteId INTEGER,
    DataHora TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),

    FormaPagamento TEXT NOT NULL CHECK (
        FormaPagamento IN (
            'Dinheiro',
            'Pix',
            'CartaoCredito',
            'CartaoDebito'
        )
    ),

    Status TEXT NOT NULL DEFAULT 'Concluida' CHECK (
        Status IN ('Concluida', 'Cancelada')
    ),

    FOREIGN KEY (ClienteId) REFERENCES Clientes(Id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS ItensVenda (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    VendaId INTEGER NOT NULL,
    ProdutoId INTEGER NOT NULL,
    Quantidade INTEGER NOT NULL CHECK (Quantidade > 0),
    PrecoUnitario NUMERIC NOT NULL CHECK (PrecoUnitario >= 0),

    UNIQUE (VendaId, ProdutoId),
    FOREIGN KEY (VendaId) REFERENCES Vendas(Id) ON DELETE RESTRICT,
    FOREIGN KEY (ProdutoId) REFERENCES Produtos(Id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS Movimentacoes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProdutoId INTEGER NOT NULL,
    VendaId INTEGER,
    Quantidade INTEGER NOT NULL CHECK (Quantidade > 0),

    Tipo TEXT NOT NULL CHECK (
        Tipo IN ('Entrada', 'Saida')
    ),

    Motivo TEXT NOT NULL,
    DataHora TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),

    FOREIGN KEY (ProdutoId) REFERENCES Produtos(Id) ON DELETE RESTRICT,
    FOREIGN KEY (VendaId) REFERENCES Vendas(Id) ON DELETE RESTRICT
);

CREATE TRIGGER IF NOT EXISTS TRG_Videogames_TipoProduto_Insert
BEFORE INSERT ON Videogames
FOR EACH ROW
WHEN NOT EXISTS (
    SELECT 1 FROM Produtos
    WHERE Id = NEW.ProdutoId AND TipoProduto = 'Videogame'
)
BEGIN
    SELECT RAISE(ABORT, 'ProdutoId deve corresponder a um produto com TipoProduto = ''Videogame''.');
END;

CREATE TRIGGER IF NOT EXISTS TRG_Jogos_TipoProduto_Insert
BEFORE INSERT ON Jogos
FOR EACH ROW
WHEN NOT EXISTS (
    SELECT 1 FROM Produtos
    WHERE Id = NEW.ProdutoId AND TipoProduto = 'Jogo'
)
BEGIN
    SELECT RAISE(ABORT, 'ProdutoId deve corresponder a um produto com TipoProduto = ''Jogo''.');
END;

CREATE TRIGGER IF NOT EXISTS TRG_Acessorios_TipoProduto_Insert
BEFORE INSERT ON Acessorios
FOR EACH ROW
WHEN NOT EXISTS (
    SELECT 1 FROM Produtos
    WHERE Id = NEW.ProdutoId AND TipoProduto = 'Acessorio'
)
BEGIN
    SELECT RAISE(ABORT, 'ProdutoId deve corresponder a um produto com TipoProduto = ''Acessorio''.');
END;


CREATE INDEX IF NOT EXISTS IX_Produtos_Nome ON Produtos(Nome);
CREATE INDEX IF NOT EXISTS IX_Vendas_ClienteId ON Vendas(ClienteId);
CREATE INDEX IF NOT EXISTS IX_Vendas_DataHora ON Vendas(DataHora);
CREATE INDEX IF NOT EXISTS IX_ItensVenda_VendaId ON ItensVenda(VendaId);
CREATE INDEX IF NOT EXISTS IX_Movimentacoes_ProdutoId ON Movimentacoes(ProdutoId);
CREATE INDEX IF NOT EXISTS IX_Movimentacoes_VendaId ON Movimentacoes(VendaId);

-- Produtos (Id 1..3: Videogames | Id 4..6: Jogos | Id 7..9: Acessorios)
INSERT INTO Produtos (Id, Nome, Preco, Quantidade, TipoProduto) VALUES
(1, 'PlayStation 5 Slim Edicao Digital', 3499.00, 6, 'Videogame'),
(2, 'Xbox Series X 1TB', 4199.00, 4, 'Videogame'),
(3, 'Nintendo Switch OLED', 2199.00, 8, 'Videogame'),
(4, 'God of War Ragnarok', 249.90, 15, 'Jogo'),
(5, 'The Legend of Zelda: Tears of the Kingdom', 299.90, 12, 'Jogo'),
(6, 'Elden Ring', 229.90, 10, 'Jogo'),
(7, 'Controle DualSense Branco', 429.00, 11, 'Acessorio'),
(8, 'Headset Gamer Sem Fio Pulse 3D', 549.00, 7, 'Acessorio'),
(9, 'Carregador Rapido Joy-Con Dock', 159.90, 9, 'Acessorio');

-- Videogames (3 registros)
INSERT INTO Videogames (ProdutoId, Marca, Modelo, Armazenamento, MemoriaRam) VALUES
(1, 'Sony', 'CFI-2000 Slim', 1024, 16),
(2, 'Microsoft', 'Series X Standard', 1024, 16),
(3, 'Nintendo', 'HEG-001 OLED', 64, 4);

-- Jogos (3 registros)
INSERT INTO Jogos (ProdutoId, Desenvolvedor, AnoLancamento) VALUES
(4, 'Santa Monica Studio', 2022),
(5, 'Nintendo EPD', 2023),
(6, 'FromSoftware', 2022);

-- Acessorios (3 registros)
INSERT INTO Acessorios (ProdutoId, Marca, Modelo, Tipo) VALUES
(7, 'Sony', 'CFI-ZCT1W', 'Controle'),
(8, 'Sony', 'CFI-ZWH1', 'Headset'),
(9, 'Nintendo', 'HAC-010-Dock', 'Carregador');

-- Plataformas (6 registros)
INSERT INTO Plataformas (Id, Nome) VALUES
(1, 'PlayStation 5'),
(2, 'PlayStation 4'),
(3, 'Xbox Series X|S'),
(4, 'Xbox One'),
(5, 'Nintendo Switch'),
(6, 'PC');

-- JogoPlataforma (6 registros)
INSERT INTO JogoPlataforma (JogoId, PlataformaId) VALUES
(4, 1),
(4, 2),
(5, 5),
(6, 1),
(6, 3),
(6, 6);

-- Clientes (6 registros)
INSERT INTO Clientes (Id, Nome, CPF, Email, Telefone) VALUES
(1, 'Lucas Almeida', '111.222.333-44', 'lucas.almeida@email.com', '(16) 99100-1001'),
(2, 'Mariana Costa', '222.333.444-55', 'mariana.costa@email.com', '(16) 99100-1002'),
(3, 'Rafael Souza', '333.444.555-66', 'rafael.souza@email.com', '(16) 99100-1003'),
(4, 'Beatriz Lima', '444.555.666-77', 'beatriz.lima@email.com', '(16) 99100-1004'),
(5, 'Thiago Oliveira', '555.666.777-88', 'thiago.oliveira@email.com', '(16) 99100-1005'),
(6, NULL, NULL, NULL, NULL); -- Cliente avulso / balcao

-- Vendas (6 registros: 5 Concluidas, 1 Cancelada)
INSERT INTO Vendas (Id, ClienteId, DataHora, FormaPagamento, Status) VALUES
(1, 1, '2026-11-10 14:20:00', 'Pix', 'Concluida'),
(2, 2, '2026-11-11 16:45:00', 'CartaoCredito', 'Concluida'),
(3, 3, '2026-11-12 10:15:00', 'Dinheiro', 'Concluida'),
(4, 4, '2026-11-13 18:05:00', 'CartaoDebito', 'Concluida'),
(5, 5, '2026-11-14 11:30:00', 'Pix', 'Cancelada'),
(6, 6, '2026-11-15 15:50:00', 'Pix', 'Concluida');

-- ItensVenda (8 registros)
INSERT INTO ItensVenda (Id, VendaId, ProdutoId, Quantidade, PrecoUnitario) VALUES
(1, 1, 1, 1, 3499.00),
(2, 1, 7, 1, 429.00),
(3, 2, 5, 1, 299.90),
(4, 2, 9, 1, 159.90),
(5, 3, 2, 1, 4199.00),
(6, 4, 6, 2, 229.90),
(7, 5, 4, 1, 249.90), -- Pertence a Venda 5 (Cancelada)
(8, 6, 8, 1, 549.00);

-- Movimentacoes (8 registros coerentes com entradas, saidas e cancelamento)
INSERT INTO Movimentacoes (Id, ProdutoId, VendaId, Quantidade, Tipo, Motivo, DataHora) VALUES
(1, 1, NULL, 7, 'Entrada', 'Reposicao inicial de estoque', '2026-11-01 09:00:00'),
(2, 1, 1, 1, 'Saida', 'Venda #1', '2026-11-10 14:20:00'),
(3, 7, 1, 1, 'Saida', 'Venda #1', '2026-11-10 14:20:00'),
(4, 5, 2, 1, 'Saida', 'Venda #2', '2026-11-11 16:45:00'),
(5, 4, NULL, 16, 'Entrada', 'Reposicao inicial de estoque', '2026-11-01 09:30:00'),
(6, 4, 5, 1, 'Saida', 'Venda #5', '2026-11-14 11:30:00'),
(7, 4, 5, 1, 'Entrada', 'Estorno por cancelamento da Venda #5', '2026-11-14 12:00:00'),
(8, 8, 6, 1, 'Saida', 'Venda #6', '2026-11-15 15:50:00');

COMMIT;