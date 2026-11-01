# Componentes compartilhados — 015B

A aparência continua centralizada em ClassicTheme, incluindo variantes, luminosidade e contraste existentes. O padrão usa #002E20 no fundo, #00452B nos painéis, #00883C na ação e #FFD400 no destaque. As cores do clube continuam nas áreas de identidade e confrontos; não redefinem automaticamente a interface inteira.

ClassicLayoutTokens expõe AppBackground, SidebarBackground, HeaderBackground, PanelBackground, PanelHeader, PanelBorder, ActiveNavigation, ActiveTab, PrimaryText, SecondaryText, AccentYellow, ActionGreen, TableHeader, TableRowOdd, TableRowEven e ButtonHover por getters em lowerCamelCase. Eles derivam do tema existente, evitando uma segunda configuração de aparência.

| Componente | Uso |
|---|---|
| AppHeader | Marca, contexto real, data e comando permitido |
| AppSidebar | Sete destinos, seleção, menu secundário e bloqueio |
| ClassicPageHeader / ClassicSectionTitle | Título e descrição de página |
| ClassicPanel | Painel, borda fina, cabeçalho em gradiente |
| ClassicTabs | Filtro ativo acessível, rolagem horizontal |
| ClassicFilterBar / ClassicFilter | Agrupamento e seletores compactos |
| ClassicTable / ClassicTableHeader | Tabelas, cabeçalhos e coluna fixada ao rolar |
| ClassicActionButton | Ações com tipografia e dimensões comuns |
| ClassicStatusIndicator | Situação de preparação/finalização |
| MoraleBar | Valor/faixa do servidor, sem número/texto visível |
| ClassicDialog | Ficha e histórico responsivos |
| ClassicTacticalPitch | Campo esquemático dos jogadores selecionados |
| ClassicMatchBoard / TeamColorName | Placares agrupados e cores individuais preservados |

A fonte é a família Roboto padrão do Flutter. Ícones Material já disponíveis e desenhos próprios substituem elementos decorativos da referência. Não foram copiados escudos, fotografias, fontes ou outros assets proprietários. O tema global configura também formulários, botões, diálogos e tabelas existentes.

Os callbacks financeiros, táticos e de navegação permanecem nas camadas e serviços anteriores. Os componentes de desenho não calculam salários, forças, prazos, resultados, juros ou classificações.
