# Mercado, busca e jogadores observados

As duas aparências compartilham filtros de posição, nome, força e somente
observados. As faixas são inclusivas: 1–10, 5–15, 10–20, 20–30, 30–40 e 40–50.
As preferências são mantidas entre visitas ao mercado e separadas entre compra
direta e leilão.

**Procurar jogadores** consulta todos os atletas do mundo, incluindo quem não
está à venda. A busca parcial começa com três caracteres, desconsidera caixa e,
na API de busca global, acentos. Os controles de nome da lista do mercado
filtram seus anúncios; a busca global tem uma tela própria.

A estrela adiciona ou remove um jogador dos observados. A lista pertence ao
treinador naquele mundo, é salva no snapshot e retorna após reconexão, inclusive
em outro dispositivo. Uma transferência não perde a observação, pois o vínculo
usa o identificador do atleta. Não cria proposta, compra ou leilão.

O menu **Jogador** oferece **Procurar jogadores** e **Jogadores observados**.
O menu **Equipa** ficou com **Jogadores**, **Finanças** e **História — Em breve**,
conforme o ajuste posterior do proprietário. Mercado, Banco e Estádio permanecem
no rodapé; Estádio também foi mantido no rodapé mobile.

## Validação deste checkpoint

- 31 testes da aplicação passaram, incluindo busca parcial e acentuada,
  isolamento entre treinadores, idempotência, serialização e transferência.
- 7 testes de contas/API passaram. O novo caso confirma persistência após
  reinício SQLite e recusa de leitura e escrita da lista de outra conta.
- Os 6 testes novos de interface passaram nas aparências atual e clássica,
  em 390 e 1366 pixels. Conferem faixas, filtros combinados, mínimo de três
  letras, inclusão de atletas fora do mercado, marcação, remoção, restauração
  e falha de gravação sem confirmação falsa.
- A regressão Flutter completa passou com 379 testes. Após o ajuste final do
  menu Equipa e do atalho Estádio mobile, os 34 testes focados de menus,
  apresentação e escalação também passaram.
- Análise Flutter sem problemas; builds release Web e API concluídos.

Na prévia com PostgreSQL, uma sala exclusiva de QA retornou 42 atletas para
**Dan**, incluindo atletas fora do mercado; **Da** não disparou busca. Foram
marcados um atleta da busca e um do leilão. O filtro do leilão mostrou somente
o atleta marcado disponível nele. Outra conta tinha zero observados. A lista
foi recuperada após recarregar a página. Nenhum `pageerror` foi registrado.

As capturas correspondem à compilação do mercado antes da última simplificação
do menu Equipa. Medições em [evidence.json](evidence.json); capturas
[mercado web](mercado-web.png), [busca web](busca-web.png),
[leilão observado](leilao-observados-web.png) e
[observados mobile](observados-mobile.png). Chromium responsivo, sem teste em
aparelho físico. Os logs estão nesta pasta.

Nenhum save do proprietário foi utilizado nas interações de QA. Não foram
alterados força, economia, contratos, regras de negociação, placares ou
MatchEngine. Campo novo de observados é aditivo no snapshot; saves antigos
começam com a lista vazia, sem migração destrutiva. Sem merge, push ou produção.
