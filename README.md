# Controle de Estoque

Case prático de Engenharia de Software: controle de estoque de **kits de onboarding** com quantidades **total, disponível, reservada e bloqueada**.
Feito em **C# (.NET 8)**, ASP.NET Core MVC e SQLite, com **tela de login**.

É a mesma solução do projeto *Controle de Estoque* (mesmas regras de estoque, mesmos testes, mesma estrutura), com estas diferenças:

1. **Layout novo**: a **tela principal** é onde o estoque é controlado (você coloca a quantidade, retira, reserva, bloqueia e libera).
2. **Login** (usuário padrão `admin` / senha `admin123`, definidos em `appsettings.json`).
3. **O kit só é cadastrado**, na aba **Kits**, informando apenas o **funcional do colaborador** (exatamente 7 números). O `IdKit` é gerado pelo sistema. Não há nenhuma operação de estoque na aba Kits.
4. **O kit "conversa" com o estoque**: cada kit cadastrado leva **1 unidade do estoque disponível**. Sem unidade disponível, o cadastro é recusado e nada muda.

## 1. Visão geral

O estoque de kits tem um total. Desse total, parte pode estar **reservada** (para uma solicitação/pedido) e parte **bloqueada** (indisponível para uso). O que sobra é o **disponível**.

| Funcionalidade | Onde fazer | Regra |
|---|---|---|
| Cadastrar kit | Kits → Novo kit | só o funcional (7 números); `IdKit` automático e único; um kit por colaborador; leva 1 unidade do disponível (recusa se não houver) |
| Entrada | Principal → Entrada | soma ao total |
| Saída | Principal → Saída | só até o disponível |
| Reservar | Principal → Reservar | só até o disponível; exige solicitação/pedido; gera identificação `RES-0001` |
| Bloquear | Principal → Bloquear | só até o disponível; exige motivo; gera identificação `BLQ-0001` |
| Liberar | Principal ou "Reservas e bloqueios" → Liberar | devolve ao disponível; não libera duas vezes |
| Consultar | Principal (saldos) e "Reservas e bloqueios" (busca pela identificação) | |

O estoque pode ter várias reservas e bloqueios ao mesmo tempo, de solicitações diferentes.

## 2. Arquitetura

```
src/ControleEstoque
  Models/       Estoque (TODAS as regras de quantidade), Kit (só funcional), Retencao (reserva/bloqueio), Usuario, EstoqueException
  DAO/          ConexaoDAO (conexão + criação das tabelas), EstoqueDAO (SQL + transações), UsuarioDAO
  Controllers/  LoginController, HomeController, EstoqueController, KitsController, RetencoesController (só recebem o pedido e chamam o DAO)
  Views/        Razor + Bootstrap, sem JavaScript
tests/ControleEstoque.Tests   xUnit: regras (Model) e integração (SQLite real + concorrência)
```

Fluxo de uma operação, por exemplo Reservar: `Controller → EstoqueDAO.Reservar → (transação) lê o Estoque → Estoque.Reservar(qtd) valida e altera → grava → Commit`.

Duas classes de regra, em lugares diferentes de propósito:
- **`Estoque`** (Model) conhece as regras e não conhece banco: dá para testar sem infraestrutura.
- **`EstoqueDAO`** conhece o banco e a transação, e **não repete regra nenhuma**: chama o Model.

## 3. Principais decisões técnicas

1. **Disponível é calculado, não gravado.** Guardamos `Total`, `Reservada` e `Bloqueada`; `Disponivel = Total - Reservada - Bloqueada`. Assim `Total = Disponível + Reservada + Bloqueada` vale sempre, por construção, e não existe a chance de os quatro números ficarem fora de sincronia.
2. **Validar antes de alterar.** Cada método do `Estoque` confere tudo e só então muda os campos. Uma operação inválida lança `EstoqueException` e o objeto continua como estava.
3. **Toda escrita roda em uma transação `BEGIN IMMEDIATE`.** Lê o estoque *dentro* da transação, aplica a regra e grava. Se qualquer coisa falha, não há `Commit` e o SQLite desfaz tudo. Isso resolve a concorrência (veja abaixo).
4. **O banco também se protege (`CHECK`).** `Total >= 0`, `Reservada >= 0`, `Bloqueada >= 0`, `Reservada + Bloqueada <= Total`; e `Funcional` do kit com exatamente 7 dígitos e único. É a última linha de defesa: se um bug futuro tentar gravar um estado inválido, o banco recusa.
5. **Reserva e bloqueio são a mesma tabela (`Retencao`)**, separados por `Tipo`. Têm a mesma estrutura (quantidade, referência, ativa/liberada); duas tabelas só duplicariam código sem ganho.
6. **Identificação própria e liberação por ela.** Cada reserva/bloqueio tem um `Id` (mostrado como `RES-0003`/`BLQ-0001`). Liberar usa esse `Id` e marca o registro como liberado, o que impede liberar duas vezes.
7. **SQLite, DAO com ADO.NET puro, sem ORM, sem camadas extras.** O case pede simplicidade; o SQLite roda sem instalar nada. As consultas são poucas e legíveis.
8. **Cadastrar kit e descontar do estoque acontecem na mesma transação.** Se o `INSERT` do kit falhar, a unidade descontada volta (há teste com trigger que força a falha). Dois cadastros simultâneos nunca passam do estoque (teste com 20 threads disputando 5 unidades).
9. **Funcional validado em três lugares**: na tela (`pattern`, 7 números), no Model (`[RegularExpression]`) e no `EstoqueDAO.CriarKit`, que também recusa funcional repetido dentro da transação; por fim o próprio banco (`CHECK` + `UNIQUE`).
10. **Login por cookie**, senha com hash PBKDF2 (nunca guardada em texto) e proteção contra CSRF nos formulários. Toda tela exige login, exceto a de login e a de erro.
11. **Erros de regra viram mensagem na tela** (`EstoqueException`); erros inesperados não são escondidos (a transação é desfeita e aparece uma página de erro).

