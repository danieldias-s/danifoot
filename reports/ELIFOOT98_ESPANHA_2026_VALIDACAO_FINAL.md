# Elifoot 98 · Validação final da Espanha 2026/27

| Verificação | Estado |
|---|---|
| Estrutura do catálogo e do ZIP | **PASS ESTRUTURAL** |
| Motor de criação offline e elencos V1.6 | **PASS DO MOTOR** no escopo offline |
| Qualidade dos dados | **PARTIAL** |
| Prontidão do catálogo | **DRAFT** |

## Escopo e Git

A auditoria de entrada encontrou a branch `main` em `514bf99`, posterior à referência `e55ad9d` e à recompactação `c24e3c6`. Foram inspecionados o histórico, o índice, as alterações de trabalho, as worktrees e o stash. Foram preservadas três alterações rastreadas de auditorias 007/008 e as entradas preexistentes fora do índice, incluindo a pesquisa do Real Madrid. O pedido colado determinou o escopo. A auditoria do ZIP foi usada como evidência e as decisões aprovadas do Plano Mestre V1.6 como regra.

| Commit local | Arquivos e escopo |
|---|---|
| `c4897c6` | `OfflineRosterV16.cs`, `OfflineSurplusV16.cs`, `OfflineWorldFactory.cs` e os testes de elencos, excedentes e DRAFT |
| `906b72a` | 171 JSONs espanhóis, `fontes.csv`, `metadados.json`, `proveniencia_pendente.csv` e o relatório de qualidade |
| `fa70633` | Exportador e testes Python, ZIP, README e manifesto SHA-256 |
| Commit que contém este relatório | HEAD final da entrega local na `main` |

## Catálogo e motor

- Os 171 JSONs novos agora resolvem `../../../_schemas/clube-1.1.schema.json`. Cada diff contém uma única substituição de caminho. Os schemas oficiais, o manifest, as identidades, os modelos e os exemplos passaram na resolução de caminhos e na validação Draft 2020-12. São 363 instâncias com referência no repositório e 345 na extração.
- Penya Deportiva e Real Madrid Castilla mantêm 22 candidatos, incluindo três G e 19 de linha. O teste sintético anterior à correção reproduziu seis falhas, incluindo o caso 22/3. Pela fórmula anterior, as primeiras seeds problemáticas entre 0 e 99 são 3 para a Penya e 2 para o Castilla. O novo seletor limita o tamanho sorteado aos candidatos compatíveis com a regra. Os dois elencos D5 produzem de 13 a 21 atletas e dois goleiros, com os 22 candidatos preservados na referência.
- Os testes cobrem D1 20–21 e dois G, D2/D3 19–20 e dois G, D4 17–19 e dois G, D5 13–23 com um ou dois G e até três somente aos 23. Exercitam 13/1, 21/2, 22/3, 23/1, 23/2, 23/3, ausência de goleiros, mínimo de dez jogadores de linha, equilíbrio de posições e seeds 0–99. O mesmo catálogo e a mesma seed produzem o mesmo elenco.
- A simulação em memória verificou 172 clubes disponíveis, seleção obrigatória de 128, montagem com um e oito países e distribuição 8/8/8/8/96. Os testes offline também cobriram empate entre divisões, versões 1.0 e 1.1, prévia idempotente e excedentes compostos por cortes reais sem duplicação.
- Os excedentes agora aplicam a prioridade aprovada por clube de origem e posição, considerando estrela, prodígio, titularidade e participação comprovada, seguida da força como desempate. O runtime exige `RetentionEvidence` explícito. O CSV auxiliar ainda carece de integração e validação dos limiares de pesquisa. As fórmulas 016B/017B permanecem intactas.

## Qualidade e prontidão

