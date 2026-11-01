# Proveniência dos atletas — 016A

A compatibilidade numérica foi preservada: 0 = vínculo atual verificado; 1 = referência histórica; 2 = fictício gerado; 3 = atual reportado pelo fornecedor. O conversor aceita os nomes antigos e novos; REAL_HISTORICAL é alias seguro de REAL_HISTORICAL_REFERENCE. Nenhuma migração modifica saves antigos.

| Classificação | Critério | Uso no mundo |
| --- | --- | --- |
| REAL_CURRENT_VERIFIED | Evidência suficiente de vínculo datado atual | Aceito no modo estrito |
| REAL_CURRENT_PROVIDER_REPORTED | `/players/squads` do ID revisado e temporada disponível; sem verificação independente | Apenas com opção explícita, fora do modo estrito |
| REAL_HISTORICAL / REFERENCE | Antigo, aberto ou sem prova atual suficiente | Referência documental; não completa elenco atual |
| FICTIONAL_GENERATED | Geração determinística autorizada | Rotulado e congelado no snapshot |

Wikidata exige datas e referência HTTPS recente para promover um vínculo ativo à classificação verificada. A simples ausência de data final não basta. Categoria desconhecida/incompatível, posição ambígua e identidade ausente ficam fora dos atletas utilizáveis.

O ID do jogador é específico da fonte; nome não é chave de deduplicação. Transferências com o mesmo ID reportado usam a associação mais recente entre clubes selecionados; datas empatadas conflitantes são excluídas até revisão. Os snapshots brutos permanecem intactos. Não se afirma reconciliar automaticamente IDs de fontes diferentes sem um vínculo comprovado.

Posições externas são mapeadas para G/D/M/A pela política existente. A descrição original e os motivos de exclusão ficam no relatório. Força 1–50 usa a calibração determinística já existente; é **estimativa do jogo**, não avaliação oficial da API. Contratos, salários e preços são parâmetros simulados da economia 012D, nunca salários reais importados. Progressão 012C e MatchEngine não foram alterados.

No piloto: 113 IDs históricos normalizados, zero atual reportado, zero atual verificado. Nos mundos de QA: jogadores gerados com autorização explícita, origem e seed congeladas. Os mundos não acompanham transferências reais após a criação.
