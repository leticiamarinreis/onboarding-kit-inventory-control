# Controle de Estoque — Kits de Onboarding

Aplicação web para controle de estoque de **kits de onboarding**, com quantidades **total, disponível, reservada e bloqueada**, desenvolvida com ASP.NET Core MVC e SQLite, com tela de login.

## Tecnologias

- C# + .NET 8: tipagem forte ajuda a manter as regras de estoque corretas
- ASP.NET Core MVC + Razor + Bootstrap (sem JavaScript): separa regra, acesso a dados e tela; telas renderizadas no servidor
- SQLite: roda sem instalar nada, o banco é criado sozinho
- ADO.NET puro + padrão DAO: poucas consultas, legíveis em SQL, com controle explícito da transação
- Autenticação por cookie
- xUnit: testes de regra (Model) e de integração (SQLite real)

## Estrutura do Projeto

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

O `App_Data/estoque.db` é criado sozinho na primeira execução.

A **regra de negócio fica toda no Model `Estoque`**, que não conhece banco nem tela. O **DAO** cuida do SQL e das transações e não repete regra: chama o Model. Os **Controllers** só recebem o pedido e chamam o DAO. Assim as camadas ficam com responsabilidades bem definidas e fáceis de testar isoladamente.

Fluxo de uma operação (exemplo: Reservar):

```
Controller → EstoqueDAO.Reservar → (transação) lê o Estoque → Estoque.Reservar(qtd) valida e altera → grava → Commit
```

A configuração da aplicação (usuário e senha padrão do login) fica em `appsettings.json`.

Os testes seguem organização por responsabilidade na pasta `tests/`: regras do Model, sem banco, e integração com SQLite real (transação, *rollback* e concorrência). A execução é padronizada por `dotnet test`.

## Funcionalidades

| Funcionalidade | Onde fazer | Regra |
|---|---|---|
| Login | Tela de login | Toda tela exige login, exceto a de login e a de erro |
| Entrada | Principal → Entrada | Soma ao total |
| Saída | Principal → Saída | Só até o disponível |
| Reservar | Principal → Reservar | Só até o disponível; exige solicitação/pedido; gera `RES-0001` |
| Bloquear | Principal → Bloquear | Só até o disponível; exige motivo; gera `BLQ-0001` |
| Liberar | Principal ou Reservas e bloqueios | Devolve ao disponível; não libera duas vezes |
| Cadastrar kit | Kits → Novo kit | Só o funcional; leva 1 unidade do disponível (recusa se não houver) |
| Consultar | Principal (saldos) e Reservas e bloqueios | Busca pela identificação (ex.: `RES-0001`) |

O estoque pode ter várias reservas e bloqueios ao mesmo tempo, de solicitações diferentes. As operações da tela principal (Entrada, Saída, Reservar, Bloquear e Liberar) acontecem nela; a aba **Kits** só cadastra kits.

## Campos

Cadastro de kit (Kits → Novo kit):

| Campo | Tipo | Obrigatório | Regras |
|---|---|---|---|
| funcional | string | ✅ | Exatamente 7 números; único (um kit por colaborador) |

O `IdKit` é gerado pelo sistema.

Entrada e Saída:

| Campo | Tipo | Obrigatório | Regras |
|---|---|---|---|
| quantidade | int | ✅ | Inteiro positivo; na saída, não pode passar do disponível |

Reservar e Bloquear:

| Campo | Tipo | Obrigatório | Regras |
|---|---|---|---|
| quantidade | int | ✅ | Inteiro positivo, até o disponível |
| solicitação/pedido (reserva) ou motivo (bloqueio) | string | ✅ | Texto livre, até 100 caracteres |

Resultado: a identificação gerada (`RES-0001` ou `BLQ-0001`), usada depois para liberar. A liberação é sempre por inteiro.

Cálculo do saldo: `Disponível = Total - Reservada - Bloqueada`.

## Configuração Local

**1. Instalar o requisito**

.NET 8 SDK (ou mais novo).

**2. Executar a aplicação**

```bash
dotnet run --project src/ControleEstoque
```

**3. Acessar**

Abra http://localhost:5192 e entre com **admin** / **admin123** (definidos em `appsettings.json`).

**4. Executar os testes**

```bash
dotnet test
```

