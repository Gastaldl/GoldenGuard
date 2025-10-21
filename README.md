# GoldenGuard — Web API + WebApp (.NET 8, Oracle, JWT, EF Core)

**GoldenGuard** é uma solução de **educação financeira e prevenção de risco**, voltada a monitorar e analisar movimentações (depósitos/saques) associadas a plataformas de aposta/fintechs.
O sistema entrega:

* **API** em ASP.NET Core (Minimal API) com **JWT** e papéis (**admin/usuário**).
* **WebApp** (Razor Pages + Bootstrap/Chart.js), com login e UI sensível a papel.
* **Oracle** via **EF Core** (migrations), **importação/exportação JSON** e **gráfico mensal** Depósitos × Saques.
* **KPI de risco mensal** (gastos do mês / renda) com alerta quando **> 30%**.
* **Integrações externas**: conversão de câmbio (ExchangeRate), manchetes (NewsAPI) e **resumo consultivo (OpenAI)**.

## Integrantes

* Márcio Gastaldi — RM98811
* Arthur Bessa Pian — RM99215
* Davi Desenzi — RM550849
* João Victor — RM551410

---

## 0) Visão — Solução & Aplicabilidade no Projeto

O GoldenGuard atua como “painel financeiro” de **conscientização e prevenção**. Centraliza transações por usuário, identifica concentração de gastos ligados a apostas e calcula um **índice de risco mensal** comparando o total movimentado com a **renda declarada**.

**Aplicabilidade prática**

* **Educação financeira**: dashboard e gráfico mensal mostram padrões de depósitos/saques.
* **Prevenção de risco**: o **KPI mensal** sinaliza quando os gastos ultrapassam **30% da renda**.
* **Operação**:

  * **Admin** cadastra usuários, importa extratos (JSON), registra transações e acompanha estatísticas.
  * **Usuário** visualiza histórico e indicadores, fomentando autoavaliação.
* **Integração**: endpoints REST + JWT para ingestões futuras (CSV/PDF, ETL, Open Banking) e serviços externos.
* **Governança**: trilhas de auditoria (arquivo de log/audit) e separação de papéis.

---

## 1) Arquitetura & pastas

```
GoldenGuard/
├─ GoldenGuard.WebApi/                         # Projeto Web API (ASP.NET Core Minimal API)
│  ├─ Program.cs
│  ├─ appsettings.json                         # JWT, Swagger toggle, chaves (sem credenciais reais no repo)
│  ├─ Properties/
│  │  └─ launchSettings.json                   # portas/URLs de desenvolvimento
│  ├─ Data/
│  │  ├─ GgDbContext.cs                        # EF Core (Oracle)
│  │  ├─ Migrations/                           # Migrations EF (geradas por 'dotnet ef')
│  │  └─ Scripts.sql                           # (opcional) DDL/DML para criar/popular o Oracle
│  ├─ Domain/
│  │  ├─ Entities/
│  │  │  ├─ UserProfile.cs
│  │  │  ├─ Transaction.cs
│  │  │  └─ UserAccount.cs
│  │  └─ DTOs/
│  │     ├─ CreateUserDto.cs
│  │     ├─ UpdateUserDto.cs
│  │     ├─ CreateTransactionDto.cs
│  │     └─ UpdateTransactionDto.cs
│  ├─ Infrastructure/
│  │  └─ Repositories/
│  │     ├─ IUserRepository.cs
│  │     ├─ ITransactionRepository.cs
│  │     ├─ EfUserRepository.cs                # EF Core
│  │     └─ EfTransactionRepository.cs         # EF Core
│  ├─ Application/
│  │  └─ Services/
│  │     ├─ TransactionService.cs              # KPI de risco e agregações
│  │     └─ FileStorageService.cs              # JSON/TXT (auditoria, snapshots)
│  ├─ Endpoints/
│  │  ├─ AuthEndpoints.cs                      # /api/auth/login (JWT, roles)
│  │  ├─ UsersEndpoints.cs                     # /api/users (CRUD via EF)
│  │  ├─ TransactionsEndpoints.cs              # /api/transactions (CRUD, import/export, stats via EF/SQL)
│  │  ├─ IntegrationsEndpoints.cs              # /api/integrations/* (câmbio, notícias)
│  │  └─ InsightsEndpoints.cs                  # /api/insights/summary (OpenAI)
│  └─ Files/                                   # Saída em runtime (gitignore)
│     ├─ transactions.json
│     └─ audit.log
│
├─ GoldenGuard.WebApp/                         # Frontend (Razor Pages)
│  ├─ Program.cs
│  ├─ appsettings.json                         # Api:BaseUrl (ex.: https://localhost:7170)
│  ├─ Properties/
│  │  └─ launchSettings.json                   # porta/URL do WebApp
│  ├─ Services/
│  │  └─ ApiClient.cs                          # HttpClient + cookies gg.jwt/gg.role/gg.userId
│  ├─ Pages/
│  │  ├─ _ViewImports.cshtml
│  │  ├─ _ViewStart.cshtml
│  │  ├─ Shared/_Layout.cshtml                 # Navbar (login/logout, papel)
│  │  ├─ Index.cshtml                          # Dashboard (Chart.js + KPI + Insights)
│  │  ├─ Index.cshtml.cs
│  │  ├─ Auth/
│  │  │  ├─ Login.cshtml                       # Login (define cookies)
│  │  │  └─ Login.cshtml.cs
│  │  ├─ Users/
│  │  │  ├─ Index.cshtml                       # Listar/Excluir usuários (admin)
│  │  │  ├─ Index.cshtml.cs
│  │  │  ├─ Create.cshtml                      # Criar usuário (admin)
│  │  │  └─ Create.cshtml.cs
│  │  └─ Transactions/
│  │     ├─ ByUser.cshtml                      # Lista por usuário + filtro período
│  │     └─ ByUser.cshtml.cs                   # Criação rápida (admin)
│  └─ wwwroot/
│     └─ css/site.css
│
└─ GoldenGuard.sln
```