Os CSVs de ranking cobrem 172 clubes. Os 171 valores de força preenchidos seguem a tabela nacional. O snapshot Opta de 12/11/2026 é referência complementar e a aprovação do ranking histórico 2025/26 segue pendente, assim como a vinculação dos 26 IDs Opta ausentes. Foram adicionadas 19 evidências de campo ou transferência vinculadas a cinco atletas. O CSV de pendências registra os campos ainda abertos dos 3.905 atletas, inclusive as divergências de minutagem de Rodri e Cubarsí. Os critérios e fontes consultadas estão em [Evidências e pendências V1.6](ELIFOOT98_ESPANHA_2026_QUALIDADE.md).

As 245 estrelas seguem em M/A. Foram conferidos 226 destaques locais (um a três por clube), 13 premiações ou seleções e seis marcações preexistentes. Os 3.905 comportamentos seguem `null` com as evidências V1.6 preservadas. O clube e a identidade do Real Madrid, bem como seus cinco arquivos de pesquisa, são idênticos aos do ZIP auditado. Seguem pendentes treinador, táticas, ranking, força e comportamentos.

## Verificação do pacote

Arquivo entregue `exports/ELIFOOT98_CATALOGO_ESPANHA_2026.zip`, com 1.301.363 bytes.

`D85BFC6BBD085DF62DC45E81CB2C2595D19DE88CDEFB876F66663DEEE1EA4873`

São 884 entradas, com 883 SHA-256 confirmados. O manifesto de hashes é a única entrada fora da própria lista. A extração temporária validou os cinco schemas oficiais, 172 clubes, 172 identidades, 3.905 jogadores, pesquisas e relatórios. O validador oficial executado na extração e no repositório retornou `valid=true`, `ready=false`, zero erros e 3.911 avisos espanhóis. O `sourceHash` foi idêntico nos dois locais, `3CF686A2824FA2BAC093D5A30150A641033D21EF7168874F841B888F1D9C6C8E`. O relatório de qualidade acompanha o ZIP. Este relatório fica ao lado do pacote para poder registrar o SHA-256 externo.

O blob de `e55ad9d` é o ZIP com SHA-256 `932C5E9572FCB95F6147DD729BA0036E172F448293B4A6FD826F2610C86A2ECA`. O de `c24e3c6` tem o SHA-256 `23467AAECA28505CF8EADC0F6DEE8CD9EC944FE48412A081CFE08912E604C3D1`, idêntico à cópia recebida na auditoria. As 876 entradas dos dois ZIPs antigos têm conteúdo, nomes e ordem idênticos. Mudaram apenas os horários internos de `EXPORT_MANIFEST_SHA256.txt` e `EXPORT_README.md`. O novo exportador fixa ordem, data e permissões, e gera o mesmo arquivo com os mesmos bytes de entrada e versão de Python/zlib.

## Build e testes

| Verificação | Resultado |
|---|---|
| `dotnet build Elifoot.slnx -c Release` | PASS, 0 avisos e 0 erros. A falha de compilação de `AiVoluntaryAuctionEngine.cs` não se reproduziu. |
| .NET offline | PASS, 60 testes de domínio e 105 de integração. A repetição de integração usou um cluster PostgreSQL isolado, com dados temporários. |
| .NET geral, aplicação/domínio/integração | Aplicação 66/66 e domínio 706/706. Os 407 cenários de integração passaram no conjunto da execução inicial e das repetições, conciliados pelos IDs dos casos no TRX. A primeira execução teve 27 falhas e dois casos ignorados. As repetições usaram PostgreSQL descartável, fixtures isolados e logging sem Event Log. Um cluster de teste esgotou o espaço no volume D: e foi substituído por um cluster temporário no volume C:. |
| Flutter | `flutter analyze` sem problemas. `flutter test --concurrency=1` passou os 566 testes. A primeira execução paralela esgotou memória. |
| Python e schemas | 68 testes passaram com a integração ao validador oficial habilitada. Cinco schemas e 363 instâncias locais validados. |
| ZIP | Extração, CRC, 883 hashes, 345 instâncias de schema e validador oficial aprovados. Repetição da geração com entradas idênticas produziu os mesmos bytes. |
