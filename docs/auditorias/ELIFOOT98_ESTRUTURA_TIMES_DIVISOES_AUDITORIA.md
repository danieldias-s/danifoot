# ELIFOOT 98 MULTIPLAYER — auditoria da estrutura de times e divisões

**STATUS: AUDITORIA CONCLUÍDA.** Auditoria somente leitura da aplicação e dos dados existentes. Instante da extração: **2026-11-11T19:16:21-03:00**. Projeto `D:\elifoot_98`, branch `main`, HEAD `4c0b64fb465a24d31a6798236606f361cd1654fa`.

## 1. Resumo executivo

O mundo principal analisado é **testeno dois jogadores**, ID `b17ae7da-5be3-483a-9bb7-fb0e8a1a38c8`, temporada **1**, rodada global **4**, estado **PREPARING**, categoria **masculina**. É o mundo de Clube QA 25 (1985) e Clube QA 28 (1985), usado no teste manual recente. Os outros nove mundos do mesmo banco foram inventariados para conferir formatos e políticas; suas listas não são misturadas ao CSV principal.

- **2 países no catálogo local ativo e no mundo atual:** Portugal (PT) e Espanha (ES).
- **128 clubes por mundo:** quatro divisões com oito clubes cada e 96 distritais. Os 128 são distribuídos entre os países selecionados.
- **Portugal: 66 clubes**, incluindo todos os 32 das divisões e 34 distritais. **Espanha: 62**, todos distritais.
- **2 clubes humanos, 126 IA e 0 sem técnico**. A sala tem limite de dois humanos e ambos estão ocupados.
- O campeonato é **uma pirâmide compartilhada**. País é atributo do clube; não gera uma liga independente.
- Nomes e elencos atuais são **dados sintéticos de QA**. O catálogo canônico de dados históricos reais contém **zero arquivos de clube**.
- A Taça atual reúne os **128 clubes**, em sete fases de jogo único, com prorrogação e pênaltis. A temporada tem **21 rodadas globais**: 14 de Liga e sete de Taça.
- O mundo atual usa `MULTIPLAYER_CALIBRATED_012D` para salários e valores, embora use evolução de força adaptada clássica e contratos de temporada. O módulo `CLASSICAL_ELIFOOT98_V3_02C` existe e está ativado em cinco outros mundos de QA, mas não neste mundo.
- Há **editor de montagem antes do save**, importação/exportação de pacote JSON e CSV de jogadores. A edição direta deste save está bloqueada pelo servidor.
- A implementação recente de renovação, todas as alterações locais anteriores, serviços, banco e saves foram preservados. Não houve implementação, teste de jogo, criação de mundo, migration, commit ou push nesta auditoria.

## 2. Estado do Git e configuração em execução

HEAD da main local: `4c0b64f` — `docs(elifoot98): record V3 final gate evidence and limitations`. O working tree contém **14 arquivos rastreados modificados** e **10 arquivos novos** no recorte de produção, testes e `docs/implementacoes`; há ainda artefatos antigos de auditoria e worktrees não rastreados. Isso não é uma main limpa nem representa um commit das últimas telas/renovação.

Arquivos rastreados modificados antes desta auditoria:

- `client/elifoot_flutter/lib/api_client.dart`
- `client/elifoot_flutter/lib/classic98_shell.dart`
- `client/elifoot_flutter/lib/coach_finance_screens.dart`
- `client/elifoot_flutter/lib/main.dart`
- `client/elifoot_flutter/lib/stadium_visual.dart`
- `client/elifoot_flutter/test/classic_squad_bank_stadium_test.dart`
- `client/elifoot_flutter/test/rankings_calendar_finance_update_test.dart`
- `client/elifoot_flutter/test/salary_request_modal_test.dart`
- `docs/audit-007/controlled/adaptability-controlled.json`
- `docs/audit-007/controlled/behavior-controlled.json`
- `src/Elifoot.Api/Program.cs`
- `src/Elifoot.Application/TransferContractPreviewQuery.cs`
- `src/Elifoot.Contracts/RenewalContractPreviewDto.cs`
- `src/Elifoot.Domain/VoluntaryRenewalEngine.cs`

Arquivos novos preexistentes no recorte auditado:

- `client/elifoot_flutter/lib/contract_negotiation_dialog.dart`
- `client/elifoot_flutter/test/contract_negotiation_test.dart`
- `docs/implementacoes/ELIFOOT98_V3_ETAPA02_PROPOSTA.md`
- `docs/implementacoes/ELIFOOT98_V3_RENOVACAO_PLANTEL_RESULTADO.md`
- `docs/implementacoes/ELIFOOT98_V3_UI_CONSOLIDADA_TESTE.md`
- `src/Elifoot.Application/RenewalNegotiationApplicationService.cs`
- `src/Elifoot.Contracts/RenewalNegotiationCommandDto.cs`
- `src/Elifoot.Domain/RenewalNegotiationEngine.cs`
- `tests/Elifoot.Domain.Tests/RenewalNegotiationTests.cs`
- `tests/Elifoot.Integration.Tests/RenewalNegotiationIntegrationTests.cs`

Foram registrados hashes dos arquivos acima em `artifacts/structure-audit-20261111/baseline.json`. Os arquivos de renovação permanecem sem commit e não foram editados aqui.

Configuração local observada no registro de execução e no iniciador usado pela API:

| Item | Valor observado |
| --- | --- |
| Ambiente | Development / QA local |
| Web | `http://127.0.0.2:5242/` |
| API | `http://127.0.0.2:5288/` |
| Build da interface | `V3-consolidada-20261111-renovacao` |
| Autenticação | AccountTest |
| Banco de mundos | PostgreSQL 18, `127.0.0.1:55442`, `elifoot_016b_v3gate_browser` |
| Catálogo ativo | SQLite separado, `artifacts/v3-manual-play/catalog-qa.sqlite` |
| Raiz dos arquivos QA | `artifacts/v3-manual-play/catalogo-qa` |
| Catálogo canônico para cadastro | `catalogo/paises` — zero JSON de clubes |
| Provedores externos | Desabilitados no modo local (`UseLegacyProviders=false`) |
| Reindexação no início | Desabilitada |
| Criação automática de mundo | Desabilitada |
| Modo de jogo | CLASSIC_SYNC |
| Força no mundo principal | ClassicAdapted / versão 1 |
| Contratos no mundo principal | `SeasonContractsEnabled=true` |
| Economia de jogadores no mundo principal | MULTIPLAYER_CALIBRATED_012D |

O texto “clássico” da aparência e as regras de contratos não selecionam a política econômica. `ClassicPlayerEconomyRules.Policy` e seus flags são a evidência da política realmente ativa. [S10]

## 3. Mundos existentes e escopo da lista de clubes

Foram lidos **10 mundos**, cada um com 128 clubes: **1.280 instâncias de clube** no banco. Oito mundos da fábrica usam Brasil; dois mundos de catálogo usam Portugal/Espanha. Há **3 países distintos nos saves analisados**, mas apenas **2 carregados no catálogo local selecionável atual**.

| Mundo | WorldId | Temporada/rodada | Clubes | Países | Política de jogadores |
| --- | --- | --- | --- | --- | --- |
| GATE V3 CARREIRA | 1c32c6ee-0cae-41f0-a1cb-a6ea96e5ee15 | 1/1 | 128 | Brasil | CLASSICAL_ELIFOOT98_V3_02C |
| GATE V3 CLASSICA 2 | 1bdca60e-ad0c-489f-8d2c-ed2d4fd07d59 | 2/4 | 128 | Brasil | CLASSICAL_ELIFOOT98_V3_02C |
| GATE V3 DEMISSAO | 28cf392d-bde9-47f0-a15f-9156ab363d91 | 1/2 | 128 | Brasil | CLASSICAL_ELIFOOT98_V3_02C |
| GATE V3 ECONOMIA E VISUAL | 4daf8b7e-38a5-4fe3-9ebd-4ef903ee5372 | 1/1 | 128 | Brasil | CLASSICAL_ELIFOOT98_V3_02C |
| GATE V3 ECONOMIA E VISUAL GATEEC2 | 7c64a79b-360d-406b-b9c0-6a2703cfd327 | 1/1 | 128 | Brasil | CLASSICAL_ELIFOOT98_V3_02C |
| GATE V3 ESTABILIDADE 4 | a2645ad2-d260-430d-94c5-0fe6ed9b3e18 | 1/1 | 128 | Brasil | MULTIPLAYER_CALIBRATED_012D |
| GATE V3 ESTABILIDADE 8 | 9e050073-72c4-41af-9e8c-b56f4e68f12b | 1/1 | 128 | Brasil | MULTIPLAYER_CALIBRATED_012D |
| GATE V3 LEGADA | b8d25e66-3cff-4576-b741-bee6d620c42f | 1/1 | 128 | Brasil | MULTIPLAYER_CALIBRATED_012D |
| Liga Mundial 98 | 934dbe0b-0384-43d1-b865-950aa90f15d5 | 1/1 | 128 | Espanha, Portugal | MULTIPLAYER_CALIBRATED_012D |
| testeno dois jogadores | b17ae7da-5be3-483a-9bb7-fb0e8a1a38c8 | 1/4 | 128 | Espanha, Portugal | MULTIPLAYER_CALIBRATED_012D |


As listas completas e o CSV desta entrega referem-se exclusivamente a `testeno dois jogadores`. Essa delimitação preserva o significado de cada ClubId e evita somar mundos diferentes como se fossem um campeonato.

## 4. Países realmente disponíveis

O índice ativo `LocalHistoricalCatalogRow` contém **128 variantes masculinas e 2.304 jogadores**. Há 62 variantes ES e 66 PT. Anos: 65 variantes de 1985 e 63 de 2025. A combinação é **PT/1985=65, PT/2025=1, ES/2025=62**. Não existe cobertura feminina nesse índice.

