# Controle de Estoque — Kits de Onboarding

Controle de estoque de **kits de onboarding** com quantidades **total, disponível, reservada e bloqueada**, feito em **C# (.NET 8)**, ASP.NET Core MVC e SQLite, com tela de login.

**Sumário:** 1. Tecnologias · 2. Estrutura do projeto · 3. Como executar · 4. Testes · 5. Documentação · 6. Visão de engenharia

---

## 1. Tecnologias utilizadas e por quê

| Tecnologia | Por quê |
|---|---|
| **C# / .NET 8** | Tipagem forte ajuda a manter as regras de estoque corretas; roda em Windows, macOS e Linux. |
| **ASP.NET Core MVC + Razor + Bootstrap (sem JavaScript)** | Separa regra, acesso a dados e tela; entrega login, cookies e proteção CSRF prontos; telas simples, renderizadas no servidor. |
| **SQLite** | Roda sem instalar nada (o banco é criado sozinho), ideal para um case fácil de avaliar. |
| **ADO.NET puro + padrão DAO** | Poucas consultas, legíveis em SQL puro; o controle explícito da transação (`BEGIN IMMEDIATE`) é essencial para a concorrência. |
| **Autenticação por cookie** | Padrão do ASP.NET Core, sem dependências extras; a senha nunca é guardada em texto. |
| **xUnit** | Padrão do ecossistema .NET; testa as regras isoladas e a integração com SQLite real, incluindo concorrência. |

---

## 2. Estrutura do projeto

```
ControleEstoque/
├── src/
│   └── ControleEstoque/
│       ├── Controllers/
│       │   ├── LoginController.cs
│       │   ├── HomeController.cs
│       │   ├── EstoqueController.cs
│       │   ├── KitsController.cs
│       │   └── RetencoesController.cs
│       ├── DAO/
│       │   ├── ConexaoDAO.cs
│       │   ├── EstoqueDAO.cs
│       │   └── UsuarioDAO.cs
│       ├── Models/
│       │   ├── Estoque.cs
│       │   ├── Kit.cs
│       │   ├── Retencao.cs
│       │   ├── Usuario.cs
│       │   └── EstoqueException.cs
│       ├── Views/
│       │   ├── Login/
│       │   │   └── Index.cshtml
│       │   ├── Home/
│       │   │   └── Index.cshtml
│       │   ├── Kits/
│       │   │   ├── Index.cshtml
│       │   │   └── Create.cshtml
│       │   ├── Retencoes/
│       │   │   └── Index.cshtml
│       │   ├── Shared/
│       │   │   ├── _Layout.cshtml
│       │   │   └── Error.cshtml
│       │   ├── _ViewImports.cshtml
│       │   └── _ViewStart.cshtml
│       ├── App_Data/
│       │   └── estoque.db
│       ├── appsettings.json
│       ├── Program.cs
│       └── ControleEstoque.csproj
├── tests/
│   └── ControleEstoque.Tests/
│       ├── EstoqueTests.cs
│       ├── EstoqueDAOTests.cs
│       └── ControleEstoque.Tests.csproj
├── ControleEstoque.sln
└── README.md
```

> `App_Data/estoque.db` é criado sozinho na primeira execução (não vem no repositório).

### Responsabilidade de cada pasta

| Pasta | O que tem | Regra de ouro |
|---|---|---|
| **Models** | `Estoque` (todas as regras de quantidade), `Kit`, `Retencao` (reserva/bloqueio), `Usuario` e `EstoqueException` (erro de regra) | Não conhece banco nem tela: dá para testar sem infraestrutura. |
| **DAO** | `ConexaoDAO` (conexão e criação das tabelas), `EstoqueDAO` (SQL e transações), `UsuarioDAO` (login) | Não repete regra nenhuma: chama o Model. |
| **Controllers** | Login, tela principal, operações de estoque, kits e reservas/bloqueios | Só recebem o pedido, chamam o DAO e devolvem a tela. |
| **Views** | Telas Razor + Bootstrap, sem JavaScript | Só mostram dados e formulários. |
| **App_Data** | Banco SQLite | Para zerar tudo, apague a pasta. |
| **tests** | Regras do Model e integração com SQLite real (transação, *rollback* e concorrência) | Cada regra do enunciado tem um teste. |

---

## 3. Como executar

**Requisito:** .NET 8 SDK (ou mais novo).

```bash
dotnet run --project src/ControleEstoque
```

Abra **http://localhost:5192** e entre com **admin** / **admin123** (definidos em `appsettings.json`). O banco é criado sozinho em `src/ControleEstoque/App_Data/estoque.db`; para zerar tudo, pare o programa e apague essa pasta.

**Para experimentar:** na tela principal registre uma **Entrada** → cadastre um kit em **Kits → Novo kit** (só o funcional, 7 números) e veja o disponível cair em 1 → tente retirar, reservar ou bloquear mais que o disponível (a tela mostra o erro e nada muda) → libere pela lista **Reservas e bloqueios** (dá para buscar por `RES-0001`).

---

## 4. Testes automatizados

```bash
dotnet test
```

- **Regras (Model):** rápidas, sem banco. Cada regra do enunciado tem pelo menos um teste que falha se ela for quebrada.
- **Integração (SQLite real):** transação, *rollback* e concorrência.
  - Se o `INSERT` do kit falha (forçado por um *trigger*), a unidade descontada volta.
  - 20 threads disputando 5 unidades: só 5 cadastros passam.
  - 30 threads reservando 1 unidade de um estoque de 10: exatamente 10 passam e 20 são recusadas.
