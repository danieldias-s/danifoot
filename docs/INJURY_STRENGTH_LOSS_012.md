# Lesões e perda de força — 012

**STATUS: PARTIAL. Prioridade histórica ainda não resolvida.** Há relato de perda grande por contusão; a pesquisa não encontrou fórmula verificável de magnitude, severidade ou recuperação. Nenhum desconto fixo, percentual ou distribuição nova foi implantado. [Fontes e proveniência](ORIGINAL_PLAYER_FORCE_RESEARCH_012.md).

## Comportamento local inspecionado e testado

`MatchEngine.cs` permanece integralmente idêntico ao checkpoint 011, incluindo a defesa.

| Aspecto | Implementação atual, não regra histórica confirmada |
| --- | --- |
| Probabilidade | Sorteio inferior a 0,0018 por equipe/minuto quando há atletas ativos. Não é probabilidade por jogador. Seleção uniforme entre ativos; tentativa em atleta já lesionado na mesma partida não gera nova lesão. |
| Momento | Durante o tick, após disciplina e antes do cálculo de setores/ataques. |
| Duração | `NextInt(1,4)`: 1, 2 ou 3 jogos do clube. Não é quantidade de rodadas globais nem dias. |
| Lesão no dia | Não decrementa a duração na liquidação dessa partida. |
| Rodada sem jogo | Contador não diminui. Isso pode alongar afastamento de distrital eliminado. |
| Próximas partidas | Contador diminui uma vez por jogo liquidado, protegido pela trava financeira existente. |
| Força nominal | Não se modifica no evento, no restante da partida nem quando termina o afastamento. |
| Força efetiva | Finalizador lesionado em campo recebe multiplicador 0,60 na finalização. Não é desconto de 40% na força nominal ou em todos os setores. |
| Permanência em campo | O evento marca lesão, mas não remove automaticamente o jogador. A IA pode substituí-lo. O comentário sobre gravidade/saída em MatchEngine não corresponde a uma retirada implementada nesse método. |
| Nova escalação | Indisponível enquanto o contador for positivo, ressalvada a exceção de emergência já existente descrita abaixo. |
| Recuperação | Disponibilidade retorna ao zerar o contador. Não há restauração de pontos nem recuperação progressiva específica de força. |
| Reincidência | Possível após recuperação, pelo mesmo sorteio. Sem memória de risco, desconto adicional ou proteção temporal. |
| Posição | Não há coeficiente específico de lesão por posição. Goleiros também estão entre os ativos sorteáveis. |

## Separação dos mecanismos

A: entrar como substituto não altera força nominal. B: ticks também não alteram nominal. C: liquidação pós-jogo não altera nominal. D: ajuste por gols/aparições/alvo existe somente no encerramento sazonal. E: contusão não altera nominal no projeto. F: lesionado durante a partida pode continuar e sofre efeito na finalização. G: esse efeito é temporário e local ao lance, não queda permanente registrada.

Portanto **não existem eventos INJURY_STRENGTH_LOSS ou RECOVERY_PROGRESS nesta entrega**. Criá-los sem alteração nominal real seria histórico falso. Uma evolução sazonal durante um afastamento continua NORMAL_PROGRESS, com regra e detalhe legados; não se chama recuperação de lesão.

## Escalação de lesionados e limites

Em condições normais, `IsAvailableForMatch`, IA e validação bloqueiam o jogador. Entretanto, já existe fallback em `AiManagerBrain.BuildStartersAndBench` para completar o time com os menos indisponíveis quando faltam atletas; `SquadValidator` também permite titular indisponível quando faltam jogadores disponíveis no setor. Não foi criada essa exceção para simular o original, e não foi alterada a regra tática preservada pelo pedido. Os testes de exclusão usam elenco com substitutos disponíveis; não são prova de proibição absoluta.

Essa exceção merece decisão separada após evidência do original. Não foi acrescentada penalidade por escalá-lo, nem se afirma que o Elifoot 98 permita isso. Regras de recém-recuperado continuam NAO_CONFIRMADO.

## Testes e investigação necessária

Testes exercitam evento real do motor, duração, não decremento no próprio jogo, liquidação repetida, recuperação sem mudança nominal, nova lesão real do mesmo atleta e lesão real de goleiro. As seeds são exploradas em intervalos fixos, sem RNG externo. Não comprovam que ausência local de perda seja historicamente correta.

Para completar a prioridade: identificar a versão/hash do original; observar muitos eventos com força nominal imediatamente antes, no minuto da contusão, no intervalo, após o jogo e após cada afastamento; registrar duração e participação; comparar força 1/10/25/50, todas as posições e recidivas. Sem essa série não se distingue perda permanente, temporária ou recuperação parcial. Nenhum binário proprietário é necessário no repositório nem deve ser incorporado a ele.
