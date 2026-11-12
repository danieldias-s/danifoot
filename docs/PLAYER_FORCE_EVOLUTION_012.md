# Evolução de força — 012

> **REGISTRO HISTÓRICO — política sazonal 012/005, não o fluxo atual de produção.** Este texto conserva a investigação e os testes da época. No código de `f1764fb`, `PlayerStrengthEvolutionEngine.ApplySeasonEnd` permanece para caracterização, sem chamada de produção em `src/`; fechamentos futuros da Liga usam +1 por ausência elegível e sorteio de 50% de -1 por participação, inclusive nos saves legados. Regras atuais e fontes: [referência oficial](catalogos/ELIFOOT98_REGRAS_FORCAS_E_INTEGRACAO_V11.md). Não houve reexecução ou alteração da implementação nesta revisão documental.

**STATUS: PARTIAL.** Implementada uma correção de escala e rastreabilidade; a evolução original por rodada continua sem reconstrução validada. [Pesquisa e classificação](ORIGINAL_PLAYER_FORCE_RESEARCH_012.md).

## Auditoria exata do sistema anterior

`CompetitionAndSeasonEngine.EvolvePlayersAtSeasonEnd` era chamado uma vez no fluxo de `FinalizeSeasonAndAdvance`, depois de promoções/rebaixamentos e antes de inicializar a próxima temporada. Percorria **todos** os jogadores na ordem de `world.Players`, inclusive reservas, lesionados e atletas sem clube. Não guardava evento de força nem trava própria.

RNG: `FromContext(seed, season, 100, índice+1, forçaAnterior, 888)`. Não usa GUID aleatório ou relógio; a ordem do elenco faz parte do contexto legado. Alterar essa ordem pode mudar os sorteios futuros.

Alvos de balanceamento antigos: Primeira 37, Segunda 30, Terceira 25, Quarta 21, demais/sem clube 17. **São parâmetros locais da 005, não valores comprovados do original, nem tetos absolutos.** A divisão utilizada já é a resultante da promoção/rebaixamento.

Ramos, nesta ordem:

1. Pelo menos seis gols na soma Liga+Taça: +1. Alternativamente, 12+ aparições, força menor que alvo+2 e sorteio de 42%: +1.
2. Dentro desse ramo, 11+ gols, força menor que 46 e sorteio de 35%: trocar +1 por +2.
3. Se não houve ganho: força maior que alvo+6 e sorteio de 45%: −1.
4. Caso anterior não aceite: força menor que alvo−4 e sorteio de 55%: +1.
5. Caso anterior não aceite: até duas aparições, força maior que 22 e sorteio de 25%: −1.
6. Delta zero não alterava o jogador. Delta não zero aplicava `clamp(força+delta,10,50)` e recalculava preço justo e salário.

As probabilidades são condicionais e os `else if` incluem o sorteio: um atleta forte com até duas aparições pode passar pelo ramo de 45% e, se este falhar, ainda sofrer queda no ramo de 25%. Não é correto resumir todos os casos de queda a 45%. Aparição de 10 minutos e de 90 minutos contam igualmente para `SeasonMatches`.

## Implementação final

A política foi isolada em `PlayerStrengthEvolutionEngine.ApplySeasonEnd`, com uma única chamada no mesmo ponto do encerramento. A rotina antiga foi removida para não somar dois processamentos. Os alvos, ordem de ramos, contexto do RNG e probabilidades foram preservados. O limite inferior foi corrigido para **1**. O teto continua **50**. Um pedido de +1 quando já está em 50 não produz evento de mudança.

`EvolvedPlayerStrengthSeasons` impede uma segunda aplicação na mesma temporada e é serializado no snapshot. Transferir ou reconectar não limpa esse conjunto. Mudanças reais atualizam preço justo e salário pelas funções existentes e geram o [histórico](PLAYER_FORCE_HISTORY_012.md).

Não foi ativada uma rotina nova por rodada, por vitória, por classificação, de fadiga ou de perda nominal por lesão. As condições incompletas do original não foram preenchidas com números inventados. A presença de uma lesão não é usada para atribuir falsamente um ajuste sazonal à contusão.

## Atleta comprado com força 1

A matriz controlada contém 18 trajetórias: seis participações × posições 1/4/8, 30 temporadas cada, com seed 199812, compra real de um atacante distrital por um clube da Quarta e partidas pelo motor real. Mantêm-se divisão e posição como controles experimentais; não são 540 competições completas de 128 clubes.

| Participação | Força inicial | Força após 30 temporadas | Líder/meio/último |
| --- | --- | --- | --- |
| Titular | 1 | 22 | Mesma trajetória |
| Banco sem entrar | 1 | 15 | Mesma trajetória |
| Fora da relação | 1 | 15 | Mesma trajetória |
| Entrada no intervalo | 1 | 22 | Mesma trajetória |
| Entrada aos 60 | 1 | 22 | Mesma trajetória |
| Entrada aos 80 | 1 | 22 | Mesma trajetória |

Logs completos: [resumo](audit-012/regression/force-012/force-one-summary.json), [11.880 observações de rodada](audit-012/regression/force-012/force-one-rounds.jsonl). Disponibilidade e entrada efetiva constam separadamente; lesão pode impedir a participação planejada. Não há mercado automático nesse controle. Contratos são mantidos longos para isolar a evolução; temporadas são encerradas pelo método de força, sem recalcular as outras competições.

Os resultados mostram crescimento de longo prazo no projeto, inclusive sem participação, mas um ritmo bem mais lento que o sugerido por alguns relatos históricos. A igualdade entre classificações comprova a ausência desse fator na política atual; não prova que o original também o ignorava.

O mundo padrão usado na regressão começa com força mínima 10 e nenhum atleta de força 1. Os testes configuram explicitamente esse jogador e o compram pela transferência real. Editor/importação já aceitam 1–50; não se mudou a geração do seed ou a oferta de mercado para introduzir jogadores fracos sem evidência. Assim, a validação de crescimento não promete disponibilidade imediata de força 1 numa sala padrão.