As tabelas genéricas `CatalogClub`, `CatalogPlayer` e `CatalogSnapshot` estão vazias neste SQLite; o catálogo local funciona pelo índice histórico próprio. Consultar somente essas tabelas genéricas levaria à conclusão errada de que o catálogo ativo está vazio. `CatalogCountry` possui ES/Espanha e PT/Portugal. `CountryDirectory016B` e `CountryCatalog016B` estão vazias.

Brasil, Argentina, Inglaterra/GB, Espanha, Alemanha, Itália, França e Portugal constam na documentação e nas fixtures da integração antiga. Aquelas oito opções não são a carga atual disponível para criar este mundo. O diretório global é extensível, e o importador local aceita códigos de duas letras; sua existência no código não significa dados reais já cadastrados. [D10] [D02] [S20]

## 5. Seleção de países e ligas

O criador principal permite escolher **1 a 8 países**, categoria e um ou mais anos do índice local. Os países e anos limitam o conjunto de variantes elegíveis. A montagem automática usa ranking global da seleção e a personalizada permite mover/trocar variantes entre as vagas. Países não recebem cotas de vagas por padrão. [S03] [D01] [D04]

O mundo atual mostra o efeito dessa regra: as primeiras 32 vagas ficaram com clubes portugueses do catálogo; espanhóis ficaram nos distritais. Não há quatro divisões separadas para cada país, nem uma Taça nacional independente por país. Confrontos entre países podem acontecer na mesma Liga compartilhada ou na mesma Taça. Não foi localizada competição continental/internacional separada implementada.

O nome `Taça Nacional` usado em textos e prêmios não cria uma restrição por nacionalidade no chaveamento. O país do clube é persistido como string `Club.Country`; a origem do catálogo também conserva código e nome do país. [S01] [S05]

## 6. Divisões e distribuição exata dos 128 clubes

Existem **quatro divisões de Liga** e a categoria **Distrital**. Não há tabela de Liga distrital com turno e returno neste formato.

| País | 1ª | 2ª | 3ª | 4ª | Distrital | Total | Humanos | IA |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Portugal | 8 | 8 | 8 | 8 | 34 | 66 | 2 | 64 |
| Espanha | 0 | 0 | 0 | 0 | 62 | 62 | 0 | 62 |


No mundo principal, os clubes humanos são:

| País | Divisão | Clube | ClubId | Treinador |
| --- | --- | --- | --- | --- |
| Portugal | 4ª | Clube QA 25 (1985) | f66578aa-6f7b-2144-8bfc-1f90bf0b9cb6 | Gate Treinador B |
| Portugal | 4ª | Clube QA 28 (1985) | c511b6f0-0d64-8040-90fc-1e95bd879ecf | crokle |

Os outros 126 clubes têm `AiManagerId`. “LIVRE” no inventário significa ausência de humano e de técnico IA; não há nenhum caso. Um clube IA pode ser elegível para atribuição humana conforme o fluxo de entrada/carreira, mas isso depende das vagas da sala, da divisão e das regras do lobby. Humanos nunca assumem distritais. A escolha manual pode indicar clube não distrital; o sorteio inicial usa a 4ª divisão.

## 7. Como os clubes são criados e classificados inicialmente

**Fábrica clássica:** `WorldSeedFactory` possui 32 modelos de nomes fictícios inspirados no futebol brasileiro, com oito por divisão. Exemplos efetivos no código: Atlético Paulistano, Grêmio Guanabara, Palestra Verde e Sport Club Atlântico. Distritais combinam prefixos e cidades. A fábrica recebe seed e quantidade; os IDs derivam da seed/tipo/índice. [S02]

**Criador atual de catálogo:** seleciona 128 variantes; ocupa quatro divisões de oito e 96 distritais. Os 32 melhores da ordenação vão às divisões na montagem automática. A ordem histórica considera ranking, pontuação e chave estável. Na personalizada, as escolhas e a ordem por divisão prevalecem. Em múltiplos anos o ano é acrescentado ao nome e ao nome curto. [S03] [S04]

**Dados presentes:** os arquivos QA contêm nomes `Clube QA ...` e `Atleta sintético ...`. Foram cadastrados como arquivos locais sintéticos para testes. Não são uma relação de clubes reais já pronta para jogar historicamente. Não há montagem privada salva na tabela `CustomWorldAssembly` deste catálogo; há três rascunhos técnicos em `WorldCreationDraft`.

**Estrutura configurável:** países, anos, categoria, seed, seleção e alocação dos clubes, consentimento de conteúdo fictício e número de humanos são configuráveis. O criador principal e a montagem privada exigem exatamente 128 clubes. Há caminhos técnicos de fábrica/benchmark para 64/256/512/1024 e importador genérico de pacote com validação mais permissiva; não são uma opção de quantidade livre homologada do criador principal. Não se deve trocar a quantidade num save editando JSON diretamente. [S05] [S12] [S15]

## 8. Lista completa dos clubes e identificadores

A ordem abaixo é a ordem determinística do snapshot, não a classificação ao vivo por pontos. Os 128 registros também estão no CSV e agrupados por país/divisão na estrutura visual.

