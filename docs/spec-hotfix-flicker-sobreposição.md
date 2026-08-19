# Spec — Hotfix: piscadelas quando fences se sobrepõem

> Recorte: duas fences layered WPF a sobrepor-se “piscam” no vidro. Causa provável no código actual: `SetWindowPos` de z-order em **todas** as fences, **todas as segundos**, mesmo quando a ordem já está correcta.
>
> **Status:** implementado no código (`v0.6.3`; skip corrigido na `v0.6.5`: host do Desktop abaixo / banda do Desktop acima); gate Windows 11 pendente.
>
> Fora: tirar o timer de 1 s, `HWND_TOPMOST`, `GWL_HWNDPARENT` no Progman, debounce de Win+D, CacheMode/BitmapCache em todas as fences.

---

## 1. Problema

Cada fence é `Window` WPF com `AllowsTransparency` (janela layered). O DWM recompõe o vidro quando a ordem Z ou o clip muda.

O arranjo actual (ADR-0003 / Fase 5):

1. Timer de ~1 s em `FenceHost.OnExplorerWatch` chama `EnsureDesktopSurvival` em **cada** fence.
2. Isso chama `DesktopWindowAnchor.KeepOnDesktop` → `SendBehindApps` → `TryPlaceAboveDesktop` → **`SetWindowPos` sempre**, com `insertAfter` calculado a caminhar `GW_HWNDPREV` desde Progman/WorkerW.
3. O shell hook (`SHELLHOOK`) também chama `KeepOnDesktop` na fence que recebe a mensagem.

`SetWindowPos` com mudança de Z, mesmo `SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE`, invalida a composição das janelas layered **por baixo e por cima**. Com duas fences a intersectar, o DWM redesenha a intersecção a 1 Hz (e de novo em cada shell hook) → piscadela.

Win+D / “Mostrar ambiente de trabalho” **precisa** deste reancoramento quando o Progman sobe por cima das fences. O utilizador pediu para **não** debouncear esse caminho. O que sobra, barato: não chamar `SetWindowPos` se a fence **já** está no sítio certo.

---

## 2. Decisão (impacto baixo)

Em `TryPlaceAboveDesktop`, **não** comparar o vizinho imediato com `insertAfter`. Essa regra falhava no Windows real:

- `SetWindowPos(hwnd, insertAfter)` coloca a fence **abaixo** de `insertAfter`; o vizinho de cima é `GW_HWNDPREV`, não `GW_HWNDNEXT`.
- `HWND_TOP` no Win32 é `0`. A regra “0 = mover sempre” fazia `SetWindowPos` em idle no caso mais comum (só fences acima do Desktop).
- Todas as fences visavam o **mesmo** `insertAfter` (a primeira janela que não é fence). A 1 Hz cada uma saltava para cima das irmãs → o DWM recompunha a sobreposição sem parar.

Skip correcto, sem reordenar irmãs:

1. Caminhar `GW_HWNDNEXT` (para baixo), saltando outras fences e `Progman`/`WorkerW`. Se o host actual do Desktop (`SHELLDLL_DefView`) aparecer **antes** de um app normal, a fence já está acima do wallpaper.
2. Caminhar `GW_HWNDPREV` (para cima), saltando outras fences. Se a primeira janela não-fence for `Progman`/`WorkerW`, o Win+D pôs o Desktop por cima → `SetWindowPos`.
3. Só então calcular `insertAfter` e chamar `SetWindowPos`. Irmãs acima ou abaixo **não** obrigam movimento.

Caminho Win+D: banda do Desktop acima da fence → `SetWindowPos` corre. Sem debounce, sem atraso.

`DwmGetWindowAttribute(CLOAKED)` e `IsIconic` no `KeepOnDesktop` mantêm-se: são baratos e não invalidam composição.

Não alterar o intervalo do timer. Não alterar a política “atrás dos apps, acima do desktop”.

---

## 3. Performance

| Hoje | Depois |
|---|---|
| N fences × 1 Hz × `SetWindowPos` + walk Z | Walk Z + 1× `GetWindow`; `SetWindowPos` só se a ordem mudou |
| Sobreposição: DWM recomposite a 1 Hz em idle | Idle: **zero** invalidação DWM por z-order |

O walk `GW_HWNDPREV` já existe; um `GetWindow` extra é desprezável. O ganho é **menos** trabalho do compositor, não mais.

Risco residual: se `GetWindow(GW_HWNDNEXT)` não coincidir com a semântica de `insertAfter` em algum desktop (WorkerW extra, fence TOPMOST), o código cai no `SetWindowPos` actual — regressão segura. Testar Win+D e overlap.

Se, no Windows 11 real, as piscadelas **continuarem** com este skip, a causa é outra (WPF `AllowsTransparency` + overlap sem z-change: blur/invalidação do próprio vidro). Isso seria **médio/alto** (CacheMode, deixar de ser layered, etc.) e **não** entra nesta spec. Reportar e parar.

---

## 4. Testes

Native/App.Tests (hwnds reais são frágeis em CI): extrair a regra pura se fizer sentido:

- `NeedsZOrderMove(hostBelow, bandAbove)` → false só com host abaixo e sem banda do Desktop por cima; true no Win+D e quando a fence está por baixo do wallpaper.

Gate Windows 11:

1. Duas fences a sobrepor ~40%, idle 10 s: o vidro **não** pisca a ~1 Hz.
2. Win+D duas vezes: fences voltam visíveis acima do wallpaper, atrás dos apps.
3. Arrastar uma fence por cima da outra: sem freeze; no máximo um redraw no `SetWindowPos` do drag (esperado).

---

## 5. Fora

- Remover ou alargar o timer de sobrevivência.
- `HWND_TOPMOST`.
- Parent no Progman (`GWL_HWNDPARENT`).
- Debounce do shell hook / Win+D.
- BitmapCache / desligar `AllowsTransparency`.
- Ordenar fences entre si de forma estável para além do skip (nice-to-have; não necessário para o flicker de idle).
