# DesktopFences — Spec técnica

App Windows 11 que agrupa ícones **reais** da área de trabalho em fences retangulares, com fundo translúcido, cantos arredondados, arrastáveis e persistentes. Inspirado no Stardock Fences; **não afiliado**.

**Stack:** C# / .NET 8 / WPF + P/Invoke. Decisão em [ADR-0001](adr/0001-stack-csharp-wpf.md).

**Princípio:** três camadas, para o ponto frágil (Explorer) poder quebrar sem reescrever UI.

| Projeto | Responsabilidade | Pode conhecer Win32? |
|---|---|---|
| `DesktopFences.Core` | Modelos, hit-test, JSON, paths | Não |
| `DesktopFences.Native` | SysListView32, COM Shell, OLE drop, DWM, âncora | Sim, e só aqui |
| `DesktopFences.App` | XAML, fence, bandeja, ghost | Só via serviços Native |

**Estado:** Fases 1–6 e hotfix `v0.5.1` fechados. A Fase 7 está implementada para a `v0.6.0`, com instaladores por usuário x64/ARM64, manutenção segura e desinstalação; gate manual no Windows 11 pendente. O hotfix `v0.6.3` acrescenta arranque multi-monitor, layout padrão, flicker em sobreposição e remover fence com confirmação. A `v0.6.4` acrescenta **Nova fence** no menu do desktop (instalação). A `v0.6.5` corrige o skip de z-order e o inbound ao mover fence. A spec de upgrade seguro do instalador está documentada e **fora** desta versão. Duplo clique no desktop, packs de tema e empurrar vizinhos estão **fora** deste ciclo. Detalhe: [plano-implementacao.md](plano-implementacao.md).

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
| `LVM_SETITEMPOSITION` | `MAKELPARAM(x,y)` | SendMessage direto (só reposicionar ícone **visível**, no ejetar) |
| `LVM_GETITEMPOSITION` | ponteiro para `POINT` | **alocar no explorer.exe** |
| `LVM_GETITEMTEXTW` | ponteiro para `LVITEM` + buffer | **alocar no explorer.exe** |

A Native aloca o `POINT` / `LVITEM` remoto. Não confiar em marshalling automático dessas mensagens.

Fluxo de hide:

1. Resolver o item: pasta Desktop (`DesktopPaths`) ou CLSID do namespace (`::{GUID}` / `shell:`).
2. Atalho, ficheiro ou pasta: **mover** para `%LocalAppData%\DesktopFences\Items\{ItemId}\{storageName}`. Lixeira / Este computador / Rede: registry `HideDesktopIcons` com o CLSID.
3. Persistir `itemId`, `kind`, `storageName` relativo e `originalPath`; o path absoluto do store é derivado. Um `SHChangeNotify` no fim do lote.
4. Desenhar a grade WPF (`SHGetFileInfo` no path do store).
5. Restaurar (mover de volta / registry `0`) no shutdown, Pausar, ejetar, remover fence.

**Não** usar `FILE_ATTRIBUTE_HIDDEN` nem coordenadas no ListView. Itens ocultos no Explorer não voltam a pintar o que já não está na pasta Desktop.

O store por item é o contrato vigente da Fase 6. O movimento físico fica coberto por journal durável e commit atômico do layout; transferir entre fences não move o payload. Contrato completo em [spec-fase-6-custodia-desktop.md](spec-fase-6-custodia-desktop.md).

---

## 2. Funcionalidades por área

### 2.1 Native — ícones

- Localizar `SysListView32` (Progman + WorkerW).
- Enumerar: índice, nome, posição. Um `OpenProcess` por varredura, não por ícone.
- Esconder: mover para o store do Fence / registry (ADR-0002). Reposicionar no ListView só depois de o ícone voltar ao Desktop.
- `SHGetFileInfo` para bitmap PNG.
- Resolver nome → path (`DesktopPaths`: Desktop do usuário + público).
- Reconectar após restart do Explorer — Fase 5.
- DPI: manifest Per-Monitor V2; clip/z-order no `DpiChanged`.

### 2.2 Native — chrome da janela

Ver [ADR-0003](adr/0003-ancoragem-desktop-e-acrylic.md).

- `AllowsTransparency=True`; vidro = brush alfa no `Border` (`#A80C0C12`), `CornerRadius=8`.
- **Não** usar `SetWindowCompositionAttribute` enquanto a janela for layered (cinza opaco no W11 testado).
- Z-order: janela top-level + `WS_EX_TOOLWINDOW`; a Native encontra o host `SHELLDLL_DefView` e coloca as fences imediatamente acima da banda Progman/WorkerW e abaixo da primeira janela de aplicativo. `HWND_BOTTOM` é somente fallback. **Não** usar `GWL_HWNDPARENT` → Progman (isso impedia o OLE drop e acoplaria a vida da fence ao Explorer).
- Sem minimize/maximize box.

### 2.3 App — fence

