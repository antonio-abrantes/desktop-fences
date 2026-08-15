# ADR-0003: Ancoragem no desktop + vidro da fence

**Date**: 2026-08-14
**Status**: accepted (emendado após validação no W11; emendado no fechamento do MVP 1)
**Deciders**: Antonio + agente

## Context

Queremos fence translúcida, cantos arredondados, acima do wallpaper e atrás das janelas normais. A primeira tentativa (`AllowsTransparency=False` + `SetWindowCompositionAttribute` + `GlassFrameThickness=-1`) no Windows 11 do desenvolvedor produziu uma janela **opaca cinza**, sem vidro.

`GWL_HWNDPARENT` → Progman (padrão NoFences) foi tentado depois e **impedia o OLE drop** do Explorer.

## Decision

- `AllowsTransparency=True`, `WindowStyle=None`.
- Vidro via brush alfa no `Border` (`#A80C0C12`, ~65% opaco), `CornerRadius=8`. Visual **travado** até pedido de tema.
- Não usar `GlassFrameThickness=-1`.
- Não aplicar `SetWindowCompositionAttribute` enquanto a janela for layered.
- Z-order: `SetWindowPos(HWND_BOTTOM)` + `WS_EX_TOOLWINDOW`. Sem `GWL_HWNDPARENT` no Progman.
- A fence é um **container**: o usuário arrasta ícones para dentro.

## Alternatives Considered

### AllowsTransparency=False + acrylic DWM
- **Pros**: blur real quando funciona.
- **Cons**: no W11 testado ficou cinza opaco.
- **Why not**: rejeitado na validação visual.

### GWL_HWNDPARENT → Progman
- **Pros**: ancora no z-order do desktop como o NoFences.
- **Cons**: o Explorer não completa o OLE drop na janela.
- **Why not**: drop inbound é requisito do MVP 1.

### SetParent(WorkerW)
- **Cons**: some com janela layered/WPF.
- **Why not**: precisamos de drop e hit-test.

## Consequences

### Positive
- Transparência visível; radius no XAML; janela top-level atrás dos apps.

### Negative
- Blur acrylic de verdade fica para um ADR futuro se o vidro alfa não bastar.
- `HWND_BOTTOM` pode perder z-order em alguns eventos; a App reaplica no `Deactivated`.

### Risks
- Layered window + OLE: tratado no [ADR-0004](0004-ole-inbound-drop.md).
