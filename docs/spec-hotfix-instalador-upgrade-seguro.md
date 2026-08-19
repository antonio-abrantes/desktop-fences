# Spec — Upgrade/desinstalação recuperáveis sem perda de dados

> Recorte: o setup e o desinstalador **nunca** deixam o utilizador num beco sem saída, **nunca** apagam layout/store/Recovery porque uma manutenção falhou, e o upgrade com “usar configurações existentes” **não** despeja os ícones no Desktop só para trocar o `.exe`. Encerrar a instância em execução é pedido pelo pipe, com UI e retry — não `taskkill` como caminho principal.
>
> **Status:** pronta; **não autorizada**. Não implementar até o desenvolvedor marcar o gate no `SESSION-HEADER.md` **e** pedir explicitamente esta etapa. O polish de flicker/Settings da `v0.6.5` já fechou; esta spec é a **próxima versão** (tag nova, não `v0.6.5`).
>
> **Prioridade absoluta:** não perder dados. Tudo o resto (UX, retry, Recovery, logs) é secundário. Se uma escolha puder apagar `layout.json`, o store ou o Recovery para “desbloquear” o setup, essa escolha está **fora**.

---

## 0. Instruções para o agente

Leia, nesta ordem, antes de escrever código:

1. `docs/SESSION-HEADER.md` — confirmar que **esta** etapa foi autorizada e pedida
2. `AGENTS.md`
3. `docs/SPEC.md`
4. `docs/plano-implementacao.md`
5. `README.md`
6. `docs/spec-fase-7-instalador.md` — contrato de instalação; esta spec **emenda**, não substitui
7. **este ficheiro** (contrato). Se o código divergir, a spec ganha.

Ao **concluir** a implementação (só depois de pedida): actualizar `SESSION-HEADER.md`, o passo em `plano-implementacao.md`, `README.md` se o fluxo de install/upgrade/desinstalar mudou, e `docs/spec-fase-7-instalador.md` na secção de manutenção. Até lá, **não** editar esses documentos “por antecipação”.

Camadas: `App` orquestra manutenção, pipe e flags de saída; `Core` regras puras (classificar falha, “keep-upgrade não liberta custódia”); `Native` só se for preciso enumerar o processo do app (opcional). Inno em `installer/DesktopFences.iss`. Sem pacote NuGet novo. Sem copiar DeskFrame / NoFences / OpenFences. Sem administrador. `asInvoker`.

Não misturar com: menu Novo, flicker, layout visual das fences, packs de tema, duplo clique no desktop.

**Teste mental obrigatório em cada decisão:** “se isto falhar a meio, o utilizador ainda tem layout, store, Recovery e itens recuperáveis?” Se a resposta não for sim, não implementar assim.

---

## 1. Contexto (o que já existe)

Instalador Inno por utilizador, `AppId` estável, path `%LocalAppData%\Programs\DesktopFences`. Script: `installer/DesktopFences.iss`.

Modos actuais (`InstallerMaintenanceMode`):

| Modo | Quando | Hoje |
|---|---|---|
| `keep` | Setup com dados, opção “usar configurações” | `ReleaseCustody()` **e depois** `SetLanguage` |
| `reset` | “Começar com configurações novas” | `ReleaseCustody` + arquivo + apagar dados |
| `finalize` | Pós-cópia dos binários | idioma, Run key, menu Novo — **sem** libertar custódia |
| `uninstallkeep` | Desinstalar, manter dados | `ReleaseCustody` + tirar Run + menu |
| `remove` | Desinstalar tudo | `ReleaseCustody` + apagar roaming/local |

`ReleaseCustody` em `InstallerMaintenance`: `Recover` dos journals; se `!Complete`, **throw**; senão `PlanOutbound` + `CommitOutbound` de **todos** os itens. Qualquer excepção → `catch { return 1; }`. O Inno só vê falha genérica.

Instância única: mutex `Local\DesktopFences.SingleInstance`. Pipe `MaintenanceProtocol`: `prepare-exit` chama `FenceHost.PrepareExit()` (que **já devolve** ícones ao Desktop) e depois `Shutdown`. `App.OnExit` volta a chamar `RestoreAllIcons` se não estiver em pausa.

Inno: `CloseApplications=no`. `PrepareToInstall` extrai `DesktopFences.exe` para `{tmp}` e corre `--maintenance=keep|reset`. Desinstalar: `RaiseException` se a manutenção falhar → diálogo “Runtime error (at 28:411)”.

