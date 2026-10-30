# Impacto de expulsões e contusões — 012C

STATUS: PASS. ComputeSectorBreakdown, GetEffectivePlayerRating e fórmula defensiva preservados exatamente no código do checkpoint.

O motor usa a lista real de jogadores ativos em cada minuto. Expulso sai definitivamente. Lesionado sai imediatamente, mantendo apenas uma identificação de vaga para eventual troca legal. Substituído deixa de estar exposto a faltas/contusões. Antes do cálculo seguinte, listas de expulsos/lesionados também removem qualquer resíduo de estado antigo.

A fórmula existente redistribui os setores pela formação, reduz vagas quando há menos atletas e usa os pesos posicionais anteriores: 1,00 no setor natural, 0,65 improvisado em outro setor de linha e 0,35 no gol improvisado. O fator coletivo 0,93 elevado ao número de atletas ausentes já existia nas auditorias 005/006. Foi preservado, sem novo desconto adicional. A defesa permanece (Defesa + 0,45×Meio) × mando × identidade × fator coletivo; ataque usa (Ataque + 0,50×Meio), com modificador clássico 5-0-5 inalterado.

Exemplo controlado 4-4-2, força 30, sem estrelas, sem mando ou bônus de identidade:

| Composição | Atletas | Força efetiva de defesa | Força efetiva de ataque | Força do goleiro |
| --- | --- | --- | --- | --- |
| 11 completos | 11 | 174 | 120 | 30 |
| Sai um atacante | 10 | 161,82 | 83,7 | 27,9 |
| Saem dois atacantes | 9 | 138,813 | 55,786 | 25,947 |
| Sai um defensor | 10 | 147,661 | 78,818 | 27,9 |
| Sai um meio-campista | 10 | 157,426 | 78,818 | 27,9 |
| Sai o goleiro; linha improvisa | 10 | 147,661 | 78,818 | 9,765 |

São consequências da composição e fórmula existente, não um percentual novo de penalização. A matriz de testes compara 11×11, 10×11, 9×11 e 10×10; testes de expulsão real cobrem G/D/M/A, proibição de retorno/substituição e ausência em eventos posteriores. A escolha do improvisado no gol retira aquele mesmo atleta das contribuições de linha: nenhum atleta gratuito é acrescentado.

Limites de troca preservados: 5 jogadores/3 janelas em 90 minutos; 6/4 na prorrogação; intervalos gratuitos conforme regra existente. Sem quota/janela/reserva apto, a vaga permanece vazia. Duas lesões podem deixar nove. A IA continua usando sua lógica aprovada; a retirada por lesão não realiza troca sozinha. O treinador humano escolhe se fará uma troca legal.

HomePlayersOnPitch/AwayPlayersOnPitch e IsOnPitch refletem a composição real na API. O lesionado e o expulso permanecem identificáveis na projeção, com IsOnPitch=false; apenas a vaga de lesão aceita substituição. Banco esgotado não é reconstruído a partir da preferência antiga. A UI usa RequiredStarterCount do servidor para permitir início reduzido quando faltam aptos e exibe a quantidade em campo.

Em 100 temporadas reais, 7.386 partidas terminaram com algum lado abaixo de 11 e 598 vagas por lesão ficaram sem preenchimento no encerramento. Esses números também podem incluir equipes que já começaram reduzidas, conforme escolha do proprietário. Não se impôs mínimo de sete; o estresse legado contém casos com cinco aptos. Um elenco sem nenhum apto bloqueia a preparação; não se inventa goleiro/atleta para liberar o jogo.
