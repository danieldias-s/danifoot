# Elencos reais e simulados — 013C

O catálogo preservado possui 158 clubes masculinos e 232 femininos verificados, 3.079 identidades documentais de atletas e 3.101 vínculos documentados. Há somente um vínculo atual utilizável e nenhum elenco real atual completo. A carga local é uma cópia com SHA-256 idêntico do bundle já auditado na integração 014A; não houve nova coleta ou ampliação artificial da cobertura.

| Origem | Significado e uso |
|---|---|
| REAL_VERIFIED_CURRENT | Identidade real, categoria e temporada compatíveis, clube/alias correspondente, posição válida e evidência com início e fim que cobrem a referência. Pode integrar o elenco inicial real. |
| REAL_HISTORICAL_REFERENCE | Registro documental histórico ou sem comprovação temporal suficiente. Permanece como referência identificada, fora do elenco atual. |
| FICTIONAL_GENERATED | Personagem inventado pelo gerador determinístico, com identidade própria. Nunca recebe a identidade de um atleta real. |

A evidência exige StatementId, texto de origem e referência não futura; um rótulo DatedActive isolado não substitui as datas do vínculo. Categoria incompatível, clube diferente, vínculo aberto, evidência ausente e contrato vencido são recusados na escalação inicial. Conflitos e versões do catálogo continuam preservados.

Elencos incompletos recebem, quando autorizado, uma composição alvo de 18 atletas: dois goleiros e distribuição de defesa, meio e ataque. Elencos reais completos não são completados sem necessidade. O limite de 24 e o mínimo de 11, com um goleiro e dez de linha, são validados antes da confirmação. IDs canônicos duplicados bloqueiam a criação. Ao atingir 24 atletas, uma substituição nunca remove a posição que está sendo preenchida, para garantir progresso e impedir loop em elencos desequilibrados. O teste de fronteira usa 24 meio-campistas e verifica a conclusão válida.

A geração usa SHA-256 sobre seed, clube, posição e índice, com variação individual de até sete pontos ao redor do nível estimado do clube e força limitada a 1..50. Um Elo autorizado fornece um nível estimado; uma classificação ordinal confiável usa uma escala logarítmica; sem informação esportiva utilizável, a referência é 26. O método é generated-club-level-v1. Confiança e origem são explícitas. Não é uma avaliação individual oficial nem utiliza a divisão escolhida para reduzir a força de um clube.

O comportamento, estrela, contratos, salários e valores seguem os mecanismos existentes de fundação do mundo. Nenhuma fórmula 012C/012D ou MatchEngine foi alterada. O salário contratado continua independente da referência de mercado e da evolução de força.

Cada atleta inicial possui WorldPlayerProvenance com origem, identidade canônica, clube inicial, gênero, país, temporada, referências, data, evidência temporal, seed e método de força quando aplicáveis. A origem descreve a fundação do mundo; transferências posteriores são acontecimentos do jogo. A ficha apresenta o vínculo comprovado no snapshot, com sua data.

Exportação/importação JSON e CSV preserva esses metadados e a proteção salarial. A regressão adicional encontrou e corrigiu um defeito anterior do leitor CSV: aspas escapadas eram descartadas. O leitor agora preserva as aspas do JSON de proveniência. Saves e CSVs antigos continuam aceitos sem exigir os campos novos.

Não foram adicionados escudos, fotografias, marcas gráficas ou contratação de API. Direitos e atribuições dos snapshots anteriores continuam anexados. A missão não inventa novas autorizações comerciais; limitações anteriores e uso público continuam descritos na documentação 013B.
