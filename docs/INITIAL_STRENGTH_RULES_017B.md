# Força inicial dos elencos 017B

A referência é a posição inicial do clube dentro da divisão na temporada 1. A montagem automática conserva a ordem aprovada do criador de mundos. A montagem personalizada conserva os clubes e a ordem em que foram definidos dentro de cada divisão. O preenchimento automático completa as vagas livres na ordem do catálogo. `ClubInitialPower` continua sendo um dado de ordenação do catálogo na escala 1–20. O resultado esportivo passa a determinar a classificação assim que os jogos começam.

| Posição inicial | 1ª divisão | 2ª divisão | 3ª divisão | 4ª divisão |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 50 | 38 | 25 | 12 |
| 2 | 50 | 37 | 24 | 11 |
| 3 | 50 | 36 | 23 | 10 |
| 4 | 46 | 34 | 21 | 8 |
| 5 | 44 | 32 | 19 | 6 |
| 6 | 42 | 30 | 17 | 4 |
| 7 | 40 | 28 | 15 | 2 |
| 8 | 39 | 27 | 14 | 1 |
| Faixa da divisão | 39–50 | 26–38 | 13–25 | 1–12 |

Cada atleta selecionado recebe uma força inteira de referência −1 a referência +1, com as extremidades ajustadas à faixa da divisão. Nos três primeiros clubes da 1ª divisão, o intervalo é 47–50. O algoritmo escolhe quatro atletas por seed para garantir os quatro valores e faz sorteios individuais para os demais. A seed do mundo, a chave do clube e o identificador estável do atleta alimentam o sorteio. Nas demais posições podem ocorrer empates. Os distritais seguem o fluxo anterior.

## Bandas exatas conferidas no código

Conferência documental em `f1764fb`: [InitialSquadStrength017B.Reference](../src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs) usa [ChampionshipStrengthLimits.For](../src/Elifoot.Domain/ChampionshipStrengthLimits.cs), não uma tabela alternativa de referências. Fora dos três primeiros da D1: mínimo = `max(mínimoDaDivisão, referência-1)`; máximo = `min(máximoDaDivisão, referência+1)`.

| Posição | D1 | D2 | D3 | D4 |
|---:|---:|---:|---:|---:|
| 1 | 47–50 | 37–38 | 24–25 | 11–12 |
| 2 | 47–50 | 36–38 | 23–25 | 10–12 |
| 3 | 47–50 | 35–37 | 22–24 | 9–11 |
| 4 | 45–47 | 33–35 | 20–22 | 7–9 |
| 5 | 43–45 | 31–33 | 18–20 | 5–7 |
| 6 | 41–43 | 29–31 | 16–18 | 3–5 |
| 7 | 39–41 | 27–29 | 14–16 | 1–3 |
| 8 | 39–40 | 26–28 | 13–15 | 1–2 |

A faixa inicial não é um teto posterior para reduzir jogadores existentes. O ganho de reserva usa a referência da posição atual; quem já estiver acima dela não sofre redução pelo teto. A geração 017B independe de posição G/D/M/A, titularidade, comportamento e estrela. A integração V1.1 aplica `relative-v1` antes da 017B e conserva essa estimativa nos distritais; o formato 1.0 mantém forças literais. Detalhes de evolução e lesões na [referência oficial](catalogos/ELIFOOT98_REGRAS_FORCAS_E_INTEGRACAO_V11.md).

O criador aplica a regra após selecionar os elencos e grava o resultado na prévia e no snapshot. A criação rápida usa a mesma tabela na ordem da fábrica clássica. A confirmação de montagem privada sorteia com os IDs da montagem antes de criar os IDs independentes da sala. Força é um atributo simulado, enquanto identidade, posição, clube, categoria e proveniência vêm da seleção original. Contratos novos usam o cálculo de fundação existente. A confirmação de montagens preserva os salários e prazos já definidos.

Prévias antigas ainda sem confirmação recebem a regra ao criar o campeonato. A recarga de um save usa a força gravada. A referência 017B é metadado do início da temporada e fica separada da classificação ao vivo e dos tetos de evolução da 012C.
