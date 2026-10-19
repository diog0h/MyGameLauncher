# MyGameLauncher

## Abrir

Duplo clique em **MyGameLauncher.exe**. (Ou, no Terminal nesta pasta: `dotnet run`.)

## Recriar o .exe depois de alterações

No Terminal, nesta pasta:

```
dotnet publish -c Release -o .
```

## O que faz

- **Início**: banner do último jogo jogado ("Continuar a jogar"), filas "Pronto a jogar", "Favoritos" e uma por coleção.
- **Cor por jogo**: o fundo da app (e a barra de título no Windows 11) muda para a cor da capa do jogo em destaque.
- **Todos os jogos / Favoritos / Coleções**: grelha de capas, pesquisa e ordenação (recentes, nome, mais jogados, tamanho).
- **Página do jogo**: banner, logótipo, Jogar, favorito, pasta, ver na Steam, tempo jogado, coleções e sessões recentes.
- **Tempo de jogo**: total (lido da Steam), gráfico dos últimos 7 dias e ranking. As sessões são registadas
  enquanto a app está aberta, mesmo que abras o jogo pela Steam.
- **Lojas**: Steam, Epic Games e Ubisoft Connect são encontradas sozinhas (sem duplicados). Qualquer outro jogo
  ou launcher pode ser adicionado à mão (barra lateral → Adicionar jogo), com o nome de loja que quiseres.
- **Barra de ajuda do comando**: aparece em baixo quando usas o comando, com os ícones Xbox ou PlayStation.
- **Imagens**: primeiro da cache da própria Steam, depois da internet, depois do SteamGridDB (chave em Definições).
  Também podes escolher as tuas (capa, plano de fundo, logótipo e capa ampla) ao adicionar um jogo ou em
  **Personalizar**, na página de cada jogo.
  Ficam guardadas no PC.
- **Segundo plano** (opcional, em Definições → Windows): abrir com o Windows e continuar na área de notificação ao
  fechar, para contar o tempo de jogo mesmo quando abres os jogos pela Steam ou pela Epic.
- **Atualizações**: avisa quando há uma versão nova no GitHub. Os erros ficam em
  `%LocalAppData%\GameLauncher\logs` (Definições → Diagnóstico → Abrir pasta dos registos).
- **Comando e teclado**: comandos Xbox e PlayStation (DualShock 4 e DualSense, por cabo ou Bluetooth).
  Ver a tabela em Definições. F11 = ecrã inteiro.
- **Idioma**: Português, English, Español, Français, Deutsch (Definições → Idioma). Na primeira vez usa a
  língua do Windows; se não for uma destas, inglês. Os textos estão em
  `Localization/Loc.cs`; para acrescentar uma língua junta-se uma coluna a cada linha.

## Criar o instalador (para partilhar)

1. Instala o Inno Setup 6 (gratuito): https://jrsoftware.org/isdl.php
2. Duplo clique em **criar-instalador.bat**.
3. O instalador fica em `installer\Output\MyGameLauncher-Setup-1.1.0.exe`.

O instalador leva o .NET incluído (funciona em qualquer PC com Windows 10/11), cria atalhos no Menu Iniciar
(e no Ambiente de Trabalho, se escolhido), aparece em Definições → Aplicações para desinstalar e, ao desinstalar,
pergunta se também deve apagar os dados. Para uma versão nova, muda a versão em `GameLauncher.csproj` e em
`installer\MyGameLauncher.iss` (MyAppVersion) — o instalador novo atualiza o anterior.

Não tem assinatura digital: noutros PCs o Windows pode mostrar o aviso do SmartScreen
("Mais informações" → "Executar mesmo assim").

## Créditos e licenças

Ver Definições → Sobre, e o ficheiro `THIRD-PARTY-NOTICES.txt` (incluído no instalador).

## Onde ficam os dados

`%LocalAppData%\GameLauncher\` — `dados.json` (favoritos, coleções, sessões, definições) e `cache\` (imagens).

## Estrutura do código

- `Scanners/` — leitura das lojas (Steam, Epic, Ubisoft) e dos jogos adicionados à mão.
- `Services/` — estado da app, imagens, registo de sessões, comandos (XInput + HID PlayStation), gravação de dados.
- `Localization/` — textos em todas as línguas.
- `Views/` — páginas (Início, Biblioteca, Jogo, Tempo de jogo, Adicionar jogo, Definições).
- `App.xaml` — estilos partilhados (botões, cartões de capa).
- `MainWindow.xaml` — barra lateral, pesquisa, navegação, teclado/comando, ecrã inteiro.
