# Montagens privadas — 014B

SALVAR MINHA MONTAGEM grava CustomWorldAssembly no banco de catálogo, com OwnerId, nome, categoria/países/seed, fonte original, pacote editado, versão, consentimento e datas. USAR MONTAGEM SALVA lista somente o usuário e recupera o rascunho completo. Fonte original compara consentimento e procedência; identidade alterada deixa de ser apresentada como atual verificada.

Propriedade é verificada em listar/obter/atualizar/confirmar. Outro proprietário recebe 404 e lista vazia. Usa o contrato LocalDev herdado; identidade de produção permanece bloqueada. Atualização exige ExpectedVersion e token EF de concorrência, retornando 409 quando outra janela venceu.

Confirmar valida, copia o pacote e troca todos os IDs de clubes, atletas e técnicos. Novo mundo recebe snapshot, calendário, livro inicial e economia aprovados, sem referências mutáveis ao rascunho. Editar depois não altera saves criados.

Chave de idempotência vinculada ao proprietário/montagem gera recibo persistente. Mesma chave devolve o mesmo mundo e estado atual; outra cria mundo com IDs independentes. O nascimento é persistido antes do save para recuperação. Chamadas simultâneas são serializadas por recibo e chave primária resolve disputa persistente. A rota antiga não interpreta recibo de montagem como prévia do catálogo.

18 testes de integração: 14 variantes inválidas, propriedade, versão e oito confirmações concorrentes. HTTP real masculino/feminino em PostgreSQL: seis repetições simultâneas, 2304 atletas por save, nenhum ID compartilhado, salário R$ 12.345 e contrato 45 preservados, quatro mutações diretas bloqueadas, CSV e snapshots inalterados. Banco/API reiniciados, montagem e recibo preservados. Evidências: `custom-assemblies-http.json`, `custom-assemblies-restart.json`, `restart-exits.json`.
