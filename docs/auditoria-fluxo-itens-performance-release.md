# Auditoria do fluxo de itens, performance e prontidão para release

**Projeto:** DesktopFences 0.3.1  
**Data:** 2026-08-16  
**Escopo:** entrada de arquivos, atalhos e pastas nas fences; transferência entre fences; restauração ao desktop; performance e segurança desses fluxos.  
**Natureza:** análise e validação. Este documento não implementa as recomendações.

---

## 1. Parecer executivo

### Resposta curta

1. **A abordagem atual é a melhor base prática para o requisito central do produto.** Mover o item real para um armazenamento controlado pelo DesktopFences elimina o item da enumeração da pasta Desktop e não depende de coordenadas, monitor, DPI, auto-arrange ou da opção “Mostrar itens ocultos”.
2. **A abordagem anterior, de estacionar o ícone fora da área visível, era mais barata por operação, mas não era uma solução correta de ocultação.** A API usada apenas reposiciona itens visíveis do ListView; suas coordenadas pertencem à view do Explorer e podem ser rearranjadas. Em múltiplos monitores, reinício do Explorer e alterações de layout, o item pode reaparecer.
3. **No cenário comum — atalhos ou arquivos pequenos no Desktop e armazenamento no mesmo volume — o movimento atual tende a ser muito rápido.** O custo relevante não é o tamanho do arquivo quando o sistema consegue fazer um movimento dentro do mesmo volume. Já movimentos entre volumes, de pastas grandes, rede ou provedores de nuvem podem virar cópia + exclusão e custar proporcionalmente ao conteúdo.
4. **A arquitetura deve ser mantida, mas o fluxo atual precisa de reforços antes de um release para usuários gerais.** Os principais bloqueios não são CPU ou memória em repouso; são consistência entre arquivo e JSON, recuperação após falha, propagação de erro, movimentação física desnecessária entre fences e trabalho síncrono/repetido na thread da interface.

### Decisão de release

| Pergunta | Parecer |
|---|---|
| A estratégia “mover para store” está aprovada? | **Sim** |
| A implementação atual está aprovada para teste controlado? | **Sim, com backup e usuários informados** |
| Está aprovada para release público/usuários gerais? | **Não — NO-GO neste estado** |
| O que falta para o GO? | Fechar os bloqueios R1–R6 e executar a matriz de validação da seção 10 |

O conteúdo dos arquivos não é apagado pelos problemas encontrados, mas algumas falhas podem deixá-lo fora do Desktop e sem referência válida no `layout.json`. Para um usuário comum, isso se apresenta como perda de arquivo; portanto, deve ser tratado como risco de dados de severidade alta.

---

## 2. Evidências e limites da auditoria

Foram revisados:

- documentação obrigatória, plano, spec e ADRs;
- fluxo atual em `FenceWindow`, `FenceHost`, `DesktopVisibility`, `ShellFileMove`, `DesktopIconService`, `FenceItemStore` e `LayoutStore`;
- implementação anterior preservada no histórico local (`HiddenIconTracker` + `LVM_SETITEMPOSITION` em `-32000, -32000`);
- testes de domínio ligados a hide, store, paths e persistência;
- contratos oficiais da Microsoft para ListView, `IFolderView`, `IFileOperation`, movimentos entre volumes, notificações da Shell, atributos hidden, OneDrive/Cloud Files e drag-and-drop OLE.

Validação isolada, sem executar o DesktopFences contra o desktop real:

- a solution atual compilou em uma cópia temporária: **0 erros e 0 avisos**;
- foram descobertos 93 testes: **92 passaram**;
- 1 teste não pôde executar porque tenta criar uma pasta no Desktop real, bloqueado pelo sandbox da auditoria. A falha foi `UnauthorizedAccessException` antes da asserção, não uma falha da regra testada;
- não existem testes automatizados do caminho Native que realmente move/restaura arquivos pelo Shell;
- não foram feitos benchmarks no Explorer real nem testes destrutivos de crash, OneDrive, rede ou arquivos bloqueados. Esses testes continuam sendo gate obrigatório.