| PAÍS | DIVISÃO | CLUBE | IDENTIFICADOR DO CLUBE | STATUS | TREINADOR |
| --- | --- | --- | --- | --- | --- |
| Portugal | 1ª Divisão | Clube QA 0 (1985) | 4d502d81-0068-1940-a598-ff0ad1f4a5d7 | IA | Rafael Carvalho |
| Portugal | 1ª Divisão | Clube QA 0 (2025) | ada5a7bc-01d2-8c47-8743-28bc205197c0 | IA | Eduardo Vieira |
| Portugal | 1ª Divisão | Clube QA 1 (1985) | 56a70f20-0caf-0c40-ad21-9728bf7803a4 | IA | Henrique Andrade |
| Portugal | 1ª Divisão | Clube QA 2 (1985) | ec48a3f3-50b5-dc4e-9c84-c600f03a4514 | IA | Leandro Mendes |
| Portugal | 1ª Divisão | Clube QA 3 (1985) | 5bdd1673-fb55-cb4c-afab-358b8eee0875 | IA | Otávio Santos |
| Portugal | 1ª Divisão | Clube QA 4 (1985) | d4c6adca-5685-e441-8b71-01d53f7e710e | IA | Sérgio Alves |
| Portugal | 1ª Divisão | Clube QA 5 (1985) | 5b4f4b74-bf5c-144c-ab5b-10a7fc48126f | IA | Wagner Carvalho |
| Portugal | 1ª Divisão | Clube QA 6 (1985) | c456cd04-3245-034c-ac6d-096b8bf1e94d | IA | Caio Vieira |
| Portugal | 2ª Divisão | Clube QA 7 (1985) | a0b184da-a4ce-614f-b76e-37b489918a3c | IA | Alex Andrade |
| Portugal | 2ª Divisão | Clube QA 8 (1985) | 88390903-ec83-8748-a8e5-c8fc48eb7925 | IA | Lucas Mendes |
| Portugal | 2ª Divisão | Clube QA 9 (1985) | bf8e23de-3a8e-3647-90e2-6ed74536364a | IA | Rafael Santos |
| Portugal | 2ª Divisão | Clube QA 10 (1985) | 3fa93ded-68c1-5946-ad90-59a450a5df36 | IA | Eduardo Alves |
| Portugal | 2ª Divisão | Clube QA 11 (1985) | 01b233e9-aacf-0943-8959-7588da1d9abd | IA | Henrique Carvalho |
| Portugal | 2ª Divisão | Clube QA 12 (1985) | 74f3a896-cdff-154b-b991-df7b5641e371 | IA | Leandro Vieira |
| Portugal | 2ª Divisão | Clube QA 13 (1985) | 90b4b31e-cab7-5546-863c-9c0e9b58cdd3 | IA | Otávio Andrade |
| Portugal | 2ª Divisão | Clube QA 14 (1985) | a2585565-8698-f240-8876-9d89bf8919c3 | IA | Sérgio Mendes |
| Portugal | 3ª Divisão | Clube QA 15 (1985) | 45a36ed6-5e2c-7140-89d7-4c241b743b4b | IA | Wagner Santos |
| Portugal | 3ª Divisão | Clube QA 16 (1985) | 8fe81b82-272f-f04d-9d8b-ff7ed55ad17d | IA | Caio Alves |
| Portugal | 3ª Divisão | Clube QA 17 (1985) | 8584d9d1-24e1-bb44-9f66-beb411d1674f | IA | Alex Carvalho |
| Portugal | 3ª Divisão | Clube QA 18 (1985) | 4dd18230-9e9f-f241-9d33-2f9518525cab | IA | Lucas Vieira |
| Portugal | 3ª Divisão | Clube QA 19 (1985) | f2042ee2-7249-4a45-aa60-dea1a6a25c82 | IA | Rafael Andrade |
| Portugal | 3ª Divisão | Clube QA 20 (1985) | 673c06f6-4733-2a41-9c1a-9542b60ec1af | IA | Eduardo Mendes |
| Portugal | 3ª Divisão | Clube QA 21 (1985) | 09fbfb1a-d941-464f-b397-d613496af920 | IA | Henrique Santos |
| Portugal | 3ª Divisão | Clube QA 22 (1985) | 7caf8132-1f4d-744d-8145-0b0016cb6e86 | IA | Leandro Alves |
| Portugal | 4ª Divisão | Clube QA 23 (1985) | d7816774-4281-c346-a83e-8850b564f97d | IA | Otávio Carvalho |
| Portugal | 4ª Divisão | Clube QA 24 (1985) | 57da6814-faec-264b-89de-b36467d77ea7 | IA | Sérgio Vieira |
| Portugal | 4ª Divisão | Clube QA 25 (1985) | f66578aa-6f7b-2144-8bfc-1f90bf0b9cb6 | HUMANO | Gate Treinador B |
| Portugal | 4ª Divisão | Clube QA 26 (1985) | 42bfe749-8efc-624a-860a-d5cee72e9402 | IA | Caio Mendes |
| Portugal | 4ª Divisão | Clube QA 27 (1985) | 9cd0288a-fb8f-3c4a-a8ac-80fd215d4d42 | IA | Alex Santos |
| Portugal | 4ª Divisão | Clube QA 28 (1985) | c511b6f0-0d64-8040-90fc-1e95bd879ecf | HUMANO | crokle |
| Portugal | 4ª Divisão | Clube QA 29 (1985) | 53a03d01-58f0-fd40-87dc-abcdb9dee385 | IA | Rafael Carvalho |
| Portugal | 4ª Divisão | Clube QA 30 (1985) | e8c39b4a-310b-e94c-ab09-c5a1675b5d23 | IA | Eduardo Vieira |
| Portugal | Distrital | Clube QA 31 (1985) | 69bd946b-071e-cf4b-9fa0-18a84f3adcf7 | IA | Henrique Andrade |
| Portugal | Distrital | Clube QA 32 (1985) | abfa7c61-9dec-5147-8ada-b9eddc1f609a | IA | Leandro Mendes |
| Portugal | Distrital | Clube QA 33 (1985) | c00269e5-6484-6d44-aab7-246f25d168b1 | IA | Otávio Santos |
| Portugal | Distrital | Clube QA 34 (1985) | 31d65a42-0b0e-ab4e-b13d-26602ccbc31a | IA | Sérgio Alves |
| Portugal | Distrital | Clube QA 35 (1985) | c6c3f7a5-c033-d345-9830-9b5be813338d | IA | Wagner Carvalho |
| Portugal | Distrital | Clube QA 36 (1985) | 0fa508ac-8502-e44e-9bdb-81c73bf31071 | IA | Caio Vieira |
| Portugal | Distrital | Clube QA 37 (1985) | 55f3c758-8f33-3842-96af-b256585ccd0c | IA | Alex Andrade |
| Portugal | Distrital | Clube QA 38 (1985) | 0e5bddb3-6d25-0e40-8472-8ff1f989614e | IA | Lucas Mendes |
| Portugal | Distrital | Clube QA 39 (1985) | b1aefaec-e6a5-a344-a6f1-076c47f2c42f | IA | Rafael Santos |
| Portugal | Distrital | Clube QA 40 (1985) | e183de64-f9d1-0a44-8fb9-cfab5276829c | IA | Eduardo Alves |
| Portugal | Distrital | Clube QA 41 (1985) | a81796bd-3e5f-0d47-b218-c6779b867481 | IA | Henrique Carvalho |
| Portugal | Distrital | Clube QA 42 (1985) | 38e39e88-1551-7b44-b996-b0bb50de6624 | IA | Leandro Vieira |
| Portugal | Distrital | Clube QA 43 (1985) | cba8523e-cce2-4248-9bd6-a4f39b9b5ce3 | IA | Otávio Andrade |
| Portugal | Distrital | Clube QA 44 (1985) | 7a0115fe-e575-8147-8231-8f05218996e3 | IA | Sérgio Mendes |
| Portugal | Distrital | Clube QA 45 (1985) | 8924f60a-3cc6-134f-a0a7-1ecd82dfe3b0 | IA | Wagner Santos |
| Portugal | Distrital | Clube QA 46 (1985) | c0ceb244-66bb-7c44-9fb7-2e86a057363b | IA | Caio Alves |
| Portugal | Distrital | Clube QA 47 (1985) | c6c0725a-40a5-994d-b16b-5bed87655dd9 | IA | Alex Carvalho |
| Portugal | Distrital | Clube QA 48 (1985) | aa764e7b-9d7f-5d47-811f-957dbbc68b28 | IA | Lucas Vieira |
| Portugal | Distrital | Clube QA 49 (1985) | 98a73f25-b521-804d-ba70-c38c15a5f088 | IA | Rafael Andrade |
| Portugal | Distrital | Clube QA 50 (1985) | 470316e0-a0b5-c140-a507-ea802f081b51 | IA | Eduardo Mendes |
| Portugal | Distrital | Clube QA 51 (1985) | b6214373-01bf-4d45-9cd0-c6df15ca043c | IA | Henrique Santos |
| Portugal | Distrital | Clube QA 52 (1985) | 87ee0365-85e9-ac4a-8789-c2edae5f7569 | IA | Leandro Alves |
| Portugal | Distrital | Clube QA 53 (1985) | c0cbbbdb-cd18-2148-9641-c134a1d11a34 | IA | Otávio Carvalho |
| Portugal | Distrital | Clube QA 54 (1985) | 89be8dc3-1db4-5545-94d3-9b10e6332a2c | IA | Sérgio Vieira |
| Portugal | Distrital | Clube QA 55 (1985) | bf70aa19-4a14-e945-9b20-910844ad6569 | IA | Wagner Andrade |
| Portugal | Distrital | Clube QA 56 (1985) | 0784d0a3-0f4c-5a4a-a8f2-fc90e7930a4a | IA | Caio Mendes |
| Portugal | Distrital | Clube QA 57 (1985) | 97e40965-1381-b941-9ab7-c6422bd83808 | IA | Alex Santos |
| Portugal | Distrital | Clube QA 58 (1985) | 31da8c2a-28d3-224e-902b-4529ec871683 | IA | Lucas Alves |
| Portugal | Distrital | Clube QA 59 (1985) | 3b812570-77e0-764f-b1b7-e19dc9bd18d9 | IA | Rafael Carvalho |
| Portugal | Distrital | Clube QA 60 (1985) | e8dd54a0-109a-684a-bc35-3f400d877912 | IA | Eduardo Vieira |
| Portugal | Distrital | Clube QA 61 (1985) | 262845d6-e2d3-db4a-bf68-1a620c76ee94 | IA | Henrique Andrade |
| Portugal | Distrital | Clube QA 62 (1985) | a7987ca8-fa03-8842-8867-49370c7000df | IA | Leandro Mendes |
| Portugal | Distrital | Clube QA 63 (1985) | 8069378a-3c79-4844-a524-a9973080a5c4 | IA | Otávio Santos |
| Portugal | Distrital | Clube QA 64 (1985) | c370164b-3830-a848-8c19-1b1fbbb8846f | IA | Sérgio Alves |
| Espanha | Distrital | Clube QA 65 (2025) | d20a1cdc-b836-1649-8f65-a8979017e29d | IA | Wagner Carvalho |
| Espanha | Distrital | Clube QA 66 (2025) | 3a049f1b-7411-6743-b6be-81de182664ab | IA | Caio Vieira |
| Espanha | Distrital | Clube QA 67 (2025) | a4d5de83-08a9-9843-ad1f-1fcb2cc86694 | IA | Alex Andrade |
| Espanha | Distrital | Clube QA 68 (2025) | 9d599032-135e-3b46-ab74-a5d90a77e4a8 | IA | Lucas Mendes |
| Espanha | Distrital | Clube QA 69 (2025) | 19d3fa46-5695-df4f-a3c8-5ecbf51849ac | IA | Rafael Santos |
| Espanha | Distrital | Clube QA 70 (2025) | 457aa0f8-ae8b-8845-a1bd-90a8fad91621 | IA | Eduardo Alves |
| Espanha | Distrital | Clube QA 71 (2025) | e926f201-c7d4-744f-bcea-2c1e10302d93 | IA | Henrique Carvalho |
| Espanha | Distrital | Clube QA 72 (2025) | 9ac42978-eeed-1145-9602-c934ee8bc231 | IA | Leandro Vieira |
| Espanha | Distrital | Clube QA 73 (2025) | 7faba1b4-d187-7141-b253-8740d07678cf | IA | Otávio Andrade |
| Espanha | Distrital | Clube QA 74 (2025) | dadbb572-186a-3d46-9024-a8d46f47f775 | IA | Sérgio Mendes |
| Espanha | Distrital | Clube QA 75 (2025) | 860ede16-7ae1-df49-82d0-e3b39339582c | IA | Wagner Santos |
| Espanha | Distrital | Clube QA 76 (2025) | 2321a481-d9bb-9f46-9d22-628f69a1b9e5 | IA | Caio Alves |
| Espanha | Distrital | Clube QA 77 (2025) | 9d19b063-278d-224b-aa26-5eaead5f1019 | IA | Alex Carvalho |
| Espanha | Distrital | Clube QA 78 (2025) | 77664085-6051-e84b-acc6-a187690837ac | IA | Lucas Vieira |
| Espanha | Distrital | Clube QA 79 (2025) | d1f5a1a9-54be-8e4f-a06c-a67b6048474d | IA | Rafael Andrade |
| Espanha | Distrital | Clube QA 80 (2025) | bb52b6ee-dfbc-6d45-ba49-0d64bd242be9 | IA | Eduardo Mendes |
| Espanha | Distrital | Clube QA 81 (2025) | c6b63f3c-afee-a442-886d-2b7ee6c57c03 | IA | Henrique Santos |
| Espanha | Distrital | Clube QA 82 (2025) | 84a76c42-8445-d548-81a2-2bbfbb89e748 | IA | Leandro Alves |
| Espanha | Distrital | Clube QA 83 (2025) | a7fbf998-91a5-3f4c-8ee9-813a4b1fab1d | IA | Otávio Carvalho |
| Espanha | Distrital | Clube QA 84 (2025) | 2ec73a97-03c5-0b43-93d1-56fdcf0e97f5 | IA | Sérgio Vieira |
| Espanha | Distrital | Clube QA 85 (2025) | 166ae730-8451-e94b-a344-685bfd94df59 | IA | Wagner Andrade |
| Espanha | Distrital | Clube QA 86 (2025) | 1f5e2fab-5917-ca44-baad-95d7d2196de5 | IA | Caio Mendes |
| Espanha | Distrital | Clube QA 87 (2025) | 2e8e682a-e35e-4544-97da-bb41ce48600c | IA | Alex Santos |
| Espanha | Distrital | Clube QA 88 (2025) | 66b81f44-6262-724e-ac16-5161074b2c0f | IA | Lucas Alves |
| Espanha | Distrital | Clube QA 89 (2025) | 8afef28a-3671-6c4a-a5ec-1c44bff0babc | IA | Rafael Carvalho |
| Espanha | Distrital | Clube QA 90 (2025) | 133ee527-8726-9748-bfcf-5b8e04fc6e1b | IA | Eduardo Vieira |
| Espanha | Distrital | Clube QA 91 (2025) | cdb4afe6-7dfd-6646-b687-8f3c3522427e | IA | Henrique Andrade |
| Espanha | Distrital | Clube QA 92 (2025) | 73f42010-06d4-2445-a40a-476be90a50dc | IA | Leandro Mendes |
| Espanha | Distrital | Clube QA 93 (2025) | 90e8a781-d73a-5641-ace7-38e803dc40a3 | IA | Otávio Santos |
| Espanha | Distrital | Clube QA 94 (2025) | a0c9116d-9368-c54f-a423-b02e5790f6fc | IA | Sérgio Alves |
| Espanha | Distrital | Clube QA 95 (2025) | 89f61893-880b-e944-a537-8625fbc33d7b | IA | Wagner Carvalho |
| Espanha | Distrital | Clube QA 96 (2025) | 1d085914-5862-4c45-b39b-a3d7990dd48e | IA | Caio Vieira |
| Espanha | Distrital | Clube QA 97 (2025) | 78edd2e8-1ed0-3e47-aad9-8a0681abeb3b | IA | Alex Andrade |
| Espanha | Distrital | Clube QA 98 (2025) | c997d79e-eafc-bf45-9cd3-a6f4528dee85 | IA | Lucas Mendes |
| Espanha | Distrital | Clube QA 99 (2025) | 86837388-9a58-7445-bd6e-1ed8e895942b | IA | Rafael Santos |
| Espanha | Distrital | Clube QA 100 (2025) | 03d90c15-3497-ff47-a083-f023b2d84683 | IA | Eduardo Alves |
| Espanha | Distrital | Clube QA 101 (2025) | 2808ba12-6e29-5544-b126-80b3f36e4e99 | IA | Henrique Carvalho |
| Espanha | Distrital | Clube QA 102 (2025) | 19f1c5c1-8f60-1b40-a741-f7270ea4f8f8 | IA | Leandro Vieira |
| Espanha | Distrital | Clube QA 103 (2025) | a831871a-69b3-784d-9170-682dfae1e19e | IA | Otávio Andrade |
| Espanha | Distrital | Clube QA 104 (2025) | d8ac1b0a-e516-c941-b079-50900bb65a70 | IA | Sérgio Mendes |
| Espanha | Distrital | Clube QA 105 (2025) | 743f2b47-edb0-b044-b413-c42ced9af4e3 | IA | Wagner Santos |
| Espanha | Distrital | Clube QA 106 (2025) | e78f2ccf-d19b-1e48-a095-7ad6cb51e6b6 | IA | Caio Alves |
| Espanha | Distrital | Clube QA 107 (2025) | 0de64a48-1b93-ce4e-884a-11c7a43bb217 | IA | Alex Carvalho |
| Espanha | Distrital | Clube QA 108 (2025) | 1edb124f-e29e-2a44-bc58-9214f7713dfb | IA | Lucas Vieira |
| Espanha | Distrital | Clube QA 109 (2025) | b577b726-af8e-424e-a530-d2477f5c5745 | IA | Rafael Andrade |
| Espanha | Distrital | Clube QA 110 (2025) | ca4797f4-5eb2-a64f-984e-653da05f71fc | IA | Eduardo Mendes |
| Espanha | Distrital | Clube QA 111 (2025) | 525ee8eb-0d50-6342-9fff-23b830d90274 | IA | Henrique Santos |
| Espanha | Distrital | Clube QA 112 (2025) | 01073572-f930-3e4a-9130-df801a52617a | IA | Leandro Alves |
| Espanha | Distrital | Clube QA 113 (2025) | 1dbd3687-65c9-ac44-be36-84462c4a1f07 | IA | Otávio Carvalho |
| Espanha | Distrital | Clube QA 114 (2025) | c67866bd-130d-124e-b69a-943d6cda0bd0 | IA | Sérgio Vieira |
| Espanha | Distrital | Clube QA 115 (2025) | 298bc03e-449d-2b4c-93a9-56a3ef71fc80 | IA | Wagner Andrade |
| Espanha | Distrital | Clube QA 116 (2025) | df71547b-140b-034b-bf26-11d91c142dad | IA | Caio Mendes |
| Espanha | Distrital | Clube QA 117 (2025) | 80dff6f2-e47f-104c-84e6-0fdaf2e811f3 | IA | Alex Santos |
| Espanha | Distrital | Clube QA 118 (2025) | 71ff9541-a224-4249-957a-765023f98c64 | IA | Lucas Alves |
| Espanha | Distrital | Clube QA 119 (2025) | 3b01b882-acb4-1543-a73a-059b92acc3ce | IA | Rafael Carvalho |
| Espanha | Distrital | Clube QA 120 (2025) | 38892068-7795-0c4a-ab41-5952a2198fd9 | IA | Eduardo Vieira |
| Espanha | Distrital | Clube QA 121 (2025) | 1f540546-4851-db42-be1b-2ce6ae1ba603 | IA | Henrique Andrade |
| Espanha | Distrital | Clube QA 122 (2025) | 557bed43-f7e2-984d-9e13-c7e2248d502f | IA | Leandro Mendes |
| Espanha | Distrital | Clube QA 123 (2025) | f9639918-d651-8c46-ae7a-3cf97e2ddb3f | IA | Otávio Santos |
| Espanha | Distrital | Clube QA 124 (2025) | d17d3a8a-26fc-8245-9711-9440542afa27 | IA | Sérgio Alves |
| Espanha | Distrital | Clube QA 125 (2025) | 857510ba-ce2e-4944-90d0-9bfcf1e60025 | IA | Wagner Carvalho |
| Espanha | Distrital | Clube QA 126 (2025) | 1bd9f1a4-6bef-074c-87c1-25e77c3fdff6 | IA | Caio Vieira |