**5. Zerar os dados**

Pare o programa e apague a pasta `src/ControleEstoque/App_Data`.

**Fluxo para experimentar:** registre uma **Entrada** → cadastre um kit em **Kits → Novo kit** e veja o disponível cair em 1 → tente retirar, reservar ou bloquear mais que o disponível (a tela mostra o erro e nada muda) → reserve e bloqueie em solicitações diferentes → libere pela lista **Reservas e bloqueios**.

## Testes

- **Regras (Model):** rápidas, sem banco. Cada regra do enunciado tem pelo menos um teste que falha se ela for quebrada.
- **Integração (SQLite real):**
  - Se o `INSERT` do kit falha (forçado por um *trigger*), a unidade descontada volta.
  - 20 threads disputando 5 unidades: só 5 cadastros de kit passam.
  - 30 threads reservando 1 unidade de um estoque de 10: exatamente 10 passam e 20 são recusadas.
- Para validar os testes, quebrei cada regra de propósito e eles falharam.

## Premissas por funcionalidade

- Há **um único estoque de kits** (geral); a quantidade é colocada pelo usuário na tela principal.
- O kit pertence a **um colaborador** (funcional de 7 números, único) e não tem nome, descrição nem quantidade.
- **Cadastrar um kit desconta 1 unidade** do total e do disponível; unidades reservadas ou bloqueadas não servem para isso.
- **Saída** retira do total; **entrada** soma ao total.
- Reservas e bloqueios **não expiram** e são liberados **por inteiro**.
- "Solicitação/pedido" e "motivo" são **texto livre** obrigatório; não existe cadastro de solicitações.
- Há **um único perfil de usuário**: quem entra pode fazer tudo. O usuário padrão é criado na primeira execução.

## Decisões do Candidato

### 1. Por que a regra fica no Model e não no DAO

Decidi concentrar todas as regras de quantidade na classe `Estoque`, sem nenhuma dependência de banco. Assim as regras são testadas rapidamente, sem infraestrutura, e o DAO só se preocupa com SQL e transação. A regra existe em um lugar só.

### 2. Padrões aplicados

Utilizei o padrão **DAO** com ADO.NET puro para abstrair o acesso ao banco, sem ORM e sem camadas extras. O case pede simplicidade, e as consultas são poucas e legíveis. Os Controllers só delegam.

### 3. Como a consistência e a concorrência foram tratadas

- **Disponível é calculado, não gravado.** Guardamos `Total`, `Reservada` e `Bloqueada`, então os quatro números nunca ficam fora de sincronia.
- **Validar antes de alterar.** Uma operação inválida lança `EstoqueException` e o objeto continua como estava.
- **Toda escrita roda em transação `BEGIN IMMEDIATE`.** O SQLite deixa só uma escrita por vez; a segunda lê o saldo já atualizado e é recusada se não houver disponível.
- **O banco também se protege** com `CHECK` (quantidades ≥ 0, `Reservada + Bloqueada <= Total`, funcional com 7 dígitos) e `UNIQUE` no funcional. É a última linha de defesa.
- **Reserva e bloqueio ficam na mesma tabela (`Retencao`)**, separados por `Tipo`. Cada um tem identificação própria; liberar usa esse `Id` e marca o registro como liberado, o que impede liberar duas vezes.
- **Cadastrar kit e descontar do estoque acontecem na mesma transação:** se o `INSERT` falha, a unidade volta.

## Destaques da implementação

- **Cadastro de kit ligado ao estoque:** o kit só informa o funcional e consome 1 unidade do disponível, de forma atômica e segura contra concorrência.
- **Identificação de reservas e bloqueios** (`RES-0001`/`BLQ-0001`), com busca na tela.
- **Defesa em três camadas** contra estado inválido: Model, transação e `CHECK`/`UNIQUE` no banco.
- **Funcional validado em três lugares:** tela, Model/DAO e banco.
- **Login por cookie.**
- **Erros de regra viram mensagem na tela;** erros inesperados desfazem a transação e mostram uma página de erro.

## Limitações conhecidas

- Sem histórico de movimentações.
- Login simples (sem cadastro de usuários, perfis ou troca de senha).
- Sem liberação parcial, expiração de reservas, edição/exclusão de kits ou paginação.
- SQLite aceita uma escrita por vez.
- Os testes de concorrência provam o SQLite local; não substituem teste de carga em outro banco.