### Concorrência

Duas reservas (ou dois cadastros de kit) ao mesmo tempo não podem passar do disponível. Com `BEGIN IMMEDIATE`, o SQLite deixa só **uma transação de escrita por vez**; a segunda espera e, ao ler, já enxerga o saldo atualizado, então é recusada se não houver mais disponível. Há um teste com 30 threads reservando 1 unidade de um estoque de 10: exatamente 10 passam e 20 são recusadas.

## 4. Premissas

- Há **um único estoque de kits** (geral). Quem coloca a quantidade é o usuário, na tela principal (**Entrada**).
- O kit é identificado pelo `IdKit` (automático) e pertence a **um colaborador**, identificado pelo **funcional de 7 números**. Como a regra é "um kit por colaborador", o funcional é **único**: cadastrar o mesmo funcional de novo é recusado.
- O kit **não tem nome, descrição nem quantidade**: o único dado de cadastro é o funcional.
- **Cadastrar um kit desconta 1 unidade** do total (e do disponível). Unidades reservadas ou bloqueadas não servem para isso.
- Quantidades são **inteiros positivos**; zero ou negativo é recusado.
- Uma reserva/bloqueio é liberado **por inteiro** (não há liberação parcial).
- A "solicitação/pedido" da reserva e o "motivo" do bloqueio são **texto livre** obrigatório (até 100 caracteres). Não existe cadastro de solicitações.
- Saída retira do **total** (as unidades saíram do estoque); entrada soma ao total.
- Reservas e bloqueios **não expiram**.
- Um único perfil de usuário: quem entra pode fazer tudo. O usuário padrão é criado na primeira execução.

## 5. Limitações conhecidas

- **Sem histórico de movimentações** (quem fez o quê e quando). Só ficam registradas reservas e bloqueios (com data de criação e liberação).
- Login simples: sem cadastro de usuários pela tela, sem perfis e sem troca de senha (troque `Login:SenhaPadrao` em `appsettings.json` **antes** da primeira execução).
- **Sem tela de Movimentações** (o projeto Controle de Estoque também não tem). É fácil acrescentar depois; veja a seção 7.
- Sem liberação parcial, sem expiração de reservas, sem edição/exclusão de kits (excluir um kit não devolveria a unidade ao estoque; use Entrada).
- SQLite aceita **uma escrita por vez**: ótimo para o case, limite para volume alto.
- Sem paginação nas listas.
- Os testes de concorrência provam o comportamento do SQLite local; não substituem teste de carga em outro banco.

## 6. Como executar

Requisito: **.NET 8 SDK** (ou mais novo).

```bash
dotnet run --project src/ControleEstoque
```

Abra **http://localhost:5192** e entre com **admin** / **admin123**. O banco é criado sozinho em `src/ControleEstoque/App_Data/estoque.db` (para zerar tudo, pare o programa e apague essa pasta).

Testes:

```bash
dotnet test
```

Fluxo para experimentar: na tela principal registre uma **Entrada** (a quantidade de kits no estoque) → cadastre um kit em **Kits → Novo kit** só com o funcional (7 números) e veja o disponível cair em 1 → tente retirar/reservar/bloquear **mais que o disponível**, ou cadastrar um kit com o estoque zerado (a tela mostra o erro e nada muda) → reserve e bloqueie em solicitações diferentes → libere pela lista **Reservas e bloqueios** (dá para buscar por `RES-0001`).

## 7. O que eu faria diferente em produção

- **Banco cliente-servidor** (PostgreSQL/SQL Server) com bloqueio por linha (`SELECT ... FOR UPDATE`) ou controle otimista (coluna de versão), para várias escritas simultâneas em kits diferentes.
- **Histórico imutável de movimentações** (ledger): cada entrada, saída, reserva, bloqueio e liberação vira uma linha com usuário, data e motivo; o saldo passa a poder ser auditado e reconstruído.
- **Autenticação e autorização** mais completas (cadastro de usuários, troca de senha, bloqueio por tentativas), com perfis (quem pode bloquear, quem pode dar saída).
- **API** (REST) com **idempotência** nas operações (chave de requisição), para um retry de rede não duplicar uma reserva.
- **Reserva vinculada a um cadastro de solicitações**, com expiração e liberação parcial.
- **Migrações versionadas** do banco (em vez de `CREATE TABLE IF NOT EXISTS`), **logs estruturados**, métricas e **CI** rodando os testes, incluindo teste de carga.
- Paginação e filtros nas listas.

