# MyShot: escopo e decisões técnicas

## Objetivo

Aplicativo de captura de tela para Windows x64: capturar uma região ou o desktop inteiro, anotar e salvar ou copiar a imagem, com atalhos globais e uma barra de captura sempre disponível.

## Decisões técnicas

- **C# com WinForms e .NET Framework 4.8.** O Framework já vem com o Windows e seu compilador (`csc.exe`) permite compilar sem SDK, NuGet ou internet. WinForms fornece janelas, diálogos, área de transferência e bandeja; `RegisterHotKey` fornece os atalhos globais.
- **Renderização com System.Drawing (GDI+).** A imagem exportada e a prévia usam o mesmo renderizador, então o que aparece no editor é o que é salvo.
- **Ícones SVG (Lucide, licença ISC) embutidos no executável.** São lidos por um leitor de SVG próprio e desenhados como vetor, então ficam nítidos em qualquer escala de DPI.
- **Atualização pelo GitHub.** O atualizador consulta a última release pública, baixa o executável e confere o SHA-256 antes de substituir o app. Só age quando o usuário clica no botão.
- **Instalador com Inno Setup 6.** Um único `.exe` com o app embutido, instalado em `%LOCALAPPDATA%` (sem administrador), com atalho no menu Iniciar e desinstalação em Aplicativos instalados. A geração do instalador exige o Inno Setup na máquina de build; o app em si continua sem dependências além do .NET Framework 4.8.

## Funcionalidades da versão atual

- Captura de região (arrastar para selecionar) e de desktop inteiro, com ou sem editor.
- Salvar direto na pasta de saída, em PNG, JPG ou BMP.
- Editor com linha, retângulo, elipse, seta, desenho livre, texto, desfoque, etapa numerada, polígono e face.
- Mover, editar e excluir objetos; desfazer e refazer.
- Cor, espessura, opacidade, preenchimento e fonte por ferramenta; texto com cor, fonte, tamanho e negrito.
- Copiar a imagem para a área de transferência.
- Atalhos globais configuráveis (qualquer combinação de Ctrl, Alt, Shift e Win), com aviso quando um atalho está em uso.
- Barra de captura flutuante, arrastável, com opacidade configurável; app na bandeja do sistema.
- Temas escuro e claro.
- Inicialização opcional com o Windows.
- Atualização pelo menu **Mais opções**.

## Fora desta versão

- OCR, fixar captura na tela (pin), captura com rolagem, temporizador e seleção automática de janela.
- Assinatura digital do executável.
- Desfazer entre sessões (o histórico é perdido ao fechar o editor).
- Ferramentas 3D: o polígono e a face são desenhados sobre a imagem, sem projeção automática.

## Testes

O projeto tem um executável de testes próprio (`MyShot.Tests.exe`), compilado e executado pelo `scripts\build.ps1 -Test`. Ele cobre o modelo de anotações, a renderização, o desfazer, o editor, as preferências, o atualizador e os atalhos. Os testes não usam internet.

Os testes automáticos não substituem a validação manual com o mouse e o teclado em uma sessão real. O roteiro está em [validation.md](validation.md).
