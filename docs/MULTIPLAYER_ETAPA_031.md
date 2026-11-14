# Multiplayer · Etapa 3.1 · Desempenho da compactação

**Classificação: PARTIAL.** A compactação segue opcional. O perfil isolado encontrou formatos mais rápidos e menores em determinadas amostras. A adoção dependeria de validação integrada e de um novo envelope com compatibilidade testada.

## O que compõe o tempo de gravação

A Etapa 3 mediu 1,62 s em JSON e 2,27 s em Gzip no save de dez temporadas. Eram três gravações por formato, primeiro JSON, depois Gzip, com outras atividades no host. A CPU mediana da gravação ficou em 1,42 e 1,31 CPU-s. A diferença de tempo de parede daquela amostra não pode ser atribuída ao compressor.

A Etapa 3.1 instrumentou a serialização do mesmo estado para UTF-8, a codificação do formato, Base64, abertura de transação, execução da atualização, commit, leitura, decodificação e hidratação. Cada janela mede relógio, CPU total do processo e alocações gerenciadas. O laboratório escreve em uma tabela isolada de snapshot, com WAL e `synchronous=FULL`. Antes de cada amostra, trunca o WAL em uma cópia descartável. Uma tabela de ledger sintética mede 4.096 inserções com e sem índices em transações idênticas. O caminho real envia o JSON diretamente para o Gzip. A separação das fases exige buffers intermediários, portanto os valores absolutos de alocação deste laboratório diferem dos do repositório.

## Resultados medidos

Medianas de três repetições para 1 e 10 temporadas. Tamanhos incluem Base64 nos formatos comprimidos e excluem os poucos bytes do prefixo. CPU, alocações, abertura de transação, leitura e hidratação por amostra estão nos dados brutos.

| Temporadas | Formato | MiB | Codificação + Base64 | UPDATE | Commit | Leitura / decodificação / hidratação |
| ---: | --- | ---: | ---: | ---: | ---: | ---: |
| 1 | JSON | 22,90 | 37 ms | 162 ms | 119 ms | 79 / 30 / 255 ms |
| 1 | Gzip Fastest | 5,17 | 89 ms | 59 ms | 43 ms | 20 / 89 / 291 ms |
| 1 | Gzip Optimal | 3,20 | 242 ms | 24 ms | 35 ms | 7 / 67 / 214 ms |
| 1 | Brotli Fastest | 2,76 | 67 ms | 21 ms | 25 ms | 6 / 106 / 229 ms |
| 1 | Brotli Optimal | 1,78 | 226 ms | 14 ms | 22 ms | 2 / 75 / 251 ms |
| 10 | JSON | 112,48 | 164 ms | 782 ms | 1.317 ms | 402 / 138 / 1.050 ms |
| 10 | Gzip Fastest | 24,76 | 400 ms | 331 ms | 1.293 ms | 76 / 389 / 1.131 ms |
| 10 | Gzip Optimal | 15,09 | 1.182 ms | 114 ms | 217 ms | 58 / 323 / 1.182 ms |
| 10 | Brotli Fastest | 13,05 | 297 ms | 95 ms | 155 ms | 50 / 318 / 1.307 ms |
| 10 | Brotli Optimal | 7,91 | 1.037 ms | 67 ms | 159 ms | 32 / 257 / 1.202 ms |

A serialização UTF-8 isolada mediu 572 ms, 547 CPU-ms e 87 MiB de alocações em uma temporada. Aos dez anos mediu 843 ms, 1.469 CPU-ms e 370 MiB de alocações. Na amostra de dez temporadas, o Gzip rápido consumiu 359 CPU-ms na compactação e 391 CPU-ms na decodificação. A mediana calculada de UPDATE e commit visitou valores muito diferentes por formato e ordem. O custo do ledger sintético aos dez anos foi 48/126 ms sem índices e 37/45 ms com índices, para execução e commit. A inversão em relação ao primeiro ano (39/11 ms e 54/34 ms) evidencia a variabilidade da amostra. O índice teve custo próprio, mas o ensaio não separa a sua contribuição estatística da atividade de disco e cache.

O Gzip rápido adicionou trabalho de compactação, Base64 e leitura descompactada. No perfil de dez temporadas, economizou parte da escrita em SQLite, enquanto o commit ainda teve latência alta. Brotli rápido foi o melhor candidato de compromisso nesse perfil. O ensaio compara medição local e de baixa amostragem. A amostra integrada da Etapa 3 reúne o trabalho do codec, EF, ledger, journal e checkpoint. O peso relativo de cada custo na regressão de 0,65 s continua incerto. [Perfil bruto](../artifacts/stage4-run/profile-1-10/profile.json) · [Etapa 3](MULTIPLAYER_ETAPA_03.md)

