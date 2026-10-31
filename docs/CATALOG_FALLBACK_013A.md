# Última versão válida e fallback — 013A

A unidade de falha é país/categoria/liga/temporada/tipo/fornecedor. O pacote do Brasil pode atualizar enquanto o da Argentina falha; a seleção combina o novo Brasil com a última Argentina válida. Os testes executam esse cenário para masculino e feminino.

Falhas de rede, timeout, 429, paginação incompleta, resposta vazia, duplicações, categoria/temporada incorretas, perda de itens, goleiro ausente, elenco curto e falha SQL não apagam a versão anterior. O histórico de jobs registra estado, número de tentativas, início, fim e código do erro. Não guarda headers/tokens nem respostas brutas de erro.

A UI recebe por referência: ID do snapshot, escopo, versão, data dos dados, data da validação, IsFixture, status e aviso. Fallback significa falha na última tentativa; Updating indica lease em andamento; Saved indica prazo vencido com uso salvo; Cached indica período de reutilização. As datas externas continuam visíveis em todos os casos. Cached não significa que o dado externo foi produzido hoje.

Se nunca houve snapshot válido, o país/tipo permanece sem cobertura. Elenco masculino não completa seleção feminina, temporada diferente não serve como fallback, ranking não se disfarça de elenco e um fornecedor não substitui silenciosamente o ID externo de outro. Elenco incompleto deixa a criação pendente. Um mundo já confirmado é independente de qualquer dessas falhas.
