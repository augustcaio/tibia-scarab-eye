# Tibia Scarab Eye

Traga para perto do centro da tela as partes do cliente do Tibia que você mais
precisa olhar, e leve exatamente o mesmo resultado para a sua live ou gravação
no OBS.

O Tibia Scarab Eye recorta áreas do cliente (minimapa, ícones de ação, barras,
slots de itens e qualquer outra região) e as exibe como pequenas janelas por
cima da área de jogo, com a borda no estilo do próprio Tibia. Com a
sincronização ligada, as mesmas overlays aparecem dentro do OBS, no mesmo lugar
e no mesmo tamanho.

![Overlays do Tibia Scarab Eye sobre o jogo, vistas pelo OBS](docs/images/overlays-no-obs.png)

*Cena do OBS: o minimapa, os ícones de ação e os slots de itens aparecem como
overlays sobre o mapa, enquanto o cliente continua inalterado.*

## O que ele faz

- **Cria e posiciona num só lugar.** O editor de áreas mostra o jogo ao vivo.
  Arraste no vazio para criar uma área; arraste uma área para posicioná-la. Por
  padrão o recorte se encaixa em blocos de slots do Tibia (34, 70, 106 px…), do
  slot da action bar até a largura do minimapa, e a posição se prende a uma
  grade invisível de 8 px. Roda do mouse dá zoom; segure **Espaço** (como no
  Photoshop), o botão do meio ou o direito e arraste para mover a visão.
- **Camadas.** Um painel lista as áreas de cima para baixo, com olho (esconder),
  cadeado (travar), ordem (subir e descer) e opacidade. A ordem vale na tela e
  no OBS, e fica salva no layout.
- **Seleção múltipla e alinhamento.** Ctrl + clique, Shift + clique, Shift +
  arrastar no vazio ou Ctrl + A selecionam várias áreas; elas se movem juntas.
  Botões alinham à esquerda, ao centro, à direita, ao topo, ao meio e à base, e
  distribuem com vãos iguais. Guias rosas mostram quando uma borda ou centro
  coincide com o de outra área.
- **Desfazer e refazer** (Ctrl + Z, Ctrl + Y), **duplicar** (Ctrl + D), leitura
  da posição do pixel sob o mouse, setas para mover (Shift: 1 px), Alt + setas
  para mover o recorte, medidas exatas e renomear.
- **Ajusta cada overlay depois**, sem precisar remover a área: o recorte, o
  tamanho (zoom da overlay) e a opacidade.
- **Controla a opacidade** de cada overlay (de 20% a 100%).
- **Trava para jogar.** Em modo jogo, as overlays aparecem sobre o jogo e
  deixam o mouse passar para o cliente. Em modo edição elas ficam escondidas e
  você as cria e posiciona no editor.
- **Atalhos globais** que funcionam mesmo com o painel minimizado:
  `Ctrl + Shift + F8` alterna edição e jogo, `Ctrl + Shift + F9` mostra ou
  oculta as overlays na tela e no OBS.
- **Detecta o Tibia sozinho.** Não há janela para escolher: o programa acha o
  cliente aberto e uma lâmpada na janela principal mostra o estado (vermelha
  procurando, verde lendo com o nome do personagem, âmbar se o jogo está
  minimizado).
- **Presets por personagem, salvos sozinhos.** Cada preset é um conjunto de
  áreas com nome, ligado a um personagem ou a todos. Ao entrar com um
  personagem, volta o último preset dele. Tudo que você muda no editor fica
  salvo, sem botão de salvar. Em **Opções** há duplicar, renomear, excluir,
  exportar (este preset ou todos) e importar, que também abre os layouts
  salvos pelas versões antigas.
- **Sincroniza com o OBS.** As overlays aparecem na cena, acima da captura do
  jogo, e acompanham o que você muda no programa em cerca de 100 ms.

![Janela principal do Tibia Scarab Eye](docs/images/interface-preview.png)

## Como funciona

1. O programa mostra cada recorte com miniaturas do DWM (o compositor do
   Windows) sobre a janela do Tibia. Não há leitura de memória, injeção nem
   alteração do cliente.
2. Para o OBS, o programa publica **somente a geometria** (posição, tamanho,
   recorte e opacidade) em `%LOCALAPPDATA%\TibiaScarabEye\obs-layout.json`.
3. O script `obs/TibiaScarabEye.lua` lê esse arquivo e a transformação real da
   sua **Captura de jogo**, e desenha cada overlay como um grupo na cena,
   reaproveitando a imagem que o OBS já captura. Nada é recapturado da tela.

Por isso, se a Captura de jogo estiver preta ou sem sinal, as overlays também
ficam sem imagem: o Tibia Scarab Eye não contorna bloqueios de captura.

## Requisitos

