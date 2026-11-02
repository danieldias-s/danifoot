# Mobile conforme a referência — 015B

Pedido do proprietário: adotar o desenho mobile fornecido, mantendo a moral em barra. Implementação exclusiva de D:\elifoot_98_015B, sobre o checkpoint 3ab57cf, na branch feature/classic-desktop-ui-015b.

## Comportamento

- Abaixo de 768 px: cabeçalho com marca, data, comando real da rodada e contexto; clube e treinador em cartão claro; quatro células para divisão/posição, barra de moral, caixa e próximo adversário.
- A moral usa o mesmo valor e faixa do servidor que o desktop. Não há número, palavra Moral ou nome de faixa visível; o leitor de tela continua recebendo esses dados.
- Plantel abre com resumo tático, formação editável, contagens reais, titulares, banco e elenco. As listas usam cores G/D/M/A e abrem a ficha do atleta.
- Táticas abre o campo e os controles reais da escalação. Trocar entre Plantel e Táticas reinicia a rolagem desse conteúdo, para não esconder o campo após visitar filtros.
- A barra inferior fixa oferece Plantel, Táticas, Mercado, Competições e Mais. Mais abre os demais destinos, incluindo calendário, finanças, carreira, rankings e aparência.
- Filtros, ordenação, salário contratado, comportamento, contrato, gols, valor justo e ações continuam acessíveis. A tabela extensa admite rolagem horizontal/vertical; não foram removidos atributos para caber na imagem.
- O aviso de inferioridade numérica permanece no Plantel e no campo. Lesionados e suspensos não se tornam escaláveis por causa do novo layout.
- O estado autoritativo da partida continua bloqueando menus, gestos do drawer e barra inferior. O início fecha o drawer aberto; ao terminar, a navegação volta a funcionar.

O desenho usa as cores reais do clube em um escudo geométrico próprio. Não apresenta esse desenho como escudo oficial. Não foram inventados números de camisa, jogadores, salários, caixa, resultados ou dados do CSA da referência. O comando do cabeçalho mantém ENVIAR TÁTICA/RETIRAR PRONTO/PRÓXIMA RODADA conforme o estado real, em vez de um AVANÇAR desconectado da sala.

O desktop continua com sidebar e a composição anterior. A formação 5-3-2, o início automático com 1/1, o bloqueio da partida e os formulários de mercado 015A permanecem cobertos pela regressão. Leilão voluntário mantém mínimo inicial R$ 500.000 e duração inicial de duas rodadas; nenhum arquivo do backend foi alterado.

## Evidência

As imagens em evidence-015b-mobile/automated-renders são renderizações do Flutter, não capturas de Chrome ou de aparelho físico. O renderer usa as respostas GET registradas de API 5167/PostgreSQL DEV 55440 e fontes Roboto/Material Icons locais. A cena mostra Flamengo RJ, 18 atletas, preparação R5, sem enviar tática ou avançar a sala. Identidades e tokens do contexto de desenvolvimento não foram versionados.

Foram inspecionados Plantel, Táticas e filtros em 390 x 844, composição vertical em 390 x 1800 e desktop em 1440 x 1000. A inspeção encontrou e corrigiu texto do dropdown sem fonte explícita, contraste dos filtros no painel claro e rolagem herdada ao abrir Táticas. As evidências anteriores da 014C e 015B foram preservadas.

Reprodução neste ambiente preparado:

```powershell
Set-Location D:\elifoot_98_015B
python scripts/record-mobile-scene-015b.py
Set-Location client/elifoot_flutter
flutter test tool/mobile_reference_capture_015b_test.dart --reporter expanded
```

O script de registro só faz GET e o renderer usa transporte HTTP em memória, sem conexão ou mutação na sala. A cena local fica em artifacts/015b/mobile-scene.json. Logs de analyzer, testes e build ficam em artifacts/015b/mobile-*.log. Resultados efetivos e hashes constam em evidence-015b-mobile/validation.json e runtime-health.json.

## Revisão manual

Jogo: http://127.0.0.1:5168/ — atualizar com Ctrl+F5. O layout mobile é aplicado abaixo de 768 px; em Chrome desktop, usar a visualização de dispositivo ou reduzir a largura da janela.

Sala individual: 015B - Teste individual Daniel / ELIC18AA38C. Treinador Daniel 015B, dispositivo 015b-daniel, Flamengo RJ. O save não foi reiniciado para produzir a prévia. A leitura registrou R5; o proprietário pode continuar avançando durante seu teste.

STATUS da auditoria visual real: PARTIAL. A ferramenta continua retornando “Local browser use is disabled in Work settings”. A prévia renderizada não é apresentada como aprovação visual no Chrome. O teste manual do proprietário depende de sua confirmação. Não houve merge, push ou publicação.
