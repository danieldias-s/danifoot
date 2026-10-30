# Elifoot Classic Multiplayer — Edição 98

Jogo original de gestão de futebol inspirado na experiência clássica do **Elifoot 98**, desenvolvido do zero com servidor autoritativo em **C# / ASP.NET Core .NET 10 + SignalR + EF Core + PostgreSQL** e cliente unificado **Flutter (Web e Android)**.

Suporta **1 a 8 treinadores humanos simultâneos** na mesma sala com contador de preparação sincronizado (`TÁTICAS ENVIADAS: 0/6`), relógio de partida compartilhado minuto a minuto, **Parada Global (`PEDIR PARADA` congela todas as partidas da rodada)**, **5 substituições em 3 oportunidades** no tempo normal (**6 substituições em 4 oportunidades** na prorrogação da Taça), **128 clubes no Brasil** (4 divisões de 8 + 96 distritais nas preliminares da Taça de 64), técnicos de IA com filosofia tática persistente e mercado bilateral racional.

---

## QA jogável 002 — iniciar sem PostgreSQL

Auditoria e evidências reais: [docs/PLAYABLE_QA_002.md](docs/PLAYABLE_QA_002.md). Inclui temporada completa pela interface, oito navegadores, pausa global, negociação, editor, simulação de 100 temporadas e APK de depuração. O diagnóstico e a validação nativa posteriores estão em [docs/ANDROID_RUNTIME_003.md](docs/ANDROID_RUNTIME_003.md); consulte seu resultado consolidado antes de considerar Android aprovado.

A auditoria Android 003 terminou **PARTIAL**: instalação, plantel, REST e conexão SignalR inicial comprovados; partida Web + Android bloqueada pela instabilidade do emulador sem aceleração. Há APKs normais debug e profile e roteiro para validação em Android estável no relatório.

A investigação [ANDROID_ROOT_CAUSE_004](docs/ANDROID_ROOT_CAUSE_004.md) reproduziu ANRs de serviços Android em dois AVDs limpos antes da instalação do Elifoot, inclusive em espera passiva. O Flutter mínimo abriu, mas teve desempenho severamente degradado. A causa exclusiva dos atrasos permanece inconclusiva; gameplay Android continua sem aprovação. O relatório contém bugreport, controles, capturas e procedimento para aparelho físico.

Em dois terminais PowerShell, a partir de `D:\elifoot_98`:

```powershell
# Terminal 1: API e save persistente em elifoot-local.db
.\scripts\run-dev.ps1 -DatabaseProvider Sqlite -ApiPort 5080

# Terminal 2: compilar e servir o Flutter Web
.\scripts\run-web.ps1 -WebPort 8083
```

Abra **http://127.0.0.1:8083**. Entre no lobby, crie uma sala de **1 treinador** para jogar sozinho ou reúna os participantes antes de enviar as táticas. Escolha um clube e use **AUTO-ESCALAR 11 + BANCO**, **ENVIAR TÁTICA**, os comandos de intervalo e **AVANÇAR PRÓXIMA RODADA**. Na última rodada, esse comando inicia a temporada seguinte. A identidade Web é independente por aba e sobrevive ao recarregamento da mesma aba.

A porta 8083 evita o cache de versões antigas que podem ter registrado um service worker na porta 8080. Os novos builds locais usam `--pwa-strategy=none`.

O APK normal de teste é entregue em `artifacts/android/elifoot-qa003-debug.apk` pelo script `scripts/build-android-qa.ps1`. A pasta `build/` também recebe APKs de testes automatizados, portanto use a cópia de `artifacts/` para jogar. O endereço padrão Android DEV é `http://10.0.2.2:5080`; para outro servidor, altere `API_BASE_URL` no build. Para telefone via USB, use `scripts/run-android-qa.ps1 -Transport UsbReverse -DeviceSerial SERIAL`. HTTP é permitido nos builds DEV debug/profile; release nativo exige HTTPS explícito. Não é uma distribuição de produção.

---

## 1. Estrutura do Repositório

```text
/src/Elifoot.Contracts          # DTOs, contratos REST/SignalR e enums compartilhados
/src/Elifoot.Domain             # Motor de partidas, táticas, IA de treinadores, mercado, estádios e ligas/Taça
/src/Elifoot.Application        # Orquestrador multiplayer autoritativo, concorrência, idempotência e casos de uso
/src/Elifoot.Infrastructure     # EF Core DbContext (PostgreSQL / SQLite), repositórios e persistência de snapshots
/src/Elifoot.Api                # API REST + SignalR Hub (/hubs/gameroom) + serviço de relógio de salas
/client/elifoot_flutter         # Cliente único Flutter para Web e Android (layout clássico verde de tabelas densas)
/tools/seed-generator           # CLI geradora e validadora do pacote inicial seed-128-clubs.json (+ CSV)
/tests/Elifoot.Domain.Tests     # Testes unitários e de monotonicidade estatística do motor, IA e economia
/tests/Elifoot.Application.Tests# Testes de sincronismo humano (0/6) e pausa global com 8 humanos
/tests/Elifoot.Integration.Tests# Testes E2E HTTP + SignalR WebSockets e persistência real no PostgreSQL 18
/tests/Elifoot.Simulation.Tests # Simulação estatística de 100 temporadas e benchmarks 128/256/512/1.024 clubes
/scripts                        # Scripts PowerShell (.ps1) e Bash (.sh) para execução DEV e backup/restore
/docs                           # Documentação técnica, matemática, comparativo histórico e relatório de validação
docker-compose.yml              # Orquestração local PostgreSQL 18 + API .NET 10
```

