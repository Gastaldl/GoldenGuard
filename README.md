# GoldenGuard — Web API + WebApp (.NET 8, Oracle, JWT)

**GoldenGuard** é uma solução voltada à **educação financeira e prevenção** de risco, com foco em monitorar e analisar movimentações (depósitos/saques) de sites de aposta/fintechs.
O sistema possui:

* **API** em ASP.NET Core (Minimal API) com **JWT** e papéis (**admin/usuário**).
* **WebApp** em Razor Pages (Bootstrap/Chart.js), com login e UI sensível a papel.
* **Oracle** como banco (Dapper), **importação/exportação JSON** e **gráfico mensal** de depósitos × saques.
* **KPI de risco mensal** (gastos do mês / renda mensal) e destaque quando acima de 30%.

---

## 1) Arquitetura & pastas

```
GoldenGuard.sln
│
├─ GoldenGuard/                     # Web API (Minimal API) 
│  ├─ Endpoints/                    # Endpoints (Auth, Users, Transactions)
│  ├─ Data/                         # OracleConnectionFactory, Scripts SQL (DDL/DML)
│  └─ appsettings.json              # Conn string Oracle + JWT
│
├─ GoldenGuard.WebApp/              # Razor Pages (UI)
│  ├─ Pages/                        # Index, Auth, Users, Transactions
│  ├─ Services/                     # ApiClient (HttpClient + JWT dos cookies)
│  └─ appsettings.json              # Api:BaseUrl (ex.: https://localhost:7170)
│
├─ GoldenGuard.Domain/              # Entidades + DTOs
├─ GoldenGuard.Infrastructure/      # Repositórios (Dapper)
└─ GoldenGuard.Application/         # Serviços de domínio (ex.: TransactionService)
```

**Tecnologias principais**

* .NET 8, ASP.NET Core Minimal API e Razor Pages
* Oracle + Dapper
* Autenticação **JWT** (Bearer) + Autorização por **roles** (admin/user)
* Chart.js no dashboard do WebApp

---

## 2) Banco de dados (Oracle)

**Conexão (exemplo)** no `appsettings.json` da **API**:

```json
{
  "ConnectionStrings": {
    "Oracle": "User Id=GG;Password=SUASENHA;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle.fiap.com.br)(PORT=1521))(CONNECT_DATA=(SID=ORCL)));"
  }
}
```

> Observações:
>
> * **HOST** correto: `oracle.fiap.com.br` (evitar typos).
> * **PORT**: `1521`, **SID**: `ORCL`.
> * Não é necessário client Oracle instalado: usamos `Oracle.ManagedDataAccess.Core` (managed).

**Tabelas principais** (resumo):

* `USERS (ID, NAME, EMAIL, MONTHLY_INCOME, CREATED_AT)`
* `TRANSACTIONS (ID, USER_ID, OPERATOR, KIND, AMOUNT, OCCURRED_AT, RAW_LABEL, CREATED_AT)`
* `USER_ACCOUNTS (ID, USER_ID, USERNAME, PASSWORD_HASH, ROLE, CREATED_AT)`

> Senhas **apenas para desenvolvimento**; em produção use hash (ex.: BCrypt).

**Dica de mapeamento Dapper + Oracle**

* Habilite `BindByName` nas conexões Oracle.
* Em SELECTs, use **aliases** para casar nomes Oracle com propriedades C#:

  ```sql
  SELECT MONTHLY_INCOME AS MonthlyIncome FROM USERS ...
  ```

  ou ligue `Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;` (Program.cs da API).

---

## 3) Autenticação & papéis

* **Login** via `/api/auth/login` (username/password) → retorna `{ token, role, userId }`.
* Cookies no WebApp:

  * `gg.jwt` (Bearer usado no ApiClient),
  * `gg.role` (admin/user),
  * `gg.userId` (usuário “ativo” na UI).

**Acesso**

* Toda a rota `/api/users` exige **login**; criar/editar/excluir exigem **admin**.
* Em `/api/transactions`, o grupo exige **login**; `POST/PUT/DELETE/import-json` exigem **admin**.

---

## 4) API — endpoints

### 4.1) Auth