---

## 3. O que o fluxo atual faz

### Entrada na fence

1. O drop fornece paths ou nomes do desktop.
2. `AddDesktopEntry` resolve o path, captura novamente os ícones do `SysListView32`, extrai o ícone visual e inclui o item na coleção da fence.
3. `HideDesktopCounterparts` percorre todos os itens da fence.
4. Para arquivo, atalho ou pasta, `DesktopVisibility` move o objeto para `%LocalAppData%\DesktopFences\Items\{FenceId}`.
5. Para Lixeira, Este Computador e Rede, aplica `HideDesktopIcons` no registro.
6. Atualiza `path` e `originalPath`, regrava o documento JSON e notifica a Shell.

### Saída da fence

1. O item é localizado no tracker em memória.
2. O payload é movido do store para o diretório original, ou para um nome com sufixo se já existir algo no destino.
3. A Shell é notificada.
4. O ícone restaurado é reposicionado no desktop quando possível.
5. O item sai do JSON da fence.

### Transferência entre fences

Atualmente o diretório do store contém o `FenceId`. Por isso, trocar o dono no JSON também move fisicamente o payload da pasta da fence A para a pasta da fence B.

Esse último movimento não é necessário para o comportamento do usuário e é a maior oportunidade arquitetural de simplificação.

---

## 4. Comparação das abordagens

| Abordagem | Custo típico | Correção em múltiplos monitores | Explorer restart | Risco para dados | Parecer |
|---|---:|---:|---:|---:|---|
| Estacionar em coordenada fora da tela | Muito baixo | Ruim | Ruim | Baixo, mas ícone reaparece | **Rejeitada** |
| `FILE_ATTRIBUTE_HIDDEN` | Muito baixo | Boa | Boa | Baixo | **Rejeitada:** o usuário pode mostrar itens ocultos |
| Hidden + System | Muito baixo | Boa | Boa | Abusa de atributos | **Rejeitada** |
| Mover para store próprio | Baixo no mesmo volume; alto entre volumes | Excelente | Excelente para arquivos | Exige transação e recovery | **Base aprovada** |
| Substituir/injetar no Explorer ou driver de filtro | Potencialmente baixo em runtime | Pode ser boa | Complexa | Muito alto | **Fora de escopo e não recomendado** |
| Apenas referenciar o arquivo original | Muito baixo | Não oculta item que está no Desktop | Boa | Muito baixo | Útil somente para origem fora do Desktop |

### Por que coordenadas não são uma alternativa válida

`LVM_SETITEMPOSITION` move um item do ListView; não existe semântica de “ocultar este item”. A própria documentação define as posições em coordenadas da view, afetadas pelo scroll e pelo modo de organização. O Explorer também pode reorganizar itens quando o controle está em auto-arrange. Logo, `-32000, -32000` é um truque de posicionamento, não um contrato de invisibilidade.

`IFolderView` não resolve esse requisito. A API pública oferece seleção e posicionamento de itens visíveis, mas não expõe uma operação suportada para excluir seletivamente um arquivo normal da renderização do Desktop enquanto ele continua enumerado na pasta.

### Por que o atributo Hidden também não resolve

O atributo `FILE_ATTRIBUTE_HIDDEN` apenas retira o item de uma listagem comum. A própria interface do Windows permite habilitar **Exibir → Itens ocultos**, e `SFGAO_HIDDEN` especifica que o item volta a ser exibido quando essa opção está ligada. Portanto, não atende ao requisito “o item agrupado não aparece solto”.

---

## 5. Performance da estratégia atual

### 5.1 Movimento físico

#### Mesmo volume local

Para o caso predominante — Desktop e `%LocalAppData%` no mesmo volume — mover um item normalmente é uma alteração de localização/nome no sistema de arquivos. A conclusão de que esse caso tende a ser barato é uma inferência consistente com o contrato de move do Windows; filtros de antivírus, sincronização e arquivos bloqueados ainda podem acrescentar latência.