### Cinquenta temporadas

O motor simulou 17.550 partidas em uma carreira de 128 clubes e gravou 261.757 movimentos no ledger. O SQLite da fixture ocupou 605,9 MiB; o snapshot JSON respondeu por 468,05 MiB. A serialização custou 3,15 s de parede, 3,70 CPU-s e 1.502 MiB de alocações gerenciadas na amostra inicial. Os cinco formatos produziram o mesmo SHA-256 do JSON quando decodificados. Estas cifras são da primeira medição, que materializava o JSON na leitura e rodou no host compartilhado. [Fixture](../artifacts/stage4-run/seasons/results.json) · [Perfil inicial](../artifacts/stage4-run/profile-50/profile.json)

| Formato | MiB | Codificação + Base64 | UPDATE | Commit | Leitura / decodificação / hidratação |
| --- | ---: | ---: | ---: | ---: | ---: |
| JSON | 468,05 | 771 ms | 4.178 ms | 17.161 ms | 1.431 / 476 / 4.740 ms |
| Gzip Fastest | 100,70 | 1.658 ms | 1.207 ms | 6.090 ms | 332 / 1.585 / 4.637 ms |
| Gzip Optimal | 61,07 | 4.563 ms | 1.531 ms | 1.130 ms | 191 / 1.370 / 4.358 ms |
| Brotli Fastest | 53,50 | 1.273 ms | 868 ms | 1.907 ms | 186 / 1.519 / 4.522 ms |
| Brotli Optimal | 31,75 | 3.740 ms | 356 ms | 242 ms | 101 / 1.370 / 4.535 ms |

O perfil com materialização de 50 temporadas chegou a cerca de 4,7 GiB de RSS nas amostras de processo e coincidiu com pressão de memória no host. O runner atualizado calcula o hash do JSON descompactado em fluxo e mede a hidratação diretamente a partir do fluxo. O reteste passou em 1 e 10 temporadas e terminou com `Out of memory` nas 50 sob limite de heap gerenciado de 3,5 GiB. A tabela de 50 usa a execução inicial, que completou os cinco formatos. No perfil atualizado, decodificação inclui SHA-256 e hidratação inclui uma nova decodificação. Saves acima de 256 MiB usam uma repetição por formato. [Reteste em fluxo](../artifacts/stage4-run/profile-stream-verified/profile.json) · [Log](../artifacts/stage4-run/profile-stream-verified.log)

## Dados históricos e compatibilidade

O snapshot completo ainda é reserializado em cada checkpoint. Para reaproveitar bytes, o histórico fechado teria de ser estável e identificado por save, temporada e revisão. O código consulta força, contratos, rankings e estatísticas entre temporadas; alguns agregados e índices em memória mudam após a leitura ou por migrações de compatibilidade. Identificar as partes realmente imutáveis exige medir e auditar essas mutações.

Uma representação incremental exigiria manifesto transacional com IDs, checksums e versão, mais recuperação de histórico e snapshot como conjunto. Duplicar seções nos dois formatos durante a transição ampliaria o disco. Manter o mesmo JSON e o codec versionado preserva consultas antigas e mantém menor o risco desta etapa. Brotli ficou no perfil de auditoria. Os leitores JSON e Gzip da Etapa 3 continuam disponíveis e a configuração permanece desligada por padrão.

## Reprodução

```powershell
$audit = 'artifacts/stage31-repeat'
dotnet build docs/audit-scalability/EngineAudit.csproj -c Release --artifacts-path "$audit/tool"
$engine = "$audit/tool/bin/EngineAudit/release/EngineAudit.dll"
dotnet $engine season-fixtures "$audit/seasons" 128 1 10 50
dotnet $engine compression-profile "$audit/profile" "$audit/seasons/season-1.db" "$audit/seasons/season-10.db" "$audit/seasons/season-50.db"
```

O driver simula as partidas em sequência no motor real, grava uma fixture por marco e acumula movimentos para preservar o ledger relacional. O perfil reabre e copia as fixtures fechadas. [Código da medição](audit-scalability/CompressionProfile.cs) · [Driver](audit-scalability/EngineAudit.cs)
