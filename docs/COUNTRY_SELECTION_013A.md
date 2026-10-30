# Seleção de países — 013A

A seleção múltipla é pesquisável. Brasil (BR), Argentina (AR), Inglaterra (GB), Espanha (ES), Alemanha (DE), Itália (IT), França (FR) e Portugal (PT) aparecem inicialmente. Outros países entram pelo catálogo/importação e ficam disponíveis no mesmo fluxo. O código GB identifica o contexto inglês desta aplicação; o adapter deve mapear o identificador do fornecedor explicitamente.

`GET /api/world-creator/countries?season=2026` conta clubes masculinos e femininos separadamente nos snapshots ativos. Zero significa falta de cobertura válida para categoria/temporada, não proibição de selecionar o país. O pacote de demonstração contém 20 clubes por país/categoria: 160 por categoria, 320 no total. Esses números são de fixtures, não cobertura real.

Não há obrigação de selecionar oito países. A API exige 1–64 códigos distintos. Ao trocar países/categoria, a interface limpa escolhas incompatíveis; ao recarregar a seleção remove clubes que deixaram de estar elegíveis. A prévia informa exatamente as vagas pendentes para atingir 128. Países sem catálogo não recebem clubes masculinos/femininos de outra seleção como substituição.

A contagem informa clubes do catálogo; a prévia valida também a integridade dos elencos. Um clube disponível com elenco incompleto não pode ser confirmado silenciosamente.