Para atalhos `.lnk`, `.url` e documentos pequenos, o custo é aceitável e não há motivo de performance para voltar ao estacionamento por coordenadas.

#### Volumes diferentes

O Windows documenta que mover arquivos entre volumes equivale a copiar e depois excluir. O tempo passa a ser `O(bytes)`. Para diretórios, `Directory.Move` não suporta volumes diferentes; o código então cai para `IFileOperation`.

Consequências:

- um arquivo de vários gigabytes pode congelar a interface durante toda a cópia;
- uma pasta grande pode produzir uma operação parcial;
- origem de rede adiciona latência e indisponibilidade;
- ACLs podem mudar em movimentos entre volumes;
- arquivo em uso pode ser copiado sem que a origem seja removida, deixando duplicata.

#### OneDrive e outros provedores de nuvem

Desktop é frequentemente redirecionado para OneDrive. Mover o item para `%LocalAppData%` tira-o da raiz sincronizada. Documentação da Microsoft trata mover um arquivo para fora da pasta OneDrive como forma de parar sua sincronização. Além disso, placeholders online-only podem precisar ser hidratados quando acessados ou retirados da raiz de sincronização.

Isso é mais importante que o tempo bruto: o usuário pode perder proteção/sincronização em nuvem sem aviso.

### 5.2 Trabalho repetido em drops com vários itens

O código não processa um drop como uma transação/lote real:

- `AddDesktopEntry` chama `_desktop.Capture()` para **cada item** (`FenceWindow.xaml.cs`, por volta da linha 1066);
- cada captura enumera todos os ícones do desktop usando mensagens e memória remota;
- cada item chama `HideDesktopCounterparts`, que volta a percorrer **todos os itens da fence**;
- cada item pode regravar o JSON;
- cada chamada pode disparar novo ciclo de `SHChangeNotify`;
- após o `foreach`, os handlers de drop chamam hide e save novamente.

Para `n` itens recebidos e `d` ícones no desktop, só as capturas custam aproximadamente `O(n × d)`. O tracker é uma `List<AppliedItem>` e `FindItem` também faz buscas lineares, elevando o custo conforme a fence cresce.

Exemplo estrutural, não benchmark: ao adicionar 100 itens a um desktop com 100 ícones, o desenho atual pode fazer 100 enumerações completas do Explorer, milhares de chamadas de conceal/match e cerca de 100 regravações de JSON, quando deveria fazer uma captura, um planejamento, um commit e uma notificação.

**Conclusão:** o mecanismo físico é adequado; a orquestração do lote não é.

### 5.3 Thread da interface

Não há `Task`, fila de I/O ou worker dedicado no caminho de movimento. `File.Move`, `Directory.Move`, `IFileOperation.PerformOperations`, extração de ícone, persistência e notificações acontecem de forma síncrona a partir dos eventos WPF.

Isso é aceitável apenas quando o movimento é comprovadamente curto. A Microsoft recomenda operações assíncronas para I/O custoso em aplicativos desktop justamente para não bloquear a thread principal.

### 5.4 Custo em repouso

O custo permanente é baixo. O timer de sobrevivência do Explorer roda a cada segundo e faz verificações pequenas por fence. Não foi identificado consumo contínuo relevante causado pelo store. O principal risco de performance está nos picos de entrada/saída, não no app ocioso.

---

## 6. Achados de confiabilidade e release

### R1 — Bloqueador: movimento e JSON não formam uma transação recuperável

`LayoutStore.Save` serializa e usa `File.WriteAllText` diretamente. Não há arquivo temporário, flush durável, substituição atômica, backup nem journal.

Janelas de falha existentes:

- o arquivo é movido e o processo morre antes de persistir o novo path;
- o JSON é truncado durante uma gravação;
- a transferência entre fences move o payload, mas o JSON ainda aponta para a pasta antiga;
- o layout corrompido lança na carga e pode impedir o app de abrir.

O conteúdo tende a permanecer no store, mas pode ficar sem referência. **Bloqueia release público.**

### R2 — Bloqueador: falha ao transferir entre fences não é propagada

