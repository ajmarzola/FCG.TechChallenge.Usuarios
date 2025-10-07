# FCG.TechChallenge.Usuarios

> Microsserviço de **Usuários / Identidade** da plataforma **FIAP Cloud Games (FCG)** — evolução do MVP do repositório **Grupo49-TechChallenge**, agora em arquitetura de **microsserviços**, com autenticação baseada em **JWT**, suporte a **Azure AD B2C** (opcional) e integração com os serviços **Jogos** e **Pagamentos** via *claims* e *tokens*.

- **Usuários** (este repositório): cadastro, autenticação, perfis, emissão de tokens  
  https://github.com/ajmarzola/FCG.TechChallenge.Usuarios
- **Jogos**: catálogo, compra, busca e integrações (Elasticsearch)  
  https://github.com/ajmarzola/FCG.TechChallenge.Jogos
- **Pagamentos**: orquestração de transações e status por eventos  
  https://github.com/ajmarzola/FCG.TechChallenge.Pagamentos

🔎 **Projeto anterior (base conceitual):**  
https://github.com/ajmarzola/Grupo49-TechChallenge

🧭 **Miro – Visão de Arquitetura:**  
<https://miro.com/welcomeonboard/VXBnOHN6d0hWOWFHZmxhbzlMenp2cEV3N0FPQm9lUEZwUFVnWC9qWnUxc2ZGVW9FZnZ4SjNHRW5YYVBRTUJEWkFaTjZPNmZMcXFyWUNONEg3eVl4dEdOZWozd0J3RzZld08xM3E1cGl2dTR6QUlJSUVFSkpQcFVSRko1Z0hFSXphWWluRVAxeXRuUUgwWDl3Mk1qRGVRPT0hdjE=?share_link_id=964446466388>

---

## Sumário

