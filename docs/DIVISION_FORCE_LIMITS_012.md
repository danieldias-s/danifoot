# Divisões e limites de força — 012

> **REGISTRO HISTÓRICO — alvos sazonais legados.** Os alvos e observações abaixo pertencem à política 005/012 e não substituem `ChampionshipStrengthLimits` nem as bandas iniciais 017B. A rotina sazonal permanece como caracterização, sem chamada de produção em `src/` no HEAD `f1764fb`. Tetos atuais por posição, sorteio inicial e evolução após criação estão na [referência oficial](catalogos/ELIFOOT98_REGRAS_FORCAS_E_INTEGRACAO_V11.md). Nenhum número histórico foi recalculado nesta revisão documental.

**STATUS: PARTIAL.** Testados limites do projeto; fórmula original de nível-base do clube não confirmada. [Pesquisa](ORIGINAL_PLAYER_FORCE_RESEARCH_012.md).

| Divisão | Alvo sazonal local preservado da 005 | É teto de evolução? |
| --- | --- | --- |
| Primeira | 37 | Não |
| Segunda | 30 | Não |
| Terceira | 25 | Não |
| Quarta | 21 | Não |
| Distrital / sem clube | 17 | Não |

Esses valores não foram introduzidos como nova mecânica da 012. Continuam por determinação de preservar comportamento não comprovadamente corrigível. Não se equipara esse alvo à base misteriosa da pesquisa pública.

Há testes com força 48 e seis gols: sobe para 49 em **todas** as divisões, inclusive Distrital. O teto absoluto é 50. O ramo de ganho por gols tem precedência sobre perda por estar acima do alvo. Um jogador abaixo de alvo−4 pode crescer mesmo sem entrar; após essa faixa, o ganho por aparições depende de 12+ jogos e força abaixo de alvo+2. Isso explica a estabilização de reservas no controle, mas não é limite universal.

Promoção/rebaixamento altera o alvo usado no fechamento porque já aconteceu antes da rotina sazonal, conforme a implementação anterior. Não há clamp no momento da promoção, rebaixamento ou transferência. Um jogador forte contratado por clube distrital mantém a força imediatamente; depois pode perder pelo ramo sazonal existente, que não é apresentado como perda histórica comprovada.

A trajetória de histórico testa Quarta→Terceira→Segunda→Primeira→Quarta com continuidade de PlayerId e força crescente sob seis gols controlados. Essa prova é de mudança de contexto e persistência; a regressão geral usa promoções/rebaixamentos reais pelo motor de competições. A matriz de força 1 fixa o clube na Quarta e não deve ser confundida com uma trajetória real de acessos consecutivos.

Nos controles de posições 1/4/8, mantendo seed, elenco, divisão e participação, a trajetória de força é igual. A tabela não é entrada da política local. Influência indireta do resultado pode existir via gols individuais e decisões de escalação/mercado; isso não demonstra um bônus direto de classificação.

Perguntas pendentes sobre o original: a base é atributo estável do clube, valor inicial do arquivo, média do elenco, função de divisão ou outro dado? Muda após acesso? Qual lado da comparação usa força anterior/posterior? Como se comporta um atleta acima do limite de crescimento, sem avaliação negativa? As respostas não podem ser substituídas por tetos fixos inventados.
