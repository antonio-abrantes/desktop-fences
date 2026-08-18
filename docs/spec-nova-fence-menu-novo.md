# Spec — Nova fence no menu Novo do desktop

> Recorte: o utilizador clica com o botão direito no vazio da área de trabalho, abre **Novo**, e escolhe **Fence**. Nasce uma fence com o visual padrão já gravado nas Configurações (`defaultTheme` / `defaultTitleAlignment`).
>
> **Status:** pronta; **não autorizada**. Não implementar até o desenvolvedor marcar o gate no `SESSION-HEADER.md` **e** pedir explicitamente esta etapa.
>
> **Não é a etapa vigente.** O hotfix `v0.6.3` e os gates da Fase 7 permanecem como estão. Este ficheiro existe para a fase seguinte, quando for pedida. Ao implementar, **não** fechar nem reabrir fases anteriores só por causa desta feature.

---

## 0. Instruções para o agente

Leia, nesta ordem, antes de escrever código:

1. `docs/SESSION-HEADER.md` — confirmar que **esta** etapa foi autorizada e pedida
2. `AGENTS.md`
3. `docs/SPEC.md`
4. `docs/plano-implementacao.md`
5. `README.md`
6. **este ficheiro** (contrato). Se o código divergir, a spec ganha.

Ao **concluir** a implementação (só depois de pedida): atualizar `SESSION-HEADER.md`, o passo correspondente em `plano-implementacao.md`, `README.md` se o produto passou a oferecer o menu Novo, e `docs/pos-mvp1.md` se o item deixar de ser reserva. Até lá, **não** editar esses documentos “por antecipação”.

Camadas: `App` orquestra; `Core` só regras puras (extensão-stub, argumentos); `Native` só se for indispensável notificar o Explorer depois de apagar o stub (`SHChangeNotify` já existe). Sem `IntPtr` em Core. Sem pacote NuGet novo. Sem copiar DeskFrame / NoFences / OpenFences.

Reutilizar `FenceHost.TryAddNew()`. Não reimplementar `PlaceNew` nem o layout padrão da `v0.6.3`.

---

## 1. Contexto do produto (o que já existe)