- [Visão Geral](#visão-geral)
- [Arquitetura](#arquitetura)
- [Tecnologias](#tecnologias)
- [Como Rodar (Rápido)](#como-rodar-rápido)
- [Configuração por Ambiente](#configuração-por-ambiente)
- [Executando com .NET CLI](#executando-com-net-cli)
- [Executando com Docker](#executando-com-docker)
- [Fluxo de Teste End-to-End](#fluxo-de-teste-end-to-end)
- [Coleções/API Docs](#coleçõesapi-docs)
- [Estrutura do Repositório](#estrutura-do-repositório)
- [CI/CD](#cicd)
- [Roadmap](#roadmap)
- [Licença](#licença)

---

## Visão Geral

O **FCG.TechChallenge.Usuarios** centraliza o **cadastro e autenticação** dos usuários finais da plataforma (jogadores). Ele:
- Armazena credenciais e perfis (ASP.NET Identity ou provider externo).
- Emite **JWTs** com *claims* necessárias para autorização nos serviços **Jogos** e **Pagamentos**.
- Oferece endpoints de **registro**, **login**, **refresh** (se habilitado) e **perfil** (*whoami*).
- Pode operar com **Identity local** (DB relacional) ou integrar-se ao **Azure AD B2C** (delegated authentication).

Como evolução do **Grupo49-TechChallenge**, a responsabilidade de identidade foi extraída para um serviço isolado, simplificando a segurança e o acoplamento entre domínios.

---

## Arquitetura

- **API Usuários** (ASP.NET Core) — endpoints REST de autenticação/gestão de usuários.
- **Identity Store** — persistência (PostgreSQL/SQL Server) para usuários, papéis (*roles*), *claims* e *tokens*.
- **JWT Issuer** — emissor de tokens (`HS256` em dev; `RS256`/B2C em prod).
- **Integrações**:
  - **Jogos**: consome JWT para autorizar compras, biblioteca, etc.
  - **Pagamentos**: consome JWT para associar intents/ordens a um usuário.

> **Observabilidade**: logs estruturados, *correlation-id* nos headers e métricas de autenticação (sucesso/falha) são recomendadas e podem ser habilitadas via *middleware*.

---

## Tecnologias

- **.NET 8**
- **ASP.NET Core Identity**
- **JWT (Bearer)**
- **EF Core** (PostgreSQL/SQL Server)
- **Docker** (local/CI)
- **Azure AD B2C** (opcional em produção)
- **Swagger/OpenAPI**

---

## Como Rodar (Rápido)

Duas opções:

1) **.NET CLI (sem Docker)** – para desenvolvimento
2) **Docker** – ambiente isolado, próximo do deploy

> Antes de iniciar, configure variáveis e *connection strings* conforme a seção abaixo.

### Pré-requisitos

- .NET SDK 8.x  
- Docker + Docker Compose (para a opção 2)  
- Banco (PostgreSQL **ou** SQL Server) acessível/local

---

## Configuração por Ambiente

Use `appsettings.Development.json` **ou** variáveis de ambiente (recomendado).  
> Dica: `dotnet user-secrets` em dev: `dotnet user-secrets set "Chave" "Valor"`

| Chave (Environment) | Exemplo / Descrição |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` |
| `ConnectionStrings__Default` | `Host=localhost;Port=5432;Database=fcg_users;Username=dev;Password=dev` |
| `Identity__UseB2C` | `false` (ou `true` em prod) |
| `Jwt__Issuer` | `https://fcg-usuarios.local` (ou URL B2C) |
| `Jwt__Audience` | `fcg-api` |
| `Jwt__Key` | **(dev)** chave simétrica para `HS256` |
| `Jwt__SigningCert__Thumbprint` | **(prod)** se assinar com `RS256` |
| `Cors__AllowedOrigins__0` | `http://localhost:5173` (exemplo SPA) |
| `Admin__Seed__Email` | `admin@fcg.local` |
| `Admin__Seed__Password` | `P@ssword1!` |

> **Importante:** verifique os nomes reais das *sections* no `appsettings` do repo. Ajuste as chaves conforme sua implementação.

---

## Executando com .NET CLI

> Estrutura típica de solução: **Application**, **Domain**, **Infrastructure**, **Presentation** e **Test**.

1. Restaurar & compilar
   ```bash
   dotnet restore
   dotnet build -c Debug
   ```

2. Aplicar **migrations** (Identity DB)
   ```bash
   dotnet ef database update \
     -s FCG.TechChallenge.Usuarios.Presentation \
     -p FCG.TechChallenge.Usuarios.Infrastructure
   ```

3. Executar a **API**
   ```bash
   dotnet run -c Debug --project FCG.TechChallenge.Usuarios.Presentation
   ```
   - Por padrão, `http://localhost:5090` (ajuste conforme `launchSettings.json`).

4. (Opcional) Criar usuário admin *seed* via `IHostedService`/endpoint utilitário (se disponível) ou manualmente pelo endpoint de **registro**.

---

## Executando com Docker

> Este repo pode conter `docker-compose.yml` para levantar a API + banco rapidamente (ajuste conforme necessidade).

1. Buildar imagens
   ```bash
   docker compose build
   ```

2. Subir serviços
   ```bash
   docker compose up -d
   ```

3. Ver logs
   ```bash
   docker compose logs -f usuarios-api
   ```

> Se preferir usar um banco compartilhado (ex.: Postgres local já rodando), **remova** o serviço de banco do compose e aponte a connection string para esse host.

---

## Fluxo de Teste End-to-End

### 1) Registrar usuário
```bash
curl -X POST http://localhost:5090/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
        "email":"player01@fcg.local",
        "password":"P@ssword1!",
        "fullName":"Player One"
      }'
```

### 2) Login → obter **JWT**
```bash
curl -X POST http://localhost:5090/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
        "email":"player01@fcg.local",
        "password":"P@ssword1!"
      }'
# => { "access_token":"<JWT>", "expires_in":3600, "token_type":"Bearer" }
```

### 3) Perfil (*whoami*)
```bash
curl http://localhost:5090/api/users/me \
  -H "Authorization: Bearer <JWT>"
```

### 4) Consumir nos outros serviços

- **Jogos** e **Pagamentos** devem validar o token emitido por **Usuários** (mesmo `Issuer`/`Audience` e chave/cert).  
- Configure o *middleware* `AddAuthentication().AddJwtBearer(...)` nesses serviços com os mesmos parâmetros.

---

## Coleções/API Docs

- **Swagger/OpenAPI**: `http://localhost:<porta>/swagger`
- **Postman**: recomenda-se criar uma Collection com *Auth → Bearer Token*.
- **CORS**: ajuste a *origin* do seu frontend (SPA/MAUI) via `Cors__AllowedOrigins`.

---

## Estrutura do Repositório

```
FCG.TechChallenge.Usuarios/
├─ FCG.TechChallenge.Usuarios.Application/
├─ FCG.TechChallenge.Usuarios.Domain/
├─ FCG.TechChallenge.Usuarios.Infrastructure/
├─ FCG.TechChallenge.Usuarios.Presentation/
├─ FCG.TechChallenge.Usuarios.Test/
├─ docker-compose.yml
└─ FCG.TechChallenge.Usuarios.sln
```

> Os nomes de projetos/pastas podem variar sutilmente. Ajuste os comandos conforme o seu repo.

---

## CI/CD

- **GitHub Actions**: *build*, *tests*, *container publish* e *deploy*.  
- **Environments** (Dev/Homolog/Prod) com **aprovação manual** para Prod.  
- **OpenID Connect (OIDC)** + `azure/login` (se fizer deploy no Azure).  
- **Secrets** nos Environments (ex.: `JWT__KEY`, `CONNECTIONSTRINGS__DEFAULT`).  
- Em cloud, use **Key Vault** para chaves/certificados de assinatura do JWT.

> O repositório **Grupo49-TechChallenge** contém pipelines que servem de referência de estrutura, *gates* e convenções.

---

## Roadmap

- [ ] **Refresh tokens** com *rotation* e *reuse detection*  
- [ ] **2FA** (TOTP) e e-mail de verificação/recuperação de senha  
- [ ] **RBAC** → papéis (`Admin`, `User`) e *policies* baseadas em *claims*  
- [ ] Integração com **Azure AD B2C** (produção) com *custom policies*  
- [ ] **Rate limiting** nos endpoints de login/registro  
- [ ] Tracing distribuído (W3C) e *auditoria* de acessos

---

## Licença

Projeto acadêmico, parte do **Tech Challenge FIAP**. Verifique os termos aplicáveis a cada repositório.

## 👥 Integrantes do Grupo
• Anderson Marzola — RM360850 — Discord: aj.marzola
• Rafael Nicoletti — RM361308 — Discord: rafaelnicoletti_
• Valber Martins — RM3608959 — Discord: valberdev
