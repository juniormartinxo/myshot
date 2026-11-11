# MyShot design system

## Context and theme

Registro product. Cena adotada para esta revisão: alguém em uma mesa iluminada alterna entre documentos e janelas Windows para explicar um detalhe rapidamente. Uma interface clara permite essa alternância sem uma mudança brusca de luminosidade. Esta é uma decisão de design, não uma preferência pessoal atribuída ao usuário.

## Color strategy

Restrained: superfícies neutras com leve matiz verde, um acento verde profundo para ação principal e seleção. Avisos têm uma superfície âmbar discreta. A imagem capturada mantém suas cores próprias.

Os tokens são definidos em OKLCH e convertidos para sRGB por Theme.Color no código WinForms, que não oferece cores OKLCH nativas.

| Token | OKLCH | Uso |
|---|---|---|
| Background | 0.975 0.005 155 | janela |
| Surface | 0.994 0.003 155 | controles e painéis |
| Canvas | 0.925 0.008 155 | espaço em torno da imagem |
| Ink | 0.27 0.018 155 | texto principal |
| Muted | 0.48 0.018 155 | texto auxiliar |
| Border | 0.86 0.008 155 | separadores |
| Accent | 0.44 0.085 160 | ação principal |
| AccentHover | 0.38 0.08 160 | hover primário |
| Selected | 0.935 0.025 160 | ferramenta selecionada |
| Warning | 0.95 0.025 85 | aviso de atalhos |
| WarningInk | 0.40 0.065 70 | texto de aviso |

## Typography

Segoe UI, fonte de sistema. Corpo 10 pt, apoio 9 pt, títulos de seção 12.5 pt semibold/bold, título da janela inicial 24 pt bold. Sem slogans de marketing, títulos em caixa alta ou fontes decorativas.

## Layout

Janela inicial: 672 × 528 unidades lógicas, margens 28, marca compacta, uma ação principal ampla, duas alternativas em linhas, status de atalhos e rodapé discreto. Editor: canvas central, ferramentas à esquerda, propriedades à direita, ações no topo e resultado no rodapé. Propriedades têm scroll vertical em janelas baixas. O editor suporta janela mínima de 940 × 560; nessa altura, o painel de propriedades pode rolar.

Ambos os painéis do editor começam recolhidos, com 60 unidades lógicas de largura, sem persistir a expansão entre capturas. Setas no topo controlam cada lado independentemente. Ferramentas expandidas usam 168; propriedades expandidas, 224. O título Anotar foi removido. Recolhidos, os botões mostram só ícones centralizados; expandidos, o ícone tem recuo lateral de 14 e o texto começa em 44, com espaço dentro do contorno de seleção. Dimensões acompanham o DPI do formulário.

Espaçamento: 4 / 8 / 12 / 16 / 24 / 28. Separadores finos entre grupos; sem cartões aninhados. Barras e painéis usam superfícies claras. O canvas recebe borda de imagem e uma sombra mínima, sem efeitos na exportação.

## Components and states

Botões com raio 7 e rótulo claro. Primário preenchido; secundário com borda sutil; utilitário discreto. Estados hover, pressionado, foco pontilhado e desabilitado distintos. Manter semântica e teclado de Button/ToolStrip nativos.

Ícones próprios de traço, 20 px, para comandos e oito ferramentas. Ferramentas têm rótulo quando expandidas, tooltip, nome acessível e seleção persistente visível. O painel mostra apenas propriedades pertinentes à ferramenta ativa; cor atual visível na amostra. Recolhido, oferece ícones para cor, espessura, opacidade, preenchimento, tamanho/fonte e ações do objeto selecionado. Campos numéricos expandem e recebem foco; cor/fonte abrem seus seletores; preenchimento alterna diretamente; editar/excluir atuam na seleção. Atalhos indisponíveis geram aviso legível e acesso a Preferências, sem códigos Win32 no fluxo normal. Foco de teclado é indicado por contorno pontilhado nos botões.

Mover (V) precede as ferramentas de desenho. Nesse modo, o objeto selecionado recebe contorno tracejado sem alças de redimensionamento; o ponteiro indica movimento quando está sobre um objeto. O painel lateral explica arraste e ajustes com setas, sem apresentar controles de estilo que não editam o objeto existente. A seleção nunca altera os pixels exportados.

Ao selecionar texto, o painel oferece Editar texto e explica o duplo clique. O diálogo reaproveita o campo multilinha com o conteúdo atual, Salvar texto e Cancelar. Salvar fica desabilitado para conteúdo vazio. Excluir (Del) aparece para qualquer objeto selecionado; a exclusão é imediata e reversível com Desfazer. Ambas as ações devolvem o foco ao canvas.

## Motion and implementation

Sem animação decorativa ou transição de entrada. Feedback imediato em interação local. Tudo em C#/WinForms e GDI+, sem assets remotos, pacotes ou recursos da referência. A composição da imagem exportada permanece em Core.cs, separada dos elementos visuais da UI.
