# SESSION-HEADER — DesktopFences 1.0

> Atualize os campos desta seção a cada nova sessão ou conclusão de fase antes de enviar.

---

## Contexto da Sessão ou fase

**Projeto:** DesktopFences 1.0
**Etapa atual:** Hotfix de segurança da Fase 6 (**`v0.5.1` implementada; gate manual pendente**).
**Objetivo desta sessão:** impedir recorrência do incidente de downgrade, entregar recuperação independente por um clique e preservar posições dos ícones na ejeção, em Pausar e em Sair, sem iniciar o instalador.

---

## Documentos que Você Deve Ler Antes de Qualquer Ação

Leia os seguintes arquivos integralmente, nesta ordem, antes de responder:

1. `AGENTS.md` — regras de comportamento, o que pode e não pode fazer, como gerenciar fases
2. `docs/SPEC.md` — arquitetura, stack, modelo de ícones, UI, persistência, riscos
3. `docs/plano-implementacao.md` — estado atual e ordem das fases
4. `README.md` — o que o produto é e como rodar

Para implementar a próxima fase, ler também integralmente:

5. `docs/spec-fase-6-custodia-desktop.md`
6. `docs/plano-fase-6-custodia-desktop.md`

Apoio (quando o assunto for relevante):

7. `docs/pos-mvp1.md` — mapa curto do ciclo (sem empurrar vizinhos)
8. `docs/adr/` — decisões já fechadas

> Não inicie nenhuma implementação antes de confirmar que leu todos os documentos obrigatórios acima.

---

## Instrução de Revisão

A Fase 6 continua **fechada e validada no Windows 11**. Após um incidente real causado pela execução de um binário antigo sobre dados v2, foi autorizado um hotfix de custódia: proteção contra downgrade, validação estrita da migração v1, snapshot atômico das posições e `DesktopFences.Recovery.exe` separado, conservador e não destrutivo. A revisão também corrigiu a ejeção para o ponto do cursor, restaurou as posições originais em Pausar/Sair e eliminou o bloqueio causado por itens do Desktop Público, que passam a ser devolvidos ao Desktop gravável do usuário. A versão interna é `0.5.1`. Build Release sem avisos de código, 176 testes e publishes self-contained `win-x64`/`win-arm64` estão aprovados; o aviso NU1900 observado antes do restore sem auditoria foi somente indisponibilidade de rede do nuget.org. O gate manual específico do hotfix precisa ser concluído antes do parecer final de release pública. O instalador é a Fase 7 e não foi iniciado.

```
ETAPA FECHADA  : Fase 6 — custódia transacional de itens do Desktop (validada no Windows 11)
HOTFIX ATUAL   : v0.5.1 — recuperação de emergência implementada; gate manual pendente
ETAPA SEGUINTE : Fase 7 — instalador (não iniciada; exige pedido explícito)
FORA DO CICLO  : duplo clique no vazio do desktop · packs de tema · empurrar fence de baixo ao expandir
```

---

## Lembrete de Regras Críticas

> Os itens `[x]` são estados confirmados pelo agente no repositório. O item `[ ]` é restrição ativa.

