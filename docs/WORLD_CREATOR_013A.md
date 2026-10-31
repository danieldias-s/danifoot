# Criador de mundos — 013A

Implementação exclusiva em `D:\elifoot_98_013A`, branch `feature/world-creator-013a`, base `50e9c1215ed6d1256cc0593f84ab8aa54629b314` (checkpoint da 012C). Nenhuma integração ou publicação foi executada. Os bancos e processos da 012D não foram utilizados.

O lobby mantém a criação legada. No diálogo NOVO CAMPEONATO / SALA, escolha CRIADOR DE MUNDOS / CATÁLOGO. O novo fluxo permite categoria, múltiplos países, automático/personalizado, divisões, distritais, revisão de elencos/fontes, prévia e confirmação. O servidor valida a montagem; a confirmação usa a prévia persistida, mesmo se o catálogo mudar depois.

## Executar em DEV isolado

```powershell
cd D:\elifoot_98_013A
.\scripts\run-world-creator-013a.ps1 -Fixtures
# API: http://127.0.0.1:5133
# Em outro terminal:
cd D:\elifoot_98_013A\client\elifoot_flutter
flutter run -d chrome --web-port 8133 --dart-define=API_BASE_URL=http://127.0.0.1:5133
```

`-Fixtures` importa apenas nomes, atletas e rankings inventados. Sem essa opção, o catálogo utiliza os dados já persistidos; uma base vazia mostra falta de cobertura. Nenhuma assinatura ou API externa é necessária para os testes.

PostgreSQL 18 local: `127.0.0.1:55433`, usuário DEV `elifoot013a`, dados em `artifacts/013a/pgdata`. Bancos separados `elifoot_013a_catalog`, `elifoot_013a_worlds` e `elifoot_013a_tests`. Não há containers/volumes compartilhados. A instância temporária pode ser encerrada com `scripts/test-world-creator-013a.ps1 -Action Stop`; não há serviço instalado.

## Validar

```powershell
.\scripts\test-world-creator-013a.ps1 -Action Test
cd client\elifoot_flutter
flutter analyze
flutter test
flutter build web --release --pwa-strategy=none --dart-define=API_BASE_URL=http://127.0.0.1:5133
```

O runner inclui domínio, aplicação, integração HTTP/SignalR/persistência e testes de simulação curtos. Exclui a classe longa `MultiSeasonAndBenchmarkTests`, incluindo os ensaios históricos de 100 temporadas, que não foram refeitos nesta missão. Evidências e capturas: `docs/audit-013a`. Resultados consolidados: `docs/DELIVERY_013A.md`.

O catálogo permanece desativado por padrão. O arquivo `src/Elifoot.Api/appsettings.WorldCreator013A.example.json` é um exemplo, não é carregado automaticamente. As rotas seguem a identidade LocalDev existente e recusam uso no modo de autenticação de produção.