## 9. Identidade: campos presentes e ausentes

| Informação pedida | Campo real / situação |
| --- | --- |
| Nome completo | `Club.Name`; preenchido em 128/128 |
| Nome curto | `Club.ShortName`; preenchido em 128/128; neste QA frequentemente repete o nome completo com ano |
| Sigla independente | **N/D**; não existe propriedade separada. O editor apresenta ShortName com o rótulo “Sigla” |
| País | `Club.Country`; origem do catálogo também tem CountryCode/CountryName |
| Divisão | `Club.DivisionTier` |
| Identificador | `Club.Id` + `GameWorldId`; origem da variante conserva sua chave própria |
| Escudo próprio/imagem licenciada | **N/D**; `ClubCrest` desenha um escudo genérico usando duas cores |
| Cores | PrimaryColorHex, SecondaryColorHex e ColorPalette opcional; QA usa duas cores do arquivo, com ColorPalette nulo |
| Estádio | `Club.Stadium.Name` |
| Capacidade | BuiltCapacity, UsableCapacity, ConditionPercent, entre outros campos de operação |
| Saldo inicial | Não há coluna imutável Club.SaldoInicial; a evidência vem da transação `InitialBalance` |
| Saldo atual | `Club.CashBalance`; diferente do saldo de fundação após partidas e mercado |
| Reputação/popularidade | `Club.Reputation`; não há segundo campo independente Popularidade |
| Força inicial do clube | ClubInitialPower (escala 1–20, ranking), InitialSquadReference (referência dos atletas), InitialSquadSize; são conceitos distintos |
| Identidade persistente | GUID e referências preservadas no snapshot; origem de catálogo por país/ano/clube/categoria |

Fontes: entidades [S01], cadastro local [S20], escudo [S21]. A escala 1–20 de ClubInitialPower não é a força 1–50 dos jogadores. [S07] [D05]

## 10. Nomes, repetição e renomeação

No mundo atual não há nomes de exibição exatamente repetidos. O cadastro usa ClubId externo e ano/categoria/país, permitindo versões do mesmo clube em vários anos. Clubes de países diferentes podem ter o mesmo nome: a identidade não é o texto de Name. A chave estável do catálogo diferencia as variantes, e o sufixo de ano diferencia versões históricas na tela. [S04] [D03]

É possível renomear e mudar nome curto no rascunho da montagem. Alterar identidade de conteúdo apresentado como real exige consentimento de personalização e muda sua classificação de procedência. Atualizar/reindexar o catálogo afeta futuras seleções; saves confirmados conservam as cópias dos nomes e elencos. A rota direta de edição recusa este mundo por ter CatalogOrigin, humanos e rodada já iniciada. [S12] [S13] [S14]

Histórico guarda referências por ID e também nomes em determinados registros de títulos/partidas. Renomear um clube diretamente no JSON de um save não é um procedimento suportado para atualizar todas essas referências.

## 11. Elencos: números reais e limites

O snapshot de criação do mundo principal tem **128 elencos de 18 jogadores**, total de **2.304**. Distribuição inicial de cada clube: **2 G, 6 D, 6 M e 4 A**. O elenco vem dos arquivos QA locais; não foi gerado agora.

Na leitura atual há 2.304 entidades de jogador: **2.303 vinculadas a clubes e 1 livre**. Após transferências/leilões/pedidos anteriores, a distribuição de tamanhos é: **105 clubes com 18; 9 com 17; 8 com 19; 3 com 20; 3 com 16**. Portanto, `Jogadores` no CSV é a quantidade atual e pode diferir dos 18 iniciais.

Limites do domínio: **mínimo 11, máximo 24, pelo menos um goleiro e dez jogadores de linha; 11 titulares e até nove no banco**. A escalação ainda respeita formação e disponibilidade por lesão/suspensão. Saídas de atletas e reservas de leilões têm validação própria para não quebrar o elenco mínimo. [S06]

