# Resultado técnico — Fase 6: custódia transacional do Desktop

**Estado:** fase fechada; implementação e gate manual no Windows 11 aprovados. Versão preparada: `v0.5.0`.

**Escopo aplicado:** somente itens do Desktop do usuário/público e itens de namespace já suportados. Arquivos externos ao Desktop são recusados pelo novo pipeline. OneDrive, Desktop redirecionado, progresso/cancelamento, ampliação geral de `IFileOperation` e instalador permanecem em stand-by.

---

## 1. Resultado entregue

- Schema `version: 2`, revisão monotônica e `ItemId` obrigatório.
- Store `%LocalAppData%\DesktopFences\Items\{ItemId}\{storageName}`; o layout não persiste o path absoluto do store.
- Migração v1 → v2 planejada integralmente antes do primeiro move, journalizada e com o v1 preservado em `layout.json.bak`.
- `layout.json.tmp` com flush durável, validação por desserialização e promoção atômica; fallback para `layout.json.bak`.
- Journal atômico `Prepared → PayloadChanged → LayoutCommitted → Completed`, revisão antes/depois e recovery idempotente antes da abertura das fences.
- Compensação em ordem inversa quando um item falha no meio do lote. Compensação incompleta mantém journal recuperável.
- Stores órfãos são preservados e registrados em `%LocalAppData%\DesktopFences\recovery-orphans.log`.
- Transferência entre fences altera somente ownership/ordem em uma cópia do layout; a UI muda apenas após o commit e não há chamada de custódia física.
- Entrada, saída, Pausar, Retomar, Sair e remover fence usam o mesmo coordenador e um único commit por gesto.
- Seleções são resolvidas/deduplicadas antes do movimento, há no máximo uma captura do Desktop por gesto e reentrada é bloqueada por `ItemId`.
- A Shell é notificada uma vez ao final do lote.
- A migração reconhece o estado v1 misto deixado por restore parcial: `Path` ainda aponta para o store antigo, enquanto o payload já existe em `OriginalPath` no Desktop. Itens já restaurados e itens ainda guardados migram juntos sem perder o cadastro.

---

## 2. Comparação estrutural de performance

A versão `v0.4.0` foi usada como baseline de código. No fluxo de entrada anterior, cada `AddDesktopEntry` fazia uma captura, percorria novamente todos os itens já adicionados e gravava o layout; o handler externo repetia hide/save no final. A tabela registra chamadas determinísticas do código, não tempo de relógio:

| Itens no gesto | Capturas v0.4.0 | Tentativas de conceal v0.4.0 | Saves de layout v0.4.0 | Rodadas de notify v0.4.0 | Capturas Fase 6 | Planos/moves Fase 6 | Saves de layout Fase 6 | Rodadas de notify Fase 6 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1 | 2 | 3 | 1 | 1 | 1 | 1 | 1 |
| 10 | 10 | 65 | 21 | 10 | 1 | 10 | 1 | 1 |
| 50 | 50 | 1.325 | 101 | 50 | 1 | 50 | 1 | 1 |
| 100 | 100 | 5.150 | 201 | 100 | 1 | 100 | 1 | 1 |

O novo fluxo troca repetição quadrática de resolução/conceal e saves por planejamento linear. O journal acrescenta quatro checkpoints duráveis por operação, sempre constantes em relação ao número de itens. Para transferência entre fences, o custo físico é zero para 1 ou 100 itens; há somente um clone/commit de metadados e aplicação visual. O gate real aprovou a performance percebida da entrega.

Medição funcional adicional do move físico no mesmo volume, em diretório temporário e build Debug desta máquina:

| Itens | Tempo do estágio físico |
|---:|---:|
| 1 | 0,659 ms |
| 10 | 9,351 ms |
| 50 | 39,134 ms |
| 100 | 125,554 ms |

Esses números comprovam a matriz funcional e servem como referência local; o tempo percebido com Explorer/UI foi aprovado no gate Windows 11.

---

## 3. Evidência automatizada

- Build Debug: sucesso, zero erros e zero avisos.
- Build Release: sucesso, zero erros e zero avisos.
- Testes: 159 aprovados, zero falhas — 131 de Core e 28 de App/Native.
- Cobertura nova: round-trip/validação v2, backup e JSON truncado, migração de arquivo/pasta/`.lnk`/`.url`/namespace, nomes iguais, estado v1 misto (parte no Desktop e parte no store), journal atômico, todos os estados de recovery, revisão pós-commit, idempotência, compensação do segundo item, ordem inversa, store órfão, transferência de 1/100 itens apenas por ownership, checkpoints de crash do coordenador, contadores por lote e movimentos físicos temporários de 1/10/50/100 itens.
- Nenhum pacote NuGet foi adicionado.

### Regressão encontrada durante a validação

Em 16/08/2026, o primeiro arranque sobre dados v1 reais expôs um caso não coberto: seis atalhos já tinham regressado aos seus caminhos originais no Desktop, mas o `layout.json` v1 continuava com `path` apontando para o store; um sétimo atalho permanecia fisicamente no store antigo. A migração interpretava o primeiro `path` ausente como perda de payload e interrompia o arranque. A regra foi corrigida para consultar `originalPath` antes de declarar perda, mantendo o erro seguro quando nenhuma das duas cópias existe. Os sete cadastros e sete payloads foram confirmados intactos antes da correção; nenhum dado local foi alterado pela inspeção.

---

## 4. Limitações e decisões mantidas

- O processamento continua síncrono na thread de interface; pastas grandes, rede, outro volume, OneDrive, placeholders e progresso/cancelamento continuam fora desta fase.
- A validação genérica de operações parciais de `IFileOperation` não foi ampliada; a Fase 6 confirma as pós-condições necessárias ao seu próprio lote.
- Não há política nova para itens externos: o coordenador da Fase 6 os recusa.
- Stores órfãos nunca são apagados automaticamente.
- Uma recuperação que não consiga reconciliar payload interrompe o arranque, mostra erro e informa a pasta preservada; não cria layout vazio.
- O instalador da Fase 7 não foi iniciado.

---

## 5. Matriz para o gate Windows 11

Executar sobre cópia segura representativa:

- [x] migrar layout v1 com duas fences, arquivos, pasta, `.lnk`, `.url` e namespace;
- [x] entrada e saída com 1, 10, 50 e 100 itens, registrando tempo percebido/total;
- [x] transferir 1 e 100 itens entre fences e confirmar que os paths em `Items\{ItemId}` não mudam;
- [x] validar multi-seleção, reorder, chrome, fence recolhida e conflito de nomes no restore;
- [x] Pausar → Retomar; remover fence; Sair; abrir novamente;
- [x] encerrar o processo nos estados `Prepared`, `PayloadChanged` e `LayoutCommitted` e confirmar recovery;
- [x] corromper somente o principal e confirmar fallback para backup;
- [x] bloquear o segundo item de um lote e confirmar ausência de estado parcial;
- [x] reiniciar Explorer, alternar DPI/monitor e testar Win+D;
- [x] confirmar que nenhum arquivo foi sobrescrito ou apagado.

**Parecer final:** `GO` para a entrega `v0.5.0` dentro do escopo definido da Fase 6. O instalador permanece como Fase 7 e não foi iniciado.
