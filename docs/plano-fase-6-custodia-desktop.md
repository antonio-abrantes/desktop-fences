# Plano complementar — Fase 6: custódia transacional de itens do Desktop

**Status:** planejada; não implementada.

**Spec:** [spec-fase-6-custodia-desktop.md](spec-fase-6-custodia-desktop.md).

**Regra de release:** os quatro marcos abaixo pertencem a uma única fase e a um único gate final.

---

## 1. Resultado da fase

Ao fim desta fase:

- cada item do Desktop sob custódia terá store estável por `ItemId`;
- mover entre fences será somente uma mudança atômica de ownership/ordem no layout;
- movimento físico, layout e recovery serão coordenados por journal;
- `layout.json` terá gravação atômica e backup válido;
- operações com vários itens usarão captura, planejamento, commit e notificação em lote;
- instalações existentes serão migradas do schema/store v1 para v2 sem perda silenciosa.

Não serão implementadas nesta fase políticas para origem externa, OneDrive, operações longas, progresso/cancelamento ou instalador.

---

## 2. Estratégia de entrega

A implementação pode ocorrer em quatro marcos internos, sempre na mesma branch e sem release entre eles:

| Marco | Bloco | Complexidade | Dependência |
|---|---|---:|---|
| 6.1 | Modelo v2, store por `ItemId` e migração recuperável | Alta | Base da fase |
| 6.2 | JSON atômico, backup, journal e recovery | Alta | 6.1 |
| 6.3 | Transferência entre fences somente por metadados | Média | 6.1–6.2 |
| 6.4 | Orquestração em lote e validação final | Média–alta | 6.1–6.3 |

O gate humano ocorre somente depois de 6.4. Uma falha que impeça concluir qualquer marco mantém toda a Fase 6 pendente.

Estimativa inicial para uma pessoa familiarizada com o projeto: **3 a 5 semanas**, incluindo testes automatizados, injeção de falhas e correções encontradas no gate local. A validação humana no Windows 11 é adicional. É uma estimativa de planejamento, não prazo fechado.

---

## 3. Preparação e linha de base

- [ ] Confirmar gate da Fase 5 no Windows 11 e registrar o resultado.
- [ ] Criar uma cópia de teste com layout v1 e payloads reais representativos.
- [ ] Medir a versão anterior com 1, 10, 50 e 100 itens: tempo, capturas do Explorer, saves, notificações e moves.
- [ ] Mapear todos os caminhos atuais de entrada, saída, Pausar, Sair, remover fence e transferência.
- [ ] Identificar qualquer save/hide/restore paralelo fora do `FenceHost` que precise convergir para o coordenador único.
- [ ] Definir checkpoints de falha habilitados somente em build de teste.

**Saída:** baseline registrado e nenhum fluxo de custódia esquecido.

---

## 4. Marco 6.1 — modelo v2, `ItemId` e store estável

### Core

- [ ] Adicionar `ItemId` obrigatório ao modelo de item.
- [ ] Diferenciar item `stored` de namespace sem introduzir API Windows no Core.
- [ ] Definir schema v2 e leitor compatível com schema v1.
- [ ] Derivar o caminho relativo do store pelo `ItemId`, não por `FenceId`.
- [ ] Criar índices por `ItemId`, original path e storage path normalizados.
- [ ] Validar unicidade de `ItemId` e impedir duas referências ao mesmo payload.

### Migração

- [ ] Planejar v1 → v2 antes de qualquer move.
- [ ] Gerar `ItemId` estável para todos os itens existentes.
- [ ] Mover payloads `{FenceId}` → `{ItemId}` sob journal de migração.
- [ ] Preservar layout v1 como backup até o commit v2.
- [ ] Não remover pasta antiga que ainda contenha qualquer arquivo.
- [ ] Interromper com recovery observável diante de item ausente ou ambíguo.

### Testes

- [ ] Round-trip v2.
- [ ] Leitura de todos os campos opcionais do v1.
- [ ] Migração de arquivo, pasta, `.lnk`, `.url` e namespace.
- [ ] Nomes iguais em fences distintas geram stores distintos.
- [ ] Reinício em cada checkpoint da migração mantém recovery possível.

