# Tarefa009 — auditoria de persistência e integração HTTP

Auditoria local de 30/10/2026. Escopo de código desta colaboração: somente `src/Elifoot.Api/PlayerRankingsEndpoints.cs` e `tests/Elifoot.Integration.Tests/PlayerRankings009IntegrationTests.cs`. Application, Program, EF e domínio são integrados pelo responsável principal.

## Estratégia recomendada: snapshot, sem DDL na 009

O estado autoritativo está em `ef_worlds.SnapshotJson`. As quatro tabelas existentes são `ef_worlds`, `ef_club_summaries`, `ef_financial_ledger` e `ef_season_histories`. A 008 migra carreira nas três leituras do repositório (`GetWorldAsync`, convite e listagem), preservando o schema SQL. Repetir esse modelo para `PlayerStatisticsEngine.EnsureMigrated` é a opção de menor impacto para os saves008.

Não criar índices EF para estatísticas que continuam exclusivamente no JSON: esses índices não participariam das consultas em memória. Preservar os índices SQL existentes, em especial o ledger único `(GameWorldId, ClubId, IdempotencyKey)` e histórico único `(GameWorldId, SeasonNumber)`. `EnsureCreatedAsync` cria banco novo, mas não atualiza schema existente; adicionar DbSets sem migração aditiva quebraria saves existentes.

Índices transitórios recomendados, sempre por instância do mundo e sob seu lock:

| Consulta | Chave ou estrutura |
| --- | --- |
| Consolidação | `(PlayerId, Season, CompetitionType, Division, ClubId, Position)` → agregado |
| Histórico | `PlayerId` → agregados; não usar o clube atual como chave histórica |
| Ranking | `(Season, CompetitionType, Division)` → agregados, agrupados por PlayerId no recorte |
| Identidades | PlayerId → jogador; ClubId → clube, construídos uma vez por consulta ou invalidados após alterações |
| Idempotência | `AppliedPlayerStatisticsMatches`, `AppliedMatchIds` por agregado e `ClosedPlayerAwardSeasons`, persistidos |
| Conquistas | `(Season, PlayerId)` / chave textual `season_top_scorer:{WorldId}:{Season}:{PlayerId}` |

O índice de consolidação não deve descartar Position se a elegibilidade depender da posição no jogo. Índices em `[JsonIgnore]` precisam ser reconstruídos no reload. Não persistir cópias divergentes do mesmo agregado. Começar com um agrupamento por consulta e medir 128/1024 clubes antes de criar caches de resultados; invalidar caches por revisão de estatísticas, transferência, avanço de temporada e premiação, não apenas por contagem de linhas.

## Atomicidade e retenção

`SaveWorldAsync` usa um semáforo próprio, diferente do lock do orquestrador, e não salva todos os minutos. O snapshot completo é gravado nos checkpoints e estados de pausa/preparação/resultados. As projeções de ledger e temporadas são sincronizadas somente em PREPARING/RESULTS. O novo prêmio deve ser incorporado a saldo, ledger, conquista e guardas antes de um único save durável. Garantir que o encerramento chegue a um desses estados para sincronizar também o ledger relacional. A transação de `SaveChangesAsync` cobre as alterações relacionais submetidas juntas; ela não torna o cache em memória durável em caso de falha no save.

Não atribuir prêmios em GET. Não reconstruir pagamentos a partir de consulta de ranking. A guarda de temporada deve existir também quando o total de gols é zero; clube ausente preserva conquista, sem criar recebedor.

O fechamento atual remove fixtures e transações do snapshot anteriores à janela de retenção. As estatísticas históricas, guardas de idempotência e conquistas 009 precisam sobreviver à poda e ao reset de contadores sazonais. A tabela de ledger conserva registros anteriores que saíram do snapshot; a reconstrução de chaves financeiras deve considerar isso em qualquer futuro reprocessamento de temporadas antigas.

## Migração de saves008

Migrar de forma aditiva, versionada e idempotente, mantendo IDs, pontos decimais, moral, empregos, títulos de treinadores, saldos e transações. Campos ausentes usam coleções vazias. Não limpar carreira nem resetar `CareerRulesVersion`. Após desserialização em todos os caminhos EF, aplicar a migração 008 existente e depois a 009. Persistir a migração no próximo checkpoint; reexecutá-la antes disso deve produzir o mesmo estado.