O fluxo genérico 016B tem ciclo de alvos **23,23,22,22,21,21,20,20,19,19,18,18,17,17** e regra de elite de 23. O fluxo histórico local REAL_COMPLETE preserva o elenco válido do arquivo, sujeito ao máximo de 24. Assim, 18 atletas neste mundo é compatível com a regra histórica atual; não significa que todo criador sempre gera 18 ou que a política econômica determina o tamanho. [S07] [S04] [D01]

## 12. Geração de jogadores, nomes, comportamento e asterisco

Na fábrica, nomes vêm de listas de prenomes/sobrenomes e RNG determinístico. A distribuição base de comportamento por faixas é: FairPlay 14%, Cordeirinho 18%, Cavalheiro 30%, Caneleiro 20%, Caceteiro 12%, Sarrafeiro 6%. O asterisco da fábrica é sorteado em posições previstas do molde; não indica nacionalidade. O molde inicial tem 18 atletas, e o fluxo rápido também cria quatro agentes livres sintéticos de leilão, motivo dos 2.308 jogadores nos mundos da fábrica. [S02]

No criador histórico, nomes e posições vêm do JSON da versão do clube. `WorldCreatorEngine.Materialize` herda Behavior/IsStar dos moldes da fábrica e calcula salário/mercado pelo fluxo de fundação. O arquivo canônico de jogador aceita PlayerId, Name, Position e, opcionalmente, Number e Source; não contém força individual oficial, comportamento ou asterisco editáveis nesse schema. O editor de montagem é outro mecanismo e permite editar esses atributos com consentimento. [S03] [S20]

O módulo econômico clássico contém derivação por nome: soma de bytes, B=H%11, D=H%10+1 e IsStar quando D≥8 para M/A. Ele também consegue derivar modificadores econômicos ausentes. A ativação com recálculo conserva Behavior/IsStar já persistidos e preenche B/D quando faltam; não se deve apresentar essa função como uma regeneração automática universal dos atributos de todos os saves. [S10]

## 13. Forças iniciais

O criador atual usa a referência 017B por posição inicial dentro da divisão. Nos três primeiros clubes da 1ª, atletas têm 47–50, com os quatro valores garantidos por sorteio da seed; demais posições usam referência ±1, limitada pela faixa da divisão. Distritais seguem estimativa anterior de catálogo. [S08] [D05]

| Posição inicial | 1ª | 2ª | 3ª | 4ª |
| --- | --- | --- | --- | --- |
| 1 | 50 | 38 | 25 | 12 |
| 2 | 50 | 37 | 24 | 11 |
| 3 | 50 | 36 | 23 | 10 |
| 4 | 46 | 34 | 21 | 8 |
| 5 | 44 | 32 | 19 | 6 |
| 6 | 42 | 30 | 17 | 4 |
| 7 | 40 | 28 | 15 | 2 |
| 8 | 39 | 27 | 14 | 1 |

| Divisão | Clubes | Força no snapshot de criação | Força atual | Saldo inicial por clube (livro) |
| --- | --- | --- | --- | --- |
| 1ª Divisão | 8 | 39–50 | 5–50 | R$ 4.200.000,00 |
| 2ª Divisão | 8 | 26–38 | 24–38 | R$ 2.600.000,00 |
| 3ª Divisão | 8 | 13–25 | 11–28 | R$ 1.650.000,00 |
| 4ª Divisão | 8 | 1–12 | 1–19 | R$ 1.100.000,00 |
| Distrital | 96 | 11–37 | 11–37 | R$ 650.000,00 |


A origem conserva a força da criação; o atributo corrente pode ter evoluído e jogadores podem ter mudado de clube. A geração relativa anterior usa ranking do clube e evidências individuais, com confiança informada; não é uma nota oficial de fornecedor. A nacionalidade e o gênero não criam uma nova fórmula esportiva por país.

## 14. Nacionalidade e estrangeiros

`Player.Nationality` existe como string opcional, mas **0/2.304 jogadores deste mundo têm nacionalidade explícita preenchida**. O código da procedência do arquivo identifica o contexto do clube importado, sem comprovar nacionalidade pessoal. Não foi inventada nacionalidade para preencher esta auditoria.

Quando a economia clássica precisa do país do jogador, o código usa Nationality se preenchido e, caso contrário, o país do clube/fallback; para o clube pode consultar a origem do catálogo. Uma nacionalidade ausente, portanto, recebe interpretação operacional pelo clube. Isso deve ser considerado antes de montar elencos multinacionais. [S10]

Não há limite esportivo/cota de estrangeiros na escalação. Na política clássica há classificação econômica Mesmo País / Equiparado PLOP-Bosman / Estrangeiro, com efeitos no preço candidato e interesse da IA. Na legada esses efeitos de preço por nacionalidade estão desativados. Não existem seleções nacionais ou competição internacional separada comprovadas no fluxo auditado.

## 15. Salários e mercado: políticas separadas

| Tema | MULTIPLAYER_CALIBRATED_012D | CLASSICAL_ELIFOOT98_V3_02C |
| --- | --- | --- |
| Regra armazenada | Padrão de compatibilidade do estado econômico | Opt-in por EnableClassicalEconomy e flags |
| Salário de referência | `round((220 + F^2,08 × 1,65) × fator*)`; *=1,20 para craque; A tem fator 1,08 | `50×C×round((35×I×sqrt(R)×F^1,04×1000^0,04)/50)`; limites 50×C a 50000×C |
| Mercado-base | `12000 + 76×F^2,65`, depois fatores por posição, comportamento, contrato e craque; piso 15000 | `round(2000×C×I×F^1,04×(1−0,04A−0,02B+0,02D))`, piso zero |
| Nacionalidade no preço | Desativada | Mesmo país `(Q+30000)×1,60`; equiparado `(Q+15000)×1,30`; estrangeiro Q, com quantização posterior |
| Tamanho/posição do elenco | Determinado pela fábrica/catálogo e limites do domínio | A política econômica sozinha não muda o elenco |
| Asterisco/comportamento | Fatores econômicos legados | Modificadores econômicos clássicos e interesse da IA; sem regenerar atletas existentes |
| Salário já contratado | Valor persistido, separado da referência | Valor persistido, separado da referência; não deve ser reescrito silenciosamente |

Na legada, os fatores de posição do mercado são A=1,12, M=1,05, G=1,03, D=0,98; comportamento varia de 1,08 a 0,82; craque multiplica 1,32 para M/A e 1,22 para G/D; contrato também modifica o valor. No clássico, I inicia em 2,0, C padrão 1, R padrão 1 e B/D são modificadores individuais. Fórmulas acima são do código atual, não transcrição de tabelas antigas que usavam outra calibração. [S09] [S10]

Neste banco há **cinco mundos com a política clássica e cinco com a legada**. Ambos os mundos de catálogo local estão na legada. A documentação 02C especifica opt-in e preservação da legada sem configuração explícita. Não foi localizada chamada de ativação automática no caminho atual de criação do catálogo; decidir o padrão desejado para futuros mundos é uma pendência de produto, não autorização para converter saves. [D08]

## 16. Ligas e critérios esportivos

Cada uma das quatro divisões possui oito clubes. São **sete jogos de ida e sete de volta por clube**, com **quatro partidas por divisão por rodada de Liga**. Total: **56 jogos por divisão e 224 de Liga por temporada**. O algoritmo circular/Berger constrói os confrontos e inverte o mando no returno; a ordem determinística dos clubes organiza o calendário. [S05]

Pontuação: vitória 3, empate 1, derrota 0. Desempates, em ordem: **pontos, vitórias, saldo de gols, gols marcados e índice determinístico**. Não há confronto direto ou critério distinto por país no ordenador observado. A classificação é separada por DivisionTier, compartilhada entre nacionalidades.

Conferência dos fixtures já persistidos: em cada divisão há exatamente 56 jogos, 28 pares de adversários com dois confrontos cada, sete mandos em casa e sete fora por clube. Não foram simulados jogos para obter esses números.

A quantidade de oito por divisão é uma premissa operacional: o gerador de Liga ignora uma divisão se seu tamanho for diferente de oito. Isso torna perigoso editar quantidade/divisão diretamente no snapshot, mesmo se um JSON puder ser desserializado.

## 17. Promoções e rebaixamentos

- Os dois últimos da 1ª descem para a 2ª; os dois primeiros da 2ª sobem à 1ª.
- Os dois últimos da 2ª trocam com os dois primeiros da 3ª.
- Os dois últimos da 3ª trocam com os dois primeiros da 4ª.
- Os dois últimos da 4ª vão aos distritais; dois distritais sobem à 4ª.
- A escolha dos distritais usa **vitórias na Taça da temporada**, depois reputação e o desempate `(DeterministicIndex + SeasonNumber×7) % 96`.
- Humanos de clubes rebaixados da 4ª para distrital são demitidos; não passam a dirigir um distrital. O fluxo de carreira gera vagas/convites e IA provisória conforme as regras existentes.

São oito acessos e oito descidas por temporada, preservando a quantidade de clubes nas divisões. País não é critério de acesso; um clube espanhol atualmente distrital pode alcançar a mesma pirâmide. IDs e histórico do clube são preservados nas mudanças de divisão. [S05] [S17]

## 18. Calendário do mundo atual

O calendário tem **21 rodadas globais**. As duas primeiras são de Liga; a Taça começa na rodada 3. A final encerra a temporada. Todos os dez snapshots analisados possuem LeagueFirstSchedule=true e 21 rodadas, inclusive os saves de economia legada.

