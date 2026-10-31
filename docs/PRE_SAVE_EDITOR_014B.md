# Editor antes do save — 014B

NOVO CAMPEONATO / SALA abre o criador. Caminhos: GERAR MUNDO AUTOMÁTICO, ESCOLHER CLUBES E DIVISÕES e CRIAR MUNDO PERSONALIZADO, preservando categoria, países, modos de dados e consentimentos da 013C. Prévia válida pode virar rascunho por REVISAR / EDITAR MONTAGEM. O modo totalmente personalizado começa com 128 clubes fictícios após consentimento explícito.

`custom_assembly_editor.dart` edita nome, categoria, seed, clubes, país, divisão, sigla, cores, estádio/capacidade/condição/reputação; jogadores, posição G/D/M/A, força 1..50, estrela, comportamento, clube, salário e contrato iniciais, fotos/licença/crédito; técnicos IA e preferências existentes. É possível adicionar/remover atletas. Mudanças de identidade exigem consentimento. JSON/CSV reutiliza o serviço de pacotes; valor de mercado usa a fórmula aprovada, salário inicial informado é preservado e protegido.

Confirmação exige 8 clubes por divisão e 96 distritais; IDs únicos e não vazios; 11..24 atletas, 1 goleiro e 10 de linha; força 1..50, posição/comportamento/categoria/país válidos; salário e contrato positivos; um técnico IA inicial por clube e preferências válidas. Erros de elenco são armazenados no rascunho e apresentados, sem criar mundo inválido. Falhas de rede/conflito não descartam alterações locais. Sair com mudanças pede guardar ou descartar.

Rotas em `/api/world-creator/assemblies`. Quatro mutações antigas do editor recusam saves com origem de catálogo/montagem, participantes ou progresso. Serviços de pacote e exportações continuam disponíveis; compatibilidade pré-jogo de salas legadas sem participante/progresso foi mantida. Montagem não recebe worldId a modificar.

Arquivos: `CustomWorldAssemblyDtos.cs`, `CustomAssemblyPolicy.cs`, `WorldAssemblyService.cs`, `CatalogDbContext.cs`, `WorldCreatorEndpoints.cs`, guardas em `Program.cs`, `world_creator.dart`, `custom_assembly_editor.dart`. Tabela adicionada ao banco separado do catálogo; backups anteriores em `audit-014b/pre-save-schema-backup.json`.
