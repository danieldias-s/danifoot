# Análise de contusões e força — 012B

**STATUS: PARTIAL. ORIGINAL_EXECUTIONS: 0.** A perda nominal por contusão é a pendência de maior prioridade. Nenhuma fórmula foi introduzida e nenhum efeito nominal inexistente foi registrado como lesão.

## Hipóteses e suas origens

| Hipótese | Origem | Situação |
| --- | --- | --- |
| Contusão pode provocar queda grande de força | [Relato de Eidy Tasaka sobre sua experiência com o Elifoot 98](https://jogoveio.com.br/elifoot-98-pc/) | ORIGINAL_PARTIALLY_SUPPORTED: relato qualitativo, não série de medições |
| A queda é imediata no mesmo minuto | Inferência possível da descrição do relato | Não confirmado: não distingue evento, atualização de tela ou avaliação seguinte |
| A queda depende da força anterior | Não consta fórmula nas fontes examinadas | Não confirmado |
| A queda depende da gravidade/duração | Hipótese de modelagem, não descoberta técnica | GAME_DESIGN_PROPOSAL se adotada |
| A queda é permanente | Não há sequência observada de lesão→afastamento→retorno | Não confirmado |
| Recuperação restaura toda ou parte da força | Não há evidência quantificada | Não confirmado |
| Segunda lesão pode descontar novamente | Plausível como modelo, sem amostra do original | Não confirmado |
| Goleiro tem risco/perda diferente | Guarda de evolução de goleiro não é rotina de lesão | Não confirmado |
| Duração de afastamento do original | Arquivo público não contém rotina correspondente | Não confirmado; não converter parâmetros do clone em história |

Não foi encontrado nas versões fixadas, commits ou forks um mecanismo de lesão com probabilidade, duração e perda nominal. O artigo de laboratório trata de escrita de força após rodada, não isola uma contusão. A ausência no pseudocódigo não prova ausência no jogo original.

## Testes novos no projeto

A matriz cobre forças 1/10/25/40/50, goleiros/atacantes, quatro divisões e posições 1/4/8. Cada condição já lesionado começa com contador 3; após três jogos do clube, passa naturalmente à condição recuperado e pode ser escalado. Cada condição recuperado começa com contador 1 e uma partida real de preparação, na qual o atleta não entra, reduz o contador a zero. Nenhuma força perdida é artificialmente restaurada no preparo.

Eventos novos de contusão do atleta monitorado são capturados **imediatamente no tick**, com minuto, força antes/depois e duração. Durante o resto da partida, no apito e na liquidação também se verifica força nominal. No clone, os contadores e o efeito de finalização já existentes continuam sem desconto nominal. Isso é caracterização do projeto, não confirmação da hipótese histórica.

O afastamento local continua em jogos do clube, não dias nem rodadas globais. A regra atual não reduz no próprio jogo da lesão; ausência de partida impede decremento. A disponibilidade retorna ao zerar contador. Uma segunda contusão real do mesmo atleta é exercitada pela regressão 012. Não se acrescentou risco especial ou proteção após retorno.

## Entrar em campo: mecanismos separados

| Momento | Instrumentação 012B | Conclusão restrita ao clone |
| --- | --- | --- |
| A. Escalação como titular | Força antes da preparação e durante os ticks | Sem desconto nominal |
| B. Entrada como reserva | Pausa após minuto 45 ou 80, força antes/depois; primeiro tick do substituto é o seguinte | Sem desconto nominal |
| C. Participação completa | Verificação em todos os 90 ticks | Sem sistema novo de fadiga |
| D. Fim do jogo | Medida no apito e depois da liquidação | Sem desconto nominal |
| E. Contusão no jogo | Registro de cada evento do atleta monitorado | Duração/indisponibilidade; sem queda nominal |
| F. Avaliação posterior | Medida separada após fechamento sazonal | Único ponto atual capaz de alterar nominal; não é avaliação original por rodada |

Não se denominou a penalidade efetiva de finalização já existente como redução da força nominal. Não se criou penalidade ao entrar para explicar uma observação ainda não isolada.

## Alternativas para avaliação de design — nenhuma implementada

As alternativas abaixo são **GAME_DESIGN_PROPOSAL**, não reconstruções históricas. Estão prontas para discutir comportamento e consequências; **magnitude, distribuição e frequência não foram escolhidas**. Um percentual inventado ou desconto fixo de 5/10 pontos não resolveria a lacuna de evidência. A aprovação do conceito ainda exigiria aprovar os parâmetros e seus ensaios antes da implementação.

| Alternativa | Comportamento proposto | Recuperação e reincidência | O que exige decisão |
| --- | --- | --- | --- |
| A. Preservar 012 enquanto se pesquisa | Manter nominal, indisponibilidade e efeito temporário existentes | Retorno apenas à disponibilidade; sem passivo de força | Nenhuma alteração de gameplay; é o estado atual |
| B. Perda nominal persistente | Um sorteio de perda variável no evento da contusão, limitado para manter 1–50; evento INJURY_STRENGTH_LOSS vinculado à lesão | Fim do afastamento não restaura pontos; nova lesão pode produzir novo sorteio | Distribuição, chance de perda zero, relação com força/gravidade, momento e critérios de elegibilidade |
| C. Perda nominal com recuperação parcial | Mesma perda variável, com saldo de recuperação explicitamente separado | Retorno permite recuperar só parte por eventos RECOVERY_PROGRESS; segunda lesão mantém identidades e saldos separados | Todos os parâmetros de B, fração recuperável, condições/prazos e combinação de episódios |
| D. Efeito somente durante o afastamento | Alterar apenas força efetiva; nominal e histórico nominal ficam intactos | Efeito termina ao recuperar; não simular perda permanente | Qual ação é afetada, intensidade variável e consequência estratégica; pode divergir do relato de queda nominal |

B e C tornariam a contusão um evento de carreira, mas sua adoção seria uma decisão nova de jogo. D tem menos impacto permanente e não atende por si só ao relato de perda de níveis. Não existe alternativa numérica historicamente recomendável com os dados atuais.

## Ensaio necessário para escolher uma regra histórica

Usar somente cópia legítima identificada por versão e SHA-256. Preservar o save anterior e o estado de aleatoriedade quando acessível. Medir a força nominal imediatamente antes/depois da contusão, no intervalo, antes/depois da avaliação final e em cada retorno. Registrar o valor mostrado na escalação separadamente de qualquer força efetiva em lance.

Repetir nos cinco níveis de força, nas quatro divisões, para goleiro e jogador de linha, com episódios de durações diferentes e segunda lesão do mesmo atleta. Isolar comparação de banco/entrada e classificação. Mesma seed do clone não é seed do original; ações diferentes também podem consumir sorteios diferentes. Dados desconhecidos permanecem nulos, não zero. O [plano de captura](audit-012b/original-capture-plan.json) contém 720 células sem observações originais inventadas.