DesktopFences é uma app nativa Windows 11 (C# / .NET 8 / WPF) que **move** os ícones reais do Desktop para um store e desenha a grade da fence por cima. Não é um clone que deixa duplicata no desktop.

Criar fence hoje:

- Só nas **Configurações** (`SettingsWindow` → `_host.TryAddNew()`).
- `FenceLayoutRules.PlaceNew` offseta +40 px da última fence, copia largura/altura, e herda `LayoutDocument.DefaultTheme` + `DefaultTitleAlignment` (hotfix `v0.6.3`).
- `Start()` chama `EnsureAtLeastOne`: se o layout vier vazio, já nasce **uma** fence de fábrica. Sempre ≥ 1 fence.

Instância única:

- Mutex `Local\DesktopFences.SingleInstance` em `App.OnStartup`.
- Segunda instância hoje: **MessageBox** (“já está a correr”) e `Shutdown()`. Não encaminha nenhum pedido.
- Named pipe de manutenção (`MaintenanceProtocol`): **só** aceita `prepare-exit`. Sucesso → `PrepareExit` + shutdown. Usado pelo instalador. Comandos desconhecidos falham. Isto **não pode** partir.

Instalador (Fase 7):

- Inno Setup por utilizador, `asInvoker`, `AppId` estável, path `%LocalAppData%\Programs\DesktopFences`.
- Upgrade no lugar; desinstalação keep/remove; HKCU `Software\DesktopFences` (`InstallVersion`, `InstallArchitecture`).
- Script: `installer/DesktopFences.iss`. Zip portable **não** escreve estas chaves de menu.

Fora deste recorte (já reserva noutros docs): duplo clique no vazio do desktop, packs de tema, empurrar vizinhos, `IExplorerCommand` / menu moderno Win11, verbo no topo do menu de contexto (`Directory\Background\shell` como caminho **principal**).

---

## 2. Problema

Criar fence exige abrir Configurações. O sítio natural no Windows para “criar um objeto novo no desktop” é **botão direito → Novo**. O utilizador espera que essa Fence nasça com o padrão visual já definido nas Configurações, sem passar pela janela de settings.

---

## 3. Decisão (caminho escolhido)

**Menu Novo (`ShellNew`)**, não o menu de contexto de topo, e não `IExplorerCommand` nesta fase.

| Escolha | Porquê |
|---|---|
| Submenu **Novo** | É o contrato mental do Explorer para criar algo no sítio onde se clicou (o Desktop). |
| `ShellNew` em HKCU + ProgID próprio | Sem COM novo, sem elevação, encaixa no instalador actual. |
| Extensão `.desktopfence` | Identidade única; o Explorer precisa de um tipo para mostrar o item em Novo. |
| Comando `--create-fence` | O `.exe` já instalado é o handler; Settings continua a funcionar. |
| IPC no pipe existente | O mutex já impede segunda UI; o 2.º processo só pede à instância viva. |

Limite aceite do Windows 11: o submenu Novo de tipos personalizados pode aparecer só em **Mostrar mais opções** (menu clássico, Shift+F10), não no flyout moderno. Não é motivo para trocar para COM nesta fase. Não prometer ícone no menu compacto do Win11.

---

## 4. Contrato de comportamento

Uma escolha em **Novo → Fence** produz **exactamente uma** fence nova, com o mesmo resultado que o botão **Nova fence** nas Settings:

- título `Loc.T("DefaultFenceTitle")` (já traduzido);
- geometria via `PlaceNew` (offset + tamanho da última);
- aparência via defaults do documento (`TryAddNew` já lê `_defaultTheme` / `_defaultTitleAlignment`);
- persistência `SaveAll()` como hoje.

Não abre Settings. Não pede confirmação. Não posiciona no cursor (fora). Não cria itens dentro da fence.

### 4.1 App já aberto

1. O Explorer lança `{app}\DesktopFences.exe --create-fence "%1"` (ver §5).
2. O 2.º processo **não** mostra a MessageBox de instância única.
3. Envia `create-fence` pelo pipe de manutenção, espera `ok` / `failed` (timeout curto, mesma ordem de grandeza do `prepare-exit`: ~10 s).
4. Sai com código 0 se o pedido foi aceite; não cria janela, não chama `Start()`, não toca no store.
5. A instância viva, no dispatcher, chama `TryAddNew()` e **não** faz shutdown.

Se o pipe falhar (app a sair, pausa de custódia a bloquear inbound irrelevante aqui, `TryAddNew` recusar): o 2.º processo sai em silêncio **sem** MessageBox de “já está a correr”. Não spawna terceira tentativa. Não escrever no `layout.json` a partir do 2.º processo.

`TryAddNew` hoje devolve sempre `true` depois de `Spawn`. Se no futuro passar a recusar (ex. barreira de remoção), o pipe devolve `failed`; não inventar retry.

App em **pausa**: criar na mesma. `TryAddNew` já chama `PauseVisual()` na fence nova. Não retomar o hide automaticamente.

App a **devolver itens** ao Desktop (`IsReturningItemsToDesktop`): ainda assim criar a fence (é geometria/UI, não inbound). Não ligar esta feature à barreira de remoção.

### 4.2 App fechado (arranque a frio)

1. O processo **é** a instância principal: toma o mutex, corre `Start()` completo (recovery, custódia, fences gravadas).
2. Depois da UI pronta, decide se chama `TryAddNew()`:

| Estado do layout **antes** de `EnsureAtLeastOne` | Acção |
|---|---|
| Já havia ≥ 1 fence | `TryAddNew()` — o utilizador pediu **mais uma** |
| Lista vazia (1.ª utilização ou reset) | **Não** chamar `TryAddNew()`. A fence criada por `EnsureAtLeastOne` **é** a fence pedida |

Sem esta regra, o primeiro “Novo → Fence” num perfil vazio nasceria **duas** fences.

3. Apagar o stub **antes** de qualquer inbound/custódia poder vê-lo (§6), inclusive neste arranque.

### 4.3 App não instalado / portable

O item **Novo → Fence** só existe quando o **instalador** gravou as chaves. O zip portable **não** regista `ShellNew`. Não auto-registar no `OnStartup` da app (o path mudaria, ficava lixo no Novo, e o portable não é o contrato de desinstalação).

Se o utilizador tiver instalação **e** portable: o mutex continua único; o menu aponta para o `.exe` instalado. Não tratar o portable neste recorte.

---

## 5. Registro (só instalador)

Todas as chaves em **HKCU**. Nenhuma em HKLM. Sem UAC.

ProgID sugerido: `DesktopFences.NewFence` (não reutilizar nomes genéricos).

```
HKCU\Software\Classes\.desktopfence
  (Default) = DesktopFences.NewFence

HKCU\Software\Classes\.desktopfence\ShellNew
  NullFile = (string vazia)     ← faz o item aparecer em Novo
  IconPath = {app}\DesktopFences.exe,0
  ItemName = texto localizado (ver §8)

HKCU\Software\Classes\DesktopFences.NewFence
  (Default) = mesmo texto que ItemName
  DefaultIcon = {app}\DesktopFences.exe,0

HKCU\Software\Classes\DesktopFences.NewFence\shell\open\command
  (Default) = "{app}\DesktopFences.exe" --create-fence "%1"
```

Notas:

- `{app}` é o `DefaultDirName` estável. Upgrade no lugar actualiza o comando (Inno `[Registry]` já reescreve valores no setup).
- `NullFile` **vai** criar um ficheiro vazio no Desktop. Isso é o preço do Novo; o app **obriga-se** a apagá-lo (§6).
- Não usar `ShellNew\Command` *em substituição* do `open\command` se isso impedir o Explorer de mostrar o item; o handler canónico é o `open` do ProgID com `--create-fence "%1"`.
- Não associar `.desktopfence` a um editor. Não aparecer em “Abrir com”. Não criar `FriendlyTypeName` longo que pareça documento do utilizador.

Inno (`installer/DesktopFences.iss`):

- Entradas `[Registry]` com `uninsdeletekey` / `uninsdeletevalue` adequados para o ProgID e a extensão **desaparecerem na desinstalação** (keep **e** remove: o programa sai, o menu não pode ficar órfão).
- `ItemName` segundo o idioma do wizard (`portuguese` / `english`), alinhado a `AppLanguage` já usado em `--language=`.
- `finalize` pode regravar `ItemName` se o idioma inicial mudar no setup; **não** é obrigatório actualizar o texto do menu quando o utilizador muda o idioma **dentro** da app (limite aceite).

Não tocar na política keep/reset/remove de dados, nem no bloqueio de downgrade, nem no `AppId`.

---

## 6. Stub `.desktopfence` (obrigatório)

O Explorer cria um ficheiro no Desktop (nome tipo `Fence.desktopfence` / `Nova fence.desktopfence`, possivelmente com `(2)`). Esse ficheiro **não é um ícone do utilizador**.

Regras:

1. `--create-fence` aceita zero ou um caminho. Se vier `%1`, apagar esse ficheiro se existir, for ficheiro (não pasta), e a extensão for exactamente `.desktopfence` (case insensitive).
2. Só apagar se o path estiver **no Desktop do utilizador ou no Desktop Público** (`DesktopPaths` no Core). Recusar path arbitrário (não apagar `C:\Windows\...`).
3. Nunca mover o stub para o store. Nunca `PlanInbound` / hide / registry de namespace para esta extensão.
4. Se o apagar falhar, **ainda assim** criar a fence; não bloquear o produto por um ficheiro vazio. Tentar de novo uma vez após um `UPDATEDIR` no directório pai **só se** o ficheiro ainda existir. Sem loop. Sem `ASSOCCHANGED` por causa do stub (o hotfix de FlushShell condicional permanece).
5. Core: helper puro, ex. `DesktopFenceStubRules.IsStubPath(path, desktopRoots)` — testes em `DesktopFences.Core.Tests`.
6. App: apagar no 2.º processo **e** na instância principal (o Explorer pode passar `%1` só ao processo lançado). Se o 2.º processo apagou, a principal recebe IPC sem path — aceitar `create-fence` sem argumento.

Não usar o stub como persistência da fence. A fence continua só no `layout.json`.

---

## 7. Processo, argumentos e IPC

### 7.1 Argumentos

Novo parser **separado** de `InstallerMaintenanceArguments`. `--maintenance=` continua a ser o único gatilho de manutenção; se ambos aparecerem, manutenção ganha e **ignora** `--create-fence` (o setup não pode criar fence no meio de keep/reset).

```
--create-fence
--create-fence=C:\Users\...\Desktop\Fence.desktopfence
--create-fence C:\Users\...\Desktop\Fence.desktopfence
```

O Explorer envia `"%1"` no `open\command`. Aceitar as três formas. Path com espaços: já vem quoted pelo Shell.

Arranque normal (zero args, ou args que não são maintenance nem create-fence): **inalterado**, incluindo a MessageBox de 2.ª instância.

### 7.2 Pipe

Estender o whitelist do pipe actual — **não** criar segundo pipe, **não** alargar a um protocolo genérico.

| Comando | Efeito na instância viva |
|---|---|
| `prepare-exit` | igual hoje: `PrepareExit` + shutdown se `ok` |
| `create-fence` | `TryAddNew()` no dispatcher; **não** shutdown |
| qualquer outro | `failed`; não shutdown |

Testes em `InstallerMaintenanceArgumentsTests` / novo teste de protocolo: `prepare-exit` continua a ser o único comando destrutivo; `create-fence` é aceite e não dispara shutdown; lixo continua rejeitado.

O servidor hoje lê **uma linha**. Manter: `create-fence` sem payload no pipe (o stub já foi tratado pelo processo lançado). Não mandar paths pelo pipe nesta fase (evita IPC a apagar ficheiros).

### 7.3 MessageBox de instância única

Só o caminho **sem** `--create-fence` (e sem maintenance) mostra o aviso actual. Não “melhorar” esse texto neste recorte.

---

## 8. Idioma e UI

Texto do item em Novo:

| Idioma do setup | `ItemName` / default do ProgID |
|---|---|
| Português | `Fence` |
| Inglês | `Fence` |

A palavra do produto é “fence” nos dois idiomas; não usar “Nova fence” no menu Novo (o Novo já significa criar). Não adicionar janela, toast, nem item na bandeja.

O botão **Nova fence** nas Settings **não muda**.

Strings do app (`Strings.resx`): só se o parser ou um erro interno precisar; o fluxo feliz é silencioso.

---

## 9. Camadas e ficheiros prováveis

| Sítio | O quê |
|---|---|
| `installer/DesktopFences.iss` | `[Registry]` ShellNew + ProgID + `uninsdelete*` |
| `App.xaml.cs` | parse `--create-fence`; 2.ª instância silenciosa; após `Start()`, regra do §4.2 |
| `Services/MaintenanceProtocol.cs` | constante `create-fence`; servidor distingue os dois comandos |
| `FenceHost.cs` | sem lógica Shell; no máximo um método fino `TryAddNew` já existente |
| `DesktopFences.Core` | `DesktopFenceStubRules` (extensão + roots do Desktop) |
| `DesktopFences.Native` | só se for preciso `SHChangeNotify`/`UPDATEDIR` após delete; reutilizar o que já há |
| Testes Core | stub path aceite/rejeitado |
| Testes App | args; whitelist do pipe; regra “lista vazia ⇒ não duplicar no arranque a frio” (lógica extraível, sem UI) |

Não alterar `PlaceNew`, schema JSON, custódia, âncora Z-order, monitores, nem o diálogo de remover fence.

---

## 10. Performance e segurança

- Custo em idle: **zero**. Sem timer, sem hook de rato, sem polling extra. O pipe já está à escuta para o instalador.
- 2.º processo: vive milissegundos (conectar, uma linha, sair).
- `asInvoker`, HKCU only.
- Apagar ficheiro: allowlist Desktop; nunca path de sistema.
- IPC: só na máquina local, nome já derivado do perfil; não abrir o whitelist a strings livres.

---

## 11. Testes automatizados e gate Windows 11

Automatizado:

- parser `--create-fence` com e sem path; maintenance prevalece se os dois vierem;
- `IsStubPath`: Desktop válido + `.desktopfence`; rejeitar `.txt`, pasta, path fora do Desktop;
- pipe: `create-fence` → sucesso sem shutdown; `prepare-exit` intacto; `rm -rf` / `unknown` → `failed`;
- arranque a frio: documento vazio + pedido de create → 1 fence, não 2 (regra testável se extraída, ex. `CreateFenceOnColdStart.ShouldAddAnother(existingCount)`).

Gate humano (instalação, não portable):

- [ ] Direito no vazio do Desktop → Novo → Fence (se só no menu clássico, Shift+F10 / Mostrar mais opções: aceite, documentar no gate)
- [ ] App aberto: uma fence nova, visual = padrão das Settings, **sem** MessageBox de instância, **sem** ficheiro `.desktopfence` no Desktop
- [ ] App fechado com fences já gravadas: app abre e acrescenta **uma**
- [ ] Perfil/layout vazio: app abre com **uma** fence (não duas)
- [ ] Upgrade do setup: o comando aponta para o `{app}` novo; o item Novo continua
- [ ] Desinstalação: o item desaparece de Novo; keep/remove de dados da Fase 7 inalterados
- [ ] Settings → Nova fence continua igual
- [ ] Pausar: a fence nova nasce visualmente em pausa

---

## 12. Fora (não fazer)

- Duplo clique no vazio do desktop / `WH_MOUSE_LL` para criar fence.
- Item no **topo** do menu de contexto do desktop como entrega principal (`Directory\Background\shell`).
- `IExplorerCommand`, `IExplorerCommandState`, pacote Appx, menu moderno Win11.
- Auto-update, serviço, tarefa agendada, HKLM, administrador.
- Registar ShellNew a partir do portable ou em todo `OnStartup`.
- Posicionar a fence no cursor ou no monitor do clique.
- Abrir Settings, Sobre, ou um wizard ao criar pelo Novo.
- Packs de tema; mudar `PlaceNew`; bump de schema.
- Levar o stub à custódia “para o utilizador arrastar depois”.
- Encerramento forçado (`taskkill`) se o pipe falhar.
- Alterar `prepare-exit`, keep/reset/remove, ou o `AppId`.
- Copiar código de DeskFrame / NoFences / OpenFences.
- Fechar o gate da Fase 7, o hotfix `v0.6.3`, ou avançar outras reservas “já que se está a mexer no Explorer”.

---

## 13. Ordem de implementação sugerida (quando autorizada)

1. Core: `DesktopFenceStubRules` + testes.
2. App: parser `--create-fence` + regra de arranque a frio extraída e testada.
3. Pipe: whitelist `create-fence` sem shutdown + testes de protocolo (não partir `prepare-exit`).
4. `App.xaml.cs`: 2.ª instância silenciosa; arranque a frio; apagar stub.
5. Inno: chaves HKCU + uninstall.
6. Gate Windows 11 da §11.

Não publicar tag nem bump de versão sem o desenvolvedor pedir.