| Rodada global | Competição | Data nominal do calendário | Data persistida no mundo |
| --- | --- | --- | --- |
| 1 | Liga 1 (ida) | 1998-08-01 | 1998-08-01 |
| 2 | Liga 2 (ida) | 1998-08-05 | 1998-08-05 |
| 3 | Taça: 128 → 64 | 1998-08-08 | 1998-08-08 |
| 4 | Liga 3 (ida) | 1998-08-12 | 1998-08-12 |
| 5 | Liga 4 (ida) | 1998-08-15 | N/D — ainda não registrada |
| 6 | Taça: 64 → 32 | 1998-08-19 | N/D — ainda não registrada |
| 7 | Liga 5 (ida) | 1998-08-22 | N/D — ainda não registrada |
| 8 | Liga 6 (ida) | 1998-08-26 | N/D — ainda não registrada |
| 9 | Taça: 32 → 16 | 1998-08-29 | N/D — ainda não registrada |
| 10 | Liga 7 (ida) | 1998-09-02 | N/D — ainda não registrada |
| 11 | Liga 8 (volta) | 1998-09-05 | N/D — ainda não registrada |
| 12 | Taça: oitavas | 1998-09-09 | N/D — ainda não registrada |
| 13 | Liga 9 (volta) | 1998-09-12 | N/D — ainda não registrada |
| 14 | Liga 10 (volta) | 1998-09-16 | N/D — ainda não registrada |
| 15 | Taça: quartas | 1998-09-19 | N/D — ainda não registrada |
| 16 | Liga 11 (volta) | 1998-09-23 | N/D — ainda não registrada |
| 17 | Liga 12 (volta) | 1998-09-26 | N/D — ainda não registrada |
| 18 | Taça: semifinais | 1998-09-30 | N/D — ainda não registrada |
| 19 | Liga 13 (volta) | 1998-10-03 | N/D — ainda não registrada |
| 20 | Liga 14 (volta) | 1998-10-07 | N/D — ainda não registrada |
| 21 | Taça: final | 1998-10-10 | N/D — ainda não registrada |


As datas nominais são uma leitura da fórmula já implementada, com intervalos alternados de quatro e três dias a partir de 01/08/1998. Somente as quatro primeiras datas estão persistidas neste mundo. O calendário adaptado pode postergar jogos por restrições de disponibilidade; datas futuras nominais não foram apresentadas como eventos já gravados. [S18]

## 19. Taça: participantes, fases e confronto

Formato efetivo atual: **todos os 128 clubes**, de todas as divisões e dos distritais, em **sete fases de jogo único**: 128→64, 64→32, 32→16, oitavas (16→8), quartas, semifinais e final. São **127 partidas** na chave completa; somadas às 224 da Liga, **351 partidas por temporada**.

A primeira fase embaralha todos os clubes usando RNG determinístico da seed/temporada/rodada. As seguintes usam os vencedores ordenados pelos índices das partidas anteriores, formando o encadeamento da chave. O primeiro de cada par joga em casa; não foi encontrada regra de campo neutro para a final. Não há separação por país ou cabeça de chave obrigatório por divisão. [S05]

Empate aos 90 minutos leva à prorrogação até 120 e, persistindo, aos pênaltis. Resultado dos pênaltis tem campos próprios e WinnerClubId; não se aplica regra de gols fora ou uma segunda partida. O motor suporta a resolução da eliminatória e as fases de intervalo da prorrogação. [S19]

O mundo atual já tem **64 jogos da fase de 128** gerados, além dos 224 da Liga. As outras fases são materializadas após concluir a anterior. Esse estado de 288 fixtures armazenadas é esperado no início da competição.

A leitura desses 64 jogos confirmou 128 participantes distintos e **30 confrontos Portugal × Espanha** nessa primeira eliminatória. São confrontos entre países dentro da Taça compartilhada, sem uma competição internacional adicional.

Existe ramo de compatibilidade de calendários antigos, com duas preliminares distritais antes da fase de 64. Ele está condicionado a LeagueFirstSchedule=false e **não é o formato dos dez mundos atuais**. Documentos antigos que descrevem 22 rodadas ou só 64 clubes na fase principal não devem ser usados para montar o campeonato atual. [D06]

## 20. Taça: prêmios e histórico

O código de encerramento da temporada registra **R$ 900.000 ao campeão da Taça e R$ 450.000 ao vice**, com chaves idempotentes. Há receitas/custos de partidas pelo motor financeiro, mas não foi localizada uma tabela adicional de prêmio fixo de classificação para cada fase nesse encerramento. O acesso distrital tem bônus próprio de R$ 380.000, separado do título da Taça. [S05]

SeasonHistories guarda campeão da Liga, campeão da Taça, nomes/IDs, artilharia e acessos/descidas. History e CoachTitles preservam títulos e vínculos; a final efetivamente jogada fundamenta os títulos. O mundo principal ainda tem **zero temporadas encerradas**, portanto não há campeão anterior a preencher. O encerramento mantém um fallback de campeão para o registro/prêmio se faltar final; é uma razão adicional para evitar manipulação de fixtures fora do fluxo oficial, não evidência de fallback usado neste mundo. [S17]

## 21. Editor/importador: possibilidades reais

| Operação | Implementação atual e limites |
| --- | --- |
| Criar novos clubes | Cadastro de arquivo local JSON para futuras seleções; pacote JSON personalizado também comporta entidades novas; sem botão dedicado de “novo clube” no editor visual de montagem observado |
| Excluir clubes | Remover variante da seleção ou substituir no rascunho; montagem final continua exigindo 128. Não há exclusão homologada de clube de um save em andamento |
| Renomear | Sim, no rascunho personalizado e no JSON de cadastro; consentimento de identidade personalizada quando necessário |
| Alterar sigla | Edita ShortName, chamado “Sigla” na UI; campo independente de abreviação não existe |
| Alterar país | Seletor do editor dentro dos países da montagem; cadastro local também tem Country/CountryCode |
| Alterar/trocar divisão | Editor e criador permitem alocação/troca antes da confirmação; tamanhos precisam continuar 8/8/8/8/96 |
| Editar escudo | Não há upload/desenho de escudo próprio; só escudo genérico derivado das cores |
| Editar cores | Sim, seletor/hex no editor e cores no arquivo local |
| Editar estádio | Nome, capacidades e condição no editor de montagem; não altera o estádio deste save |
| Alterar elencos iniciais | Sim, editar/adicionar/remover jogadores no rascunho e importar CSV de jogadores; limites continuam valendo |
| Importar lista de clubes | JSON de cadastro por versão ou pacote completo com clubs/players/aiManagers; não foi localizado importador CSV exclusivo de clubes |
| Exportar lista de clubes | Pacote JSON inclui clubes; o CSV nativo do editor é de jogadores. O CSV de clubes entregue aqui é uma exportação de auditoria independente |
| Mundo personalizado | Sim, montagem privada com validação e confirmação antes do save |
| Alterar clube existente neste mundo | Bloqueado pelo servidor, por origem confirmada/humanos/rodada; editar os arquivos canônicos não reescreve este mundo |

O editor está ligado ao criador por `CustomAssemblyEditor` / “REVISAR / EDITAR MONTAGEM”. Salvar e confirmar usam rotas do serviço de montagens, não edição direta do mundo corrente. Há teste Flutter existente de campos editados e rascunhos inválidos e testes de integração de propriedade, concorrência, IDs independentes e persistência. **Nenhum teste ou edição pela interface foi executado nesta auditoria.** A ligação e as capacidades foram verificadas por código e evidência documental anterior; a escrita ponta a ponta no ambiente corrente não foi revalidada, porque a missão proíbe alterações e criação de mundo. [S11] [S12] [S13] [D07]

## 22. Persistência, isolamento e multiplayer

Cada mundo tem seu próprio snapshot em `ef_worlds`, suas coleções de clubes/jogadores/fixtures e suas referências por GameWorldId. O catálogo é separado; a montagem guarda sua própria fonte e versão. Editar o catálogo ou uma montagem após a confirmação não atualiza um mundo já criado. Montagens privadas conferem proprietário, versão otimista e chave de confirmação para reutilizar o mesmo mundo em reenvios. [S13] [D02] [D07]

**ClubId não é globalmente único entre todos os saves.** Há 1.280 instâncias e 1.024 GUIDs distintos neste banco. `GATE V3 CLASSICA 2` e `GATE V3 LEGADA` compartilham os 128 ClubIds por seed; o mesmo ocorre entre `GATE V3 ECONOMIA E VISUAL` e `GATE V3 ECONOMIA E VISUAL GATEEC2`. A identidade operacional usa **WorldId + ClubId**. A tabela de resumo tem chave composta GameWorldId/Id. A fábrica usa IDs determinísticos, e o importador genérico conserva IDs do pacote; a confirmação de montagem privada remapeia clubes, atletas e técnicos para novos IDs. [S02] [S15] [S16] [S12]

O CSV é contextualizado por um único WorldId e registra esse WorldId em Observacoes. Ferramentas externas não devem mesclar os ClubIds dos dez saves como uma chave global.

Humanos assumem clubes pelo lobby/entrada e depois pelos convites de carreira; uma identidade desconectada conserva sua vaga. Ao assumir clube, o técnico IA anterior perde o vínculo daquele clube. A IA administra clubes sem humano e o fluxo de carreira pode instalar técnico provisório em vagas. Promoções/rebaixamentos mudam DivisionTier, sem recriar clubes. Dependências de índice de slot, 8 equipes por divisão e desempate distrital `%96` são explícitas no código.

O pacote JSON de montagem/exportação não é um backup integral do progresso: a importação cria outro mundo e inicializa calendário/economia. Ele não substitui snapshot/backup do banco para preservar uma partida em andamento.

## 23. Comparação com decisões e documentos existentes

As regras abaixo usam fontes locais identificadas. Documentos intitulados PROPOSTA e referências históricas não foram tratados como autorização nova. Não foi feita pesquisa externa nem declaração independente de fidelidade a um executável original.