**Stack**

* .NET 8, ASP.NET Core Minimal API e Razor Pages
* **Oracle + EF Core** (com migrations)
* Autenticação **JWT** + **roles** (admin/user)
* Chart.js no dashboard
* Integrações: ExchangeRate, NewsAPI, OpenAI

---

## 2) Banco de dados (Oracle + EF Core)

* **Conexão**: configure via **User Secrets** (local) e **Application Settings** (Azure).
* **Criação/Atualização**: a API chama `db.Database.Migrate()` na inicialização (aplica migrations pendentes).
* **Scripts opcionais**: `Data/Scripts.sql` contém DDL/DML de referência.

**Tabelas**

* `USERS (ID, NAME, EMAIL, MONTHLY_INCOME, CREATED_AT)`
* `TRANSACTIONS (ID, USER_ID, OPERATOR, KIND, AMOUNT, OCCURRED_AT, RAW_LABEL, CREATED_AT)`
* `USER_ACCOUNTS (ID, USER_ID, USERNAME, PASSWORD_HASH, ROLE, CREATED_AT)`

> **Produção**: use **hash de senha** (ex.: BCrypt) e gerencie chaves JWT/segredos com segurança (User Secrets/Key Vault).

---

## 3) Autenticação & papéis

* `POST /api/auth/login` → `{ token, role, userId }`.
* WebApp define cookies:

  * `gg.jwt` (Bearer para o ApiClient),
  * `gg.role` (admin/user),
  * `gg.userId` (usuário ativo na UI).

**Regras**

* `/api/users`: login obrigatório; criar/editar/excluir = **admin**.
* `/api/transactions`: login obrigatório; criar/editar/excluir/import = **admin**.

---

## 4) API — Endpoints

### 4.1) Auth

* `POST /api/auth/login`
  Body: `{ "username": "...", "password": "..." }`
  **200** → `{ "token": "...", "role": "admin", "userId": 1 }`

### 4.2) Users (auth)

* `GET /api/users` — listar
* `GET /api/users/{id}` — detalhe
* `POST /api/users` (admin) — cria → **201** `{ "id": 123 }`
* `PUT /api/users/{id}` (admin) — atualiza
* `DELETE /api/users/{id}` (admin) — remove

### 4.3) Transactions (auth)

* `GET /api/transactions/by-user/{userId}?from=&to=` — listar por usuário/período
* `GET /api/transactions/{id}` — detalhe
* `POST /api/transactions` (admin) — cria → **201** `{ "id": 456 }`
* `PUT /api/transactions/{id}` (admin) — atualiza
* `DELETE /api/transactions/{id}` (admin) — remove

**Import/Export & Estatísticas**

* `POST /api/transactions/import-json` (admin) — importa lista e salva snapshot JSON.
* `GET /api/transactions/export-json/{userId}` (auth) — exporta JSON do usuário.
* `GET /api/transactions/risk/{userId}/{year}/{month}` (auth) → `{ ratioPercent, above30 }`
* `GET /api/transactions/stats/monthly/{userId}?year=YYYY` (auth) →
  `[{ "YEAR_MONTH": "2025-01", "DEPOSITS": 650, "WITHDRAWALS": 120 }, ...]`

### 4.4) Integrações externas (auth)

* `GET /api/integrations/fx/convert?from=USD&to=BRL&amount=100`

  * Usa **ExchangeRate** (sem API key).
* `GET /api/integrations/news/top?country=br&category=business`

  * Usa **NewsAPI** (requer `NewsApi:ApiKey`).
* `POST /api/insights/summary`

  * Usa **OpenAI** (`OpenAI:ApiKey` e `OpenAI:Model`), gera **resumo consultivo** com base no total de depósitos/saques (últimos *n* meses).

