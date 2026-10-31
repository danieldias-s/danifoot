# Aparência

APARÊNCIA abre uma rota visual no cabeçalho, sem recolocar Editor e Pacotes na temporada. Usar cores do clube escolhe o modo Club; Personalizar cores permite fundo, principal, secundária, texto e destaque; Restaurar padrão grava o modo Club e volta à paleta original, ou ao neutro quando não existe fonte.

O seletor visual tem matiz, saturação, luminosidade, amostras táteis, leitura hexadecimal e alternativa manual #RRGGBB. Alterações válidas atualizam a prévia e o tema global imediatamente, sem reiniciar. Salvar aparência persiste no servidor com a versão esperada. Sair sem salvar restaura o estado confirmado. Uma edição posterior a um salvamento volta a ser considerada pendente. Mensagens de erro e validação usam texto.

As escolhas pertencem ao usuário/mundo/clube. Nem a paleta oficial nem o tema dos outros treinadores é alterado. O editor pré-save reaproveita o seletor para definir principal/secundária em clubes reais ou fictícios; a personalização do clube fica restrita à montagem e às cópias criadas a partir dela.

Testes em 390 e 1366 pixels acionam menu, campos, três controles visuais, gravação, restauração e retorno ao painel do treinador. Troca de clube e retorno são exercitados no controlador; ainda falta a sequência completa de contratação no navegador real.

Se o clube mudar com o painel aberto, o rascunho anterior é bloqueado para não gravar/restaurar no clube novo. Reabrir Aparência carrega o contexto atual; caso coberto por teste de widget.