Recovery: `DesktopFences.Recovery.exe` no `{app}`. Copia payloads para o Desktop **sem apagar o store**. Não é chamado pelo setup.

Arquivo já existe para reset: `%LocalAppData%\DesktopFences.Backups\Reset-…`.

Incidente real (teste, `v0.6.3`, sem perda de dados): app provavelmente aberto → setup cancelou na preparação → desinstalar falhou a devolver itens → `RaiseException` → retry do setup continuou a falhar até o utilizador usar o Recovery à mão, limpar e reinstalar. Os dados estavam lá; o produto não oferecia saída.

---

## 2. Problema

Três falhas distintas usam a mesma mensagem e o mesmo `exit 1`:

1. **Instância ocupada** — mutex/pipe/timeout. Retry depois de fechar o app deveria bastar.
2. **Custódia/journal partido** — `Recover.Complete == false` ou `CommitOutbound` falha. Repetir `keep` **nunca** desbloqueia.
3. **Aborto tosco do Inno** — `RaiseException` parece crash, não cancelamento seguro.

O upgrade `keep` faz o trabalho de **desinstalação** (devolver tudo ao Desktop) só para substituir binários. Os payloads vivem em `%LocalAppData%\DesktopFences\Items`, não em `{app}`. Esse passo é desnecessário e é o que parte o estado quando corre a meio ou duas vezes (`PrepareExit` + helper).

Não houve perda de dados no incidente. A regra “não apagar se não devolver” está certa. Falta **classificar a falha**, **não despejar ícones no upgrade keep**, **retry**, **arquivo**, **oferecer Recovery** — sem limpeza automática.

---

## 3. Decisões fechadas (segurança primeiro)

1. **Nunca** apagar `layout.json`, `layout.json.bak`, store, journals, snapshot ou Recovery porque o setup/desinstalador falhou.
2. **Nunca** `RaiseException` no Inno para abortar manutenção. Mensagem + abortar a remoção de ficheiros. Programa e dados ficam.
3. **Nunca** `taskkill` / `CloseApplications=yes` como caminho principal. Encerramento forçado só se o utilizador confirmar **depois** de o pipe falhar, e mesmo aí **não** apagar dados.
4. **Nunca** lançar Recovery nem restaurar o Desktop sem confirmação explícita.
5. **Nunca** tratar falha de upgrade como desinstalação limpa automática.
6. Upgrade com “usar configurações existentes”: **não** chamar `ReleaseCustody`. Pedir à instância: gravar layout e sair **sem** devolver payloads. Itens continuam no store. O app novo no arranque retoma a custódia como hoje.
7. `reset`, `uninstallkeep` e `remove` **continuam** a devolver itens ao Desktop **antes** de apagar o que a política manda. Se a devolução falhar: parar, preservar tudo, oferecer Recovery.
8. `catch` da manutenção **não** pode engolir a causa. Gravar log + código de falha para o Inno mostrar.

---

## 4. Classificação de falha (Core)

Extrair regra pura, testável, sem Win32. Exemplo de nomes (ingleses no código):

```
enum MaintenanceFailureKind { None, InstanceBusy, CustodyBlocked, InvalidRequest }

MaintenanceFailureKind Classify(bool mutexHeldByOther, bool pipeOk, bool recoverComplete, bool outboundOk)
```

| Situação | Kind | O que o Inno faz |
|---|---|---|
| Outro processo tem o mutex e o pipe falhou/timeout | `InstanceBusy` | Pedir para fechar / retry; **não** falar em Recovery como primeira acção |
| Mutex livre, mas `!Recover.Complete` ou outbound falhou | `CustodyBlocked` | Arquivar estado; oferecer Recovery; **não** copiar binários; **não** apagar dados |
| Args inválidos | `InvalidRequest` | Não tocar em dados (já é o parser actual, exit 2) |
| Tudo ok | `None` | Continuar |

O helper escreve um ficheiro de resultado que o Inno lê (não só exit code):

`%LocalAppData%\DesktopFences\maintenance-last.log` (ou `.json` pequeno)

Campos mínimos: `utc`, `mode`, `kind`, `exitCode`, `message` (uma linha, sem secrets de outros users). Sobrescrever a cada corrida. Se não der para gravar o log, o exit code continua a valer (1 = falha).

