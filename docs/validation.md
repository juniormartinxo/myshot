# Roteiro de validação manual

Os testes automáticos (`scripts\build.ps1 -Test`) cobrem a lógica e a renderização das janelas. Eles não substituem o uso real com mouse e teclado. Use este roteiro numa sessão real do Windows, com a área de trabalho desbloqueada.

## Captura

1. Abra o app. A barra de captura deve aparecer no topo da tela, centralizada.
2. Com outro aplicativo em foco, aperte o atalho de captura de região (padrão Ctrl+Alt+S). A seleção deve abrir sobre a tela inteira, com as guias de cursor.
3. Arraste uma região, solte e confira se o editor abre com a imagem certa, em resolução original.
4. Repita com **Esc** e com clique direito: nenhuma imagem deve ser criada.
5. Teste o atalho de salvar desktop direto (padrão Ctrl+Alt+F). O arquivo deve aparecer na pasta de saída.
6. Com a barra fechada, confira se o app continua na bandeja e se a barra volta pelo ícone da bandeja.

## Barra de captura

- Arraste pelo fundo e pela alça de pontos embaixo. A barra deve reabrir na última posição.
- A barra deve ficar na frente das outras janelas.
- Passe o mouse sobre cada botão e confira a dica (com o atalho).
- Esc fecha a barra.

## Editor

- Cada ferramenta: desenhe uma forma e confira a prévia durante o arraste e o resultado após soltar.
- Polígono: clique três vértices, solte o botão entre os cliques, feche no primeiro ponto. A linha de prévia deve continuar até o fechamento.
- Face: três vértices não fecham a face. Enter fecha. A face seguinte deve encaixar num vértice já criado.
- Texto: crie um texto, altere cor, fonte, tamanho e negrito pela barra. A alça de pontos deve mover o texto. Enter confirma; Esc cancela e restaura.
- Etapa: confira a cor de fundo, a cor da fonte e o número centralizado, também com tamanho de fonte grande.
- Mover (V): selecione, arraste, use as setas, exclua com Del.
- Desfazer e refazer (Ctrl+Z, Ctrl+Y) devem reverter cada ação, inclusive edições de texto.
- Salvar (Ctrl+S) e copiar (Ctrl+C): confira o arquivo gerado e a imagem colada em outro aplicativo.

## Preferências

- Altere a pasta, o formato, a aparência e os atalhos. Escolha uma combinação que já esteja em uso por outro programa e confira o aviso.
- Altere a opacidade da barra e confira o efeito ao salvar.
- Desmarque e marque a inicialização com o Windows. O registro do Windows deve refletir a escolha.

## Atualização

- Com a versão atual instalada e nenhuma release mais nova, o botão **Atualizar** deve dizer que o app está na versão mais recente.
- Com uma release mais nova publicada, o botão deve oferecer a atualização, baixar, conferir o SHA-256 e reiniciar. Teste também com um arquivo baixado alterado (o SHA-256 não confere): a instalação deve ser recusada.

## Instalador

- Execute `MyShot-Setup.exe` sem permissão de administrador. O app deve ser copiado para `%LOCALAPPDATA%\Programs\MyShot`, aparecer no menu Iniciar e abrir.
- Com o app instalado e rodando, desinstale por Aplicativos instalados do Windows. O processo deve ser encerrado, a pasta e o atalho do menu Iniciar removidos, e a entrada `MyShot` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` apagada.

## Registro de execução

Anote a versão testada, a escala de DPI do monitor, o número de monitores e o resultado de cada item acima.
