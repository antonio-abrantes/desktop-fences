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

### Emenda da Fase 5 — Win+D

A primeira correção de Win+D tentou impedir `SC_MINIMIZE`, `WM_SHOWWINDOW(false)`, `SWP_HIDEWINDOW`, estacionamento em coordenadas negativas e cloak do DWM. Também registrou shell hook e uma verificação periódica para restaurar a janela. No Windows 11 real isso continuou insuficiente.

O motivo observado é um estado diferente de hide/minimize: depois de “Mostrar ambiente de trabalho”, a fence pode continuar normal, uncloaked e `IsWindowVisible=true`, mas Progman/WorkerW passa a estar acima dela no z-order. Nesse estado não existe nada para `ShowWindow` restaurar.

Decisão revisada:

- continuar como janela top-level independente e `WS_EX_TOOLWINDOW`;
- localizar o top-level que contém `SHELLDLL_DefView` / `SysListView32`;
- caminhar o z-order acima desse host, atravessando Progman/WorkerW e as demais fences;
- inserir cada fence depois da primeira janela que não pertence à banda do Desktop, deixando-a acima do Desktop e abaixo dos aplicativos;
- repetir em shell hook, deactivation, DPI e verificação de sobrevivência;
- manter `HWND_BOTTOM` apenas como fallback quando o Explorer ainda não expôs a view.

Não usar `HWND_TOPMOST`: isso faria a fence cobrir aplicativos. Não retomar `GWL_HWNDPARENT`/`SetParent`: além do problema de drop já validado, uma owned window é escondida com seu owner e destruída quando ele é destruído; isso acoplaria as fences ao restart do Explorer.

Não há uma API pública documentada “excluir esta janela do Win+D”. A correção usa APIs documentadas de z-order sobre a árvore Progman/WorkerW que o produto já precisa localizar. Portanto, o gate em Windows 11 real permanece obrigatório após mudanças relevantes do Explorer.

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
- A árvore Progman/WorkerW pode mudar entre versões do Explorer; há fallback e rebind, mas o comportamento de Win+D exige gate real.

### Risks
- Layered window + OLE: tratado no [ADR-0004](0004-ole-inbound-drop.md).
