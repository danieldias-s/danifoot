# Instalação do catálogo Espanha 2026/27

**Instalação e validação estrutural: PASS. Prontidão e qualidade dos dados: PARTIAL.** O catálogo permanece DRAFT.

## Backup e substituição

- Branch `main`, HEAD inicial `fa70633d96fa42b8ea34492e1ec684d411a75cd2`. Foram conferidos o índice, os arquivos rastreados, as alterações locais e as entradas não rastreadas antes da instalação.
- A instalação foi registrada no commit local `5b74054`. O HEAD final é o commit de exportação que contém este relatório.
- Backup local `backups/catalogos_antes_espanha_20261112_235623.zip`, com 928 arquivos conferidos por SHA-256 e CRC do ZIP. Inclui toda a árvore de catálogos, identidades, pesquisas, evidências V16 e documentação pertinente. SHA-256 do backup `CF15713A5D56CE92AB1A3A8224C9BB30421ECF49EAECC25AD841E7004F17C435`.
- Foram substituídos 172 cadastros em `catalogos/espanha/2026/clubes/`. Os 172 IDs coincidem com os anteriores. Nenhum arquivo de clube ficou órfão.
- Foram retirados `manifest.json` e `clubes/.gitkeep` dos seis rascunhos vazios `brasil/1998`, `brasil/2026`, `espanha/1998`, `inglaterra/2026`, `italia/2026` e `portugal/1998`. Essas temporadas tinham zero clubes. As pesquisas anteriores, os modelos e a documentação permanecem disponíveis.
- A instalação foi validada primeiro em uma pasta temporária. As alterações preexistentes de auditoria e os arquivos de pesquisa não rastreados foram preservados. A instalação e suas verificações são locais, sem push, deploy ou alterações em saves, banco e mundos QA.

## Dados instalados

| Categoria nacional | Clubes | Jogadores | Estrelas | Clubes com ranking |
|---|---:|---:|---:|---:|
| LaLiga | 20 | 460 | 43 | 20 |
| Segunda corrigida | 22 | 506 | 36 | 22 |
| Primera Federación | 40 | 908 | 68 | 40 |
| Segunda Federación | 90 | 2.031 | 98 | 0 |
| Total | **172** | **3.905** | **245** | **82** |

O manifest único tem ID `es-2026-principal`, versão 1.1, país ES, ano 2026, gênero Male e status DRAFT. Suas 172 referências seguem a ordem dos quatro catálogos-base, confirmada nos CSVs dos pacotes. As 172 identidades correspondentes foram conferidas com as dos pacotes. Nenhum pacote forneceu escudos. Os 1.113 hashes internos dos quatro ZIPs de categoria passaram. O ZIP nacional anterior serviu como referência histórica. Os JSONs de `02_PREVIA` ficaram fora da instalação.

Foram preservados os dados de cada jogador dos catálogos-base, sem força individual, e os 172 treinadores com as táticas recebidas. O Real Madrid veio no pacote LaLiga com José Mourinho, formação 4-5-1 em casa e fora e Denzel Dumfries no lugar de Raúl Asencio em relação ao cadastro anterior. A força coletiva do Real Madrid é 20 e o ranking é 15. As 245 estrelas estão em M/A e os 3.905 comportamentos permanecem `null`. Os 390 arquivos V16 dos pacotes da Terceira e da Quarta coincidem byte a byte com a pesquisa já armazenada.

## Força, ranking e pendências

- Cada força de 20 a 5 foi atribuída a exatamente dois clubes, nas posições nacionais 1–32. Os 140 clubes das posições 33–172 têm força 1. A ordem de importação é a dos manifests dos pacotes, com a Quarta agrupada conforme a ordem provisória de origem.
- Os 82 rankings disponíveis correspondem ao valor total de mercado em milhões de euros dividido por 100. Fonte e data registradas nos JSONs foram preservadas. Os pacotes da Segunda e Terceira indicam que a data original dos valores indexados carece de confirmação. Os levantamentos Opta anteriores ficam como pesquisa histórica.
- Os 90 clubes da Quarta têm ranking e prioridade de desempate nulos. Os desempates existentes nos outros pacotes foram preservados. A aprovação dos 3.905 comportamentos continua pendente. O catálogo aparece para consulta e a criação de mundos está bloqueada pelo estado DRAFT.
- As fontes e os relatórios de origem, com o inventário nacional, estão em `catalogos/_pesquisa/espanha/2026/importacao-2026-27/`.

## Validação e pacote

- Cinco schemas oficiais e 345 documentos V1.1 validados com Draft 2020-12 e resolução real de `$schema`.
- Build Release da solução passou em uma pasta de saída isolada, com zero avisos e erros. A primeira tentativa na pasta padrão encontrou DLLs ocupadas por uma simulação da auditoria anterior.
- O validador oficial `offline-catalog-validate` retornou uma única Espanha 2026, 172 clubes, zero erros, 3.905 avisos `BEHAVIOR` e 90 `RANKING_RESOLUTION` no repositório e no ZIP extraído. Ambos retornaram `sourceHash` `4DF15E3AF6326964FD953AE491A833DC313A712BB0C14293041B106E197DFD7D`.
- Testes .NET Release relevantes passaram, 60 de domínio e 47 de integração. A seleção em memória rejeitou 172 vagas e aceitou 128, distribuídas em 8/8/8/8/96, com limites de elenco e goleiros por divisão. Penya Deportiva e Real Madrid Castilla foram exercitados com 100 seeds cada. Nenhum mundo foi persistido. Os 68 testes Python também passaram, com o validador oficial habilitado.
- `exports/ELIFOOT98_ESPANHA_2026_COMPLETA.zip` contém 908 entradas. A extração confirmou 907 SHA-256 e CRC. Inclui instruções de instalação e pesquisas de origem. SHA-256 externo `0969BF988A15C0AE0F37ADC2FC5FD78EA1CD6F13C27DEDE388CCD4A6B4F96E3B`.