- Para validar os testes, quebrei cada regra de propósito e eles falharam.
- Os testes de concorrência provam o SQLite local; não substituem teste de carga em outro banco.

---

## 5. Documentação

### 5.1 Visão geral

O estoque tem um total; parte fica **reservada** (para uma solicitação/pedido), parte **bloqueada** (indisponível) e o resto é o **disponível**. Pode haver várias reservas e bloqueios ao mesmo tempo.

| Funcionalidade | Onde | Regra |
|---|---|---|
| Cadastrar kit | Kits | Só o funcional (7 números); `IdKit` automático; um kit por colaborador; leva 1 unidade do disponível (recusa se não houver) |
| Entrada / Saída | Principal | Entrada soma ao total; saída só até o disponível |
| Reservar / Bloquear | Principal | Só até o disponível; exige solicitação/motivo; gera `RES-0001` / `BLQ-0001` |
| Liberar | Principal ou Reservas e bloqueios | Devolve ao disponível; não libera duas vezes |
| Consultar | Principal e Reservas e bloqueios | Saldos e busca pela identificação |

### 5.2 Arquitetura

Fluxo de uma operação (exemplo: Reservar):

```
Controller → EstoqueDAO.Reservar → (transação) lê o Estoque → Estoque.Reservar(qtd) valida e altera → grava → Commit
```

A regra fica só no Model (testável sem infraestrutura); o DAO cuida de banco e transação.

### 5.3 Principais decisões técnicas

1. **Disponível é calculado:** `Disponivel = Total - Reservada - Bloqueada`. Assim os quatro números nunca ficam fora de sincronia.
2. **Validar antes de alterar:** uma operação inválida lança `EstoqueException` e o objeto continua como estava.
3. **Toda escrita em transação `BEGIN IMMEDIATE`:** o SQLite deixa só uma escrita por vez; a segunda lê o saldo já atualizado e é recusada se não houver disponível. Isso resolve a concorrência.
4. **O banco também se protege:** `CHECK` (quantidades ≥ 0, `Reservada + Bloqueada <= Total`, funcional com 7 dígitos) e `UNIQUE` no funcional. É a última linha de defesa.
5. **Reserva e bloqueio na mesma tabela (`Retencao`)**, separados por `Tipo`, com identificação própria. Liberar usa esse `Id` e marca como liberado, o que impede liberar duas vezes.
6. **Cadastrar kit e descontar do estoque na mesma transação:** se o `INSERT` falha, a unidade volta.
7. **Funcional validado em três lugares:** tela, Model/DAO e banco.
8. **Erros de regra viram mensagem na tela;** erros inesperados desfazem a transação e mostram uma página de erro.

### 5.4 Premissas

- Há **um único estoque de kits**; a quantidade é colocada pelo usuário na tela principal (**Entrada**).
- O kit pertence a **um colaborador** (funcional de 7 números, único) e não tem nome, descrição nem quantidade.
- **Cadastrar um kit desconta 1 unidade** do total e do disponível.
- Quantidades são **inteiros positivos**; liberação é **por inteiro**.
- "Solicitação/pedido" e "motivo" são **texto livre** obrigatório (até 100 caracteres).
- Reservas e bloqueios **não expiram**; há **um único perfil de usuário** (o padrão é criado na 1ª execução).

### 5.5 Limitações conhecidas

- **Sem histórico de movimentações** (quem fez o quê e quando); só reservas e bloqueios têm data de criação e liberação.
- Login simples: sem cadastro de usuários, perfis ou troca de senha (altere `Login:SenhaPadrao` em `appsettings.json` **antes** da 1ª execução).
- Sem liberação parcial, expiração de reservas, edição/exclusão de kits ou paginação.
- SQLite aceita **uma escrita por vez**: ótimo para o case, limite para volume alto.

### 5.6 O que eu faria diferente em produção

- **Banco cliente-servidor** (PostgreSQL/SQL Server) com bloqueio por linha (`SELECT ... FOR UPDATE`) ou controle otimista (coluna de versão).
- **Histórico imutável de movimentações** (*ledger*) com usuário, data e motivo, para auditar e reconstruir o saldo.
- **Autenticação e autorização** completas, com perfis (quem pode bloquear, quem pode dar saída).
- **API REST com idempotência**, para um *retry* de rede não duplicar uma reserva.
- **Migrações versionadas**, logs estruturados, métricas e **CI** rodando os testes, incluindo teste de carga.

---

## 6. Visão de engenharia: 5 aspectos mais importantes

1. **Consistência do estoque:** é o coração do problema; disponível calculado, validação antes de alterar, transação e `CHECK` se protegem entre si.
2. **Concorrência:** duas reservas simultâneas não podem usar a mesma unidade; `BEGIN IMMEDIATE` hoje, bloqueio por linha ou versão otimista em produção.
3. **Testes que provam as regras:** cada regra do enunciado tem um teste que falha se ela for quebrada, incluindo rollback e concorrência com SQLite real.
4. **Simplicidade e legibilidade:** poucas classes e regra em um lugar só, para quem lê entender rápido e evoluir.
5. **Rastreabilidade e evolução:** hoje há identificação por reserva/bloqueio; o próximo passo é um histórico imutável de movimentações.
