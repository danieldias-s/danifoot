# Tema dinâmico global

`lib/club_theme.dart` centraliza ClassicTheme, extensão de ThemeData, contraste e ClubThemeController. Os tokens são Background, Surface, SurfaceAlternate, Primary, Secondary, TextPrimary, TextSecondary, Accent, Border, ButtonBackground, ButtonText, SelectedBackground, SelectedText, TableHeader, TableRow e TableRowAlternate.

Primary/Secondary retêm a paleta registrada. Background e superfícies são variações de luminosidade; texto e destaques são ajustados para contraste de pelo menos 4,5:1. Para preservar texto escolhido quando possível, o fundo se aproxima do preto ou branco que maximiza o contraste. O ajuste altera somente a renderização. Isso também trata texto vermelho sobre azul-marinho e cinza intermediário. Cabeçalhos, tabelas, menus, formulários e diálogos usam os tokens. Estados de erro, cartões e moral mantêm rótulos/símbolos, além da cor.

ElifootClassicApp mantém o mesmo cliente API, controlador, Navigator e shell ao reconstruir ThemeData. Trocar de clube aplica imediatamente a paleta disponível e carrega a preferência daquela identidade. Respostas assíncronas de um clube anterior são descartadas por geração. Desemprego/saída da sala aplica neutro; retornar recupera a configuração anterior.

As telas de plantel/tática, divisões/taça, mercado/leilões, finanças/banco/estádio/contratos, ranking/carreira, calendário e estatísticas foram adaptadas sem substituir seus fluxos. Os estados sem significado de identidade ainda podem ter cores funcionais (por exemplo cartões e indicação textual de aptidão). A navegação clássica de sete grupos permanece.

TeamColorName aplica as cores de cada time aos nomes no quadro de jogos, sem usar a paleta do treinador para todos. Times sem fonte usam superfície neutra. Cores únicas não ganham segunda cor oficial fictícia. Os placares continuam centrais; divisão, expansão e destaque do clube não mudaram.

Testes: seis paletas-base/neutral, 17 paletas importadas, texto preto em vermelho e cinza intermediário, preferências/restauração/troca/desemprego, nomes com identidade independente e viewport desktop/mobile. Renderer registra dados efetivamente retornados pelo servidor. Testes não equivalem à auditoria de navegador pendente.