O fluxo atual:

1. remove o item do tracker da origem (`DetachHidden`);
2. remove o item da coleção da origem;
3. insere na coleção do destino;
4. tenta mover o payload para a pasta do destino;
5. retorna sucesso e salva mesmo que o conceal/move tenha falhado.

Se o arquivo estiver bloqueado ou a operação falhar, ele pode continuar na pasta física da fence A enquanto o JSON passa a dizer que pertence à B; nenhum dos trackers fica responsável por restaurá-lo corretamente. **Bloqueia release público.**

### R3 — Bloqueador: conclusão de `IFileOperation` é validada de forma insuficiente

Depois de `PerformOperations`, o código considera sucesso se o destino existir. Para diretório, uma pasta de destino parcial já satisfaz essa condição. A interface declara `GetAnyOperationsAborted`, mas o método não é chamado. Também não existe `IFileOperationProgressSink.PostMoveItem` para receber o `HRESULT` real de cada item e o path final escolhido pela Shell.

A documentação oficial alerta que `PerformOperations` pode retornar sucesso mesmo se a operação for abortada. **Bloqueia os casos de pasta grande/cross-volume e, portanto, o release geral.**

### R4 — Bloqueador: operações potencialmente longas bloqueiam a UI e não têm progresso

Pastas grandes, rede, outro volume e arquivos de nuvem são processados de forma síncrona, silenciosa e sem cancelamento. A janela pode parecer travada por tempo indeterminado. **Bloqueia release se o produto continuar aceitando paths arbitrários do Explorer.**

### R5 — Bloqueador de produto: item externo ao Desktop é movido sem confirmação

`DesktopHide.For` classifica qualquer path absoluto existente como `MoveToStore`, mesmo que venha de Documentos, Downloads, rede ou outra pasta. Assim, soltar um arquivo do Explorer na fence retira o arquivo de sua pasta original.

Para o foco “organizar o Desktop”, isso é mais agressivo que o necessário. Um item que já está fora do Desktop não precisa ser ocultado do Desktop.

Política recomendada:

- origem no Desktop: mover para o store;
- origem fora do Desktop: adicionar referência/atalho por padrão;
- oferecer “Mover para a fence” apenas como ação explícita, com confirmação e progresso.

Se a semântica atual for mantida, ela precisa estar explícita na UI antes do drop. **Bloqueia release para usuários não técnicos enquanto silenciosa.**

### R6 — Bloqueador: OneDrive/desktop redirecionado não está protegido

Não há detecção de sync root, placeholder, volume diferente ou Desktop redirecionado. O app pode retirar arquivos da proteção OneDrive e hidratar conteúdo online-only sem informar o usuário.

Antes do release, é necessário ao menos:

- detectar origem em sync root/placeholder;
- avisar que o item deixará a pasta sincronizada, ou escolher store dentro do mesmo domínio de sincronização;
- mostrar progresso para hidratação/cópia;
- validar restore com OneDrive online, offline e pausado.

### R7 — Alta: o store depende da fence e causa I/O desnecessário

`FenceItemStore.FolderFor(Guid fenceId)` acopla localização física e dono visual. Transferir entre fences deveria ser somente uma alteração de `fenceId`/ordem no layout. Hoje ela faz outro move, pode falhar por lock e cria uma nova janela de inconsistência.

Recomendação: store estável por item, não por fence:

```text
%LocalAppData%\DesktopFences\Items\{ItemId}\payload.ext
```

O `ItemId` e o `storagePath` não mudam quando o usuário reorganiza fences. Isso torna a transferência `O(1)` em I/O e elimina o risco R2 na origem.

### R8 — Alta: erros críticos são silenciosos

`SaveAll`, `SaveLayout`, hide, restore e várias operações Shell capturam exceções sem registro nem mensagem. Se o move falhar, o item pode ficar simultaneamente no desktop e na fence; se o save falhar, o usuário não sabe que o estado não é recuperável.

O release precisa de resultado por item, mensagem clara e log diagnóstico sem dados sensíveis desnecessários.