- Windows 10 ou 11, x64
- Com o instalador, nada além disso: ele já traz o .NET. Para rodar a partir
  da compilação, o [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
  (x64); para compilar, o .NET 8 SDK
- OBS Studio (testado na versão 32.2.2) com uma fonte **Captura de jogo**
  mostrando o Tibia, direto na cena (fora de grupos)

## Começando

### Instalar

Baixe `TibiaScarabEye-Setup-<versão>.exe` na página de
[Releases](https://github.com/augustcaio/tibia-scarab-eye/releases) e execute.
Por padrão instala só para o seu usuário, sem pedir administrador, e cria o
atalho no menu Iniciar.

Se o OBS já foi aberto alguma vez neste computador, o instalador oferece
**adicionar o script à lista de scripts do OBS**: ele registra
`TibiaScarabEye.lua` em Ferramentas > Scripts, na coleção de cenas ativa, e o
OBS passa a carregá-lo sozinho. O OBS precisa estar fechado nesse momento (o
instalador avisa); a coleção ganha um backup `.scarab-eye.bak`. Ao desinstalar,
o script sai da lista. Sem essa opção, o script fica na pasta `obs` da
instalação (há um atalho no menu Iniciar) e se adiciona à mão, como abaixo.

### Compilar

```powershell
dotnet build TibiaScarabEye.sln -c Release
```

O executável sai em `artifacts\bin\TibiaScarabEye\release\TibiaScarabEye.exe`.
Para gerar o instalador (publica o programa autocontido e compila o instalador
com o Inno Setup, baixado como pacote NuGet):

```powershell
powershell -File installer\build.ps1 -Version 0.2.0
```

O instalador sai em `installer\Output\TibiaScarabEye-Setup-<versão>.exe`.

### Testes

```powershell
dotnet test TibiaScarabEye.sln
```

Os testes da categoria `Desktop` abrem janelas reais fora da área visível e
usam o DWM do Windows, então precisam de uma sessão de desktop. Para rodar só
os testes que não dependem dela: `dotnet test --filter "Category!=Desktop"`.

### Usar no OBS

1. Abra o Tibia e depois o `TibiaScarabEye.exe`
   e abra o **Editor de áreas** para criar e posicionar suas áreas. O Tibia é
   detectado sozinho; a lâmpada no topo da janela mostra se está sendo lido.
2. O script já está no OBS se você aceitou a opção do instalador. Se não, vá em
   **Ferramentas > Scripts**, clique em `+` e escolha `obs/TibiaScarabEye.lua`.
3. No programa, clique em **Sincronizar com OBS**. Os grupos
   `Tibia Scarab Eye xxxxxx` aparecem no topo da cena.
4. Clique em **Travar para jogar** e jogue.

No **modo manual** (padrão do script), cada overlay aparece na primeira vez
onde o programa a colocou. Depois você pode arrastá-la e redimensioná-la no
preview do OBS, e a posição fica salva. Desmarque esse modo para que a posição
venha sempre do programa. O guia completo, com diagnóstico e ajuste fino, está
em [`docs/LEIA-ME.txt`](docs/LEIA-ME.txt).

## Estrutura do projeto

| Caminho | Conteúdo |
|---|---|
| `TibiaScarabEye.sln` | Solução com o aplicativo e os testes |
| `src/TibiaScarabEye/` | Aplicativo (WinForms, .NET 8, x64) |
| `src/TibiaScarabEye/Interop/` | P/Invoke e DWM. `Native.ThumbnailBounds` é a origem correta das coordenadas da miniatura; `TibiaLocator` acha o cliente do Tibia |
| `src/TibiaScarabEye/Layouts/` | `RegionSpec`, `Preset` e `PresetStore` (presets por personagem, exportar e importar); `Layout` é o formato antigo, que ainda pode ser importado |
| `src/TibiaScarabEye/Obs/` | `ObsBridge` publica o layout para o OBS a cada 100 ms |
| `src/TibiaScarabEye/UI/` | Janela principal, editor de áreas, overlays e tema |
| `tests/TibiaScarabEye.Tests/` | Testes xUnit (unitários e de desktop) |
| `obs/TibiaScarabEye.lua` | Script do OBS: cria as overlays como grupos na cena |
| `installer/` | Script do Inno Setup, `build.ps1` (gera o instalador) e `obs-script.ps1` (registra o script no OBS) |
| `docs/LEIA-ME.txt` | Instruções de uso e instalação no OBS |
| `Directory.Build.props`, `Directory.Packages.props`, `global.json` | Propriedades comuns, versões de pacotes e versão do SDK |

## Notas técnicas

- O programa declara DPI do sistema (`ApplicationHighDpiMode=SystemAware` no
  `.csproj`).
- As coordenadas da miniatura DWM começam em `GetWindowRect` (que inclui a
  borda invisível de 7 a 8 px), e o tamanho é o do quadro estendido. O código
  usa `Native.ThumbnailBounds` para isso. Usar `DwmGetWindowAttribute` como
  origem desloca as overlays no OBS.
- O título da janela mostra a data e a hora da compilação (`build dd/MM HH:mm`).
  Antes de diagnosticar um deslocamento, confira se ela é a do executável que
  você acabou de compilar.

## Limitações

- A Captura de jogo precisa estar diretamente na cena. Se estiver dentro de um
  grupo, nenhuma overlay é criada naquela cena, e o script registra um aviso no
  log do OBS (Ajuda > Arquivos de log, linha `Tibia Scarab Eye: AVISO`).
- Jogo em tela cheia exclusiva não foi testado. O programa depende das
  miniaturas do DWM, então o recomendado é o Tibia em janela ou maximizado.
- Com monitores de escalas diferentes, o alinhamento não foi verificado.
- O script do OBS foi validado no OBS 32.2.2 com fonte de imagem. Com o
  Tibia real ainda não foi exercitado nesta base.

## Licença

Todos os direitos do programa pertencem a [augustcaio](https://github.com/augustcaio). O repositório é público apenas para consulta. Veja [LICENSE](LICENSE).

## Avisos

Tibia é marca registrada da CipSoft GmbH. Este programa é um projeto de fã, sem
vínculo com a CipSoft nem aprovação dela. Ele não usa imagens do jogo: as
molduras e cores da interface são desenhadas pelo próprio programa.