---

## 2. Execução Rápida (Windows PowerShell)

### Pré-requisitos
- **.NET 10 SDK** (`10.0.x`)
- **PostgreSQL 16/18** local em `127.0.0.1:5432` (ou Docker Compose), opcional ao usar `-DatabaseProvider Sqlite`
- **Flutter SDK** (`3.38.x`) com suporte Web (`Chrome`/`Edge`)

### 1) Gerar a Seed Oficial de 128 Clubes
```powershell
dotnet run --project tools/seed-generator/Elifoot.SeedGenerator.csproj -c Release -- --clubs 128 --seed 199801 --output tools/seed-generator/seed-128-clubs.json
```

### 2) Compilar e Executar Todos os Testes Automatizados
```powershell
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build
```

### 3) Iniciar o Servidor Autoritativo (.NET 10 + SignalR + PostgreSQL)
```powershell
./scripts/run-dev.ps1 -ApiPort 5080
```
*(Ao iniciar pela primeira vez, a API cria automaticamente o schema no PostgreSQL e popula a sala padrão `Campeonato Brasileiro 98 (128 Clubes)` com código de convite `ELIF98`.)*

### 4) Executar o Cliente Flutter Web
```powershell
cd client/elifoot_flutter
flutter test
flutter build web --release
flutter run -d chrome --web-port 8080
```

---

## 3. Execução Rápida (Linux / macOS)

```bash
chmod +x scripts/run-dev.sh scripts/backup-restore.sh

# 1. Compilar e rodar testes .NET 10
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build

# 2. Subir PostgreSQL + API via Docker Compose (ou script local)
docker compose up --build -d
# OU localmente:
./scripts/run-dev.sh

# 3. Rodar cliente Flutter Web
cd client/elifoot_flutter
flutter analyze
flutter test
flutter build web --release
```

---

## 4. Backup e Restauração de Mundos / Saves

### Windows PowerShell
```powershell
# Exportar backup JSON do mundo ativo
./scripts/backup-restore.ps1 -Action backup -ApiUrl "http://localhost:5080" -FilePath "backups/mundo-128.json"

# Restaurar backup como um novo mundo validado
./scripts/backup-restore.ps1 -Action restore -ApiUrl "http://localhost:5080" -FilePath "backups/mundo-128.json"
```

### Linux / macOS
```bash
WORLD_ID="<guid-da-sala>" ./scripts/backup-restore.sh backup
 FILE_PATH="backups/world-backup.json" ./scripts/backup-restore.sh restore
```

---

## 5. Aviso de Segurança: Perfil `LocalDev` vs. Produção

- O perfil padrão `Elifoot:AuthMode = "LocalDev"` utiliza login simplificado por `Nickname + DeviceId` exclusivamente para desenvolvimento e testes locais em rede confiável.
- **Não exponha o modo `LocalDev` na internet pública.**
- Se `Elifoot:AuthMode` for alterado para qualquer valor diferente de `LocalDev` (ex.: `Production`), a API exige obrigatoriamente a configuração de `Elifoot:ProductionJwtSecret` (além de TLS/HTTPS, autenticação real e rate limiting) e bloqueia a inicialização caso não esteja presente.

---

## 6. Documentação Detalhada (`/docs`)

- [`/docs/REGRAS_VS_ORIGINAL.md`](docs/REGRAS_VS_ORIGINAL.md) — Comparativo entre observações históricas comprovadas do Elifoot 98, aproximações matemáticas e inovações desta edição.
- [`/docs/MOTOR_DEFESA.md`](docs/MOTOR_DEFESA.md) — Fórmulas de força por setores (`G/D/M/A`), bônus `*`, sinergia de meio-campo, disciplina (`Fair Play` a `Sarrafeiro`) e exemplos reproduzíveis.
- [`/docs/IA_TREINADORES.md`](docs/IA_TREINADORES.md) — Filosofia tática persistente dos técnicos de IA (`AiManager`), seleção contextual de esquema, `ManagerSquadPlan` e reação em partida.
- [`/docs/MULTIPLAYER_SINCRONISMO.md`](docs/MULTIPLAYER_SINCRONISMO.md) — Máquina de estados da rodada, contador `0/N`, pausa global compartilhada e quotas `5/3` (`6/4` na prorrogação).
- [`/docs/MERCADO_IA.md`](docs/MERCADO_IA.md) — Curva convexa `FairValue`, negociações bilaterais e leilões com `maxBid` privado.
- [`/docs/ECONOMIA_ESTADIOS.md`](docs/ECONOMIA_ESTADIOS.md) — Ingresso automático, público, custos operacionais, desgaste anual `1%..6%`, manutenção e ROI de obras.
- [`/docs/LIGAS_E_TACA.md`](docs/LIGAS_E_TACA.md) — 4 divisões de 8 clubes, 96 distritais, preliminares da Taça de 64, promoção/rebaixamento e demissão de humano ao cair para Distrital.
- [`/docs/EDITOR_IMPORTADOR.md`](docs/EDITOR_IMPORTADOR.md) — Editor de clubes/atletas/técnicos IA e importação/exportação `JSON`/`CSV` com preview de diferenças.
- [`/docs/VALIDACAO_FINAL.md`](docs/VALIDACAO_FINAL.md) — Resultados reais de compilação, testes unitários/integração/E2E, simulação de 100 temporadas e benchmarks 128/256/512/1.024 clubes.
