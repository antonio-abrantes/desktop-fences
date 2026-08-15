# ADR-0004: Drop inbound do Explorer numa fence layered

**Date**: 2026-08-14
**Status**: accepted
**Deciders**: Antonio + agente

## Context

A fence WPF usa `AllowsTransparency` (`UpdateLayeredWindow`, alfa por pixel). O Explorer, no `DoDragDrop`, hit-testa o hotspot: a janela do thumbnail OLE fica na frente, o `IDropTarget` do WPF devolve NONE ou nem é chamado, o ícone some e o cursor vira “proibido”.

Overlays `LWA_ALPHA` cobrindo a fence inteira pioram o z-order (a imagem do arraste fica atrás da camada). Quadrado opaco no ponteiro foi rejeitado visualmente.

O arraste **para fora** da fence não usa OLE: hook `WH_MOUSE_LL` + ghost WPF. O usuário quer o inbound com o mesmo ghost e a mesma seta.

## Decision

1. `RegisterDragDrop` nativo (`ShellOleDropTarget` + `IDropTargetHelper`) no HWND da fence. `AllowDrop` WPF desligado nessa janela.
2. Alvo OLE extra: janela minúscula (LWA_ALPHA uniforme, não per-pixel), colada no cursor, `HWND_TOPMOST` a cada move, só enquanto o arraste inbound está sobre a fence. Ghost WPF fica acima dela com `WS_EX_TRANSPARENT`.
3. Ghost inbound é o mesmo `DragGhostWindow` do outbound (não o thumbnail do Explorer, que some atrás do vidro).
4. Cursor: o Explorer continua a pintar `IDC_NO`. Enquanto o ponteiro está sobre a fence, substituímos `OCR_NO` pela seta e reaplicamos no pump; ao sair ou soltar, `SPI_SETCURSORS` restaura o tema.
5. Fallback no mouse-up: se o OLE não entregar paths, o hit do desktop no press ainda agrupa o ícone.

## Alternatives Considered

### Só WPF AllowDrop / DragOver
- **Why not**: eventos não disparam de forma confiável na janela layered; cursor permanece NONE.

### Overlay LWA_ALPHA em cima da fence inteira
- **Why not**: esconde o thumbnail; hit-test do hotspot ainda pega a janela de drag do Explorer.

### Quadrado opaco seguindo o cursor
- **Why not**: visível e rejeitado; ainda falhou como alvo OLE enquanto ficou abaixo do thumbnail.

### Aceitar o cursor de proibido e só dropar no soltar
- **Why not**: o usuário trata o “no drop” como bug; o drop já funcionava e mesmo assim a UX estava errada.

## Consequences

### Positive
- Inbound e outbound têm o mesmo ghost; a seta no inbound casa com a saída; soltar continua agrupando.

### Negative
- `SetSystemCursor` é session-wide enquanto o inbound está ativo. Tem que restaurar em mouse-up, sair da fence, Pausar e shutdown.
- O alvo no cursor é um HWND extra para manter.

### Risks
- Tema de cursor do Windows: restaurar sempre com `SPI_SETCURSORS`.
- Não reintroduzir overlay full-fence nem quadrado visível no ponteiro.
