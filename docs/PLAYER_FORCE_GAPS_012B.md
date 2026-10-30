# Lacunas e decisão final — 012B

**STATUS: PARTIAL.** A investigação pública foi aprofundada, os ensaios locais foram ampliados e as regressões exigidas foram executadas. A fidelidade histórica solicitada não pode ser declarada enquanto faltar a evidência abaixo.

| Lacuna | Evidência que falta | Critério para fechar |
| --- | --- | --- |
| Avaliação de rendimento | Definição, entradas, elegibilidade e momento da avaliação | Observação independente identificável ou documentação completa coerente com ensaios |
| Nível-base do clube | Origem do parâmetro e atualização ao longo dos anos | Testes distinguindo base inicial, média, divisão e promoção/rebaixamento |
| Reservas | Medidas sem entrar, entrando aos 45/80 e fora da relação | Trajetórias do original com condições e aleatoriedade controladas |
| Classificação | Relação direta com avaliação, separada de gols/resultado/moral | Comparação líder/meio/último sem inferir causalidade só de correlação |
| Divisão/teto | Desempate da contradição dos limites publicados | Casos exatamente na fronteira e acima/abaixo dela, com base conhecida |
| Goleiros | Avaliador e frequência efetiva de oportunidades | Comparação equivalente; não basta aplicar 50% isoladamente à política sazonal |
| Contusão | Momento, distribuição, duração, força anterior e gravidade | Série de lesões observadas antes/depois e durante retorno |
| Recuperação/reincidência | Permanência da perda e episódios posteriores | Acompanhamento do mesmo atleta e identificação de cada episódio |
| Perda ao entrar | Separação entre entrada, lance, contusão e avaliação final | Medições em pausa antes/depois da entrada e após avaliação |
| Proveniência do original | Executável local legítimo identificável | Versão/hash, referência de licença e saves de ensaio, sem redistribuir binário |

O repositório público e seus três forks não forneceram novos arquivos para fechar essas lacunas. Foram identificadas precisamente suas versões, hashes e limites. Isso permite encerrar esta etapa de pesquisa com um resultado verificável de insuficiência, sem declarar o objetivo histórico cumprido.

## Classes de evidência

- **ORIGINAL_VERIFIED:** nenhum novo algoritmo completo confirmado em execução original nesta missão.
- **ORIGINAL_PARTIALLY_SUPPORTED:** pós-jogo, guarda de goleiro, tolerância de queda e âncora de clube nas descrições públicas; crescimento de atletas baratos e perda por contusão em relatos. Limites contraditórios e condições omitidas continuam explícitos.
- **GAME_DESIGN_PROPOSAL:** alternativas funcionais de lesão documentadas para avaliação, sem parâmetros arbitrários ativados. A política sazonal existente é identificada como comportamento herdado do projeto.

Um teste verde do clone não promove uma hipótese para ORIGINAL_VERIFIED. A matriz deixa OriginalObserved=false e medidas originais nulas. Zero é um resultado medido; null é dado não disponível.

## Preservação e validação

Nenhum arquivo de produção foi alterado pela 012B. Escala, histórico, guardas de idempotência, finanças, táticas, IA, carreira, rankings, MatchEngine/defesa e multiplayer permanecem como na 012. O checkpoint 011 e todo o trabalho 012 foram mantidos e comparados por hash. Não houve publicação, mudança de outro projeto ou download de executável.

Entregas adicionais: dois arquivos de testes, coletor de proveniência, verificador/gerador de plano de captura, metadados públicos, logs próprios e estes quatro relatórios. As [propostas de lesão](PLAYER_INJURY_FORCE_ANALYSIS_012B.md) podem fundamentar uma decisão futura de design, mas não foram aprovadas ou implementadas implicitamente por este pedido de pesquisa.

## Relatório final obrigatório

```text
STATUS: PARTIAL
ORIGINAL_EVOLUTION: descrição pública incompleta; AvaliarRendimento ausente, sem execução original
RESERVE_EVOLUTION: relato histórico parcial; matriz do clone concluída, regra original não confirmada
TEAM_POSITION_EFFECT: sem efeito direto no clone; transformação original desconhecida
DIVISION_FORCE_LIMIT: âncora original desconhecida e limites públicos contraditórios; escala 1–50 preservada
GOALKEEPER_EVOLUTION: guarda condicional de 50% parcialmente sustentada; avaliador completo pendente
ON_FIELD_FORCE_LOSS: nenhuma perda nominal nas fases medidas do clone; original não confirmado
INJURY_FORCE_LOSS: relato qualitativo; fórmula, gravidade e permanência não comprovadas
INJURY_RECOVERY: retorno e segunda lesão testados no clone; recuperação nominal original desconhecida
IMPLEMENTED_CHANGES: pesquisa, proveniência fixada, matriz ampliada, contraexemplos e plano de captura; zero mudanças de produção
UNCONFIRMED_BEHAVIORS: avaliador, base, reservas, classificação, fronteiras, perda/recuperação por lesão e perda ao entrar
DOTNET_TESTS: 276 casos únicos PASS; 275 na suíte completa e um caso novo no fechamento
FLUTTER_TESTS: 50 PASS; flutter analyze sem problemas
REGRESSIONS: build Release, suíte completa, 100 temporadas com distribuições comparadas à 012; sem mudança de defesa
EVIDENCE_SOURCES: commit 2f7ff68f50bf3af8a57d0baf5a4938f65abf0f03, oito commits/três forks, postagens do pesquisador e relatos históricos vinculados
```

Relatórios complementares: [pesquisa original](PLAYER_FORCE_ORIGINAL_RESEARCH_012B.md), [testes diferenciais](PLAYER_FORCE_DIFFERENTIAL_TESTS_012B.md), [análise de contusão](PLAYER_INJURY_FORCE_ANALYSIS_012B.md).
