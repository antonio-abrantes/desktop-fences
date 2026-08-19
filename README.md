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
  <a href="https://antonio-abrantes.github.io/desktop-fences/#demo">Vídeo</a>
  ·
  <a href="https://github.com/antonio-abrantes/desktop-fences/releases">Download</a>
  ·
  <a href="https://github.com/antonio-abrantes/desktop-fences">GitHub</a>
</p>

## Demo

[Assistir no site](https://antonio-abrantes.github.io/desktop-fences/#demo) (GitHub Pages reproduz o MP4). Ficheiro no repositório: [`docs/assets/presentation.mp4`](docs/assets/presentation.mp4).

O README no GitHub.com não incorpora o leitor de vídeo; o site sim.

## O problema

A área de trabalho acumula atalhos, pastas e arquivos soltos. Os clones open source (DeskFrame, NoFences, OpenFences) colocam esses itens em janelas flutuantes, mas **deixam os ícones originais no desktop**. O resultado é duplicata: o atalho continua lá e também aparece no painel.

O DesktopFences faz o que esses clones não fazem: tira o ícone real da pasta Desktop (move para um armazenamento estável por item; registry para Lixeira / Este computador / Rede), desenha a nossa grade por cima, e devolve o ícone ao desktop se você tirar o item da fence ou sair do app.

## O que o MVP 3 entrega

O hide/restore do MVP 1, mais várias fences no mesmo desktop:

- Vidro escuro translúcido (o wallpaper aparece atrás), cantos arredondados, atrás das janelas comuns.
- Arrastar do desktop ou do Explorer para dentro: um ou vários ícones; somem de lá, entram na grade; o ponteiro permanece a seta. Arrastar para fora devolve o primeiro ícone no ponto do cursor e organiza os demais ao redor; o ghost acompanha o cursor. Itens do Desktop Público voltam para o Desktop gravável do usuário, sem pedir administrador. Arrastar **entre** fences muda o dono sem reaparecer o ícone real. Ícones de sistema (Este computador, Lixeira, Rede) usam o pictograma da Shell.
- Seleção, multi-seleção e reordenação dentro da fence.
- Mover só pela alça **⋮⋮** (ímã nas bordas da tela e nas outras fences ao soltar); redimensionar pelas bordas (ímã no fim do gesto); recolher para só a barra (▴); duplo clique no **texto** do título para renomear.
- Com o setup instalado, direito no vazio do Desktop → **Nova fence** cria a mesma fence que o botão nas Configurações.
- **Idioma:** Sistema / Português / Inglês. Troca ao vivo. Título já gravado não muda.
- **Iniciar com o Windows** em path estável quando instalado; o portable continua atualizando o path para a pasta em uso. Uma só instância.
- Bandeja: Pausar / Retomar / Configurações / Sobre / Sair. Pausar e Sair restauram os ícones reais e reaplicam suas posições originais; se uma célula estiver ocupada, o Explorer resolve o alinhamento.
- Persistência v2 em `%AppData%\DesktopFences\layout.json`, com gravação atômica e backup. Ficheiros em `%LocalAppData%\DesktopFences\Items\{ItemId}`; transações interrompidas são recuperadas antes de mostrar as fences.
- Se um item devolvido ao Desktop for apagado enquanto o app estiver fechado, no próximo arranque somente sua referência é removida da fence; os demais itens continuam normalmente. Estados inacessíveis ou ambíguos permanecem bloqueados por segurança.
- Snapshot atômico das posições dos ícones no arranque e `DesktopFences.Recovery.exe` separado para recuperação por um clique. A ferramenta copia os dados sem apagar o store e nunca sobrescreve um arquivo do Desktop.

Explorer reiniciado, DPI e Win+D foram validados no Windows 11. A `v0.6.0` acrescenta instaladores x64/ARM64, idioma inicial e desinstalação segura; o gate manual dos setups ainda precisa ser feito. A `v0.6.3` acrescenta arranque multi-monitor (espera o ecrã gravado no logon), layout padrão para novas fences (tema + alinhamento do título), flicker em sobreposição e remoção de fence com confirmação + barreira. A `v0.6.4` acrescenta **Nova fence** no menu de contexto do desktop (setup instalado; no Windows 11 o item clássico pode aparecer só em Mostrar mais opções; Novo → Fence usa `ShellNew\Command` sem criar ficheiro). A `v0.6.5` corrige o skip de z-order (fences sobrepostas deixam de piscar em idle porque o timer já não chama `SetWindowPos` à toa), deixa de tratar o arrastar de uma fence como drop de ficheiro, e reorganiza os botões das Configurações. O hotfix do instalador (upgrade com o app aberto) fica para a versão seguinte. Duplo clique no vazio do desktop não faz parte deste ciclo.

## Instalação e desinstalação

Baixe o setup da sua arquitetura em [Releases](https://github.com/antonio-abrantes/desktop-fences/releases). A instalação é por usuário, sem administrador, e Português vem selecionado por padrão; Inglês também pode ser escolhido e o idioma continua alterável nas Configurações.

Ao encontrar dados anteriores, o setup oferece usar as configurações existentes ou começar com novas depois de devolver os itens ao Desktop e arquivar o estado antigo em `%LocalAppData%\DesktopFences.Backups`. Na desinstalação, é possível manter as configurações ou remover tudo. Se a devolução dos itens falhar, a limpeza é cancelada e o aplicativo, Recovery e store são preservados.

## Requisitos

- Windows 10/11 (alvo: Windows 11)
- O setup e os zips são self-contained; o usuário não precisa instalar o .NET.
- Para compilar o projeto: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

Rode **sem** administrador (`asInvoker`). Elevar o processo só complica o acesso ao `explorer.exe`.

## Build e execução

```powershell
dotnet build DesktopFences.sln
dotnet run --project src/DesktopFences.App
```

Ou abra `DesktopFences.sln` e dê F5 (perfil Debug). O binário fica em:

`src/DesktopFences.App/bin/Debug/net8.0-windows/DesktopFences.exe`

O build local também coloca `DesktopFences.Recovery.exe` nessa mesma pasta, permitindo o handoff automático de segurança em Debug.

Fecha o app pela bandeja antes de gerar um build novo — o `.exe` em execução trava as DLLs.

## Recuperação de emergência

Se o aplicativo não conseguir iniciar com segurança, aceite a opção de abrir a recuperação ou execute `DesktopFences.Recovery.exe` diretamente, com o DesktopFences fechado. Clique em **Restaurar tudo no Desktop**.

A recuperação copia os payloads ausentes para o Desktop, preserva o store como fonte de segurança e evita sobrescrita. A organização atual é preservada por padrão; reaplicar posições antigas é uma opção explícita. O registro da sessão fica em `%LocalAppData%\DesktopFences\Recovery`. Detalhes e limites estão em [`docs/hotfix-v0.5.1-recuperacao-emergencia.md`](docs/hotfix-v0.5.1-recuperacao-emergencia.md).

## Testes automatizados

```powershell
dotnet test DesktopFences.sln
```

`DesktopFences.Core.Tests` cobre domínio/persistência; `DesktopFences.App.Tests` cobre o coordenador, checkpoints de crash, contadores de lote e moves físicos somente em diretórios temporários.

## Release

O GitHub Action **não** roda em push de branch. Só em tag `v*`:

```powershell
git tag -a v0.6.5 -m "DesktopFences v0.6.5"
git push origin v0.6.5
```

Isso publica, para `win-x64` e `win-arm64`, um setup e um zip portable self-contained, ambos com o aplicativo e o executável independente de recuperação. Download: [Releases](https://github.com/antonio-abrantes/desktop-fences/releases).

## Licença

MIT. “Fences” é marca da Stardock.

Contribuição (humano ou agente): comece por [`AGENTS.md`](AGENTS.md).