Uma janela por fence: título editável (duplo clique **no texto**); Enter, LostFocus, clique fora do campo **grava**; Escape cancela. Alinhamento do título: esquerda (padrão) ou centro, nas Configurações — por fence, com checkbox para aplicar a todas (vale também para cores). Alça ⋮⋮ para mover; ao soltar (e ao terminar o resize), ímã nas bordas da área de trabalho e nas arestas de outras fences, sem empurrar vizinhos. Roll-up ▴; resize ao vivo; grade WrapPanel; scrollbar custom; menu de contexto (recolher, diagnóstico). Remover fence só nas Settings.

O `App` usa `FenceHost`: N instâncias de `FenceWindow`, um único `layout.json`. Aparência por fence (fundo, borda, header, texto + alfa); radius 8 e `AllowsTransparency` continuam fixos. Packs de tema nomeados estão fora deste ciclo. Grade: atalho/pasta/ficheiro → `SHGetFileInfo` no path (igual ao MVP 1). Só se **não** existir no disco (Lixeira, Este computador, Rede) é que se usa o PIDL do `IShellFolder` do desktop. Abrir esses itens só via `::{CLSID}` / `shell:` — não se substitui o `Process.Start` de um `.lnk`.

### 2.4 Drag & drop

O WPF `AllowDrop` **não** funciona de forma confiável nesta janela layered: o Explorer não acerta o `IDropTarget` (thumbnail OLE no hotspot) e pinta o cursor de proibido.

Contrato vigente — [ADR-0004](adr/0004-ole-inbound-drop.md):

- **Inbound:** `RegisterDragDrop` nativo + alvo OLE minúsculo no cursor; ghost WPF próprio (com `+N` se a seleção do desktop tiver vários ícones); cursor de “proibido” substituído pela seta só enquanto o ponteiro está sobre a fence. A seleção do `SysListView32` é lida no início do arraste (`LVM_GETNEXTITEM` / `LVNI_SELECTED`).
- **Outbound / reorder:** não usamos `DoDragDrop` do Explorer; hook `WH_MOUSE_LL` + `DragGhostWindow` (click-through).
- Soltar na fence esconde o ícone real e adiciona na grade; soltar fora restaura o primeiro ícone no ponto do cursor e distribui uma seleção múltipla em células próximas. Como a notificação da Shell é assíncrona, a Native repete por um intervalo curto durante essa ejeção e ao repor posições originais em Pausar/Sair.
- Entre fences, soltar no **corpo** muda somente ownership/ordem em uma cópia do layout; após um commit atômico bem-sucedido a UI é atualizada. `ItemId`, store e metadados de restore permanecem iguais, com zero I/O de payload. Barra de título / fence recolhida preservam o comportamento anterior.

### 2.5 Persistência

O formato vigente é `version: 2`. `%AppData%\DesktopFences\layout.json` usa temporário, flush durável, validação, substituição atômica e `layout.json.bak`. Operações físicas usam `%LocalAppData%\DesktopFences\Transactions\{OperationId}.json` e recovery antes da abertura das fences. A leitura v1 existe apenas para migração recuperável; todo commit novo grava v2. Se um principal v1 tiver sido produzido por downgrade e o backup continuar em v2, o documento v2 tem precedência.

O destino de saída é sempre o Desktop do usuário. Um item originalmente vindo do Desktop Público não volta para a pasta pública, pois o processo `asInvoker` pode não ter permissão de escrita ali; ele é devolvido ao Desktop gravável do usuário e continua visualmente no mesmo ambiente de trabalho. Nenhum caminho externo ao Desktop é usado como destino de restore.

Um snapshot independente e atômico das posições fica em `%LocalAppData%\DesktopFences\Recovery\desktop-snapshot.json`. Ele é gravado antes de qualquer recovery, migração ou retomada de custódia. A release também inclui `DesktopFences.Recovery.exe`, que faz recuperação conservadora por cópia, preserva o store e só desativa referências ativas depois da cópia completa. A organização atual do Desktop prevalece por padrão; reaplicar posições antigas exige ação explícita. Contrato e limites: [hotfix-v0.5.1-recuperacao-emergencia.md](hotfix-v0.5.1-recuperacao-emergencia.md).

No arranque após uma liberação normal, um item físico ausente tanto no store quanto no Desktop é tratado como removido externamente somente quando as duas raízes podem ser inspecionadas com segurança. Nesse caso, apenas sua referência é retirada por commit atômico e os demais itens retomam a custódia normalmente. Falta de acesso, path incompatível ou qualquer estado ambíguo preserva os metadados e mantém a falha segura de inicialização.

```json
{
  "version": 2,
  "revision": 12,
  "uiLanguage": "system",
  "fences": [
    {
      "id": "guid",
      "title": "Trabalho",
      "titleAlignment": "left",
      "theme": {
        "fill": "#A80C0C12",
        "border": "#4DFFFFFF",
        "header": "#33000000",
        "text": "#F2FFFFFF"
      },
      "x": 100, "y": 100, "width": 420, "height": 280,
      "monitorDeviceName": "\\\\.\\DISPLAY1",
      "collapsed": false,
      "items": [
        { "itemId": "guid-do-item", "kind": "stored", "name": "Relatorio.docx", "storageName": "Relatorio.docx", "originalPath": "C:\\\\Users\\\\…\\\\Desktop\\\\Relatorio.docx", "originalX": 12, "originalY": 48 }
      ]
    }
  ]
}
```

