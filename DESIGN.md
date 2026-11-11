# MyShot design system

## Context and theme

Registro product. Cena desta revisão: alguém captura uma região, anota um detalhe e compartilha em seguida, sempre com a imagem como foco. A interface tem dois temas, escolhidos em Preferências: **Escuro** (padrão), em tons carbono, e **Claro**. Os dois usam a mesma estrutura e o mesmo acento. Em ambos, a imagem capturada é o elemento de maior destaque da tela. Esta é uma decisão de design, não uma preferência pessoal atribuída ao usuário. O tema escolhido vale na próxima abertura do app.

## Color strategy

Restrained: superfícies neutras com pouca saturação, um azul para ação principal e seleção. Avisos têm uma superfície âmbar discreta. A imagem capturada mantém suas cores próprias e nunca recebe tintas da interface.

Os tokens são valores sRGB definidos em `Theme.Apply(dark)` (Theme.cs). Contraste mínimo: texto principal acima de 7:1 e texto auxiliar acima de 4,5:1 nos dois temas; texto branco sobre Accent acima de 4,5:1.

| Token | Escuro (sRGB) | Claro (sRGB) | Uso |
|---|---|---|---|
| Background | 36 39 44 | 243 244 246 | janela e área de formulários |
| Surface | 46 50 56 | 255 255 255 | barras, painéis, campos |
| Raised | 58 63 70 | 233 235 239 | hover de controles, teclas de atalho |
| Canvas | 30 33 37 | 221 224 229 | moldura em torno da imagem no editor |
| Ink | 232 235 240 | 28 31 36 | texto principal |
| Muted | 160 166 176 | 92 98 110 | texto auxiliar |
| Border | 72 78 88 | 204 208 215 | separadores e contornos |
| Accent | 43 111 214 | 43 111 214 | ação principal, ferramenta ativa, Salvar |
| AccentHover | 58 127 227 | 33 92 184 | hover e pressionado do primário |
| OnAccent | 255 255 255 | 255 255 255 | texto e ícones sobre Accent |
| Selected | 38 64 100 | 219 234 254 | fundo de ferramenta selecionada |
| Warning | 70 56 26 | 254 243 199 | aviso de atalhos |
| WarningInk | 255 213 138 | 120 53 15 | texto de aviso |

Preferência salva em `preferences.json` como `Appearance` (`"dark"` ou `"light"`).

## Typography

Segoe UI, fonte de sistema. Corpo 10 pt, apoio 9 pt, títulos de seção 12.5 pt semibold/bold, título da janela inicial 24 pt bold. Atalhos aparecem como teclas (Ctrl, Alt, S) em 9 pt bold. Sem slogans de marketing, títulos em caixa alta ou fontes decorativas.

## Layout

Não há janela inicial. A interface de entrada é uma barra pequena no topo da tela, centralizada, sem título nem rodapé, como a barra de captura do Windows: Capturar região (destacada), Desktop inteiro, Salvar desktop direto, Preferências, Mais opções e Fechar. Cada ícone traz o atalho na dica. Esc ou Fechar oculta a barra e o app continua na bandeja. A barra só some durante a captura. Janelas se abrem apenas para Preferências e para o editor. Editor: barra flutuante centralizada no topo, sobre a imagem, com Nova captura, Desfazer, Refazer, as oito ferramentas (só ícones), Estilo, Copiar, Salvar e Fechar. A imagem começa abaixo da barra, no fundo Canvas. Cor, espessura, opacidade e fonte ficam num popover aberto por Estilo, logo abaixo da barra. Rodapé com dimensões e dicas. Propriedades têm scroll vertical em janelas baixas. O editor suporta janela mínima de 940 × 560; nessa altura, o painel de propriedades pode rolar.

Não há faixas laterais nem botões de expandir/recolher. Os ícones da barra têm dica (tooltip) e nome acessível; a ferramenta ativa fica destacada em Selected com contorno Accent. O popover de Estilo mostra só os campos da ferramenta ativa e, com um objeto selecionado, Editar texto e Excluir. Esc fecha o popover antes de limpar a seleção. Dimensões acompanham o DPI do formulário.

Espaçamento: 4 / 8 / 12 / 16 / 24 / 28. Separadores finos entre grupos; sem cartões aninhados. A barra de título da janela usa o modo escuro do Windows (DWM), quando disponível.

## Components and states

Botões com raio 5 (ferramentas da barra, 5) e rótulo de contraste com o fundo. Primário preenchido em Accent; secundário em Surface com borda sutil; utilitário sem borda. Estados hover (Raised), pressionado, foco pontilhado e desabilitado distintos. Manter semântica e teclado de Button/ToolStrip nativos.

Ícones próprios de traço, 20 px, para comandos e oito ferramentas. Ferramentas têm rótulo quando expandidas, tooltip, nome acessível e seleção persistente visível (fundo Selected com contorno Accent). O painel mostra apenas propriedades pertinentes à ferramenta ativa; cor atual visível na amostra. Recolhido, oferece ícones para cor, espessura, opacidade, preenchimento, tamanho/fonte e ações do objeto selecionado. Campos numéricos expandem e recebem foco; cor/fonte abrem seus seletores; preenchimento alterna diretamente; editar/excluir atuam na seleção. Atalhos indisponíveis geram aviso legível e acesso a Preferências, sem códigos Win32 no fluxo normal. Foco de teclado é indicado por contorno pontilhado nos botões.

Mover (V) precede as ferramentas de desenho. Nesse modo, o objeto selecionado recebe contorno tracejado sem alças de redimensionamento; o ponteiro indica movimento quando está sobre um objeto. O painel lateral explica arraste e ajustes com setas, sem apresentar controles de estilo que não editam o objeto existente. A seleção nunca altera os pixels exportados.

Ao selecionar texto, o painel oferece Editar texto e explica o duplo clique. O diálogo reaproveita o campo multilinha com o conteúdo atual, Salvar texto e Cancelar. Salvar fica desabilitado para conteúdo vazio. Excluir (Del) aparece para qualquer objeto selecionado; a exclusão é imediata e reversível com Desfazer. Ambas as ações devolvem o foco ao canvas.

Overlay de captura: a tela recebe uma camada preta semitransparente; guias de cursor horizontal e vertical atravessam a tela até o início do arraste; a região selecionada fica sem camada, com contorno branco fino; um rótulo escuro acompanha o cursor com dimensões em pixels e a dica de Esc.

## Motion and implementation

Sem animação decorativa ou transição de entrada. Feedback imediato em interação local. Tudo em C#/WinForms e GDI+, sem assets remotos, pacotes ou recursos da referência. A composição da imagem exportada permanece em Core.cs, separada dos elementos visuais da UI.
