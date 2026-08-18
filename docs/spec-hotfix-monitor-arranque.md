# Spec — Hotfix: ecrãs em falta no arranque

> Recorte: ao logon, o Windows pode arrancar o DesktopFences (Run `HKCU`) **antes** dos ecrãs secundários estarem enumerados. As fences gravadas noutro monitor ficam fora do ecrã visível.
>
> **Status:** implementado no código (`v0.6.3`); gate Windows 11 pendente.
>
> Fora: empurrar vizinhos, packs de tema, duplo clique no desktop, debounce de Win+D, espera global de N segundos mesmo quando todos os ecrãs já estão prontos.

---

## 1. Problema

O Windows **não** espera pelos displays extra antes de lançar apps do Run. Com 3 monitores, o `EnumDisplayMonitors` no `Start()` pode devolver 1. O `layout.json` guarda `Left`/`Top` em DIP no espaço virtual; o WPF coloca a janela nessas coordenadas mesmo que o monitor ainda não exista. Quando o ecrã liga, a janela **não** volta sozinha.

`FenceState.MonitorDeviceName` já existe no modelo (schema v2) e **não é gravado** em `FenceWindow.CaptureState()`. A colocação actual é só `Left`/`Top`.

---

## 2. Decisão

1. **Persistir o ecrã** de cada fence: `MONITORINFOEX.szDevice` (ex. `\\.\DISPLAY2`) no campo já existente `monitorDeviceName`. Sem bump de `version`.
2. **No arranque**, se o dispositivo gravado **já** está em `EnumDisplayMonitors`, posicionar de imediato (caminho actual). Sem espera.
3. Se o dispositivo **falta**, a fence **nasce visível no ecrã principal** (clamp à `rcWork` do monitor primário) e entra em **espera limitada**. Não se mata a janela nem se apaga do JSON.
4. Enquanto espera: a cada ~500 ms, reconsultar monitores. Se o `szDevice` aparecer, **mover** a janela para o `Left`/`Top` gravados (ainda válidos no espaço virtual).
5. Se o timeout esgotar e o ecrã não voltar: **ficar** no ecrã disponível (já está no primário). Não destruir. No próximo save, gravar o novo `monitorDeviceName` + posição.
6. **Nunca** `Thread.Sleep` no UI thread. `DispatcherTimer` só para fences cujo monitor falta.

Timeout sugerido: **8 s**. Constante única (`MonitorWaitTimeout`). Não alargar sem evidência.

---

## 3. Identidade do ecrã

- Chave: `szDevice` de `MONITORINFOEX` (`user32 GetMonitorInfoW`). A struct actual `MONITORINFO` **não** tem `szDevice`; a Native passa a expor `MONITORINFOEX` (CCHDEVICENAME = 32).
- Coordenadas: `Left`/`Top` continuam DIP no espaço virtual WPF. O nome do dispositivo só decide **esperar vs clamp**.
- Se `monitorDeviceName` for null/vazio (layouts antigos): comportamento actual — colocar já, sem espera. No primeiro `CaptureState` após o hotfix, preencher o campo.

`EnumDisplayMonitors` + `GetMonitorInfo` é barato (dezenas de µs). Polling 16 vezes em 8 s, só no arranque e só se faltar ecrã: impacto de performance **desprezável**.

---

## 4. Clamp (ecrã indisponível)

Reutilizar a ideia de `FenceLayoutRules.ClampToWorkArea`: a fence tem de caber na `rcWork` de **algum** monitor vivo (preferir o primário `MONITORINFOF_PRIMARY`). Não centrar a menos que a posição gravada caia toda fora.

Invariante: **nenhuma fence fica com `Visibility=Collapsed` nem é omitida do `Start()`** por falta de ecrã.

---

## 5. Runtime (não é só Start)

Se o utilizador **desligar** um monitor com o app já aberto, o Windows costuma empurrar janelas. Não é obrigatório neste hotfix. Obrigatório é:

- `CaptureState` gravar `monitorDeviceName` em todo save (drag, resize, fecho).
- Arranque + wait + fallback.

Ligar um monitor a meio da sessão **não** exige reposicionar fences já clampadas (evita saltos). Só o wait do Start move de volta para o sítio gravado.

---

## 6. Testes (sem hardware multi-monitor)

Core / App.Tests:

- Fence com `monitorDeviceName` conhecido e lista de devices que o inclui → `ShouldWait = false`.
- Device em falta → `ShouldWait = true`; após timeout, `ClampToPrimary`.
- Device aparece no 3.º poll → alvo = `Left`/`Top` originais.
- `monitorDeviceName` null → nunca espera.
- `CaptureState` preenche o campo quando a Native devolve um nome.

Gate Windows 11: 3 ecrãs, “Iniciar com o Windows”, desligar HDMI, logon, ligar cabo dentro de 5 s → fence volta ao sítio; ligar depois de 8 s → fence no primário, visível.

---

## 7. Fora

- Esperar sempre N segundos mesmo com 1 monitor estável.
- Guardar índice `DISPLAY1/2/3` em vez de `szDevice` (o Windows troca índices).
- `HWND_TOPMOST` / mudar z-order por causa disto.
- Alterar o timer de 1 s do `KeepOnDesktop`.
