# Multiplayer · Etapa 3 · Armazenamento de saves

**Classificação: PARTIAL.** A compactação reversível preservou o estado, o histórico e os resultados verificados em 0, 1 e 10 temporadas. A latência de gravação aumentou na amostra em todas as idades. A opção permanece desativada por padrão e o teste persistido de 50 temporadas ficou pendente.

## Por que o banco cresce

O SQLite conserva um snapshot por mundo em `ef_worlds`. Em cada checkpoint durável, incluindo mudanças pequenas de mercado ou prontidão, o repositório regrava o estado completo. As versões intermediárias geram escrita e páginas livres reutilizáveis. Na auditoria anterior, o arquivo da 10ª temporada chegou a 270,3 MiB, com 139,9 MiB ocupados e 130,4 MiB na freelist. São medidas de uma carreira que cresceu progressivamente. O ensaio desta etapa mede bancos recriados a partir dos mesmos estados nos dois formatos. [Auditoria original](audit-scalability/RELATORIO.md) · [Páginas por objeto](../artifacts/scalability-audit/final-season-storage/storage.json)

| Componente do JSON legado | 1 temporada | 10 temporadas | Conteúdo |
| --- | ---: | ---: | --- |
| `playerStrengthHistory` | 2,77 MiB | 31,90 MiB | 49.872 alterações documentadas, com rodada, força anterior e posterior |
| `playerStatistics` | 2,13 MiB | 21,35 MiB | 41.535 entradas, com gols, assistências, presença, cartões e agregados por período e competição |
| `fixtures` | 7,15 MiB | 14,64 MiB | Calendário, placares, escalações e eventos detalhados ainda retidos |
| `players` | 3,08 MiB | 8,94 MiB | Atributos atuais, contrato, lesões, cartões e suspensões |
| `history` | 1,57 MiB | 7,85 MiB | Históricos de clubes e jogadores, temporadas e trajetórias |
| `auditLogs` | 0,93 MiB | 7,13 MiB | 17.202 registros mantidos no snapshot de dez temporadas |
| `transactions` | 3,17 MiB | 6,48 MiB | 10.463 movimentos recentes, também presentes no ledger quando gravados em PREPARING ou RESULTS |
| `economy` e `jobInvitations` | 1,19 MiB | 9,85 MiB | Contratações, solicitações salariais, renovações, crédito, recibos e convites |

O ledger relacional original tinha **51.903 movimentos** após 10 temporadas e ocupava 14,06 MiB de tabela e 13,13 MiB de índices. Os índices atendem à chave primária, idempotência por mundo e clube e busca por rodada da temporada. O snapshot contém os movimentos recentes, usados durante partidas e nas telas financeiras. O repositório lista todos os IDs relacionais nas gravações PREPARING e RESULTS para inserir os lançamentos faltantes. Resumos de temporada aparecem tanto no JSON quanto em `ef_season_histories`; o JSON conserva campos adicionais. A retenção anterior do motor mantém históricos consolidados e poda certos detalhes antigos. A compactação conserva integralmente o conteúdo disponível no save e no ledger.

## Implementação e compatibilidade

`Elifoot:CompressNewSqliteSnapshots` habilita Gzip rápido para novos mundos SQLite. O JSON UTF-8 vai direto ao compressor. O envelope `ELIFOOT:GZIP:1:` e Base64 usa a coluna de texto atual e permite identificar a versão. O leitor aceita JSON legado e Gzip. O repositório conserva o formato escolhido na primeira gravação de cada mundo, mesmo após mudar a opção ou reiniciar a API. A leitura do checkpoint para recuperação após commit incerto aceita ambos os formatos.

Desabilitar a opção volta a criar mundos em JSON. Para voltar a executar um binário anterior com mundos compactados, restaure um backup compatível ou decodifique cada envelope para JSON UTF-8 em uma cópia descartável e valide a restauração antes de promovê-la. A opção preserva o esquema, o histórico e o contrato HTTP e SignalR. PostgreSQL continua usando JSON comum.