| REQUISITO | REGRA APROVADA / FONTE | IMPLEMENTAÇÃO ATUAL | CONFORMIDADE | DIFERENÇA | AÇÃO NECESSÁRIA |
| --- | --- | --- | --- | --- | --- |
| 128 clubes por mundo | 016B e catálogo histórico [D04][D01] | 8/8/8/8/96 em todos os mundos analisados | PASS | Nenhuma na distribuição total | Preservar formato na montagem |
| Países múltiplos | 1..8 por mundo, descoberta local [D01][D04] | Portugal/Espanha na mesma pirâmide | PASS | Não existe cota nacional automática | Distribuir manualmente se desejar equilíbrio |
| Oito países iniciais sempre disponíveis | Texto antigo 013A [D10]; fluxo local posterior [D02] | Só 2 carregados; 0 clubes canônicos | DIVERGENTE (documentação antiga) | Oito fixtures não equivalem ao catálogo real atual | Atualizar documentação operacional depois, sem inventar carga |
| Liga independente por país | N/D — decisão aprovada não localizada | Uma Liga compartilhada por DivisionTier | NÃO IMPLEMENTADO | Não há campeonatos nacionais paralelos | Decisão explícita se isso passar a ser requisito |
| Times históricos reais para montar | Cadastro real canônico pendente [D11] | Infraestrutura pronta, dados ativos sintéticos de QA | PARTIAL | 0 arquivos reais canônicos | Cadastrar dados documentados; não usar QA como relação oficial |
| Identificar dados fictícios/QA | Proveniência e consentimento do criador [D01][D11] | Ambiente sinaliza QA, mas variantes técnicas estão REAL_CLUB_VERIFIED/REAL_HISTORICAL_CATALOG | DIVERGENTE (semântica dos dados QA) | Os rótulos técnicos não comprovam que Clube QA/Atleta sintético sejam reais | Separar o cadastro oficial do QA e revisar rotulagem antes de divulgação |
| Elenco histórico do ano | Preservar arquivo da variante, limite 24 [S04][D01] | 18 por clube no snapshot de criação | PASS | Ciclo 17..23 da 016B genérica não é obrigatório aqui | Preservar nomes/posições da versão escolhida |
| Limites 11..24 | SquadValidator [S06] | 16..20 vinculados nos clubes atuais; mínimo respeitado | PASS | Quantidade corrente mudou pelo jogo | Não confundir quantidade inicial e atual |
| Referência inicial 017B | Faixas e tabela por slot [D05] | Origem tem 39..50 / 26..38 / 13..25 / 1..12 | PASS | Distritais seguem a estimativa anterior | Conservar a referência separada do ranking ao vivo |
| Economia clássica e saves legados | Ativação explícita e preservação sem configuração [D08] | 5 mundos clássicos, 5 legados; atual legado | PASS para compatibilidade | Aparência V3 não ativa a fórmula clássica | Documentar a política escolhida para cada novo mundo |
| Política padrão para todo novo mundo | N/D — não foi encontrada decisão de converter automaticamente todo criador | Criador atual segue defaults legados; método clássico exige opt-in | PENDENTE | Não há seleção explícita de política nesse criador | Definir o padrão futuro sem converter saves |
| Nacionalidade individual real | Campo opcional; não gerar nacionalidade sem evidência | 0 preenchidas no mundo atual; fallback por clube | PARTIAL | Não há dados pessoais de nacionalidade no catálogo local atual | Definir fonte/modelo de cadastro se necessário |
| Cota esportiva de estrangeiros | Ausência de cota preservada [D09][D08] | Sem limite na escalação | PASS | Preço/interesse clássico pode usar nacionalidade | Distinguir regra esportiva e econômica |
| País sempre meramente informativo | Texto histórico [D09] | Política 02C permite efeito econômico [D08] | DIVERGENTE (documentação antiga) | Documento-base ficou anterior à economia clássica | Reconciliar a documentação, mantendo as decisões posteriores |
| Temporada de 21 rodadas e Taça com 128 | Regra posterior de calendário [D06] | 14 Liga + 7 Taça, primeira eliminatória na rodada 3 | PASS | Descrições antigas com preliminares/22 rodadas não são atuais | Usar a regra de calendário posterior |
| Acesso e descida de dois por divisão | Motor e regras de Liga/Taça [S05] | 2 por fronteira; distrital por vitórias na Taça | PASS | País não limita acesso | Não reorganizar a pirâmide pelo país durante o save |
| Editor antes do save e isolamento | Montagem privada 014B [D07] | Editor/rotas/testes existentes; 0 montagens privadas salvas neste catálogo | PARTIAL na verificação operacional | Escrita visual não foi repetida na auditoria somente leitura | Fazer validação de uma montagem separada em execução autorizada futura |
| Edição direta de mundo iniciado | Bloqueio de save confirmado [S14][D07] | Mundo atual bloqueado | PASS | Renomear arquivo não renomeia clube no save | Editar futura montagem, manter este mundo |
| Sigla separada de nome curto | N/D — campo independente não localizado | UI usa ShortName para “Sigla” | NÃO IMPLEMENTADO | Um campo para dois conceitos pedidos | Decidir se um segundo campo é necessário |
| Escudo personalizado | N/D — editor de imagem não localizado | Escudo genérico por cores | NÃO IMPLEMENTADO | Sem arquivo de escudo próprio | Decisão futura, sem implementação nesta auditoria |
| Renovação recente preservada | Solicitação recente e relatório de renovação | Código e build presentes; nenhum arquivo alterado pela auditoria | PASS | Sem commit, como antes | Preservar alterações locais |

## 24. Divergências e riscos para os saves

1. **Cobertura real pendente:** zero cadastro real canônico; 128 variantes QA têm marcações de “real” próprias do importador local. Tratar QA como base histórica produziria nomes e elencos artificiais apresentados com procedência indevida.
2. **Países desbalanceados:** o ranking global concentra as quatro divisões num país neste conjunto. Montar uma lista multinacional não cria ligas nacionais ou divisão proporcional automática.
3. **Políticas diferentes sob a mesma aparência:** o mundo atual possui contratos sazonais e força clássica, mas economia legada. Alterar o Policy do save ou recalcular salários em massa seria uma mudança de jogo, não uma correção de cadastro.
4. **IDs com escopo:** GUIDs podem repetir entre mundos; mudar/substituir IDs quebra fixtures, vínculos, contratos, leilões, histórico e livro financeiro se as referências não forem remapeadas pelo fluxo autorizado.
5. **Quantidade e divisões:** gerador pressupõe oito em cada divisão; trocar 128 por outra quantidade ou remover clube diretamente pode deixar jogos ausentes, chaves incompletas e acessos inconsistentes. O importador genérico valida ao menos 32 clubes, enquanto a montagem privada adiciona as verificações estritas 8/8/8/8/96.
6. **Renomeação e nacionalidade:** nomes não são identidade e determinados históricos conservam nomes antigos. Nationality ausente usa país do clube, com interpretação econômica limitada para elencos multinacionais.
7. **Pacotes não são saves completos:** export/import de pacote inicializa outro campeonato, pode conservar IDs no caminho genérico e não transporta todo o progresso e configuração econômica do snapshot.
8. **Reindexação não atualiza partidas:** futuras versões do catálogo não devem ser confundidas com correção dos clubes do mundo já confirmado. A cópia no save é intencional.

Nenhum desses riscos foi resolvido por mutação nesta auditoria. Não há operação destrutiva autorizada ou executada.

## 25. Decisões necessárias e recomendação para montar os times

É possível começar o **cadastro dos dados e a preparação de uma futura montagem** usando a infraestrutura existente. A base ativa atual deve permanecer identificada como teste.

Ordem recomendada para uma execução futura, sem executar agora:

1. Preparar os arquivos canônicos a partir de `catalogo/modelos/clube.json` e `club.schema.json`, com ClubId estável, país/código, ano, categoria, ranking, cores e jogadores documentados. Não colocar força oficial inventada no arquivo.
2. Revisar a cobertura por país/ano. O total selecionado precisa preencher 32 vagas de Liga e 96 distritais; versões do mesmo clube em anos diferentes ocupam vagas independentes.
3. Confirmar a expectativa de campeonato compartilhado. Ligas independentes por país exigiriam decisão e trabalho adicional; não estão implementadas.
4. Escolher a política econômica para futuros mundos de teste de conteúdo. A atual legada pode ser mantida; ativar a clássica é uma escolha explícita para o novo mundo, sem transformar este save.
5. Usar a distribuição personalizada para equilibrar países nas divisões, se desejado, e revisar elencos e referências 017B na prévia.
6. Salvar uma montagem privada e validá-la antes de criar outro save, somente numa execução que autorize essas escritas. Preservar o mundo atual para continuidade do teste manual.

Não é necessário decidir nomes de clubes antes de entender a estrutura; a lista real atual e os limites estão documentados nesta entrega. Permanecem decisões de produto, caso desejadas: ligas nacionais independentes, campo separado de sigla, imagem de escudo e fonte de nacionalidade pessoal. Não foi iniciada implementação para nenhuma delas.

## Entrega e limites da evidência

- `docs/auditorias/ELIFOOT98_ESTRUTURA_TIMES_DIVISOES_AUDITORIA.md`: este relatório, com todos os 128 clubes e seus IDs.
- `docs/auditorias/ELIFOOT98_CLUBES_ATUAIS.csv`: 128 linhas de dados; separador `;`, UTF-8 com BOM. Capacidade construída; saldo inicial do livro financeiro; quantidade atual vinculada; N/D para sigla independente.
- `docs/auditorias/ELIFOOT98_ESTRUTURA_VISUAL.md`: distribuição por país/divisão e relação completa agrupada.

Leitura do PostgreSQL executada dentro de `BEGIN READ ONLY`; SQLite aberto com `mode=ro` e `PRAGMA query_only=ON`. Não foram chamadas rotas de criação/edição/reindexação, não foram executados testes, migrations, simulações ou comandos de inicialização, e não foram encerrados processos. A verificação do editor é estática e documental, com testes existentes identificados para referência. A análise compara documentos locais do projeto; propostas não aprovadas e disponibilidade histórica de fornecedores não foram promovidas a decisões do proprietário.

**Conferência final de preservação:** os hashes dos **10 snapshots permaneceram idênticos** entre o início e o fim da coleta. Os **24 arquivos preexistentes** de código/testes/documentação local registrados no baseline permaneceram byte a byte iguais; HEAD e branch permaneceram iguais. API respondeu OK, Web HTTP 200 e os processos da API, PostgreSQL e Web continuaram executando. Evidências em `verification.json`, `snapshot-preservation.json` e `services-readonly.json` na pasta da auditoria.