## 8. Uso de IA

Usei o Claude (Anthropic) do começo ao fim. O texto abaixo resume o que aconteceu de verdade; revise e reescreva com suas palavras antes de entregar, porque você precisa conseguir defender cada ponto.

### Principais prompts

1. Pedido inicial: construir o **OnboardKit** (ASP.NET Core MVC, Razor, Bootstrap, ADO.NET, padrão DAO, nível júnior, só as pastas Controllers/DAO/Models/Views) para controlar o estoque de um kit de boas-vindas, com solicitações de kits. *(resumo do pedido)*
2. "faça uma versão em SQLite" — troca do SQL Server pelo SQLite para rodar no meu Mac.
3. "O projeto não está respeitando essas regras: não deve ser possível retirar/reservar/bloquear uma quantidade superior à disponível; uma operação inválida não deve deixar o estoque em estado inconsistente. [funcionalidades Entrada, Saída, Reservar, Bloquear, Liberar, Consultar] … aplique funcionalidades que respeitem fielmente essas regras".
4. "em sua opinião, como eu poderia melhorar esse projeto?" — a IA sugeriu usuário nas movimentações, fila de prioridade etc.
5. Com o PDF do case em mãos: "refaça o programa da maneira mais simples possível respeitando os requisitos do pdf" — origem do projeto *Controle de Estoque*.
6. "quero um projeto exatamente igual a esse; o que muda é o layout, mais a tela de login, e para cadastrar um novo kit é necessário seguir as regras já existentes, com a única informação sendo o funcional do colaborador com 7 dígitos" — origem deste projeto.
7. "o kit vai ser apenas cadastrado; a única funcionalidade é conversar com a quantidade do estoque; quem coloca a quantidade sou eu, na aba principal, não na aba kit" — ajuste final: estoque geral na tela principal e cada kit cadastrado descontando 1 unidade.

### Onde a IA mais ajudou

- Estruturar o projeto, escrever os DAOs com transação e as views.
- Montar a bateria de testes (inclusive o de concorrência e o de "falha no meio desfaz tudo") e **provar que eles detectam erro**: quebrei a regra de propósito e os testes falharam.
- Portar para SQLite e explicar a diferença de trava (`BEGIN IMMEDIATE` no lugar de `UPDLOCK`).

### Sugestão da IA que decidi não usar

A versão anterior (OnboardKit) já tinha kits, solicitações, login, histórico com usuário e **fila de prioridade**. O case diz que uma solução simples e bem justificada vale mais que uma complexa, e nada disso é pedido. Decidi cortar tudo e ficar só com o domínio de estoque. Neste projeto o **login** voltou porque foi pedido explicitamente; o histórico de movimentações e a fila de prioridade continuam fora e estão listados em Limitações.

### Erro / limitação da IA

- **Regras não respeitadas:** a primeira versão era centrada em *kits* e não oferecia Reservar/Liberar por produto com identificação, como o case descreve; só percebi ao comparar com o enunciado e tive que cobrar ("o projeto não está respeitando essas regras"). Lição: dar o enunciado completo à IA e conferir cada requisito.
- **Sem o pacote real do SQLite no ambiente da IA:** o ambiente dela não conseguia baixar pacotes do NuGet. Ela validou o código com um substituto do `Microsoft.Data.Sqlite` (sobre a biblioteca SQLite nativa) e com um mini-executor no lugar do xUnit. **Rode `dotnet test` na sua máquina** para confirmar com os pacotes reais.

## 9. Visão de engenharia: 5 aspectos mais importantes

1. **Consistência do estoque.** É o coração do problema. Por isso: disponível calculado, validação antes da alteração, transação e `CHECK` no banco, três camadas que se protegem entre si.
2. **Concorrência.** Duas reservas simultâneas não podem vender a mesma unidade duas vezes. Transação `BEGIN IMMEDIATE` hoje; bloqueio por linha ou versão otimista quando houver um banco cliente-servidor.
3. **Testes que provam as regras.** Regras no Model (rápidos, sem banco) e integração com SQLite real (transação, rollback, concorrência). Cada regra do enunciado tem pelo menos um teste que falha se ela for quebrada.
4. **Simplicidade e legibilidade.** Poucas classes, regra em um lugar só, sem camadas que não pagam o seu custo. Quem lê entende rápido e consegue evoluir.
5. **Rastreabilidade e evolução.** Hoje há identificação por reserva/bloqueio; o próximo passo natural é um histórico imutável de movimentações (quem, quando, por quê), que dá auditoria e permite reconstruir qualquer saldo.