* `POST /api/auth/login`
  **Body**: `{ "username": "admin", "password": "123456" }`
  **200**: `{ "token": "...", "role": "admin", "userId": 1 }`

### 4.2) Users

* `GET /api/users` → lista usuários (auth)
* `GET /api/users/{id}` → detalhe (auth)
* `POST /api/users` → cria (admin)
  **Body**: `{ "name": "...", "email": "...", "monthlyIncome": 4500 }`
  **201**: `Location: /api/users/{id}`, body `{ "id": 123 }`
* `PUT /api/users/{id}` → atualiza (admin)
* `DELETE /api/users/{id}` → exclui (admin)

### 4.3) Transactions

* `GET /api/transactions/by-user/{userId}?from=...&to=...` → lista (auth)
* `GET /api/transactions/{id}` → detalhe (auth)
* `POST /api/transactions` → cria (admin)
  **Body**:

  ```json
  {
    "userId": 2,
    "operator": "PIX *BET365",
    "kind": "DEPOSIT",
    "amount": 250.00,
    "occurredAt": "2025-01-05T10:15:00",
    "rawLabel": "PIX BET365 12345"
  }
  ```

  **201**: `Location: /api/transactions/{id}`, body `{ "id": 456 }`
* `PUT /api/transactions/{id}` → atualiza (admin)
* `DELETE /api/transactions/{id}` → exclui (admin)

#### Import/Export & Estatísticas

* `POST /api/transactions/import-json` (admin) — importa uma lista de transações e grava snapshot JSON.
* `GET /api/transactions/export-json/{userId}` (auth) — exporta as transações do usuário em JSON.
* `GET /api/transactions/risk/{userId}/{year}/{month}` (auth) → `{ ratioPercent, above30 }`
* `GET /api/transactions/stats/monthly/{userId}?year=2025` (auth) →
  `[{ "YEAR_MONTH": "2025-01", "DEPOSITS": 650, "WITHDRAWALS": 120 }, ...]`

> Na estatística mensal usamos binds **seguro** para Oracle (`:p_user_id`, `:p_year`) e `BindByName = true` para evitar `ORA-01745`.

---

## 5) WebApp — UI & funcionalidades

**Páginas principais**

* `/Auth/Login` — autenticação; ao logar, cookies `gg.jwt`, `gg.role`, `gg.userId` são definidos.
* `/` (Dashboard) — mostra:

  * **Gráfico** (Chart.js) de **Depósitos × Saques por mês** (consulta `/stats/monthly`).
  * **KPI de risco** do mês (consulta `/risk/...`).
  * **Transações recentes** do usuário ativo.
* `/Users` — lista de usuários; admins veem “Novo” e “Excluir”.
* `/Users/Create` — criação de usuário (admin).
* `/Transactions/ByUser` — filtro por usuário/intervalo; admins podem **adicionar transações**.

**Integração com a API**

* O `ApiClient` injeta o **Bearer** a partir do cookie `gg.jwt`.
* `appsettings.json` do WebApp deve conter a **URL absoluta** da API:

  ```json
  { "Api": { "BaseUrl": "https://localhost:7170" } }
  ```

**UI sensível a papel**

* Elementos de criação/exclusão são exibidos só para **admin** (UX).
* A **API** reforça a regra via policies (segurança real no backend).

---

## 6) Como rodar

### 6.1) Pré-requisitos

* **Visual Studio 2022** (ou .NET SDK 8)
* Oracle acessível (`oracle.fiap.com.br:1521`, `SID=ORCL`) e credenciais válidas
* Pacotes NuGet (API):

  * `Oracle.ManagedDataAccess.Core` (para .NET 8)
  * `Dapper`
  * `Swashbuckle.AspNetCore`
  * `Microsoft.AspNetCore.Authentication.JwtBearer` **8.x** (não 9.x)
* Pacotes NuGet (WebApp):

  * `Microsoft.AspNetCore.Authentication.Cookies` (se usar)
  * Nenhum adicional além dos padrões do template e do seu `ApiClient`

### 6.2) Configuração

**API (`GoldenGuard/appsettings.json`)**

