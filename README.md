# GoldenGuard — Web API + WebApp (.NET 8, Oracle, JWT)

**GoldenGuard** é uma solução de **educação financeira e prevenção de risco**, voltada a monitorar e analisar movimentações (depósitos/saques) associadas a plataformas de aposta/fintechs.
O sistema entrega:

* **API** em ASP.NET Core (Minimal API) com **JWT** e papéis (**admin/usuário**).
* **WebApp** (Razor Pages + Bootstrap/Chart.js), com login e UI sensível a papel.
* Banco **Oracle** (Dapper), **importação/exportação JSON** e **gráfico mensal** Depósitos × Saques.
* **KPI de risco mensal** (gastos do mês / renda) com alerta quando **> 30%**.

---

## 0) Visão — Solução & Aplicabilidade no Projeto

**Ideia**
GoldenGuard atua como um “painel financeiro” de conscientização e prevenção. Ele centraliza transações por usuário, identifica concentração de gastos ligados a apostas e calcula um **índice de risco mensal** comparando o total movimentado com a **renda declarada**.

**Aplicabilidade prática (no contexto Golden Guard / FIAP)**

* **Educação financeira**: dashboard e gráfico mensal mostram rapidamente padrões de depósitos/saques, facilitando conversas de orientação.
* **Prevenção de risco**: o **KPI mensal** destaca quando os gastos ultrapassam **30% da renda**, servindo como sinal de alerta.
* **Operação**:

  * **Admin** cadastra usuários, importa extratos (JSON), registra transações e acompanha estatísticas.
  * **Usuário** visualiza seu histórico e indicadores, fomentando a autoavaliação.
* **Integração**: endpoints simples (REST + JWT) permitem ingestões futuras de CSV/PDF, ETL ou integrações com outros sistemas.
* **Governança**: trilhas de auditoria (arquivo de log/audit) e separação de papéis, reduzindo exposição a dados sensíveis.

---

## 1) Arquitetura & pastas

```
GoldenGuard/
├─ GoldenGuard.WebApi/                     # Projeto Web API (ASP.NET Core Minimal API)
│  ├─ Program.cs
│  ├─ appsettings.json                     # JWT, logs etc. (sem credenciais no repo)
│  ├─ Properties/
│  │  └─ launchSettings.json               # portas/URLs de desenvolvimento
│  ├─ Data/
│  │  ├─ OracleConnectionFactory.cs        # fábrica de conexões (ODP.NET + BindByName)
│  │  └─ Scripts.sql                       # DDL/DML para criar e popular o Oracle
│  ├─ Domain/
│  │  ├─ Entities/
│  │  │  ├─ UserProfile.cs
│  │  │  └─ Transaction.cs
│  │  └─ DTOs/
│  │     ├─ CreateUserDto.cs
│  │     ├─ UpdateUserDto.cs
│  │     ├─ CreateTransactionDto.cs
│  │     └─ UpdateTransactionDto.cs
│  ├─ Infrastructure/
│  │  └─ Repositories/
│  │     ├─ IUserRepository.cs
│  │     ├─ ITransactionRepository.cs
│  │     ├─ UserRepository.cs              # Dapper + aliases (MONTHLY_INCOME AS MonthlyIncome)
│  │     └─ TransactionRepository.cs
│  ├─ Application/
│  │  └─ Services/
│  │     ├─ TransactionService.cs          # KPI de risco, agregações de domínio
│  │     └─ FileStorageService.cs          # JSON/TXT (auditoria, snapshots)
│  ├─ Endpoints/
│  │  ├─ AuthEndpoints.cs                  # /api/auth/login (JWT, roles)
│  │  ├─ UsersEndpoints.cs                 # /api/users (CRUD)
│  │  └─ TransactionsEndpoints.cs          # /api/transactions (+ import/export/stats)
│  └─ Files/                               # Saída em runtime (gitignore)
│     ├─ transactions.json
│     └─ audit.log
│
├─ GoldenGuard.WebApp/                     # Frontend (Razor Pages)
│  ├─ Program.cs
│  ├─ appsettings.json                     # Api:BaseUrl (ex.: https://localhost:7170)
│  ├─ Properties/
│  │  └─ launchSettings.json               # porta/URL do WebApp
│  ├─ Services/
│  │  └─ ApiClient.cs                      # HttpClient + cookies gg.jwt/gg.role/gg.userId
│  ├─ Pages/
│  │  ├─ _ViewImports.cshtml
│  │  ├─ _ViewStart.cshtml
│  │  ├─ Shared/_Layout.cshtml             # Navbar (login/logout, papel)
│  │  ├─ Index.cshtml                      # Dashboard (Chart.js + KPI)
│  │  ├─ Index.cshtml.cs
│  │  ├─ Auth/
│  │  │  ├─ Login.cshtml                   # Login (define cookies)
│  │  │  └─ Login.cshtml.cs
│  │  ├─ Users/
│  │  │  ├─ Index.cshtml                   # Listar/Excluir usuários (admin)
│  │  │  ├─ Index.cshtml.cs
│  │  │  ├─ Create.cshtml                  # Criar usuário (admin)
│  │  │  └─ Create.cshtml.cs
│  │  └─ Transactions/
│  │     ├─ ByUser.cshtml                  # Lista por usuário + filtro período
│  │     └─ ByUser.cshtml.cs               # Criação rápida (admin)
│  └─ wwwroot/
│     └─ css/site.css
│
└─ GoldenGuard.sln                         # Solução (ambos os projetos)
```