### R9 — Alta: protocolo OLE comunica Copy enquanto o app move

`ShellOleDropTarget.ChooseEffect` prefere `DROPEFFECT_COPY`, cujo contrato diz que a origem permanece intacta. O DesktopFences, porém, move o próprio path. O target também não publica `CFSTR_PERFORMEDDROPEFFECT`.

Mesmo que o comportamento aparente funcione no Explorer testado, o protocolo está semanticamente incompleto e pode variar com outras fontes Shell. Deve ser alinhado à política real: link para origem externa, move otimizado para Desktop, ou operação explicitamente escolhida.

### R10 — Média: identidade e lookup por nome/path são frágeis

O tracker usa lista linear e possui fallback por stem. Arquivos como `Projeto.lnk`, `Projeto.url` e uma pasta `Projeto` podem ficar ambíguos. Um `ItemId` persistente e índices por storage/original path eliminam a maior parte desse problema.

### R11 — Média: layout roaming e payload local podem divergir

O `layout.json` fica em `%AppData%` (Roaming), mas os payloads ficam em `%LocalAppData%`. Em perfil corporativo/roaming, o layout pode aparecer em outra máquina sem seus arquivos. Layout e payload precisam de política coerente ou de um identificador explícito de máquina/store.

### R12 — Média: testes não cobrem o coração do produto

Os testes Core validam decisões e paths, mas não exercitam:

- move/restore real;
- arquivo bloqueado;
- diretório parcial;
- cross-volume;
- OneDrive/placeholder;
- crash entre move e commit;
- layout corrompido;
- duplicata de nomes;
- remoção/pausa com falha parcial.

Para um app cujo diferencial é custodiar arquivos reais, estes são testes de release, não opcionais.

### R13 — Baixa: documentação e ordem real do fallback divergem

O comentário/ADR sugere `IFileOperation` com fallback para `File.Move`, mas `ShellFileMove.Move` tenta `File.Move`/`Directory.Move` primeiro e COM depois. A ordem do código é boa para performance no mesmo volume, porém a documentação deve descrever o comportamento real quando a implementação for estabilizada.

---

## 7. Arquitetura recomendada

Não é recomendada uma troca completa do mecanismo. A evolução mais segura é:

```text
Drop
  → classificar origem e política (move, link, namespace)
  → criar ItemId e registro PREPARED durável
  → executar/validar a operação física
  → atualizar layout por substituição atômica
  → marcar COMMITTED
  → atualizar UI e notificar Shell uma vez
```

### 7.1 Store estável por ItemId

Modelo conceitual:

```json
{
  "itemId": "guid",
  "name": "Relatorio.docx",
  "storagePath": "...\\Items\\item-guid\\Relatorio.docx",
  "originalPath": "...\\Desktop\\Relatorio.docx",
  "fenceId": "fence-guid",
  "state": "stored"
}
```

Benefícios:

- transferir entre fences não toca o sistema de arquivos;
- nomes iguais não colidem;
- tracker pode usar dicionário por `ItemId`;
- recovery sabe quais diretórios pertencem a quais itens;
- mudança de título/id visual não muda o path do payload.

### 7.2 Persistência atômica e journal

Requisitos mínimos:

1. gravar o próximo layout em arquivo temporário no mesmo volume;
2. fazer flush quando a operação envolver custódia de arquivo;
3. substituir `layout.json` atomicamente e manter `layout.json.bak`;
4. manter journal por operação com origem, destino, ItemId, tipo e estágio;
5. no startup, reconciliar journal + layout + diretórios do store;
6. nunca ignorar um payload órfão: mostrar recuperação e permitir restaurar.

### 7.3 Pipeline de lote

Um drop com vários itens deve:

1. capturar o desktop no máximo uma vez;
2. resolver e deduplicar todos os itens;
3. construir todos os planos sem mudar arquivos;
4. registrar a intenção;
5. executar o lote e coletar resultado por item;
6. atualizar a coleção uma vez;
7. salvar uma vez;
8. chamar `SHChangeNotify` uma vez por pasta afetada.