```json
{
  "ConnectionStrings": {
    "Oracle": "User Id=GG;Password=SUASENHA;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle.fiap.com.br)(PORT=1521))(CONNECT_DATA=(SID=ORCL)));"
  },
  "Jwt": {
    "Key": "uma-chave-bem-grande-e-segura-para-dev",
    "Issuer": "GoldenGuard",
    "Audience": "GoldenGuard"
  },
  "AllowedHosts": "*"
}
```

**WebApp (`GoldenGuard.WebApp/appsettings.json`)**

```json
{
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  },
  "AllowedHosts": "*",
  "Api": {
    "BaseUrl": "https://localhost:7170"
  }
}
```

> Se a API subir em outra porta, ajuste `BaseUrl`.

**Opcional — CORS (se consumir API via JS no browser)**
Na API (Program.cs):

```csharp
builder.Services.AddCors(o => o.AddPolicy("WebApp", p =>
    p.WithOrigins("https://localhost:7275").AllowAnyHeader().AllowAnyMethod()));
...
app.UseCors("WebApp");
```

### 6.3) Rodando

1. **Crie o schema**/tabelas no Oracle (use os scripts do projeto ou os seus).
2. **Abra a solução no VS** → **Properties da Solution** → **Multiple startup projects**:

   * `GoldenGuard` (API): **Start**
   * `GoldenGuard.WebApp`: **Start**
3. **F5**. Abra:

   * API Swagger: `https://localhost:7170/swagger`
   * WebApp: `https://localhost:7275/`
4. **Login**: no WebApp, vá em `/Auth/Login` (ou use o Swagger `POST /api/auth/login`).

   * Usuários demo típicos: `admin / 123456` (admin), `marcio / 123456` (user).
5. Use o **Dashboard** e as páginas **Users**/**Transactions**.

---

## 7) Dicas de teste rápido

* **Login via Swagger**:

  1. `POST /api/auth/login` → pegue o `token`.
  2. Clique em **Authorize** no Swagger → cole `Bearer <token>`.
* **CRUD Users**:

  * `POST /api/users` (admin) → cria; `GET /api/users` → lista; `DELETE /api/users/{id}` (admin) → exclui.
* **Transactions**:

  * `POST /api/transactions` (admin) → cria.
  * `GET /api/transactions/by-user/{userId}` → lista.
  * `GET /api/transactions/stats/monthly/{userId}?year=2025` → dados do gráfico.

---

## 8) Solução de problemas comuns

* **401 Unauthorized no WebApp**
  → Token expirado/ausente. Faça login em `/Auth/Login`. O `ApiClient` usa o cookie `gg.jwt`.

* **InvalidOperationException: BaseAddress must be set**
  → No WebApp, configure `Api:BaseUrl` **absoluto** e registre o `HttpClient` com `BaseAddress`.

* **ORA-12545 (não resolve host)**
  → Host incorreto (verifique **oracle.fiap.com.br**), VPN/DNS e porta 1521.

* **ORA-00942 (tabela/visão não existe)**
  → Confirme o **schema** (usuário da conexão) e se as tabelas foram criadas ali.

* **ORA-01745 (bind inválido)**
  → Em Oracle, evite binds `:uid/:yy`; use `:p_user_id`, `:p_year` e `BindByName = true`.

* **Renda mensal aparecendo 0**
  → Garanta **aliases** nas colunas (`MONTHLY_INCOME AS MonthlyIncome`) ou ligue
  `Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true`.

* **Chart não carrega**
  → Verifique o fetch JS (Authorization: Bearer do cookie), CORS na API e se o endpoint `/stats/monthly` está ok.

---

## 9) Segurança (prod)

* **NUNCA** versionar a `Jwt:Key`. Use **User Secrets**/KeyVault.
* Use **hash de senha** (ex.: BCrypt) em `USER_ACCOUNTS`.
* Logue apenas o necessário (cuidado com dados sensíveis).
* Ative HTTPS obrigatório e políticas de CORS mínimas necessárias.

---

## 10) Roadmap (ideias)

* Exportar CSV/PDF direto da UI.
* Notificações por e-mail quando KPI de risco > 30%.
* Multi-tenant / segregação de dados.
* Auditoria mais detalhada com correlação (request id).