Testes Core: tabela da classificação. Testes App: parser do log / códigos; `keep` **não** chama libertação de custódia.

---

## 5. Upgrade `keep`: saída sem devolver ícones

### 5.1 Modo novo ou semântica nova

Duas opções aceitáveis (escolher **uma** na implementação e documentar no código):

**A (preferida).** Novo modo `--maintenance=upgradekeep` usado **só** pelo `PrepareToInstall` quando a página de dados está em “usar existentes” (ou não há página e já existe `{app}\DesktopFences.exe`). `keep` antigo deixa de ser usado pelo setup de upgrade. `reset` inalterado.

**B.** `keep` deixa de chamar `ReleaseCustody`; só `SetLanguage` depois de ter exclusividade. `reset` / uninstall continuam a libertar.

Não deixar o setup de upgrade a chamar o `keep` actual (com `ReleaseCustody`).

`finalize` permanece sem libertar custódia.

### 5.2 Pedido à instância viva (pipe)

Hoje `prepare-exit` = `PrepareExit()` = `ReleaseAll` + fechar janelas + shutdown. Isso é correcto para **desinstalar / reset**, errado para **upgrade keep**.

Estender o whitelist do pipe (não criar segundo pipe):

| Comando | Efeito |
|---|---|
| `prepare-exit` | Como hoje: devolver ícones, depois shutdown. Usado por reset/uninstall e pelo helper desses modos. |
| `prepare-exit-upgrade` | **Novo.** `SaveAll()`, parar timers, fechar fences **sem** `ReleaseAll` / `RestoreAllIcons`, shutdown. |
| `create-fence` | Inalterado; sem shutdown. |
| outro | `failed`; sem shutdown. |

`App.OnExit`: se a saída foi `prepare-exit-upgrade`, **não** chamar `RestoreAllIcons`. Flag explícita (não reutilizar “pausado”, que significa outra coisa). Se a flag falhar e o OnExit restaurar, o upgrade keep volta a despejar o Desktop — regressão desta spec.

Timeouts: `prepare-exit-upgrade` pode ser **curto** (gravação JSON, ~10–15 s). `prepare-exit` (restore de muitos ficheiros) **alargar** (sugestão 60 s de pipe + 60 s de mutex, constantes únicas). Não tratar timeout de upgrade como `CustodyBlocked`.

### 5.3 Sequência de upgrade keep (feliz)

1. Wizard: dados existentes → “usar configurações” (default).
2. Se o mutex estiver ocupado: página ou MsgBox “o DesktopFences está aberto (bandeja). O setup vai pedir para sair sem mexer nos teus ícones.” Botões: **Tentar agora** / **Cancelar**. Cancelar = dados intactos, nada copiado.
3. Helper `{tmp}\DesktopFences.exe --maintenance=upgradekeep --language=…`
4. Helper: se mutex ocupado → `prepare-exit-upgrade` → esperar mutex.
5. Exclusividade: **não** `ReleaseCustody`. Só `SetLanguage` se o layout for v2 válido. Se o layout não for v2, **não** migrar no helper de upgrade; deixar o app novo migrar no `Start()` (já existe). Se `SetLanguage` falhar por ficheiro bloqueado → `InstanceBusy` / retry, não apagar.
6. Inno copia binários (`ignoreversion`).
7. `finalize` no `{app}\DesktopFences.exe`.
8. Oferecer abrir o app. Fences e itens no store como antes.

### 5.4 Sequência reset (inalterada na intenção, melhor na falha)

Continua a devolver itens, arquivar em `DesktopFences.Backups`, apagar dados **só depois** de `ReleaseCustody` ok. Se falhar: `CustodyBlocked`, arquivo **ainda assim** se o copy for possível **sem** delete, Recovery oferecido, dados originais intactos.

---

## 6. Instância ocupada (produto)

Antes ou no `PrepareToInstall`:

- Detectar mutex e/ou processo `DesktopFences.exe` do mesmo utilizador (sem admin; o nosso exe em `{app}` ou portable). Não matar.
- Texto distinto de custódia partida.
- **Tentar agora** volta a correr o helper. Sem limite ridículo (3 retries está bem; depois o utilizador cancela).
- Se o utilizador confirmar “encerrar mesmo assim” **depois** de o pipe falhar: último recurso documentado. Preferir `CloseApplications=yes` **só nesse clique**, ou recusar força e pedir bandeja. **Não** implementar `taskkill /F` como default. Se a força for incluída, tem de ser um botão à parte, texto a vermelho, e **ainda assim** não apagar dados.