Reconstituir somente fatos recuperáveis dos eventos oficiais concluídos: competição/divisão/temporada da fixture e IDs de autores. As fixtures 008 têm escalação ativa final e eventos de substituição, mas não um snapshot inicial imutável, nem um livro completo de minutos por jogador/goleiro. A lista de eventos preserva ordem, mas não possui EventId independente. Não inferir jogos/sofridos exatos quando faltarem dados, nem reinterpretar gols pela divisão atual. Totais `CareerGoals`/`CareerMatches` não permitem recuperar temporadas ou divisões podadas. Registrar cobertura incompleta; não inventar estatísticas nem premiar retroativamente sem fechamento e dados confiáveis.

O banco original 008 foi aberto **somente para leitura**; schema, índices, integridade e resumos estão em `sqlite008-readonly-audit.json`. Testes 009 usam bancos próprios em `docs/audit-009/sqlite`, incluindo backup consistente via API SQLite, sem copiar isoladamente um arquivo em modo WAL.

## Integração necessária pelo responsável principal

Os endpoints usam os nomes de engine e DTO confirmados, com `MapPlayerRankings(this WebApplication app)` em namespace `Elifoot.Api`. Mapear no Program com `app.MapPlayerRankings()` e importar esse namespace. A extensão não fecha temporadas, não aplica partidas e não salva em GET.

Adicionar no orquestrador, mantendo a mesma instância de `GetLock` usada por mutações e relógio:

```csharp
public async Task<T?> ReadWorldAsync<T>(Guid worldId, Func<GameWorld, T> read,
    CancellationToken ct = default) where T : class
{
    var gate = GetLock(worldId);
    await gate.WaitAsync(ct);
    try
    {
        var world = await _repository.GetWorldAsync(worldId, ct);
        return world == null ? null : read(world);
    }
    finally { gate.Release(); }
}
```

O DTO deve ser totalmente materializado no callback; não devolver enumerações sobre coleções mutáveis. Cancelamento é propagado. Não adicionar um segundo dicionário de locks na API, nem expor o semáforo via reflection em produção.

## Validação de query preparada

Defaults: `category=scorers`, `scope=general`, `period=career`. Categorias e filtros conhecidos aceitam caixa indiferente e espaços externos. Valor explicitamente vazio, parâmetro repetido, enum desconhecido, season não inteiro positivo ou fora do mundo retornam 400 com `error`. `period=season` exige season; career/current rejeitam season em vez de ignorá-lo. Temporada passada sem dados retorna DTO vazio com cobertura, e não 404. Mundo ou jogador ausente retornam 404. O endpoint de prêmios aceita season opcional e não gera pagamentos.

## Evidências e limites

- Baseline anterior ao arquivo de endpoints: **9/9 testes de integração 007/008 aprovados**, `api-baseline-tests.log` e `baseline-tests/baseline-008-007.trx`.
- A suíte 008 existente gera seu próprio novo banco de QA e regrava `docs/audit-008/browser-fixture.json`; esse arquivo gerado não é prova de imutabilidade. O banco referenciado por `browser-used-fixture.json` foi apenas lido nesta auditoria.
- Arquivos protegidos têm hashes iniciais em `api-persistence-baseline-manifest.json`; mudanças concorrentes feitas pelo responsável principal não são atribuídas a esta colaboração.
- Sem navegador e sem publicação. Resultados HTTP/009 só podem ser declarados após integração do engine, da leitura sob lock e do mapeamento no Program.

## Evolução futura, se as medições justificarem SQL

Usar projeções reconstruíveis e migração aditiva, preservando `ef_worlds`. Índices propostos: agregado único `(WorldId, PlayerId, Season, CompetitionType, Division, ClubId, Position)`; leitura `(WorldId, Season, CompetitionType, Division)` e `(WorldId, PlayerId, Season)`; aplicação única `(WorldId, MatchId, PlayerId)`; conquista única `(WorldId, Season, PlayerId)` e `(WorldId, AwardId)`. Tratar Division nula explicitamente na unicidade, pois provedores podem permitir múltiplos NULLs. A mesma transação deve atualizar snapshot, projeção, guardas e ledger. Nada disso requer DDL na entrega inicial 009.
