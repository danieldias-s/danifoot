# Evidência visual — 014B

STATUS: PARTIAL.

Aplicação real executada: API própria 5145, Web compilada 5146, PostgreSQL 55437. Servidor gravou snapshots autênticos de espera, início, Taça, liga, pausa e resultados. 20 PNGs em 390×1000 e 1366×1000 pelos widgets Flutter e respostas gravadas, incluindo editor, elenco, montagem restaurada e eventos. Fontes locais carregadas apenas no harness, sem copiar para produto.

São renderizações automatizadas, não screenshots do Chrome. O agente inspecionou imagens desktop/mobile e corrigiu expansão compartilhada da Taça. Não se alega auditoria humana ou sessão real no navegador com base nessas imagens.

Controle Chrome anterior encerrado pela ferramenta por não determinar URL com confiança para a política. A autorização do usuário foi recebida; não contornou o bloqueio. Disponibilidade pode ser reavaliada pela ferramenta documentada; até haver capturas nativas, item pendente. Referências visuais citadas no anexo não foram fornecidas como imagens; comparação fiel é pendente.

Hashes: `audit-014b/visual-evidence.json`. Harness: `client/elifoot_flutter/tool/classic_ux_capture_014b_test.dart`, dois casos desktop/mobile aprovados. Exemplos: [liga desktop](audit-014b/automated-renders/league-1366.png), [liga mobile](audit-014b/automated-renders/league-390.png), [espera](audit-014b/automated-renders/waiting-1-of-2-390.png), [editor](audit-014b/automated-renders/pre-save-editor-390.png).