> **Custo/limites**: NewsAPI e OpenAI podem ter custo/limites de uso (veja seus planos).

---

## 5) WebApp — UI & funcionalidades

* `/Auth/Login`: login; define `gg.jwt`, `gg.role`, `gg.userId`.
* `/`: **Dashboard** — gráfico (Chart.js) Depósitos × Saques do ano, KPI do mês e últimas transações; **Insights** (resumo) quando configurado.
* `/Users`: lista (admin visualiza **Novo**/**Excluir**).
* `/Users/Create`: cadastro (admin).
* `/Transactions/ByUser`: filtro por usuário/intervalo; **admin** pode adicionar transações.

**Integração**
O `ApiClient` lê `gg.jwt` e envia `Authorization: Bearer ...`.
O WebApp usa `Api:BaseUrl` para montar as chamadas (ex.: `https://localhost:7170` em dev).

---

## 6) Configuração (Local)

### 6.1) Pré-requisitos

* **Visual Studio 2022** (ou .NET 8 SDK)
* Oracle acessível (host/porta/SID e credenciais do seu ambiente)
* Pacotes (API): `Oracle.ManagedDataAccess.Core`, `Microsoft.EntityFrameworkCore`, `Oracle.EntityFrameworkCore`, `Swashbuckle.AspNetCore`, `Microsoft.AspNetCore.Authentication.JwtBearer` (8.x)

### 6.2) Segredos (User Secrets)

**API — `appsettings.json` (exemplo sem segredos):**

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*",
  "Swagger": { "Enable": true }
}
```

**Defina segredos localmente (User Secrets)**:

```bash
# API
dotnet user-secrets set "ConnectionStrings:Oracle" "User Id=SEU_USUARIO;Password=SUA_SENHA;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle.fiap.com.br)(PORT=1521))(CONNECT_DATA=(SID=ORCL)));"
dotnet user-secrets set "Jwt:Key" "uma-chave-bem-grande-e-segura"
dotnet user-secrets set "Jwt:Issuer" "GoldenGuard"
dotnet user-secrets set "Jwt:Audience" "GoldenGuard"
dotnet user-secrets set "NewsApi:ApiKey" "SUA_NEWSAPI_KEY"
dotnet user-secrets set "OpenAI:ApiKey" "SUA_OPENAI_KEY"
dotnet user-secrets set "OpenAI:Model" "gpt-4o-mini"
```

**WebApp — `appsettings.json`:**

```json
{ "Api": { "BaseUrl": "https://localhost:7170" } }
```

### 6.3) Executar

1. No VS, **Multiple startup projects**:

   * `GoldenGuard.WebApi` → Start
   * `GoldenGuard.WebApp` → Start
2. **F5**:

   * API (Swagger): `https://localhost:7170/swagger`
   * WebApp: `https://localhost:7275/`
3. **Login** no WebApp (`/Auth/Login`) — usuários demo (seeds/SQL conforme seu ambiente).

> **Cert HTTPS**: se der erro de certificado no WebApp chamando a API local, confie no **certificado de desenvolvimento** (.NET/Dev-certs) ou troque para HTTP em dev (não recomendado).

---

## 7) Publicação (Azure App Service)

* Publique **API** e **WebApp** como **Web App** separados.
* Em **Configuration** (Application Settings) do App Service da **API**, crie:

  * `ConnectionStrings:Oracle` (valor completo da connection string)
  * `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`
  * `Swagger:Enable = true` (opcional em prod)
  * `NewsApi:ApiKey` (se for usar `/news/top`)
  * `OpenAI:ApiKey` e `OpenAI:Model`
* Ative **HTTPS Only**, configure **CORS** para o host do WebApp.
* Garanta que o App Service **tenha rota externa** para o host do Oracle (firewall/VPN conforme seu ambiente).

---

## 8) Segurança (produção)

* **NÃO** versionar **ConnectionStrings** e **Jwt:Key**.
* Use **hash de senha** em `USER_ACCOUNTS`.
* Mantenha **CORS mínimo**, **HTTPS** obrigatório e logs sem dados sensíveis.
* **GitHub Push Protection**: se um commit incluiu chave por engano, reescreva histórico (BFG ou `git filter-repo`) e force-push.

---

## 9) Solução de problemas

* **401 no WebApp** → token ausente/expirado. Faça login em `/Auth/Login`.
* **InvalidOperationException: BaseAddress must be set** → configure `Api:BaseUrl` no WebApp e registre `HttpClient` com `BaseAddress`.
* **ORA-00942** → crie as tabelas no **schema** correto (migrations rodando na API).
* **ORA-01745** → use binds nomeados e `BindByName` (caso esteja com SQL manual).
* **Timeout/ORA-50000 no Azure** → confirme **network outbound** do App Service até o host Oracle (firewall/VNet/Private Link).
* **Certificados (dev)** → confie no *dev-certs* ou use HTTP apenas em ambiente de desenvolvimento.
