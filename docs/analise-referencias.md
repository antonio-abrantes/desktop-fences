# Análise das referências open source

Três clones MIT do Stardock Fences foram estudados para decidir o que reaproveitar como **padrão** (não como código copiado). Nenhum dos três implementa o comportamento que queremos no MVP: esconder os ícones reais do desktop e desenhar a nossa grade.

| Projeto | Stack | Estrelas (aprox.) | Modelo de conteúdo | UI |
|---|---|---|---|---|
| [DeskFrame](https://github.com/PinchToDebug/DeskFrame) | C# WPF + WPF-UI | ~500 | Frame = portal de uma pasta | Melhor visual; customização rica |
| [NoFences](https://github.com/Twometer/NoFences) | C# WinForms, .NET Framework 4.8 | ~670 | Lista de paths (referências) | Blur Aero simples, visual datado |
| [OpenFences](https://github.com/chrisdfennell/OpenFences) | C# WPF .NET 8 | ~9 | Pasta backing + `.lnk` | Estrutura boa; chrome quadrado, transparência fraca |

Nenhum deles é “Fences de verdade”. Todos deixam os ícones originais no desktop (issue clássica do NoFences: *“Icons still appearing on my desktop”*). OpenFences chega a documentar: *Auto-import creates shortcuts; it does not move or delete your actual desktop items.*

---

## 1. DeskFrame — o que pegar

**Abordagem:** cada frame é uma janela WPF que mostra o conteúdo de uma pasta. Não fala com `SysListView32`. É um “portal de pasta”, não um agrupador de ícones do desktop.

**Pegar**

- Barra de título usável: arrastar, duplo clique para roll-up, scroll na title bar para z-order.
- Customização por frame (cor de fundo com alpha, borda, texto, tamanho de ícone com Ctrl+scroll).
- Busca instantânea com o mouse sobre o frame (Passo 4, não MVP).
- Frames sticky/lock e “double click no desktop esconde os frames”.
- Consciência de Virtual Desktop (lib `VirtualDesktop`) — Passo 3+.
- Lição negativa: `DeskFrameWindow.xaml.cs` tem ~190 KB. **Não repetir**. Code-behind fino, lógica em serviços.

**Rejeitar para o MVP**

- Modelo “frame = pasta”. No nosso produto a fence agrupa **ícones que já estão no desktop**.
- Dependência WPF-UI no Passo 0. Podemos adotar no Passo 4 se o XAML próprio não chegar no nível visual. Não bloquear a POC nela.

---

## 2. NoFences — o que pegar

**Abordagem:** WinForms sem borda, blur via `SetWindowCompositionAttribute` (`ACCENT_ENABLE_BLURBEHIND`), itens = paths em XML. Também **não** esconde ícones reais.

**Pegar**

- **Ancorar no desktop sem `SetParent` em WorkerW.** NoFences usa `GWL_HWNDPARENT` → Progman. Nós avaliamos e **não usamos**: impedia o OLE drop. MVP 1: `HWND_BOTTOM` + `WS_EX_TOOLWINDOW`.
- Blur real com `SetWindowCompositionAttribute` em vez de só `AllowsTransparency` + brush alfa.
- `PreventMinimize` (tirar `WS_MINIMIZEBOX` / `WS_MAXIMIZEBOX`) para a fence não sumir com Win+D da forma errada.
- Cache de thumbnails com limite de concorrência (Passo 1, quando formos desenhar ícones nossos).
- Isolar Win32 numa pasta/projeto próprio (`Win32/` no original → o nosso `DesktopFences.Native`).

**Rejeitar**

- WinForms e .NET Framework 4.8.
- `SetWindowLong` 32-bit (`ToInt32()` no handle). Em x64 isso é bug; usamos `SetWindowLongPtr`.
- XML como formato de layout. JSON versionado é o nosso contrato.
- Não tratar hide de ícones reais — é exatamente o buraco que o nosso produto fecha.

---

## 3. OpenFences — o que pegar

**Abordagem:** WPF .NET 8, fences como janelas, conteúdo em pastas `Desktop\Fences\<nome>` com atalhos. Controller + tray. Melhor fatiamento de projeto entre os três (`Models/`, `Services/`, `Themes/`, `Dialogs/`), mas a UI é o ponto fraco que o usuário já apontou: botões crus, transparência ruim, sem radius.

**Pegar**

- Fatiamento Models / Services / Themes — no nosso caso viram projetos Core / Native / App.
- Persistência JSON em `%AppData%\…\config.json` (nós: `%AppData%\DesktopFences\layout.json`).
- `SHGetFileInfo` para extrair ícone (Passo 1).
- Monitor de duplo clique no desktop via `WH_MOUSE_LL`, **com o trabalho pesado fora do hook thread** (senão o Windows derruba o hook). Passo 2.
- Tray + “run at startup” (Passo 4).
- Pipeline de release: `dotnet publish` self-contained + artefato zip/MSI. O nosso workflow no Passo 0 gera o zip; MSI fica para o Passo 4.
- Roadmap deles confirma a nossa prioridade visual: Acrylic/Mica, roll-up, snap, WorkerW parenting mais forte.

**Rejeitar**

- Esconder **todos** os ícones do desktop (toggle global) em vez de esconder só os que pertencem à fence.
- Criar `.lnk` numa pasta backing como modelo principal. Podemos oferecer “importar como atalho” depois; o MVP mexe no ícone real.
- Chrome quadrado, `AllowsTransparency` sem DWM corner preference, botões default do WPF.
- Duplicar pastas `Models/` e `models/` (eles têm os dois). Um namespace, um diretório.

---

## Decisão combinada (o nosso produto)

```
Visual          ← chrome próprio (vidro alfa + radius 8); acrylic DWM ficou de fora no W11
Ancoragem       ← z-order HWND_BOTTOM (GWL_HWNDPARENT no Progman quebrou o OLE)
Arquitetura     ← OpenFences (camadas + JSON + services), com Native isolado de verdade
Conteúdo        ← nenhum dos três: SysListView32 hide/restore + grade WPF própria
```

Conflito conhecido (já visto em tentativas de WorkerW + WPF): `AllowsTransparency="True"` + `SetParent` no WorkerW frequentemente **não desenha** a janela. O MVP 1 usa `AllowsTransparency=True` com vidro alfa (não composition attribute) e z-order `HWND_BOTTOM`, sem `GWL_HWNDPARENT` no Progman. Detalhe em `docs/adr/0003-ancoragem-desktop-e-acrylic.md` e `docs/adr/0004-ole-inbound-drop.md`.