Foram avaliadas compactação, escrita incremental e separação entre estado atual e histórico. A compactação tem o menor impacto estrutural e preserva todos os campos. Escrita incremental exigiria cursor e recuperação transacional para cada projeção. Separar históricos exigiria carregar e recompor dados usados por rankings, finanças, evolução e recibos. Evicção do cache do servidor exigiria garantir que leitores, relógio e comandos mantivessem uma única instância mutável por mundo e que saves pendentes ficassem disponíveis para recuperação. O cache `MemoryCache` e os mapas de locks continuam sem limite. A memória do estado descompactado continua dependente da duração e do número de mundos.

## Medição pareada

Windows, .NET 10, Intel Xeon E5-2630 v4, cerca de 16 GiB de RAM. Um mundo de 128 clubes e semente 199801 por variante. O estado inicial foi gerado no ensaio, a temporada 1 veio de uma nova execução descartável e a temporada 10 veio do benchmark descartável anterior. Uma cópia do ledger original foi conferida com `EXCEPT`. A comparação mantém as **51.903** linhas e os dez resumos aos dez anos. As gravações, leituras e viradas usaram o repositório EF. Cada leitura limpou o cache de identidade do repositório, mantendo disponíveis os caches do runtime e do sistema operacional. [Dados brutos](../artifacts/stage3-storage/verified/results.json) · [Hashes](../artifacts/stage3-storage/verified.log)

| Temporadas concluídas | Snapshot JSON → Gzip | Páginas ocupadas | Arquivo após checkpoint | WAL amostrado pré-checkpoint | Cópia após VACUUM |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 4,6 → 0,9 MiB | 4,8 → 1,1 MiB | 9,4 → 2,0 MiB | 9,3 → 2,8 MiB | 4,8 → 1,1 MiB |
| 1 | 22,9 → 5,2 MiB | 25,7 → 8,0 MiB | 48,7 → 13,2 MiB | 46,2 → 14,9 MiB | 25,6 → 7,8 MiB |
| 10 | 112,5 → 24,8 MiB | 139,8 → 52,0 MiB | 139,8 → 76,8 MiB | 118,8 → 70,2 MiB | 138,3 → 50,5 MiB |

Após dez temporadas, a economia foi **87,8 MiB em páginas ocupadas (62,8%)** e **63,0 MiB no arquivo físico após checkpoint (45,1%)**. Entre as temporadas 1 e 10, o crescimento médio medido foi de **12,7 → 4,9 MiB por temporada**, uma diferença de **7,8 MiB por temporada** naquele intervalo. Projeções lineares além da amostra dependem dos dados produzidos pelo motor e da ocupação dos índices. A execução persistida comparável de 50 temporadas ficou pendente diante do custo da carreira de dez temporadas e do crescimento de memória observado no processo de medição.

