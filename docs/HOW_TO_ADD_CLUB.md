# Cadastrar um clube

Copie o [modelo](../catalogo/modelos/clube.json) para a pasta `times` do país e do ano. Substitua todos os valores de exemplo pelos dados documentados e cadastre os jogadores daquela versão. O nome do arquivo serve para organizar os dados. O campo `ClubId` identifica a equipe.

Para incluir o mesmo clube em outro ano, mantenha o ID e crie um arquivo naquela pasta com o elenco correspondente. A validação rejeita dois arquivos com a mesma combinação de país, ano, ID e categoria.

Use `Players: []` para um clube cujo elenco ainda precisa ser cadastrado. Esse clube aparece no catálogo e só fica elegível para o modo de elencos reais quando cumprir os requisitos de elenco. [Regras dos campos](CLUB_JSON_SCHEMA.md) · [Validar e reindexar](CATALOG_IMPORT_VALIDATION.md)