- [x] Documentação de contexto (`AGENTS.md`, `SESSION-HEADER`, spec, plano, ADRs)
- [x] Solution fatiada em Core / Native / App + testes de domínio
- [x] Native lê ícones no `SysListView32` (Progman e fallback WorkerW); hide via store + registry
- [x] Fence translúcida (vidro escuro, wallpaper visível), radius 8, atrás dos apps
- [x] Drop inbound (desktop/Explorer) com ghost + seta; vários ícones de uma vez; drop outbound devolve o ícone
- [x] Grade própria (`SHGetFileInfo`), seleção, reordenação, hide/restore dos ícones reais
- [x] Mover pela alça ⋮⋮; resize ao vivo; recolher; título só com duplo clique no texto (clique fora grava)
- [x] Persistência `layout.json`; bandeja Pausar / Retomar / Configurações / Sobre / Sair; ícone do app
- [x] Desenvolvedor validou no Windows 11: inbound drop, ghost e cursor (seta, não “proibido”)
- [x] Fase 1 fechada (N fences, settings, cores, Sobre, iniciar com o Windows + mutex)
- [x] Fase 2 fechada (UI pt/en, seletor Sistema/PT/EN, `uiLanguage` opcional)
- [x] Ícones de sistema (Este computador, Lixeira, Rede): ícone e abrir via namespace do desktop, não só path de ficheiro
- [x] Desenvolvedor validou no Windows 11: Lixeira / Este computador / Rede visíveis na fence (patch `v0.3.1`)
- [x] Fase 3 fechada: soltar no corpo de outra fence muda o dono; chrome/recolhida não ejetar; desktop ejetar; tracker de hide segue o item
- [x] Desenvolvedor validou no Windows 11: arrastar um ou vários itens de uma fence para outra (ícone real continua escondido)
- [x] Fase 4 fechada: ímã às bordas da área de trabalho e às arestas de outras fences ao soltar a alça e no resize; não empurra vizinhos
- [x] Desenvolvedor validou no Windows 11: soltar a alça ⋮⋮ perto da borda da tela / de outra fence cola; resize idem; vizinho não se mexe
- [x] Fase 5 fechada: Explorer morre → re-Conceal; DPI atualiza clip; Win+D reancora a fence acima de Progman/WorkerW; hide por move para o store
- [x] Fase 6 documentada: store por ItemId; transação/JSON atômico/backup/recovery; transferência por metadados; lote; somente Desktop
- [x] Fase 6 implementada no código: schema v2/migração; commit atômico/journal/recovery; ownership por metadados; pipeline em lote
- [x] Fase 6 validada automaticamente: builds Debug/Release sem avisos e 159 testes verdes (131 Core + 28 App/Native)
- [x] Desenvolvedor validou a matriz da Fase 6 no Windows 11 real
- [x] Versão do aplicativo alinhada para `0.5.0` / tag `v0.5.0`
- [x] Hotfix `0.5.1`: backup v2 prevalece sobre principal v1 rebaixado; migração rejeita payload incompatível e stem vazio não resolve para o Desktop
- [x] Snapshot atômico e independente das posições é atualizado uma vez no arranque, antes de recovery, migração ou retomada de custódia
- [x] `DesktopFences.Recovery.exe` copia sem apagar o store, preserva conflitos, arquiva diagnóstico e só reseta metadados após sucesso completo
- [x] Recuperação preserva a organização atual por padrão; reaplicar posições antigas exige seleção explícita
- [x] Falha de segurança no arranque oferece handoff para a recuperação separada
- [x] Build local copia `DesktopFences.Recovery.exe` para junto do aplicativo; handoff também funciona em Debug
- [x] Ejeção posiciona o primeiro item no ponto do cursor e distribui múltiplos itens próximos, com tentativas curtas após a atualização assíncrona do Explorer
- [x] Pausar/Sair usam o destino físico realmente restaurado e repetem por janela limitada a posição original; snapshot cobre coordenada ausente
- [x] Item vindo do Desktop Público é restaurado no Desktop do usuário, sem exigir administrador e sem bloquear o restante do lote
- [x] Hotfix validado automaticamente: 176 testes verdes (138 Core + 38 App/Native), build Release com 0 avisos de código/0 erros e publishes `win-x64`/`win-arm64` com os dois executáveis
- [ ] Desenvolvedor validar o hotfix `v0.5.1` no Windows 11 e os dois executáveis nos zips de release
- [x] Desenvolvedor validou no Windows 11: Explorer/DPI e Win+D / Mostrar ambiente de trabalho; fences permanecem visíveis; Pausar/Sair continua restaurando
- [ ] Não implementar a Fase 7 (instalador / path estável no arranque) sem novo pedido explícito
- [ ] Não implementar duplo clique no vazio do desktop, packs de tema, nem empurrar a fence de baixo ao expandir

Se qualquer inconsistência for encontrada na revisão, reporte antes de sugerir qualquer ação.
