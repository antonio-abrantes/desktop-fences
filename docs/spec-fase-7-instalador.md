# Especificação complementar — Fase 7: instalador e desinstalação segura

**Status:** implementada e validada automaticamente para a `v0.6.0`; gate manual pendente.

**Objetivo:** distribuir o DesktopFences por um instalador por usuário, com caminho estável, atualização no lugar, seleção de idioma e desinstalação que nunca remova dados antes de devolver com segurança os itens sob custódia ao Desktop.

---

## 1. Decisões fechadas

- Tecnologia: Inno Setup, sem pacote NuGet e sem serviço residente.
- Escopo: dois instaladores, `win-x64` e `win-arm64`.
- Instalação por usuário, sem elevação, em `%LocalAppData%\Programs\DesktopFences`.
- `AppId` estável: versões novas atualizam a instalação existente e mantêm uma única entrada em Aplicativos instalados.
- Downgrade é bloqueado quando a versão instalada for mais nova que a versão do setup. Ao manter configurações na desinstalação, o marcador de versão também é preservado; “remover tudo” apaga ambos.
- O zip portable continua disponível como artefato secundário.
- Português é o idioma inicial selecionado no instalador; Inglês também está disponível.
- O idioma escolhido é persistido em `uiLanguage` e continua alterável nas Configurações do aplicativo.

---

## 2. Instalação e atualização

Quando não houver dados anteriores, o setup instala os binários, grava o idioma escolhido e oferece abrir o aplicativo.

Quando encontrar configuração existente, o setup oferece:

1. **Usar configurações existentes** — opção recomendada; preserva fences, aparência, posições, preferências e dados de recuperação.
2. **Começar com configurações novas** — primeiro devolve todos os itens ao Desktop, arquiva o estado anterior e somente depois cria uma configuração vazia no idioma escolhido.

Antes de substituir uma instalação existente, o setup executa a manutenção segura. Se o aplicativo estiver aberto, solicita a ele uma saída normal pelo canal local de manutenção. O setup nunca usa encerramento forçado como caminho principal.

O valor `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DesktopFences`, quando já habilitado pelo usuário, é atualizado para o caminho estável da instalação sem criar entrada duplicada.

---

## 3. Desinstalação

O desinstalador oferece duas escolhas:

1. **Manter configurações** — devolve os itens ao Desktop, remove programa, atalhos e inicialização automática, mas preserva layout, preferências, snapshot, recovery e store para uma reinstalação futura.
2. **Remover tudo** — devolve os itens ao Desktop e valida o resultado; somente depois remove layout, backup, journals, snapshot, logs e store.

Se a devolução ou a validação falhar:

- a desinstalação é cancelada;
- binários, `DesktopFences.Recovery.exe`, layout e store permanecem disponíveis;
- nenhuma pasta de dados é apagada.

Itens do Desktop Público continuam sendo devolvidos ao Desktop gravável do usuário. Nenhum path externo ao Desktop entra no fluxo.

---

## 4. Protocolo de manutenção

O aplicativo principal expõe um canal local apenas enquanto está em execução. O modo de manutenção do executável:

1. tenta obter exclusividade pelo mutex já existente;
2. se o app estiver aberto, pede `PrepareExit` pelo canal local;
3. aguarda a liberação do mutex por tempo limitado;
4. carrega o melhor layout válido e reconcilia journals pendentes;
5. devolve os payloads ao Desktop pela transação de saída vigente, mantendo o layout quando solicitado;
6. atualiza a inicialização automática e o idioma;
7. somente no modo destrutivo, arquiva ou remove dados após sucesso completo.

O protocolo não cria polling contínuo. Fora de instalação, atualização ou desinstalação, o único custo é um servidor local bloqueado aguardando conexão, sem trabalho por frame e sem I/O periódico.

---

## 5. Idioma

- Idiomas do setup: Português e Inglês.
- Default do seletor: Português.
- A escolha do setup define `uiLanguage` como `pt` ou `en`.
- Instalação sobre configuração existente altera apenas essa preferência; não renomeia fences nem nomes de arquivos.
- O seletor Sistema / Português / Inglês dentro do app permanece disponível e tem precedência depois que o usuário o alterar.

---

## 6. Segurança e limites

- O setup corre `asInvoker`; não instala serviço, driver ou tarefa administrativa.
- Nenhum dado em `%AppData%\DesktopFences` ou `%LocalAppData%\DesktopFences` é removido antes do sucesso da liberação transacional.
- Depois de uma liberação normal, item físico confirmado como ausente no store e no Desktop é considerado removido externamente: somente sua referência é retirada por commit atômico e os demais itens continuam o arranque.
- A reconciliação acima exige store e Desktop inspecionáveis. Falta de permissão, path externo ou estado ambíguo preserva a referência e mantém o bloqueio de segurança anterior.
- O instalador não promete recuperar falha física do disco nem dados apagados externamente.
- Assinatura Authenticode permanece fora desta fase; a ausência de assinatura pode gerar aviso do SmartScreen.
- O gate manual deve cobrir instalação limpa, upgrade, ambos os modos de configuração, os dois modos de desinstalação, idioma e inicialização com o Windows.

---

## 7. Critérios de aceite automatizados

- parser do comando de manutenção aceita somente modos e idiomas conhecidos;
- idioma é aplicado por gravação atômica sem perder fences ou itens;
- reset só remove dados depois de uma liberação confirmada;
- falha de liberação preserva layout, store e journals;
- IPC rejeita comandos desconhecidos e confirma somente depois de `PrepareExit` bem-sucedido;
- reconciliação do arranque remove apenas itens confirmadamente ausentes, preserva namespace/itens disponíveis e não aceita indisponibilidade como exclusão;
- build/test/publish `win-x64` e `win-arm64` verdes;
- script Inno compila os dois instaladores;
- workflow da tag `v*` publica zip portable e setup por arquitetura.