Portable + instalado: o mutex é o mesmo. O menu Novo aponta para `{app}`. O setup fala com o processo que tiver o mutex. Não inventar segundo mutex.

---

## 7. Custódia bloqueada + Recovery (produto)

Quando `kind = CustodyBlocked`:

1. Tentar `InstallerDataPolicy` arquivo **não destrutivo**: copiar roaming + local para `%LocalAppData%\DesktopFences.Backups\MaintenanceFail-{timestamp}-{guid}\` (mesma ideia do `Reset-…`, **sem** `DeleteDataRoots`). Se o copy falhar, continuar na mesma; o log regista. Nunca apagar a origem porque o backup falhou.
2. MsgBox / página Inno (PT/EN):
   - Título: manutenção não concluída; **os teus dados estão no sítio**.
   - Corpo: não foi possível concluir com segurança (journal ou devolução). O programa **não** foi removido. Há uma cópia em `DesktopFences.Backups\…` se o arquivo correu.
   - Botões: **Abrir Recovery** / **Tentar outra vez** / **Cancelar**
3. **Abrir Recovery:** `Exec` `{app}\DesktopFences.Recovery.exe` se existir; senão o Recovery extraído do setup para `{tmp}` (o `[Files]` já inclui o exe no publish). O Recovery **já** pede confirmação antes de copiar para o Desktop. Não passar flags novas que restoram sem UI.
4. Depois do Recovery, **Tentar outra vez** só faz sentido se o utilizador fechou o Recovery. Não apagar journals automaticamente para “passar” o `Recover.Complete`. Se o Recovery deixou o store consistente, o `Recover` seguinte pode passar; se não, continua bloqueado — correcto.
5. **Não** avançar a cópia de binários neste estado (setup). **Não** apagar `{app}` (uninstall).

Desinstalar com `CustodyBlocked`: mesma oferta Recovery. Depois de restore **confirmado pelo utilizador**, pode repetir `uninstallkeep` / `remove`. `remove` só apaga dados se `ReleaseCustody` tiver sucesso **nesta** corrida.

---

## 8. Desinstalador (bug)

`CurUninstallStepChanged`:

- Se `RunMaintenance` falhar: `MsgBox` da mensagem actual (dados preservados) **e parar**. Não `RaiseException`.
- Forma Inno correcta: `Abort` / flag global `UninstallBlocked` + sair do step sem apagar o resto, **ou** `Result` equivalente da documentação Inno 6 para cancelar uninstall sem runtime error. Validar na implementação contra Inno 6.x: o utilizador **não** pode ver “Runtime error (at …)”.
- Cancelar no Yes/No/Cancel inicial continua a não desinstalar.

Texto do Yes/No: pode ficar; não é o incidente. Opcional (produto): uma linha “se a devolução falhar, nada é apagado e podes usar o Recovery”.

---

## 9. Inno — `PrepareToInstall` e mensagens

- Ler `maintenance-last.log` / exit code para escolher `{cm:MaintenanceInstanceBusy}` vs `{cm:MaintenanceCustodyBlocked}`.
- `MaintenanceFailed` genérico só como fallback se o log não existir (helper antigo).
- `PrepareToInstall` devolve string vazia só em sucesso. String não vazia = Inno cancela a instalação **antes** de copiar — já é o comportamento actual; manter.
- Não pôr `AlwaysRestart`.
- `CloseApplications` permanece `no` no `[Setup]`, salvo o clique explícito da §6 se for implementado.

Página Finished / menu Novo / faixa amarela da `v0.6.4`: **não mexer** nesta spec.

---

## 10. Camadas e ficheiros prováveis

| Sítio | O quê |
|---|---|
| `DesktopFences.Core` | `MaintenanceFailureKind` + `Classify` + testes |
| `InstallerMaintenance.cs` | `upgradekeep` sem `ReleaseCustody`; log; não `catch` vazio |
| `InstallerMaintenanceArguments.cs` | novo modo; testes App |
| `MaintenanceProtocol.cs` | `prepare-exit-upgrade`; dispatch sem shutdown no comando errado; testes whitelist |
| `FenceHost` / `App.xaml.cs` | saída upgrade sem restore; flag OnExit |
| `InstallerDataPolicy.cs` | `ArchiveCurrentState` reutilizável **sem** delete (já há copy; extrair o copy) |
| `installer/DesktopFences.iss` | modos, mensagens, retry, Exec Recovery, uninstall sem RaiseException |
| Testes App | keep/upgradekeep não liberta; reset/remove ainda libertam; pipe |

Não alterar `PlaceNew`, schema JSON, custódia de arraste, âncora Z-order, monitores, diálogo de remover fence.

---

## 11. Performance

Idle do app: **zero** extra (o pipe já existe). Log = uma escrita por manutenção. Arquivo = copy tree só na falha `CustodyBlocked` ou no reset (já existia). Sem polling novo no frame.

---

## 12. Testes automatizados e gate Windows 11

Automatizado:

- Classificar: busy vs blocked vs none.
- Parser: `upgradekeep` aceite; modo lixo rejeitado; `prepare-exit-upgrade` no whitelist; lixo `failed`; `prepare-exit` continua destrutivo (shutdown); `create-fence` não shutdown.
- `upgradekeep` / novo `keep`: **não** chama outbound (mock/spy ou policy extraída).
- `reset` / `remove` / `uninstallkeep`: ainda exigem libertação antes de delete (testes de política já existentes; não os enfraquecer).
- Arquivo sem delete: pasta origem intacta depois do copy.
- Uninstall script: revisão — zero `RaiseException` ligado a manutenção.

Gate humano (obrigatório; o incidente foi isto):

- [ ] Upgrade com app **aberto**, “usar configurações”: pede saída, **não** despeja ícones, fences iguais depois de abrir o app novo
- [ ] Recusar fechar o app → setup cancela; dados e fences intactos; retry depois de fechar pela bandeja funciona
- [ ] `reset`: devolve itens, arquivo em Backups, fences novas
- [ ] Desinstalar com falha simulada de devolução: **sem** runtime error Inno; Recovery e layout presentes; `{app}` não apagado
- [ ] `CustodyBlocked` (journal pendente de teste): setup oferece Recovery; recusar Recovery não apaga nada; arquivo em Backups se o copy correu
- [ ] Recovery manual continua a copiar sem apagar store
- [ ] Desinstalar keep vs remove, os dois, com app fechado, caminho feliz
- [ ] Portable aberto + setup: mensagem de instância; não corromper o layout

---

## 13. Fora (não fazer)

- Apagar dados para o setup “passar”.
- `RaiseException`, `taskkill /F` como fluxo normal.
- Recovery silencioso / restore sem Yes.
- Desinstalação automática após falha de upgrade.
- `ReleaseCustody` no upgrade keep “por segurança” — isso **é** o bug.
- Debilitar `Recover.Complete` para ignorar journals.
- HKLM, UAC, serviço, Authenticode (continua fora).
- Menu Novo, flicker, layout visual, duplo clique, packs de tema, empurrar vizinhos.
- Fechar gates da Fase 7 / `v0.6.3` / `v0.6.4` só porque se mexeu no `.iss`.
- Copiar clones open source.

---

## 14. Ordem de implementação (quando autorizada)

1. Core: classificar falha + testes.
2. Pipe `prepare-exit-upgrade` + flag OnExit **sem** restore + testes de protocolo (não partir `prepare-exit` nem `create-fence`).
3. `upgradekeep` (ou `keep` sem `ReleaseCustody`) + log; `reset`/uninstall intocados na intenção.
4. Inno: mensagens, retry InstanceBusy, uninstall sem `RaiseException`.
5. Arquivo não destrutivo + botão Abrir Recovery em CustodyBlocked.
6. Gate Windows 11 da §12.

Não publicar tag nem bump de versão sem o desenvolvedor pedir.

---

## 15. Relação com a Fase 7

A Fase 7 permanece: Inno por utilizador, keep/reset na **primeira** instalação com dados, desinstalar keep/remove, bloqueio de downgrade, `AppId` estável. Esta spec **corrige** o upgrade com instância aberta, o duplo restore, o beco `CustodyBlocked`, e o crash Pascal do desinstalador. O gate manual da Fase 7 (upgrade `v0.5.1` → actual, etc.) deve incluir os casos da §12 quando esta etapa for feita.