**Stack**

* .NET 8, ASP.NET Core Minimal API e Razor Pages
* Oracle + Dapper
* Autenticação **JWT** (Bearer) + Autorização por **roles** (admin/user)
* Chart.js no dashboard do WebApp

---

## 2) Banco de dados (Oracle)

> ⚠️ **Sem credenciais no repositório.** A conexão do Oracle deve ser configurada via **User Secrets** (ou variável de ambiente).
> Os **scripts** de criação/população estão em: `GoldenGuard/Data/Scripts.sql`.

**Tabelas principais**

* `USERS (ID, NAME, EMAIL, MONTHLY_INCOME, CREATED_AT)`
* `TRANSACTIONS (ID, USER_ID, OPERATOR, KIND, AMOUNT, OCCURRED_AT, RAW_LABEL, CREATED_AT)`
* `USER_ACCOUNTS (ID, USER_ID, USERNAME, PASSWORD_HASH, ROLE, CREATED_AT)`

**Dicas Dapper + Oracle**

* Ative `BindByName = true` nas conexões Oracle.
* Em SELECTs, use **aliases**: `MONTHLY_INCOME AS MonthlyIncome`, etc.
  (ou ligue `Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true` no `Program.cs` da API).

---

## 3) Autenticação & papéis

* `POST /api/auth/login` (username/password) → `{ token, role, userId }`.
* Cookies no WebApp:

  * `gg.jwt` (Bearer para o ApiClient),
  * `gg.role` (admin/user),
  * `gg.userId` (usuário ativo na UI).

**Regras**

* `/api/users`: login obrigatório; criar/editar/excluir = **admin**.
* `/api/transactions`: login obrigatório; criar/editar/excluir/import = **admin**.

---

## 4) API — endpoints

### 4.1) Auth

* `POST /api/auth/login`
  **Body**: `{ "username": "admin", "password": "123456" }`
  **200** → `{ "token": "...", "role": "admin", "userId": 1 }`

### 4.2) Users

* `GET /api/users` (auth) — lista
* `GET /api/users/{id}` (auth) — detalhe
* `POST /api/users` (admin) — cria
  Body: `{ "name": "...", "email": "...", "monthlyIncome": 4500 }`
  **201** com `Location` + corpo `{ "id": 123 }`
* `PUT /api/users/{id}` (admin)
* `DELETE /api/users/{id}` (admin)

### 4.3) Transactions

* `GET /api/transactions/by-user/{userId}?from=...&to=...` (auth) — listar
* `GET /api/transactions/{id}` (auth) — detalhe
* `POST /api/transactions` (admin) — cria
  Body:

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

  **201** com `Location` + corpo `{ "id": 456 }`
* `PUT /api/transactions/{id}` (admin)
* `DELETE /api/transactions/{id}` (admin)

#### Import/Export & Estatísticas

* `POST /api/transactions/import-json` (admin) — importa lista e salva snapshot JSON.
* `GET /api/transactions/export-json/{userId}` (auth) — exporta JSON do usuário.
* `GET /api/transactions/risk/{userId}/{year}/{month}` (auth) → `{ ratioPercent, above30 }`
* `GET /api/transactions/stats/monthly/{userId}?year=2025` (auth) →
  `[{ "YEAR_MONTH": "2025-01", "DEPOSITS": 650, "WITHDRAWALS": 120 }, ...]`
  (biding nomeado e `BindByName = true` para Oracle).

---

## 5) WebApp — UI & funcionalidades

