# DesktopFences — Spec técnica

App Windows 11 que agrupa ícones **reais** da área de trabalho em fences retangulares, com fundo translúcido, cantos arredondados, arrastáveis e persistentes. Inspirado no Stardock Fences; **não afiliado**.

**Stack:** C# / .NET 8 / WPF + P/Invoke. Decisão em [ADR-0001](adr/0001-stack-csharp-wpf.md).

**Princípio:** três camadas, para o ponto frágil (Explorer) poder quebrar sem reescrever UI.

| Projeto | Responsabilidade | Pode conhecer Win32? |
|---|---|---|
| `DesktopFences.Core` | Modelos, hit-test, JSON, paths | Não |
| `DesktopFences.Native` | SysListView32, COM Shell, OLE drop, DWM, âncora | Sim, e só aqui |
| `DesktopFences.App` | XAML, fence, bandeja, ghost | Só via serviços Native |

O que os clones fazem de errado (e nós não): ver [analise-referencias.md](analise-referencias.md).

**Estado:** MVP 1 fechado — uma fence. Várias fences e o resto: [pos-mvp1.md](pos-mvp1.md).

---

## 1. Como o desktop realmente funciona

```
Progman ("Program Manager")
 └── SHELLDLL_DefView
      └── SysListView32 "FolderView"   ← ícones

-- ou, em várias builds / multi-monitor / wallpaper por tela --

WorkerW
 └── SHELLDLL_DefView
      └── SysListView32 "FolderView"
```

A busca tenta Progman primeiro e cai para `EnumWindows` procurando `SHELLDLL_DefView`. Detalhe em [ADR-0002](adr/0002-esconder-icones-reais.md).

| Mensagem | lParam | Cross-process |
|---|---|---|
| `LVM_GETITEMCOUNT` | — | SendMessage direto |
| `LVM_SETITEMPOSITION` | `MAKELPARAM(x,y)` | SendMessage direto |
| `LVM_GETITEMPOSITION` | ponteiro para `POINT` | **alocar no explorer.exe** |
| `LVM_GETITEMTEXTW` | ponteiro para `LVITEM` + buffer | **alocar no explorer.exe** |

A Native aloca o `POINT` / `LVITEM` remoto. Não confiar em marshalling automático dessas mensagens.

Fluxo de hide (MVP 1):

1. Ler posições.
2. Hit-test Core: ícone ∩ retângulo da fence (célula ~76×92 px, ajustável).
3. Guardar posição original em memória e no JSON.
4. Mover para `(-32000, -32000)`.
5. Desenhar a grade WPF (`SHGetFileInfo`).
6. Restaurar no shutdown, no Pausar, ao remover o item e se o hide falhar.

**Limitação:** “Auto organizar ícones” / “Alinhar à grade” no menu do desktop desfaz o hide. Detectar e avisar é pós-MVP 1. Não escrever no registry.

---

## 2. Funcionalidades por área

### 2.1 Native — ícones

- Localizar `SysListView32` (Progman + WorkerW).
- Enumerar: índice, nome, posição. Um `OpenProcess` por varredura, não por ícone.
- Mover / esconder / restaurar.
- `SHGetFileInfo` para bitmap PNG.
- Resolver nome → path (`DesktopPaths`: Desktop do usuário + público).
- Reconectar após restart do Explorer — pós-MVP 1.
- DPI: manifest Per-Monitor V2.

### 2.2 Native — chrome da janela

Ver [ADR-0003](adr/0003-ancoragem-desktop-e-acrylic.md).

- `AllowsTransparency=True`; vidro = brush alfa no `Border` (`#A80C0C12`), `CornerRadius=8`.
- **Não** usar `SetWindowCompositionAttribute` enquanto a janela for layered (cinza opaco no W11 testado).
- Z-order: `HWND_BOTTOM` + `WS_EX_TOOLWINDOW`. **Não** `GWL_HWNDPARENT` → Progman (isso impedia o OLE drop).
- Sem minimize/maximize box.

### 2.3 App — fence (MVP 1)

Uma janela: título editável (duplo clique **no texto**); Enter, LostFocus, clique fora do campo (barra, grade ou desktop) **grava** o que está no TextBox; Escape cancela e volta o título anterior. Alça ⋮⋮ para mover; roll-up ▴; resize ao vivo com thumbs (faixa leste some se a scrollbar vertical estiver visível); grade WrapPanel; scrollbar custom; menu de contexto (recolher, diagnóstico, fechar).

