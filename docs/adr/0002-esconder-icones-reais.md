# ADR-0002: Esconder ícones reais (mover para store + namespace)

**Date**: 2026-08-15
**Status**: accepted
**Deciders**: Antonio + agente

## Context

Não existe API pública do Shell (`IFolderView`) para “não desenhar este ícone” enquanto o ficheiro continua na pasta Desktop. Coordenadas no ListView falham com vários monitores (ecrã virtual). `FILE_ATTRIBUTE_HIDDEN` some com a opção **Exibir → Itens ocultos**. `Hidden + System` ainda depende de “mostrar ficheiros de sistema” e é semanticamente errado num `.docx`.

O requisito: **enquanto o item pertence a um Fence, não pode aparecer solto no Desktop**, mesmo com itens ocultos ligados.

## Decision

1. Atalho / ficheiro / pasta: **mover** para um store fora do Desktop (`IFileOperation` se o COM responder; senão `File.Move` / `Directory.Move`). Desde a Fase 6, o path vigente é `%LocalAppData%\DesktopFences\Items\{ItemId}\{storageName}`; o antigo `{FenceId}` permanece apenas como formato de migração v1. O Explorer não tem o que desenhar.
2. Guardar `originalPath` no `layout.json` para devolver o objeto no Pausar, Sair, ejetar, remover fence.
3. Lixeira / Este computador / Rede: DWORD `1` em `HKCU\...\Explorer\HideDesktopIcons\NewStartPanel` e `ClassicStartMenu` (não são ficheiros).
4. Um `SHChangeNotify` no lote. Sem coordenadas no ListView, sem `FileAttributes.Hidden` como mecanismo, sem `FileSystemWatcher`, sem loop de 1s.

`SysListView32` continua só para hit-test de drop e reposicionar o ícone **depois** de voltar ao Desktop.

### Evolução concluída antes do instalador

A decisão de mover o item real para um store continua aceita. A Fase 6 preservou esse mecanismo, trocou o diretório `{FenceId}` por `{ItemId}`, acrescentou transação/recovery e tornou a transferência entre fences somente metadados. A fase está fechada e validada. Ver [spec-fase-6-custodia-desktop.md](../spec-fase-6-custodia-desktop.md).

## Alternatives Considered

### `LVM_SETITEMPOSITION` fora da vista
- **Why not**: ecrã virtual; snap-to-grid manda o ícone para outro monitor.

### `FILE_ATTRIBUTE_HIDDEN`
- **Why not**: “Mostrar itens ocultos” volta a pintá-los (`SFGAO_HIDDEN`).

### `Hidden + System`
- **Why not**: ainda reversível; abusa o atributo System.

### Toggle global de ícones do desktop
- **Why not**: some com tudo, inclusive o que não está em fence.

## Consequences

### Positive
- Independente de N monitores, DPI, “itens ocultos”, restart do Explorer.
- Pausar/Sair/ejetar devolve o ficheiro ao `originalPath` (pasta Desktop do utilizador/público, ou a origem se não era o Desktop).

### Negative
- O path físico muda enquanto o item está na fence (um `Contrato.docx` passa a viver no store). Conceito: a fence é dona do objeto até o ejetar.
- Mover da Desktop pública pode falhar sem permissão; nesse caso o ícone fica visível.

### Risks
- Crash a meio do move: gravar o layout **depois** do move. Arranque: se o store não existe, `ResolveExisting` volta a achar o ficheiro no Desktop pelo nome.
- `IFileOperation` COM: fallback para `File.Move` (já houve AV com COM do FolderView).
