# Validação Final de Execução — Elifoot Classic Multiplayer

> Atualização 29/10/2026: este documento registra a entrega anterior. Para a auditoria de jogabilidade, defeitos corrigidos, testes de navegador e situação atual do Android, consulte [PLAYABLE_QA_002.md](PLAYABLE_QA_002.md). O SDK foi instalado no projeto e o APK de depuração passou a compilar; a execução no emulador continua sem validação.

Este documento registra os resultados **reais** obtidos na execução das ferramentas, compiladores, banco de dados PostgreSQL 18, suítes de testes `.NET 10`, simulação estatística de 100 temporadas, benchmarks de 128 a 1.024 clubes e build/testes do cliente Flutter.

---

## 1. Ambiente Verificado

- **Sistema Operacional**: Windows 10 Pro 64-bit (`10.0.19045`)
- **.NET SDK**: `10.0.302` (`net10.0`)
- **Banco de Dados**: `PostgreSQL 18` (`postgresql-x64-18` ativo em `127.0.0.1:5432`)
- **Flutter SDK**: `3.38.5` • Dart `3.10.4` (Web Chrome/Edge habilitado)
- **Limitações de Ambiente Detectadas**:
  - **Android SDK**: `flutter doctor` reportou `Unable to locate Android SDK` na máquina local. Portanto, o código Android (`client/elifoot_flutter/android`) foi gerado e validado via `flutter analyze` e `flutter test`, mas o comando `flutter build apk` está marcado como **BLOCKED** exclusivamente pela ausência do Android SDK instalado no host.
  - **Docker CLI**: o binário `docker` não está instalado no PATH desta máquina Windows; a persistência e os testes de integração foram validados diretamente contra o serviço nativo **PostgreSQL 18** em execução na porta `5432`, e os arquivos `docker-compose.yml` e `Dockerfile` foram entregues prontos para ambientes com Docker.

---

## 2. Resultados dos Testes Automatizados (.NET 10 e Flutter)

| Suíte de Testes | Comando Executado | Resultado | Detalhes Validados |
| :--- | :--- | :--- | :--- |
| **Elifoot.Domain.Tests** | `dotnet test tests/Elifoot.Domain.Tests -c Release` | **PASS (5/5)** | `*` literal (`João Silva*`), limites `11..24` e goleiro único, monotonicidade estatística do motor (defesa, goleiro, `*`, expulsão, comportamento `Fair Play` sem cartões vs `Sarrafeiro`), adaptação tática da IA (`4-3-3` -> `4-4-2`), curva convexa `FairValue`, desgaste de estádio `1..6%` vs `0..1%`, e roundtrip JSON/CSV. |
| **Elifoot.Application.Tests** | `dotnet test tests/Elifoot.Application.Tests -c Release` | **PASS (3/3)** | Sincronismo `0/6 -> 1/6 -> 5/6 -> 6/6`, humano desempregado no denominador, idempotência, concorrência sem duplo início, `Retirar pronto`, Pausa Global no minuto `37'` com 8 humanos em 4 jogos simultâneos, quotas `5/3` e prorrogação da Taça com `6/4` e intervalos gratuitos aos `45'`, `90'` e `105'`. |
| **Elifoot.Integration.Tests** | `dotnet test tests/Elifoot.Integration.Tests -c Release` | **PASS (2/2)** | Fluxo E2E completo via `WebApplicationFactory` + cliente `SignalR` real (6 técnicos, pausa aos `79'`, desconexão e reconexão por snapshot, tabela, editor) e persistência real com recuperação de restart no **PostgreSQL 18**. |
| **Elifoot.Simulation.Tests** | `dotnet test tests/Elifoot.Simulation.Tests -c Release` | **PASS (2/2)** | Simulação contínua de **100 temporadas** com 128 clubes (32 divisões + 96 distritais, Taça de 64 com preliminares, promoção/rebaixamento, demissão de humano ao cair para Distrital, `0` elencos inválidos, `0` clubes duplicados em chaves) + Benchmarks de `128`, `256`, `512` e `1.024` clubes. |
| **Flutter Analyze & Test** | `flutter analyze && flutter test` | **PASS (1/1, 0 issues)** | Teste E2E de interface validando Login `LocalDev`, Lobby, contador `TÁTICAS ENVIADAS: 0/6`, nomes com `*` literal, disparo de partida, `PEDIR PARADA` (`PARADAS 1/3`) e navegação. |
| **Flutter Build Web** | `flutter build web --release` | **PASS** | Compilação de produção gerada em `client/elifoot_flutter/build/web`. |

---

## 3. Resultados Reais de Benchmark (`128`, `256`, `512` e `1.024` Clubes)

Extraído de [`/docs/BENCHMARK_REPORT.json`](BENCHMARK_REPORT.json):

| Total de Clubes | Divisões / Distritais | Total de Atletas | Técnicos IA | Geração da Seed (ms) | Simulação Rodada 90' Completa (ms) | Delta Memória (MB) | Tamanho Snapshot (KB) |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **128** | 32 / 96 | 2.308 | 128 | **5 ms** | **196 ms** | 3,17 MB | 2.434,0 KB |
| **256** | 32 / 224 | 4.612 | 256 | **9 ms** | **218 ms** | 6,05 MB | 4.364,0 KB |
| **512** | 32 / 480 | 9.220 | 512 | **45 ms** | **282 ms** | 11,77 MB | 8.215,0 KB |
| **1.024** | 32 / 992 | 18.436 | 1.024 | **43 ms** | **442 ms** | 14,91 MB | 15.939,5 KB |