**Saída:** modelo e store deixam de depender da fence; instalações v1 migram de forma recuperável.

---

## 5. Marco 6.2 — transação, JSON atômico, backup e recovery

### Persistência

- [ ] Substituir gravação direta por temporário + flush + validação + substituição atômica.
- [ ] Manter `layout.json.bak` como último layout válido anterior.
- [ ] Devolver falha de save ao chamador; remover catches silenciosos do caminho crítico.
- [ ] Impedir dois commits concorrentes do layout.
- [ ] Identificar revisão anterior/posterior no journal para evitar aplicar operação sobre layout inesperado.

### Journal

- [ ] Modelar tipos e estados definidos na spec.
- [ ] Persistir journal atomicamente antes do primeiro efeito físico.
- [ ] Coletar resultado individual e persistir checkpoints por estágio, sem save de journal por item.
- [ ] Manter journal até que payload, layout e UI tenham um estado coerente.
- [ ] Tornar conclusão e compensação idempotentes.

### Recovery

- [ ] Executar recovery antes de abrir as fences.
- [ ] Carregar backup se o layout principal estiver inválido.
- [ ] Reconciliar inbound, outbound, Pausar/Sair, remoção de fence e migração.
- [ ] Detectar payload esperado ausente e store órfão sem excluir dados.
- [ ] Exibir aviso simples e acesso à pasta quando a recuperação automática não concluir.
- [ ] Nunca criar layout vazio por cima de primário e backup inválidos.

### Resultado físico mínimo

- [ ] O serviço de move/restore deve retornar sucesso/falha e path final por item.
- [ ] O coordenador só avança ao commit quando a pós-condição física do item do Desktop estiver confirmada.
- [ ] Este contrato é restrito ao necessário para a transação da Fase 6; a ampliação geral de `IFileOperation` permanece futura.

### Testes

- [ ] Falha antes/depois de cada mudança de estado do journal.
- [ ] JSON truncado antes da promoção não substitui o principal válido.
- [ ] Principal corrompido carrega backup sem limpar store.
- [ ] Falha no segundo item compensa o primeiro.
- [ ] Recovery executado duas vezes produz o mesmo resultado.
- [ ] Conflito no restore nunca sobrescreve destino.
- [ ] Falha de save preserva ownership e UI anteriores.

**Saída:** nenhuma operação concluída pode deixar payload sem referência recuperável.

---

## 6. Marco 6.3 — transferência entre fences por metadados

- [ ] Remover o move físico entre diretórios de fences.
- [ ] Alterar ownership e ordem em uma cópia do documento em memória.
- [ ] Fazer um único commit atômico por transferência, inclusive multi-seleção.
- [ ] Aplicar as coleções visuais somente depois do commit.
- [ ] Em falha de save, manter itens na fence de origem e não alterar trackers.
- [ ] Não chamar hide, restore, captura do Desktop ou `SHChangeNotify`.
- [ ] Preservar `ItemId`, diretório do store, `originalPath` e metadados de restore.

### Testes

- [ ] Um item A → B: zero chamada ao serviço de move.
- [ ] Cem itens A → B: um save e zero I/O de payload.
- [ ] Falha de save: modelo e UI permanecem em A.
- [ ] Corpo transfere; chrome/recolhida mantém comportamento atual.
- [ ] Pausar/Sair depois da transferência restaura todos ao Desktop.

**Saída:** transferência visual não pode mais falhar por lock ou movimentação física desnecessária.

---

## 7. Marco 6.4 — processamento em lote

### Orquestração

- [ ] Criar pipeline único `Capture → Resolve → Plan → Journal → Execute → Commit → Apply UI → Notify`.
- [ ] Fazer no máximo uma captura do Desktop por gesto que precise dela.
- [ ] Resolver, normalizar e deduplicar a seleção inteira antes do primeiro move.
- [ ] Bloquear reentrada para `ItemId` que participe de operação ativa.
- [ ] Executar entrada com semântica tudo ou nada e compensação em falha.
- [ ] Reusar o mesmo coordenador em ejetar, Pausar, Sair e remover fence.
- [ ] Fazer um commit do layout por gesto.
- [ ] Notificar a Shell uma vez por diretório afetado, somente no final.
- [ ] Atualizar a UI depois do commit, em uma única aplicação de lote.