Visual da fence está **travado** até pedido de tema: não alterar alfa, radius nem `AllowsTransparency` “para melhorar”.

### 2.4 Drag & drop

O WPF `AllowDrop` **não** funciona de forma confiável nesta janela layered: o Explorer não acerta o `IDropTarget` (thumbnail OLE no hotspot) e pinta o cursor de proibido.

Contrato vigente — [ADR-0004](adr/0004-ole-inbound-drop.md):

- **Inbound:** `RegisterDragDrop` nativo + alvo OLE minúsculo no cursor; ghost WPF próprio (com `+N` se a seleção do desktop tiver vários ícones); cursor de “proibido” substituído pela seta só enquanto o ponteiro está sobre a fence. A seleção do `SysListView32` é lida no início do arraste (`LVM_GETNEXTITEM` / `LVNI_SELECTED`).
- **Outbound / reorder:** não usamos `DoDragDrop` do Explorer; hook `WH_MOUSE_LL` + `DragGhostWindow` (click-through).
- Soltar na fence esconde o ícone real e adiciona na grade; soltar fora restaura.
- Entre fences: ainda não existe (uma fence só).

### 2.5 Persistência

Arquivo único `%AppData%\DesktopFences\layout.json` (`LayoutStore`). O schema **já aceita lista de fences**; o MVP 1 só instancia uma.

```json
{
  "version": 1,
  "fences": [
    {
      "id": "guid",
      "title": "Trabalho",
      "x": 100, "y": 100, "width": 420, "height": 280,
      "monitorDeviceName": "\\\\.\\DISPLAY1",
      "collapsed": false,
      "items": [
        { "name": "Relatorio.docx", "path": "C:\\\\Users\\\\…\\\\Desktop\\\\Relatorio.docx", "originalX": 12, "originalY": 48 }
      ]
    }
  ]
}
```

Coordenadas da fence em **DIPs** WPF; posições de ícone em **pixels** do ListView. Conversão é responsabilidade da App (DPI). Core só persiste números.

### 2.6 Ainda não são MVP 1

- Várias fences; tela de configurações.
- Duplo clique em vazio do desktop → cria fence.
- Snap / empurrar fence de baixo ao expandir.
- Auto-hide com fade; temas; iniciar com o Windows.

---

## 3. Riscos

| Risco | Mitigação |
|---|---|
| Árvore Progman/WorkerW muda | 100% em Native; fallback duplo |
| Auto-arrange desfaz hide | Avisar (pós-MVP 1); não desligar registry agora |
| DPI por monitor | DIPs vs pixels documentados; `WM_DPICHANGED` depois |
| Restart do Explorer | Redetectar handle e reaplicar hide (pós-MVP 1) |
| Defender / `WriteProcessMemory` | `asInvoker`; signing só quando houver release assinado |
| Acrylic + WPF layered | ADR-0003: vidro alfa, sem composition attribute |
| OLE inbound + janela layered | ADR-0004: alvo no cursor + override da seta |
| `GWL_HWNDPARENT` no Progman | Não usar; z-order via `HWND_BOTTOM` |

---

## 4. Estrutura do repositório

```
DesktopFences/
├── DesktopFences.sln
├── Directory.Build.props
├── AGENTS.md
├── README.md
├── .github/workflows/release.yml    ← somente tags v*
├── docs/
│   ├── index.html                   ← landing; GitHub Pages em /docs
│   ├── SESSION-HEADER.md
│   ├── SPEC.md
│   ├── plano-implementacao.md
│   ├── pos-mvp1.md
│   ├── analise-referencias.md
│   └── adr/
├── src/
│   ├── DesktopFences.Core/
│   ├── DesktopFences.Native/
│   └── DesktopFences.App/           ← Assets/app.ico
└── tests/
    └── DesktopFences.Core.Tests/
```

---

## 5. Fora de escopo (até alguém abrir ADR)

- Afiliação ou engenharia reversa do binário Stardock Fences.
- Sincronizar layout na nuvem.
- Linux / macOS.
- Injeção de DLL no Explorer.
- UI em WinUI 3.
