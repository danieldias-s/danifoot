# Compatibilidade do editor antes da integração offline

Inspeção de 11/11/2026, HEAD `4c0b64f`, main com alterações locais preservadas.

O criador existente já oferece países, anos, quatro divisões de oito e 96 distritais, prévia e confirmação. O editor privado anterior permite nomes, ShortName (chamado Sigla), cores, estádio/capacidade e edição/importação de jogadores.

Faltam nesse editor campos independentes de nome curto/sigla, escudo local, nacionalidade individual e as quatro preferências contextuais do treinador. Os pacotes antigos também não representam integralmente esses metadados. O criador anterior recalibra força por slot (017B) e materializa Behavior/IsStar por moldes; esses caminhos não podem substituir os valores explícitos do novo catálogo.

A integração offline terá uma entrada no criador existente, preservará seu formato de divisões e oferecerá seleção/distribuição próprias para evitar a recalibração anterior, com leitura dos novos campos no loader e cópia própria para novos mundos. A edição dos novos campos ficará nos arquivos JSON versionados; o editor privado antigo não será oferecido para esse novo formato enquanto não preservar todos os metadados. Não haverá expansão das telas antigas de jogador ou treinador, nem alteração de saves confirmados.

Servidor: mínimo 11, máximo 24, pelo menos um goleiro e dez jogadores de linha. Novo catálogo: até 23; esse subconjunto é compatível e não altera o máximo 24 do motor. Formações nativas: 4-4-2, 4-3-3, 3-4-3, 5-3-2, 5-4-1, 4-5-1, 3-5-2, 4-2-4, 3-6-1, 5-0-5. IDs como F2/F5 não são os identificadores atuais. O motor também valida códigos numéricos D-M-A cuja soma é dez; a preferência deve passar por essa mesma validação.

IA anterior: preferências ponderadas globais e alternativas de estratégia. Novos treinadores terão duas preferências de casa e duas de fora em metadados próprios; esses valores serão uma preferência, sem limitar o conjunto de formações válidas. Campos futuros de aprendizado serão opcionais no treinador do mundo e não terão cálculo ou efeito nesta etapa.