### Performance

- [ ] Substituir buscas lineares críticas por índices do marco 6.1.
- [ ] Eliminar chamadas redundantes a hide/save feitas dentro e depois do `foreach`.
- [ ] Comparar 1, 10, 50 e 100 itens com o baseline.
- [ ] Confirmar que um item no mesmo volume não teve regressão perceptível.
- [ ] Confirmar zero moves físicos entre fences.
- [ ] Registrar contadores de captura, save, notificação e move nos testes.
- [ ] Confirmar que os checkpoints duráveis do journal não crescem com a quantidade de itens.

### Testes

- [ ] Entrada em lote bem-sucedida: uma captura, um save, uma notificação.
- [ ] Falha no meio: lote não aparece parcialmente na UI/layout.
- [ ] Saída em lote: um save e destinos não destrutivos.
- [ ] Pausar/Sair/remover fence compartilham as mesmas garantias.
- [ ] Multi-seleção, reorder e namespace não sofrem regressão.

**Saída:** custo deixa de crescer por repetição de capturas e saves dentro do mesmo gesto.

---

## 8. Validação integrada

### Automatizada

- [ ] Todos os testes existentes continuam verdes.
- [ ] Novos testes Core cobrem modelo, migração, journal, recovery e lote.
- [ ] Testes de orquestração usam doubles para contar captura, move, save e notificação.
- [ ] Build Debug e Release sem erros nem novos avisos.
- [ ] Nenhum novo pacote NuGet sem decisão documentada.

### Windows 11 real

- [ ] Migrar layout/store real de teste com duas fences.
- [ ] Executar a matriz de 1, 10, 50 e 100 itens.
- [ ] Transferir blocos entre fences e observar que os paths físicos não mudam.
- [ ] Ejetar, Pausar, Retomar, remover fence e Sair.
- [ ] Matar o processo em cada checkpoint de teste e validar recovery.
- [ ] Testar layout principal corrompido e backup válido.
- [ ] Testar arquivo bloqueado durante entrada e durante restore.
- [ ] Reiniciar Explorer, mudar DPI e usar Win+D.
- [ ] Verificar manualmente que nenhum arquivo foi sobrescrito ou apagado.

### Documentação ao concluir

- [ ] Atualizar `docs/SESSION-HEADER.md` com código concluído e gate humano ainda aberto.
- [ ] Atualizar `docs/SPEC.md` para tornar o schema v2 o contrato vigente.
- [ ] Atualizar `docs/plano-implementacao.md` com os itens efetivamente concluídos.
- [ ] Atualizar `README.md` se paths, recovery ou comportamento visível mudarem.
- [ ] Registrar medições antes/depois e limitações conhecidas.

---

## 9. Gate final da Fase 6

O desenvolvedor valida no Windows 11:

- [ ] migração v1 → v2 sem perda;
- [ ] entrada e saída em lote;
- [ ] transferência entre fences sem I/O físico;
- [ ] falhas e encerramentos recuperados no próximo arranque;
- [ ] fallback para backup de layout;
- [ ] Pausar/Sair/remover fence restaurando corretamente;
- [ ] performance mantida ou melhorada frente ao baseline;
- [ ] Fase 5 continua funcionando.

Somente depois deste gate o instalador da Fase 7 pode ser autorizado. Os itens mantidos em stand-by continuam registrados na auditoria e não são condição para marcar os quatro blocos desta fase como implementados; a decisão final de release público deve ser reavaliada separadamente.

---

## 10. Stand-by — não implementar nesta fase

- validação completa de `IFileOperation` e operações parciais genéricas;
- trabalho assíncrono, progresso e cancelamento para pasta grande/outro volume/rede;
- política para arquivos externos ao Desktop;
- proteção específica para OneDrive, placeholders e Desktop redirecionado;
- alinhamento geral do protocolo OLE;
- cache progressivo de ícones e outras otimizações não medidas;
- instalador, atualização e desinstalação.

Esses pontos permanecem em [auditoria-fluxo-itens-performance-release.md](auditoria-fluxo-itens-performance-release.md) como riscos ou melhorias futuras.
