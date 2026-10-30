# Pesquisa de força do Elifoot 98 — 012

30/10/2026, America/Sao_Paulo. **STATUS: PARTIAL.** Não se declara fidelidade ao executável original.

## Preservação e método

O Git estava limpo no checkpoint 011 `0c7fc1b` (anterior `efc5c3d`), sem remotos. Nenhum reset, checkout, publicação ou alteração de outro projeto. Manifesto anterior às alterações: [audit-012/baseline/manifest.json](audit-012/baseline/manifest.json). O trecho sazonal anterior está em [seasonal-005.txt](audit-012/baseline/seasonal-005.txt).

Foram consultados os relatórios principais 005–011: GAMEPLAY_BALANCE_100_SEASONS_005, DISCIPLINE_GAMEPLAY_AUDIT_006, METRIC_AI_AUDIT_007, COACH_CAREER_AUDIT_008, PLAYER_RANKINGS_LOCAL_VALIDATION_009, VISUAL_POLISH_010, COMPLETE_GAMEPLAY_UI_011 e KNOWN_ISSUES_011. A 005 identifica expressamente a evolução sazonal como balanceamento próprio; 008–011 a preservam.

Pesquisa pública por páginas indicadas, arquivos técnicos, roteiro do pesquisador e relatos históricos. Nenhum binário original foi obtido ou executado; nenhum código extraído de executável foi copiado. O código de produção preserva a política deste projeto. Resultados dos testes locais comprovam o clone, não o original.

## Fontes e proveniência

| ID | Fonte consultada | Natureza e limites |
| --- | --- | --- |
| T1 | [README do pesquisador](https://github.com/trsthales/desvendando-elifoot-98/blob/main/README.md) | Atribui a evolução pós-jogo a FUN_17fb_4a41; ganhos/perdas unitários, guarda extra de 50% para goleiros e tolerância de 50% para queda. Não publica a avaliação individual completa nem a origem da base do clube. |
| T2 | [player_evolution.c](https://github.com/trsthales/desvendando-elifoot-98/blob/main/src/player_evolution.c) | Pseudocódigo limpo, não dump bruto. Diverge de T1 nos extremos da faixa. |
| T3 | [Roadmap](https://github.com/trsthales/desvendando-elifoot-98/blob/main/ROADMAP.md) | Identifica os arquivos como pseudocódigo e deixa aprofundamento da evolução no roteiro. Mesma autoria de T1/T2, não confirmação independente. |
| T4 | [Relato de laboratório do autor](https://www.tabnews.com.br/trsthales/cheat-engine-nativo-no-linux-como-inspecionei-a-memoria-do-elifoot-98-no-debian-12) | Descreve breakpoint na escrita de força de um titular após avançar rodada e associação a FUN_17fb_4a41. Não disponibiliza aqui reprodução independente com saves e rastreamento completo. |
| H1 | [Eidy Tasaka, Jogo Véio, 09/05/2017](https://jogoveio.com.br/elifoot-98-pc/) | Relato de experiência: escala 1–50, grandes perdas por contusão e estratégia de comprar atletas fracos para desenvolver. Não fornece probabilidades, fórmula de lesão ou série de medidas. |
| H2 | [Guia Elifoot 98, janeiro de 2011](https://elifoot98.blogspot.com/2011/01/?m=1) | Recomenda substituições no intervalo para melhorar resultados. Não demonstra perda nominal por entrada ou fadiga. A página inicial retornou HTTP 429; o arquivo mensal foi acessível. |
| H3 | [Arquivo de dicas iGamex/GeoCities](https://www.oocities.org/igamex/e/elifoot-98.html) | Relato de comprar força 1 e valorizar ao longo do campeonato. Sem autoria/data verificáveis e com dicas de trapaça; não serve para deduzir algoritmo. |

Consulta em 30/10/2026. T1–T3 foram lidos em `main`; não foi obtido SHA de commit verificável. A descrição de ferramenta/compilador e os endereços são afirmações do pesquisador. Não se dispõe de hash/versão identificável do ELIFOOT.EXE analisado, saves de controle, amostras de lesões ou desassemblagem que permitam validar todas as conclusões. Múltiplas publicações do mesmo autor não aumentam a independência da evidência.

## Descobertas classificadas

| Descoberta | Classificação | Decisão |
| --- | --- | --- |
| Escala nominal 1–50 | EVIDENCIA_TECNICA + RELATO_HISTORICO | Corrigir mínimo 10 da política local para 1. |
| Mudança pós-jogo/por rodada | EVIDENCIA_TECNICA | Periodicidade publicada difere da política local; gatilho completo desconhecido. Não converter vitórias em ganhos automáticos. |
| Avaliação individual, titularidade, minutos, banco | NAO_CONFIRMADO | O avaliador é omitido. Percorrer elenco não prova que reservas recebem avaliação positiva. |
| Classificação, moral e divisão definem ganho | HIPOTESE | Não se conhece transformação de tabela/moral em avaliação nem cálculo da base. |
| Base do clube e faixa de aproximadamente ±5 | EVIDENCIA_TECNICA com contradição | Não fixar teto por divisão nem calcular base pela média sem evidência. |
| Guarda extra de 50% para ganho de goleiros | EVIDENCIA_TECNICA | Condicional à avaliação positiva; frequência real de ganhos continua desconhecida. Não enxertar isoladamente na política sazonal. |
| Queda automática ao entrar em campo | NAO_CONFIRMADO | Não implementar. |
| Perda significativa por contusão | RELATO_HISTORICO | Efeito qualitativo relevante; magnitude, sorteio e permanência não quantificados. Não inventar desconto. |
| Recuperação total/parcial, reincidência, risco por posição | NAO_CONFIRMADO | Preservar o projeto e registrar lacunas. |
| Mínimo local 10 causa salto de força 1 para 10 | CONFIRMADO_POR_TESTE — clone | Corrigido para 1→2 no ganho unitário. |
| Participação, lesão e liquidação não alteram força nominal durante a rodada local | CONFIRMADO_POR_TESTE — clone | Preservado; divergência de lesão continua aberta. |

### Contradição decisiva nos limites

Em T1, a comparação é feita **antes** da alteração: força igual a base+5 ainda pode subir, chegando a base+6; base−5 pode cair a base−6. Em T2, o resultado é verificado **depois** e o incremento/decremento é cancelado fora de base±5. Com base 20, força 25 evoluiria para 26 em T1 e permaneceria 25 em T2. Na queda, 15 poderia ir a 14 em T1 e seria mantida em T2. São comportamentos diferentes, não uma tradução equivalente.

Nenhuma das versões resolve o avaliador de desempenho ou a regra de contusão. Portanto não há base suficiente para substituir o sistema ativo por uma transcrição desses arquivos. Uma implementação futura exige ensaios independentes identificando versão/hash do original, snapshots antes/depois e controle de RNG/participação. As perguntas experimentais estão nos relatórios de lesão e limites.
