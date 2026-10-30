# Categoria e dados — 013A

`FootballGender` aceita Male/Female. País, categoria, liga, temporada, tipo de dado e fornecedor compõem a chave do escopo. O catálogo distingue equipes femininas e masculinas mesmo quando compartilham nome ou identificador externo. Clubes salvos nos mundos usam identificadores próprios do jogo, com proveniência externa armazenada separadamente.

A categoria integra a prévia e o snapshot de origem. `GameWorld.Gender` tem setter privado com restauração JSON explícita; `SetCatalogOrigin` recusa uma segunda atribuição e mundos com temporada/rodada/partida já iniciadas. Pacotes antigos sem categoria continuam masculinos. Exportar/importar um pacote novo preserva categoria e origem.

O MatchEngine, regulamentos e evolução da 012C não foram alterados. As forças importadas são inteiros de 1 a 50, sem fator de redução feminino. Posições são normalizadas para G/D/M/A; posições desconhecidas são rejeitadas. Elencos precisam de 11–24 atletas, pelo menos um G e dez jogadores de linha. A proteção existente do último goleiro permanece coberta por teste.

A criação usa o modo ClassicAdapted da 012C e recalcula o registro de forças iniciais depois de substituir os elencos da seed. Salários e valor inicial usam as funções econômicas existentes; não foi criada uma fórmula econômica nova. A integração com a 012D deverá reconciliar o construtor dos jogadores com os contratos aprovados naquela missão.