## O que eu faria diferente em produção

 - **Banco no Amazon RDS** (PostgreSQL ou SQL Server) no lugar do SQLite, para ter mais governança: backups automáticos, criptografia e controle de acesso. Também permitiria várias escritas ao mesmo tempo, usando bloqueio por linha (`SELECT ... FOR UPDATE`).
- **Log das operações no Amazon CloudWatch Logs:** cada entrada, saída, reserva, bloqueio, liberação e cadastro de kit gravaria um registro com usuário, data e resultado. Para saber quem mexeu nos recursos da própria AWS, usaria o **AWS CloudTrail**.
- **Kafka na API:** depois de cada operação confirmada, a API publicaria um evento (por exemplo, `reserva-criada`, `reserva-liberada` ou `kit-cadastrado`) em um tópico. Outros serviços leriam esses eventos, por exemplo para montar o histórico de movimentações ou enviar notificações, sem deixar a API mais lenta nem acoplada a eles.
- **Histórico de movimentações** (usuário, data e motivo).
- **Autenticação e autorização** completas, com perfis (quem pode bloquear, quem pode dar saída).

## Uso de IA

Este projeto parte de outro projeto meu, o [Resume Management System](https://github.com/leticiamarinreis/resume-management-system) (ASP.NET Core MVC, Razor, ADO.NET e padrão DAO, com SQL Server). Reaproveitei a organização em camadas (Controllers, DAO, Models, Views).
### Principais casos de uso

**Caso 1: adaptar a base de SQL Server para SQLite**
- **Prompt:** "faça uma versão em SQLite", para rodar no meu Mac sem instalar nada.
- **Resultado:** o acesso a dados foi portado e a IA explicou a diferença de trava entre os bancos: `BEGIN IMMEDIATE` no SQLite no lugar do `UPDLOCK` do SQL Server. Isso é o que garante que duas escritas simultâneas não passem do disponível.

**Caso 2: entender o escopo e definir as entidades**
- **Pedido:** ajuda para destrinchar o enunciado (total, disponível, reservada e bloqueada) e definir quais entidades o sistema precisava.
- **Resultado:** quatro entidades: `Estoque` (total, reservada e bloqueada, com o disponível calculado), `Kit` (identificado pelo funcional do colaborador), `Retencao` (reserva ou bloqueio, com identificação própria) e `Usuario` (login).

**Caso 3: testes que provam as regras**
- **Pedido:** uma bateria de testes de regra (Model) e de integração com SQLite real, incluindo concorrência e "falha no meio desfaz tudo".
- **Resultado:** testes com 20 e 30 threads disputando o estoque e um teste com *trigger* que força falha no `INSERT`. Para validar, quebrei cada regra de propósito e confirmei que os testes falhavam.

**Caso 4: desenvolver o README**
- **Prompt:** "quero fazer um read me formatado copia e cola que contemple essas etapas" (as etapas pedidas pelo case), e depois "deixe o read me mais resumido".
- **Resultado:** a IA me ajudou a organizar e redigir o README nas etapas do case (tecnologias, estrutura, execução, testes, documentação e visão de engenharia), com tabelas de funcionalidades e campos, e a deixá-lo mais objetivo.
- **Decisão minha:** o conteúdo descreve o que o projeto faz; eu revisei o texto para que regras, premissas e limitações correspondam ao que foi implementado.

## Visão de engenharia: 5 aspectos mais importantes

1. **Consistência do estoque:** é o coração do problema; disponível calculado, validação antes de alterar, transação e `CHECK` se protegem entre si.
2. **Concorrência:** duas reservas simultâneas não podem usar a mesma unidade; `BEGIN IMMEDIATE` hoje, bloqueio por linha ou versão otimista em produção.
3. **Testes que provam as regras:** cada regra do enunciado tem um teste que falha se ela for quebrada, incluindo *rollback* e concorrência com SQLite real.
4. **Simplicidade e legibilidade:** poucas classes e regra em um lugar só, para quem lê entender rápido e evoluir.
5. **Rastreabilidade e evolução:** hoje há identificação por reserva/bloqueio; o próximo passo é um histórico imutável de movimentações.