Coordenadas da fence em **DIPs** WPF; posições de ícone em **pixels** do ListView. Conversão é responsabilidade da App (DPI). Core só persiste números.

### 2.6 Fora do que já está no código (ciclo em `plano-implementacao.md`)

- Fase 6 e hotfix `v0.5.1`: fechados, com custódia transacional, recuperação independente e snapshot de posições.
- Fase 7 / `v0.6.0`: instaladores Inno Setup x64/ARM64, idioma inicial, upgrade, desinstalação segura e reconciliação conservadora de itens apagados com o app fechado implementados; gate manual pendente. Contrato em [spec-fase-7-instalador.md](spec-fase-7-instalador.md).
- Hotfix `v0.6.3`: arranque multi-monitor, layout padrão de novas fences, flicker em sobreposição e remover fence com confirmação + barreira; gates Windows 11 pendentes. Specs em `docs/spec-hotfix-*.md` e `docs/spec-layout-padrao-nova-fence.md`.
- `v0.6.4`: **Nova fence** no menu de contexto do desktop (`Directory\Background`, `DesktopBackground\shell`, `ShellNew\Command` sem `NullFile`, `--create-fence`). Gate Windows 11 pendente. Contrato em [spec-nova-fence-menu-novo.md](spec-nova-fence-menu-novo.md).
- `v0.6.5`: skip de `SetWindowPos` só com host do Desktop abaixo e sem banda do Desktop acima; clique que começa numa fence não dispara inbound; layout das Configurações. Contrato do skip em [spec-hotfix-flicker-sobreposição.md](spec-hotfix-flicker-sobreposição.md).
- Próxima versão (não nesta tag): [spec-hotfix-instalador-upgrade-seguro.md](spec-hotfix-instalador-upgrade-seguro.md) — pronta, não autorizada.
- **Fora do ciclo:** empurrar a fence de baixo ao expandir; duplo clique no vazio do desktop cria fence; packs de tema.
- **Stand-by da auditoria:** itens externos ao Desktop, OneDrive/redirected Desktop, progresso/cancelamento, ampliação geral de `IFileOperation` e demais melhorias não selecionadas para a Fase 6.

---

## 3. Riscos

| Risco | Mitigação |
|---|---|
| Árvore Progman/WorkerW muda | 100% em Native; fallback duplo |
| Crash a meio do move | Journal durável, revisão antes/depois, compensação e recovery idempotente antes da UI |
| Versão antiga rebaixa o layout e referencia payload incorreto | Preferir backup de schema mais novo, validar nome do payload v1, impedir stem vazio e oferecer recuperação independente por cópia |
| Aplicativo principal não inicia | `DesktopFences.Recovery.exe` usa snapshot próprio, preserva store/conflitos e gera recibo da sessão |
| Item do Desktop Público não pode ser restaurado sem elevação | Restaurar no Desktop do usuário; nunca pedir administrador nem deixar o item bloquear o lote |
| Explorer materializa o ícone depois do `SHChangeNotify` | Repetir o posicionamento por janela curta após ejeção/Pausar/Sair; usar o destino físico real e deixar o Explorer resolver eventual colisão da grade |
| DPI por monitor | DIPs vs pixels documentados; `PerMonitorV2` + `DpiChanged` (Fase 5) |
| Restart do Explorer | Ficheiro já não está no Desktop; reaplicar só CLSID de namespace |
| Win+D eleva Progman/WorkerW acima da fence sem marcá-la oculta | Reancorar o grupo de fences acima da banda do Desktop por shell hook + verificação de sobrevivência; gate Windows 11 obrigatório |
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
├── installer/DesktopFences.iss      ← setup/desinstalação x64 e ARM64
├── docs/
│   ├── index.html                   ← landing; GitHub Pages em /docs
│   ├── SESSION-HEADER.md
│   ├── SPEC.md
│   ├── plano-implementacao.md
│   ├── spec-fase-6-custodia-desktop.md
│   ├── plano-fase-6-custodia-desktop.md
│   ├── resultado-fase-6-custodia-desktop.md
│   ├── hotfix-v0.5.1-recuperacao-emergencia.md
│   ├── spec-fase-7-instalador.md
│   ├── plano-fase-7-instalador.md
│   ├── auditoria-fluxo-itens-performance-release.md
│   ├── pos-mvp1.md
│   └── adr/
├── src/
│   ├── DesktopFences.Core/
│   ├── DesktopFences.Native/
│   ├── DesktopFences.App/           ← Assets/app.ico
│   └── DesktopFences.Recovery/      ← restauração independente por um clique
└── tests/
    ├── DesktopFences.Core.Tests/
    └── DesktopFences.App.Tests/
```

---

## 5. Fora de escopo (até alguém abrir ADR)

- Afiliação ou engenharia reversa do binário Stardock Fences.
- Sincronizar layout na nuvem.
- Linux / macOS.
- Injeção de DLL no Explorer.
- UI em WinUI 3.
