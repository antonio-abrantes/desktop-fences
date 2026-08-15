# DesktopFences

App nativo para Windows 11 que agrupa os ícones **reais** da área de trabalho em painéis translúcidos — *fences* — no espírito do Stardock Fences. Não é afiliado à Stardock.

Landing (MVP): [`docs/index.html`](docs/index.html) — abra no navegador.

## O problema

A área de trabalho acumula atalhos, pastas e arquivos soltos. Os clones open source (DeskFrame, NoFences, OpenFences) colocam esses itens em janelas flutuantes, mas **deixam os ícones originais no desktop**. O resultado é duplicata: o atalho continua lá e também aparece no painel.

O DesktopFences faz o que esses clones não fazem: esconde o ícone real do Explorer (`SysListView32`), desenha a nossa grade por cima, e devolve o ícone ao desktop se você tirar o item da fence ou sair do app.

## O que o MVP 1 entrega

Uma fence usável no dia a dia:

- Vidro escuro translúcido (o wallpaper aparece atrás), cantos arredondados, atrás das janelas comuns.
- Arrastar do desktop ou do Explorer para dentro: um ou vários ícones (seleção do Explorer); somem de lá, entram na grade; o ponteiro permanece a seta.
- Arrastar para fora devolve o ícone ao desktop; o ghost acompanha o cursor nos dois sentidos.
- Seleção, multi-seleção e reordenação dentro da fence.
- Mover só pela alça **⋮⋮**; redimensionar pelas bordas (a faixa direita some quando há barra de rolagem).
- Recolher para só a barra (▴); duplo clique no **texto** do título para renomear (Enter ou clique fora grava; Escape cancela); duplo clique na barra vazia recolhe/expande.
- Título longo com reticências; scrollbar fina escura.
- Persistência em `%AppData%\DesktopFences\layout.json` (posição, tamanho, título, itens).
- Bandeja: Pausar / Retomar / Sair. Pausar restaura os ícones reais.
- Ícone próprio no `.exe`, atalho e bandeja.

Ainda é **uma** fence. Várias fences, tela de configurações e empurrar vizinhos ao expandir estão no [pós-MVP 1](docs/pos-mvp1.md).

## Requisitos

- Windows 10/11 (alvo: Windows 11)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

Rode **sem** administrador (`asInvoker`). Elevar o processo só complica o acesso ao `explorer.exe`.

## Build e execução

```powershell
dotnet build DesktopFences.sln
dotnet run --project src/DesktopFences.App
```

Ou abra `DesktopFences.sln` e dê F5 (perfil Debug). O binário fica em:

`src/DesktopFences.App/bin/Debug/net8.0-windows/DesktopFences.exe`

Fecha o app pela bandeja antes de gerar um build novo — o `.exe` em execução trava as DLLs.

## Testes (domínio, sem Win32)

```powershell
dotnet test DesktopFences.sln
```

## Release

O GitHub Action **não** roda em push de branch. Só em tag `v*`:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

Isso publica um GitHub Release com zip portable `win-x64` e `win-arm64` (self-contained).

## Licença

MIT. “Fences” é marca da Stardock.

Contribuição (humano ou agente): comece por [`AGENTS.md`](AGENTS.md).