Na temporada 10, a cópia compactada tinha 6.343 páginas livres de 4 KiB, cerca de 24,8 MiB. O SHM amostrado caiu de 256 para 160 KiB. O WAL ficou zerado com `wal_checkpoint(TRUNCATE)` na janela isolada. `VACUUM INTO` criou cópias novas e o arquivo principal foi medido separadamente. A ocupação e o WAL variam com o padrão de transações, páginas livres e checkpoints. Todos os bancos do ensaio passaram em `quick_check`. [WAL](https://www.sqlite.org/wal.html) · [DBSTAT](https://www.sqlite.org/dbstat.html) · [VACUUM](https://www.sqlite.org/lang_vacuum.html)

### CPU, RAM e latência

Três amostras de gravação e três de leitura por variante, em ordem fixa, com mediana. A rodada inclui simulação, avanço de fase e gravação, com uma amostra. CPU é o tempo total do processo, incluindo threads do runtime. RAM indica o crescimento do pico de RSS dentro da janela de cada operação. O código mede o processo inteiro e a execução compartilhou o host com suítes locais. Os números indicam os custos observados, com variação entre amostras. [Dados brutos](../artifacts/stage3-storage/verified/results.json)

| Temporadas | Gravação, ms | Leitura, ms | Rodada, ms | CPU gravação / leitura / rodada, ms | Aumento RSS gravação / leitura, MiB |
| ---: | ---: | ---: | ---: | --- | --- |
| 0 | 364 → 1.226 | 272 → 252 | 1.684 → 1.011 | 344 → 906 / 344 → 469 / 1.453 → 1.188 | 15,9 → 8,5 / 13,5 → 3,5 |
| 1 | 811 → 1.115 | 436 → 354 | 1.599 → 1.316 | 547 → 328 / 563 → 359 / 2.172 → 875 | 93,1 → 41,6 / 58,8 → 21,2 |
| 10 | 1.622 → 2.265 | 1.709 → 1.591 | 7.722 → 2.507 | 1.422 → 1.313 / 1.797 → 1.734 / 3.750 → 1.953 | 451,2 → 34,3 / 114,5 → 67,3 |

O aumento de latência mediana no save e a ausência de uma amostra concorrente comparável são motivos para manter o recurso opcional. A rodada teve variação alta e amostragem insuficiente para estimar percentis. O pico absoluto de RSS na rodada de dez temporadas foi 2.259 → 1.400 MiB no processo de ensaio, ainda com objetos e buffers de outras operações. O aumento dentro da janela dessa rodada foi 169 → 239 MiB. Nenhuma dessas cifras estima diretamente o consumo por mundo ativo em produção. [GZipStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.gzipstream.-ctor?view=net-10.0)

## Integridade e regressões

- SHA-256 igual nos pares para todos os campos do JSON, excluindo somente a hora de gravação. Após carregar cada variante, a rodada seguinte teve o mesmo digest de partidas, placares, eventos, força, lesões, cartões, estatísticas selecionadas e lançamentos financeiros.
- A compra com dois clientes SignalR foi verificada nos dois formatos, incluindo rejeição temporária de leitura, rollback, retry idempotente, reinício e opção invertida. Os testes também cobriram clientes incrementais, nova época e última versão durável. Quatro casos de checkpoint confirmaram recuperação de falhas induzidas antes e depois do commit, com JSON e Gzip. [Checkpoint](../artifacts/stage3-storage/checkpoint-verified.log)
- A seleção de integração passou com 48 aprovações e um caso comercial ignorado. Aplicação passou com 70 testes e domínio com 706. A suíte Flutter teve 576 aprovações e as mesmas oito falhas documentadas na Etapa 1. [Integração](../artifacts/stage3-storage/integration.log) · [Aplicação](../artifacts/stage3-storage/application-final.log) · [Domínio](../artifacts/stage3-storage/domain-final.log) · [Flutter](../artifacts/stage3-storage/flutter-full.log)
- Permanecem abertos o perfil concorrente com SQLite persistido, 50 temporadas, comparações com cache controlado e os testes de WAN, FPS e RAM do Flutter da Etapa 2.1. [Sincronização](MULTIPLAYER_ETAPA_021.md)

## Arquivos e reprodução

- `src/Elifoot.Infrastructure/SqliteSnapshotOptions.cs` define o envelope, o compressor e os leitores.
- `src/Elifoot.Infrastructure/EfWorldRepository.cs` preserva o formato de cada mundo e recupera checkpoints nos dois formatos. `src/Elifoot.Api/Program.cs` liga a opção de configuração.
- `docs/audit-scalability/EngineAudit.cs` contém o ensaio pareado, métricas, verificações de SQLite e hashes. Os fixtures em `tests/Elifoot.Integration.Tests` cobrem falhas, compra idempotente e reinício.

Execute a partir da raiz do repositório e escolha diretórios novos. Use somente bancos descartáveis produzidos pela auditoria.

```powershell
$audit = 'artifacts/stage3-repeat'
dotnet build docs/audit-scalability/EngineAudit.csproj -c Release --artifacts-path "$audit/build"
$engine = "$audit/build/bin/EngineAudit/release/EngineAudit.dll"
dotnet $engine seasons "$audit/one" 128 1
dotnet $engine seasons "$audit/ten" 128 10
dotnet $engine storage-pair "$audit/pairs" 128 "$audit/one/season.db" "$audit/ten/season.db"
```

As duas execuções longas acima usam o formato legado como origem. O modo pareado grava os dois formatos em arquivos novos, compara a integridade, mede checkpoints e cria cópias compactadas. O checkpoint prévio à implementação registra o estado local e os hashes de 51 arquivos. [Manifesto](../artifacts/stage3-checkpoint-9684bc0c29bf/sha256.csv)
