# Elencos reais — 016A

**INCOMPLETE** para todos os seis clubes. Não há elenco atual real completo neste piloto.

| Clube | Linhas recebidas | Pessoas documentadas | Histórico/não confirmado | Atual reportado | Atual verificado | Estado |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Flamengo | 500 | 0 | 0 | 0 | 0 | INCOMPLETE |
| Corinthians | 500 | 0 | 0 | 0 | 0 | INCOMPLETE |
| Real Madrid | 500 | 0 | 0 | 0 | 0 | INCOMPLETE |
| Barcelona | 500 | 0 | 0 | 0 | 0 | INCOMPLETE |
| FC Porto | 500 | 0 | 0 | 0 | 0 | INCOMPLETE |
| FC Barcelona Feminino | 155 | 126 | 126 | 0 | 0 | INCOMPLETE |

Cinco consultas SPARQL atingiram 500 linhas; foram rejeitadas como truncadas. Não são 500 jogadores válidos importados. Barcelona Feminino: 155 linhas → 126 IDs distintos → 113 atletas com dados suficientes para a tabela normalizada (G16, D35, M31, A31). Os 13 restantes ficam no relatório/cache com NAME_MISSING ou POSITION_MISSING_OR_AMBIGUOUS. Não receberam posição arbitrária. Os 113 são históricos/não confirmados para o dia de referência, inclusive pessoas ainda em atividade cuja associação atual não foi comprovada por esta fonte.

Todo o histórico é preservado por snapshot. As 226 linhas físicas de jogadores desta missão representam duas versões históricas dos mesmos 113 IDs, não 226 atletas distintos. A versão ativa contém 113, sem IDs repetidos. A auditoria encontrou ruído de versionamento por precisão de timestamps e o corrigiu sem apagar versões anteriores.

Mais de 24 jogadores permanecem no catálogo. Somente vínculos atuais elegíveis entram na seleção determinística do novo mundo, limitada a 24. Elenco insuficiente bloqueia o modo estrito; geração fictícia depende de consentimento explícito. Nenhum histórico do piloto foi ativado como atual em mundo de QA.

API-Football real continua pendente. O teste de protocolo com 26 atletas é fixture rotulada, não cobertura real do Flamengo. [Proveniência](PLAYER_PROVENANCE_016A.md).
