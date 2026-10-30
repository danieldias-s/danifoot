# Catálogo e cache — 013A

A persistência no PostgreSQL é o cache compartilhado e durável; não depende de cache exclusivamente em memória. `CatalogScope.ActiveSnapshotId` aponta a última versão válida de cada escopo. `LastSuccess` registra a verificação válida e cada snapshot mantém separadamente a data dos dados externos e a data da validação.

Ao preparar a montagem, a seleção consulta somente os países/categoria/temporada escolhidos e tenta os fornecedores configurados cujos prazos venceram. A UI oferece uso imediato dos dados salvos, exibindo datas e avisos. Não existe atualização externa implícita de mundos em andamento.

O lease persistido usa atualização condicional no banco e vale dois minutos. Processos diferentes compartilham esse bloqueio; o segundo consumidor pode usar o snapshot salvo enquanto a atualização está em andamento. Sucesso define o próximo prazo conforme o tipo. Falha gera espera de 15 minutos antes de nova tentativa, evitando chamadas repetidas ao fornecedor.

Cada atualização possui no máximo um retry de rede, timeout configurável de 1–5 segundos por tentativa e nenhuma repetição imediata em HTTP 429. O adapter limita o payload a 20 MB e mantém no máximo quatro fetches simultâneos por instância. Os países são independentes; dentro de cada país os tipos são processados na ordem países, ligas, clubes, elencos e rankings, respeitando as referências da primeira carga.

Os limites de requisições são uma proteção local, não um cálculo automático da quota contratada. Cadências/fontes devem ser configuradas conforme o contrato. Sem fonte configurada não há chamadas externas.
