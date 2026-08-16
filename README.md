<p align="center">
  <a href="https://antonio-abrantes.github.io/desktop-fences/">
    <img src="src/DesktopFences.App/Assets/app.png" alt="DesktopFences" width="96" height="96">
  </a>
</p>

<h1 align="center">DesktopFences</h1>

<p align="center">
  App nativo para Windows 11 que agrupa os ícones <strong>reais</strong> da área de trabalho em painéis translúcidos — <em>fences</em> — no espírito do Stardock Fences. Não é afiliado à Stardock.
</p>

<p align="center">
  <a href="https://antonio-abrantes.github.io/desktop-fences/">Site</a>
  ·
  <a href="https://github.com/antonio-abrantes/desktop-fences/releases">Download</a>
  ·
  <a href="https://github.com/antonio-abrantes/desktop-fences">GitHub</a>
</p>

## O problema

A área de trabalho acumula atalhos, pastas e arquivos soltos. Os clones open source (DeskFrame, NoFences, OpenFences) colocam esses itens em janelas flutuantes, mas **deixam os ícones originais no desktop**. O resultado é duplicata: o atalho continua lá e também aparece no painel.

O DesktopFences faz o que esses clones não fazem: tira o ícone real da pasta Desktop (move para o armazenamento do Fence; registry para Lixeira / Este computador / Rede), desenha a nossa grade por cima, e devolve o ícone ao desktop se você tirar o item da fence ou sair do app.

## O que o MVP 2 entrega

O hide/restore do MVP 1, mais várias fences no mesmo desktop:

- Vidro escuro translúcido (o wallpaper aparece atrás), cantos arredondados, atrás das janelas comuns.
- Arrastar do desktop ou do Explorer para dentro: um ou vários ícones; somem de lá, entram na grade; o ponteiro permanece a seta. Arrastar para fora devolve o ícone; o ghost acompanha o cursor. Arrastar **entre** fences muda o dono sem reaparecer o ícone real. Ícones de sistema (Este computador, Lixeira, Rede) usam o pictograma da Shell.
- Seleção, multi-seleção e reordenação dentro da fence.
- Mover só pela alça **⋮⋮** (ímã nas bordas da tela e nas outras fences ao soltar); redimensionar pelas bordas (ímã no fim do gesto); recolher para só a barra (▴); duplo clique no **texto** do título para renomear.
- Sempre **pelo menos uma** fence. Nas Configurações: criar, remover (nunca a última), alinhar o título, **cores**, e botões para abrir a pasta do `layout.json` e a dos ficheiros agrupados.
- **Idioma:** Sistema / Português / Inglês. Troca ao vivo. Título já gravado não muda.
- **Iniciar com o Windows** (o atalho usa o `.exe` desta pasta; se mover o programa, abra-o uma vez no sítio novo). Uma só instância.
- Bandeja: Pausar / Retomar / Configurações / Sobre / Sair. Pausar restaura os ícones reais.
- Persistência em `%AppData%\DesktopFences\layout.json`. Ficheiros das fences em `%LocalAppData%\DesktopFences\Items`.

Explorer reiniciado, DPI e Win+D foram validados no Windows 11; as Fases 3–5 estão fechadas e a release `v0.4.0` está preparada. A seguir no plano: reforçar a custódia dos itens do Desktop com store por item, recovery, transferência entre fences sem mover o payload e processamento em lote. O instalador vem depois desse gate. Duplo clique no vazio do desktop não faz parte deste ciclo.

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
git tag v0.4.0
git push origin v0.4.0
```

Isso publica um GitHub Release com zip portable `win-x64` e `win-arm64` (self-contained). Download: [Releases](https://github.com/antonio-abrantes/desktop-fences/releases).

## Licença

MIT. “Fences” é marca da Stardock.

Contribuição (humano ou agente): comece por [`AGENTS.md`](AGENTS.md).
