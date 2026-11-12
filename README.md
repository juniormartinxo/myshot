# MyShot

Aplicativo de captura de tela para Windows (x64), feito em C# com WinForms. Captura uma região ou o desktop inteiro, permite anotar e salva ou copia a imagem. Roda na bandeja do sistema.

## Requisitos

- Windows 10 ou 11 (x64).
- .NET Framework 4.8, que já vem instalado no Windows.
- Não precisa de SDK .NET, NuGet, WSL ou permissão de administrador.
- Para gerar o instalador (não é necessário para usar o app): [Inno Setup 6](https://jrsoftware.org/isinfo.php), por exemplo com `winget install JRSoftware.InnoSetup`.

## Instalar

Baixe `MyShot-Setup.exe` na página de [releases](https://github.com/juniormartinxo/myshot/releases) e execute. O instalador copia o app para `%LOCALAPPDATA%\Programs\MyShot`, cria o atalho no menu Iniciar e aparece em Aplicativos instalados do Windows, onde pode ser desinstalado. Desinstalar remove também a inicialização com o Windows; as preferências em `%LOCALAPPDATA%\MyShot` são mantidas.

Alternativa sem instalador: baixe `MyShot.exe` e `MyShot.exe.config` e mantenha os dois na mesma pasta.

## Usar

Atalhos padrão (configuráveis em Preferências):

- **Ctrl+Alt+S**: capturar uma região. Arraste para selecionar; **Esc** ou clique direito cancela.
- **Ctrl+Alt+F**: salvar o desktop inteiro direto na pasta escolhida.

A barra de captura aparece no topo da tela ao abrir o app. Ela pode ser arrastada pela alça de pontos embaixo, fica sempre na frente das outras janelas e pode ser fechada com **Esc** ou com o X. Fechada, o app continua na bandeja do sistema. Use **Sair** no menu da bandeja para encerrar.

### Editor

Depois de capturar uma região, o editor abre com a imagem. A barra de ferramentas fica no topo e, logo abaixo, uma linha mostra as opções da ferramenta ativa (cor, espessura, opacidade, preenchimento, fonte).

Ferramentas: linha, retângulo, elipse, seta, desenho livre, texto, desfoque, etapa numerada, polígono e face.

- **Polígono:** cada clique coloca um vértice. Feche no primeiro ponto, com duplo clique ou com Enter; Esc cancela.
- **Face:** igual ao polígono, mas a face fica aberta até ser confirmada, e os vértices se encaixam nos vértices das faces já criadas.
- **Texto:** clique na imagem e digite. A barra de texto permite mudar cor, fonte, tamanho e negrito, e arrastar o texto pela alça do fim da barra. Enter confirma o texto, Esc cancela.
- **Mover (V):** selecione um objeto e arraste. As setas do teclado movem 1 px (Shift + setas, 10 px). Del exclui.

Atalhos do editor: **Ctrl+S** salva, **Ctrl+C** copia, **Ctrl+Z** desfaz e **Ctrl+Y** (ou Ctrl+Shift+Z) refaz. A exportação usa a resolução original da captura.

### Preferências

Em Preferências você define a pasta de saída, o formato (PNG, JPG ou BMP), os atalhos globais (qualquer combinação de Ctrl, Alt, Shift e Win), a opacidade da barra, o som ao capturar e a inicialização com o Windows. As preferências ficam em `%LOCALAPPDATA%\MyShot\preferences.json`.

Se um atalho já estiver em uso por outro programa, o app avisa. Escolha outra combinação em Preferências.

## Atualização

O botão **Atualizar** (menu **Mais opções** da barra) consulta a última release pública do GitHub, baixa `MyShot.exe` e confere o SHA-256 com `MyShot.exe.sha256` antes de instalar. A consulta só acontece quando você clica no botão.

## Desenvolvimento

Compilar, testar e gerar os arquivos de release (executável, configuração, checksum e instalador) com o compilador do .NET Framework e o Inno Setup 6:

```powershell
powershell -NoProfile -File .\scripts\build.ps1           # gera artifacts\
powershell -NoProfile -File .\scripts\build.ps1 -Test     # também compila e roda os testes
```

Os artefatos ficam em `artifacts\` (ignorada pelo Git). O script gera:

- `MyShot.exe`, `MyShot.exe.config`
- `MyShot.exe.sha256`, usado pelo atualizador
- `MyShot-Setup.exe`, o instalador (Inno Setup, script em `installer\MyShot.iss`), com o app embutido

### Publicar uma versão

1. Altere `AppInfo.Version` em `src\Updater.cs` (ex.: `0.0.5`).
2. Rode o build com `-Test` e confira que os testes passam.
3. Assine o manifesto da versão com `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\sign-release.ps1`. Ele gera `artifacts\MyShot.manifest` (versão, SHA-256 e tamanho do executável) e `MyShot.manifest.sig`. A chave privada fica em `%USERPROFILE%\.myshot-keys` e nunca entra no repositório.
4. Faça commit, crie a tag `v0.0.5` e publique a release no GitHub com estes arquivos: `MyShot.exe`, `MyShot.exe.config`, `MyShot.exe.sha256`, `MyShot.manifest`, `MyShot.manifest.sig` e `MyShot-Setup.exe`. Os nomes precisam ser exatamente esses.

O atualizador só oferece uma versão cuja assinatura do manifesto confere com a chave pública embutida no app (`assets\update-public-key.xml`). Sem assinatura válida, a atualização é recusada.

Sem release mais nova que a versão instalada, o botão Atualizar informa que o app está na versão mais recente.

## Documentação

- [PRODUCT.md](PRODUCT.md): contexto do produto e princípios.
- [DESIGN.md](DESIGN.md): sistema visual (cores, tipografia, componentes).
- [docs/product-scope.md](docs/product-scope.md): escopo da versão e decisões técnicas.
- [docs/validation.md](docs/validation.md): roteiro de testes manuais.