* `/Auth/Login`: login; define `gg.jwt`, `gg.role`, `gg.userId`.
* `/`: **Dashboard** — gráfico (Chart.js) Depósitos × Saques do ano, KPI de risco e últimas transações.
* `/Users`: lista (admin visualiza “Novo”/“Excluir”).
* `/Users/Create`: cadastro (admin).
* `/Transactions/ByUser`: filtro por usuário/intervalo; **admin** pode adicionar transações.

**Integração**
O `ApiClient` lê `gg.jwt` dos cookies e envia `Authorization: Bearer ...` para a API.
O WebApp usa `Api:BaseUrl` para montar as chamadas.

---

## 6) Como rodar

### 6.1) Pré-requisitos

* **Visual Studio 2022** (ou .NET 8 SDK)
* Acesso a Oracle (credenciais e host fornecidos pelo seu ambiente)
* NuGet na **API**: `Oracle.ManagedDataAccess.Core`, `Dapper`, `Swashbuckle.AspNetCore`, `Microsoft.AspNetCore.Authentication.JwtBearer` **8.x**
* NuGet no **WebApp**: padrão do template + seu `ApiClient`

### 6.2) Configuração de **segredos** (sem vazar conexão)

> Use **User Secrets** no projeto **GoldenGuard (API)**. Não comite credenciais no repo.

```bash
# No diretório do projeto GoldenGuard (API):
dotnet user-secrets init

# 1) Connection string do Oracle (use a conexão fornecida pelo seu ambiente)
dotnet user-secrets set "ConnectionStrings:Oracle" "<SUA-CONNECTION-STRING-ORACLE>"

# 2) Chave JWT (apenas DEV; em produção use Key Vault)
dotnet user-secrets set "Jwt:Key" "uma-chave-bem-grande-e-segura-para-dev"

# (Opcional) Issuer/Audience se quiser personalizar
dotnet user-secrets set "Jwt:Issuer" "GoldenGuard"
dotnet user-secrets set "Jwt:Audience" "GoldenGuard"
```

No **WebApp**, informe a URL da API (pode ficar no `appsettings.json` ou também em secrets):

```json
// GoldenGuard.WebApp/appsettings.json
{
  "Api": { "BaseUrl": "https://localhost:7170" }
}
```

> Se a API rodar em outra porta/host, ajuste `BaseUrl`.

### 6.3) Banco de dados

* Execute o script: `GoldenGuard/Data/Scripts.sql` no **schema** que a API usa.

### 6.4) Executar

1. No VS, defina **Multiple startup projects**:

   * `GoldenGuard` (API) → **Start**
   * `GoldenGuard.WebApp` → **Start**
2. **F5**:

   * Swagger da API: `https://localhost:7170/swagger`
   * WebApp: `https://localhost:7275/`
3. **Login** no WebApp (`/Auth/Login`) — usuários de exemplo:

   * `admin / 123456` (admin)
   * `marcio / 123456` (user)

---

## 7) Dicas de teste rápido

* **Swagger** → `POST /api/auth/login` → copie o `token` → **Authorize** → `Bearer <token>`.
* **Users** → `POST /api/users` (admin) → `GET /api/users` → `DELETE /api/users/{id}`.
* **Transactions** → `POST /api/transactions` (admin) → `GET /api/transactions/by-user/{id}` → `GET /api/transactions/stats/monthly/{id}?year=...`.

---

## 8) Solução de problemas

* **401 no WebApp** → token ausente/expirado. Faça login em `/Auth/Login` (cookie `gg.jwt`).
* **InvalidOperationException: BaseAddress must be set** → configure `Api:BaseUrl` no WebApp e registre o `HttpClient` com `BaseAddress`.
* **ORA-00942** → crie as tabelas no **schema** correto (rode o `Scripts.sql`).
* **ORA-01745** → use binds nomeados (`:p_user_id`, `:p_year`) e `BindByName = true`.
* **Renda mensal “0”** → garanta aliases nos SELECTs (`MONTHLY_INCOME AS MonthlyIncome`) ou ligue `DefaultTypeMap.MatchNamesWithUnderscores`.

---

## 9) Segurança (produção)

* **NÃO** versionar `ConnectionStrings` e `Jwt:Key`. Use **User Secrets**/Key Vault/variáveis de ambiente.
* Hash de senha em `USER_ACCOUNTS` (ex.: BCrypt).
* Menor CORS possível, HTTPS obrigatório e logs sem dados sensíveis.

---

## 10) Roadmap

* Upload/Parser de CSV/PDF direto na UI.
* Alertas proativos quando KPI > 30%.
* Multi-tenant / segregação por organização.
* Auditoria expandida e relatórios.