Isso implementa o comportamento já declarado na spec: “um `SHChangeNotify` no fim do lote”.

### 7.4 I/O fora da thread visual

- movimentos comprovadamente rápidos ainda devem retornar resultado estruturado;
- operação potencialmente longa deve usar worker/STA dedicado compatível com COM;
- mostrar estado “Movendo…”, progresso e cancelamento seguro;
- a UI só confirma pertencimento à fence depois do commit;
- cancelamento/falha mantém ou restaura a origem e não inclui o item como concluído.

### 7.5 Política de origem

| Origem | Padrão recomendado |
|---|---|
| Desktop do usuário/público | Move para store, com transação |
| Outra fence | Só altera dono/ordem no JSON |
| Explorer fora do Desktop | Referência/atalho; move somente explícito |
| Namespace Shell | Registry/namespace com ownership global e referência contada |
| OneDrive/sync root | Aviso/política sync-aware antes do move |
| Outro volume/rede | Progresso, confirmação e operação cancelável |

---

## 8. Melhorias de performance por prioridade

### Antes do release

1. Batch real: uma captura, um save, uma notificação.
2. Store por ItemId para eliminar moves entre fences.
3. Não capturar `SysListView32` quando o drop já contém path absoluto fora do Desktop.
4. Dicionários por ItemId/path normalizado no lugar da busca linear em `_applied`.
5. Operação longa fora da thread WPF com feedback.
6. Resultado por item; não tratar `Directory.Exists(destino)` como prova suficiente.

### Depois do gate de segurança

1. Cache de ícones por identidade/path + última alteração ou tipo.
2. Carregamento progressivo dos ícones da grade no startup.
3. Substituir polling de aspectos não críticos por eventos quando houver ganho comprovado.
4. Medir antes de otimizar layout/renderização; não há evidência de que WPF seja o gargalo principal.

---

## 9. Metas propostas de aceitação

São orçamentos iniciais, ainda não medições do produto:

| Cenário | Meta proposta |
|---|---|
| 1 atalho, mesmo volume | feedback visual imediato; conclusão P95 ≤ 150 ms |
| 50 atalhos, mesmo volume | uma captura/save/notificação; conclusão P95 ≤ 1,5 s |
| Transferência de 50 itens entre fences | sem I/O de payload; P95 ≤ 100 ms |
| Arquivo/pasta que levará > 500 ms | UI responsiva e progresso visível |
| App ocioso com 10 fences/500 itens | sem atividade de disco causada por re-hide contínuo |
| Falha em qualquer etapa | zero payload sem referência recuperável |
| Layout corrompido | app abre modo recovery e preserva store |

“Zero perda de referência recuperável” é requisito absoluto; não deve ser trocado por uma meta de latência.

---

## 10. Matriz obrigatória antes do GO

### Performance

- 1, 10, 50, 100 e 500 atalhos;
- 1, 10 e 50 arquivos pequenos;
- arquivo de 1 GB e pasta com muitos arquivos;
- mesmo volume e volume diferente;
- HDD/SSD de máquina mínima suportada;
- Desktop com 1 e 3 monitores, DPI misto;
- medir tempo total, tempo bloqueado da UI, capturas do Explorer, saves e notificações.

### Integridade e recovery

- matar o processo antes/depois de cada estágio do move e do commit;
- corromper/truncar `layout.json` e validar backup/recovery;
- falhar o segundo item de um lote e confirmar rollback/resultado parcial correto;
- arquivo aberto sem `FILE_SHARE_DELETE`;
- destino sem permissão;
- conflito de nome na restauração;
- dois itens com mesmo nome/stem;
- store com diretório órfão;
- remover/pausar/sair com uma restauração falhando;
- instalador/update/uninstall nunca apagar store não vazio.

### Shell e origens