## Fontes locais verificadas

- **S01:** [src/Elifoot.Domain/Entities.cs:69](D:/elifoot_98/src/Elifoot.Domain/Entities.cs:69).
[S01]: D:/elifoot_98/src/Elifoot.Domain/Entities.cs:69
- **S02:** [src/Elifoot.Domain/WorldSeedFactory.cs:95](D:/elifoot_98/src/Elifoot.Domain/WorldSeedFactory.cs:95).
[S02]: D:/elifoot_98/src/Elifoot.Domain/WorldSeedFactory.cs:95
- **S03:** [src/Elifoot.Domain/Catalog/WorldCreatorEngine.cs:12](D:/elifoot_98/src/Elifoot.Domain/Catalog/WorldCreatorEngine.cs:12).
[S03]: D:/elifoot_98/src/Elifoot.Domain/Catalog/WorldCreatorEngine.cs:12
- **S04:** [src/Elifoot.Domain/Catalog/HistoricalCatalogPolicy.cs:6](D:/elifoot_98/src/Elifoot.Domain/Catalog/HistoricalCatalogPolicy.cs:6).
[S04]: D:/elifoot_98/src/Elifoot.Domain/Catalog/HistoricalCatalogPolicy.cs:6
- **S05:** [src/Elifoot.Domain/CompetitionAndSeasonEngine.cs:14](D:/elifoot_98/src/Elifoot.Domain/CompetitionAndSeasonEngine.cs:14).
[S05]: D:/elifoot_98/src/Elifoot.Domain/CompetitionAndSeasonEngine.cs:14
- **S06:** [src/Elifoot.Domain/TacticsAndSquadRules.cs:66](D:/elifoot_98/src/Elifoot.Domain/TacticsAndSquadRules.cs:66).
[S06]: D:/elifoot_98/src/Elifoot.Domain/TacticsAndSquadRules.cs:66
- **S07:** [src/Elifoot.Domain/Catalog/WorldCatalogRules016B.cs:7](D:/elifoot_98/src/Elifoot.Domain/Catalog/WorldCatalogRules016B.cs:7).
[S07]: D:/elifoot_98/src/Elifoot.Domain/Catalog/WorldCatalogRules016B.cs:7
- **S08:** [src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs:10](D:/elifoot_98/src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs:10).
[S08]: D:/elifoot_98/src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs:10
- **S09:** [src/Elifoot.Domain/MarketAndEconomyEngine.cs:16](D:/elifoot_98/src/Elifoot.Domain/MarketAndEconomyEngine.cs:16).
[S09]: D:/elifoot_98/src/Elifoot.Domain/MarketAndEconomyEngine.cs:16
- **S10:** [src/Elifoot.Domain/ClassicPlayerEconomyEngine.cs:119](D:/elifoot_98/src/Elifoot.Domain/ClassicPlayerEconomyEngine.cs:119).
[S10]: D:/elifoot_98/src/Elifoot.Domain/ClassicPlayerEconomyEngine.cs:119
- **S11:** [client/elifoot_flutter/lib/custom_assembly_editor.dart:120](D:/elifoot_98/client/elifoot_flutter/lib/custom_assembly_editor.dart:120).
[S11]: D:/elifoot_98/client/elifoot_flutter/lib/custom_assembly_editor.dart:120
- **S12:** [src/Elifoot.Domain/Catalog/CustomAssemblyPolicy.cs:89](D:/elifoot_98/src/Elifoot.Domain/Catalog/CustomAssemblyPolicy.cs:89).
[S12]: D:/elifoot_98/src/Elifoot.Domain/Catalog/CustomAssemblyPolicy.cs:89
- **S13:** [src/Elifoot.Infrastructure/Catalog/WorldAssemblyService.cs:130](D:/elifoot_98/src/Elifoot.Infrastructure/Catalog/WorldAssemblyService.cs:130).
[S13]: D:/elifoot_98/src/Elifoot.Infrastructure/Catalog/WorldAssemblyService.cs:130
- **S14:** [src/Elifoot.Api/Program.cs:309](D:/elifoot_98/src/Elifoot.Api/Program.cs:309).
[S14]: D:/elifoot_98/src/Elifoot.Api/Program.cs:309
- **S15:** [src/Elifoot.Domain/WorldEditorAndPackageService.cs:289](D:/elifoot_98/src/Elifoot.Domain/WorldEditorAndPackageService.cs:289).
[S15]: D:/elifoot_98/src/Elifoot.Domain/WorldEditorAndPackageService.cs:289
- **S16:** [src/Elifoot.Infrastructure/ElifootDbContext.cs:114](D:/elifoot_98/src/Elifoot.Infrastructure/ElifootDbContext.cs:114).
[S16]: D:/elifoot_98/src/Elifoot.Infrastructure/ElifootDbContext.cs:114
- **S17:** [src/Elifoot.Domain/HistoryEngine.cs:8](D:/elifoot_98/src/Elifoot.Domain/HistoryEngine.cs:8).
[S17]: D:/elifoot_98/src/Elifoot.Domain/HistoryEngine.cs:8
- **S18:** [src/Elifoot.Domain/ClassicForceEvolutionEngine.cs:54](D:/elifoot_98/src/Elifoot.Domain/ClassicForceEvolutionEngine.cs:54).
[S18]: D:/elifoot_98/src/Elifoot.Domain/ClassicForceEvolutionEngine.cs:54
- **S19:** [src/Elifoot.Application/MatchInteractionCommands.cs:92](D:/elifoot_98/src/Elifoot.Application/MatchInteractionCommands.cs:92).
[S19]: D:/elifoot_98/src/Elifoot.Application/MatchInteractionCommands.cs:92
- **S20:** [src/Elifoot.Infrastructure/Catalog/LocalHistoricalCatalogFiles.cs:25](D:/elifoot_98/src/Elifoot.Infrastructure/Catalog/LocalHistoricalCatalogFiles.cs:25).
[S20]: D:/elifoot_98/src/Elifoot.Infrastructure/Catalog/LocalHistoricalCatalogFiles.cs:25
- **S21:** [client/elifoot_flutter/lib/club_crest.dart:5](D:/elifoot_98/client/elifoot_flutter/lib/club_crest.dart:5).
[S21]: D:/elifoot_98/client/elifoot_flutter/lib/club_crest.dart:5
- **D01:** [docs/MULTI_YEAR_WORLD_CREATION.md:1](D:/elifoot_98/docs/MULTI_YEAR_WORLD_CREATION.md:1).
[D01]: D:/elifoot_98/docs/MULTI_YEAR_WORLD_CREATION.md:1
- **D02:** [docs/LOCAL_CATALOG_ARCHITECTURE.md:1](D:/elifoot_98/docs/LOCAL_CATALOG_ARCHITECTURE.md:1).
[D02]: D:/elifoot_98/docs/LOCAL_CATALOG_ARCHITECTURE.md:1
- **D03:** [docs/CLUB_JSON_SCHEMA.md:1](D:/elifoot_98/docs/CLUB_JSON_SCHEMA.md:1).
[D03]: D:/elifoot_98/docs/CLUB_JSON_SCHEMA.md:1
- **D04:** [docs/WORLD_CREATION_LIMITS_016B.md:1](D:/elifoot_98/docs/WORLD_CREATION_LIMITS_016B.md:1).
[D04]: D:/elifoot_98/docs/WORLD_CREATION_LIMITS_016B.md:1
- **D05:** [docs/INITIAL_STRENGTH_RULES_017B.md:1](D:/elifoot_98/docs/INITIAL_STRENGTH_RULES_017B.md:1).
[D05]: D:/elifoot_98/docs/INITIAL_STRENGTH_RULES_017B.md:1
- **D06:** [docs/CLASSIFICATION_FORCE_EVOLUTION.md:1](D:/elifoot_98/docs/CLASSIFICATION_FORCE_EVOLUTION.md:1).
[D06]: D:/elifoot_98/docs/CLASSIFICATION_FORCE_EVOLUTION.md:1
- **D07:** [docs/CUSTOM_WORLD_PRESET_014B.md:1](D:/elifoot_98/docs/CUSTOM_WORLD_PRESET_014B.md:1).
[D07]: D:/elifoot_98/docs/CUSTOM_WORLD_PRESET_014B.md:1
- **D08:** [docs/implementacoes/ELIFOOT98_V3_ETAPA02C_RESULTADO.md:1](D:/elifoot_98/docs/implementacoes/ELIFOOT98_V3_ETAPA02C_RESULTADO.md:1).
[D08]: D:/elifoot_98/docs/implementacoes/ELIFOOT98_V3_ETAPA02C_RESULTADO.md:1
- **D09:** [docs/REGRAS_VS_ORIGINAL.md:1](D:/elifoot_98/docs/REGRAS_VS_ORIGINAL.md:1).
[D09]: D:/elifoot_98/docs/REGRAS_VS_ORIGINAL.md:1
- **D10:** [docs/COUNTRY_SELECTION_013A.md:1](D:/elifoot_98/docs/COUNTRY_SELECTION_013A.md:1).
[D10]: D:/elifoot_98/docs/COUNTRY_SELECTION_013A.md:1
- **D11:** [docs/LOCAL_HISTORICAL_CATALOG_REPORT.md:1](D:/elifoot_98/docs/LOCAL_HISTORICAL_CATALOG_REPORT.md:1).
[D11]: D:/elifoot_98/docs/LOCAL_HISTORICAL_CATALOG_REPORT.md:1

Dados congelados da leitura: `artifacts/structure-audit-20261111/worlds.jsonl`, `current-world.json`, `catalog-index.json`, `world-summary.json`, `baseline.json` e `read-worlds.sql`. Os snapshots de evidência não são importados, restaurados ou convertidos. Não contêm a lista de clubes de outro banco presumido.