- Desktop do usuário e Desktop público;
- Lixeira, Este Computador e Rede;
- Explorer fora do Desktop;
- OneDrive ativo, pausado, offline e placeholder online-only;
- Desktop redirecionado para outro volume ou rede;
- paths longos, caracteres Unicode, reparse points e atalhos quebrados;
- reinício do Explorer durante entrada e durante saída;
- Win+D e mudança de DPI conforme gate atual da Fase 5.

### Critério de aprovação

O GO só deve ocorrer quando:

- R1–R6 estiverem resolvidos;
- nenhum cenário deixar payload órfão sem recuperação automática;
- os casos longos não bloquearem a UI;
- a política para itens externos e OneDrive estiver explícita;
- a Fase 5 estiver validada no Windows 11 real;
- resultados e ambiente dos testes estiverem registrados.

---

## 11. Conclusão

O projeto tomou a decisão correta ao abandonar o estacionamento por coordenadas. A solução atual troca uma operação artificialmente barata e instável por uma operação de sistema de arquivos mais correta. Para atalhos e arquivos normais no mesmo volume, essa troca não representa um problema de performance relevante.

O próximo passo não deve ser procurar outro truque de hide. Deve ser transformar o mecanismo atual em uma pequena camada de custódia transacional:

- store estável por item;
- journal e persistência atômica;
- recovery no startup;
- lote verdadeiro;
- operação longa assíncrona e observável;
- política segura para origem externa e nuvem.

**Parecer final:** abordagem aprovada; implementação ainda não aprovada para usuários gerais.

---

## 12. Fontes oficiais consultadas

- Microsoft Learn — [About List-View Controls](https://learn.microsoft.com/en-us/windows/win32/controls/list-view-controls-overview)
- Microsoft Learn — [LVM_SETITEMPOSITION](https://learn.microsoft.com/en-us/windows/win32/controls/lvm-setitemposition)
- Microsoft Learn — [IFolderView::SelectAndPositionItems](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifolderview-selectandpositionitems)
- Microsoft Learn — [IFolderView2](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifolderview2)
- Microsoft Learn — [File Attribute Constants](https://learn.microsoft.com/en-us/windows/win32/fileio/file-attribute-constants)
- Microsoft Support — [View hidden files and folders in Windows](https://support.microsoft.com/en-us/windows/view-hidden-files-and-folders-in-windows-97fbc472-c603-9d90-91d0-1166d1d9f4b5)
- Microsoft Learn — [SFGAO flags](https://learn.microsoft.com/en-us/windows/win32/shell/sfgao)
- Microsoft Learn — [MoveFileEx](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw)
- Microsoft Learn — [.NET File.Move](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move)
- Microsoft Learn — [.NET Directory.Move](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.move)
- Microsoft Learn — [IFileOperation::PerformOperations](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-performoperations)
- Microsoft Learn — [IFileOperationProgressSink::PostMoveItem](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperationprogresssink-postmoveitem)
- Microsoft Learn — [IFileOperationProgressSink](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifileoperationprogresssink)
- Microsoft Learn — [SHChangeNotify](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shchangenotify)
- Microsoft Learn — [.NET File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace)
- Microsoft Learn — [.NET FileStream.Flush](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush)
- Microsoft Learn — [.NET FileStream e I/O assíncrono](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream)
- Microsoft Learn — [Known Folders e OneDrive Known Folder Move](https://learn.microsoft.com/en-us/windows/win32/shell/working-with-known-folders)
- Microsoft Learn — [Redirect and move Windows known folders to OneDrive](https://learn.microsoft.com/en-us/sharepoint/redirect-known-folders)
- Microsoft Support — [How to cancel or stop sync in OneDrive](https://support.microsoft.com/en-us/onedrive/how-to-cancel-or-stop-sync-in-onedrive)
- Microsoft Learn — [Cloud Files hydration and sync-root policies](https://learn.microsoft.com/en-us/windows/win32/api/cfapi/nf-cfapi-cfregistersyncroot)
- Microsoft Learn — [DROPEFFECT constants](https://learn.microsoft.com/en-us/windows/win32/com/dropeffect-constants)
- Microsoft Learn — [Shell Clipboard Formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard)
