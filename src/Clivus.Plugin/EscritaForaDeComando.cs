using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

#if DEBUG
[assembly: CommandClass(typeof(Clivus.Plugin.GanchosDeTeste))]
#endif

namespace Clivus.Plugin;

/// <summary>
/// Toda escrita do plugin feita fora de um comando (botão de janela solta,
/// abertura do desenho) passa por aqui: trava o documento e cala o vigia,
/// para o que o plugin pinta não virar "mesa pendente" vermelha na folga
/// seguinte (04/10/2026, sombras e análises pela janela).
/// </summary>
internal static class EscritaForaDeComando
{
    internal static T Fazer<T>(Document documento, Func<T> operacao)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(operacao);

        using (documento.LockDocument())
        using (LayoutWatcher.Calar(documento))
            return operacao();
    }

    internal static void Fazer(Document documento, Action operacao) =>
        Fazer(documento, () =>
        {
            operacao();
            return true;
        });
}

#if DEBUG
/// <summary>
/// Só no build de teste: o nível 2 chama, por LISP, o mesmo caminho do botão
/// da janela de sombras, fora de um comando. (clivus-sombras-pela-janela
/// "dd/mm/aaaa" "hh:mm" "fuso" ["escopo" "áreas" "mesas"]) escreve a frase e
/// devolve T; sem o escopo, o desenho inteiro.
/// </summary>
public static class GanchosDeTeste
{
    /// <summary>
    /// O carregamento do terreno ao abrir (31/10/2026), que com interface roda
    /// na primeira folga: no Core Console não há folga, então o nível 2 chama
    /// o mesmo por aqui. Devolve T se carregou.
    /// </summary>
    [LispFunction("CLIVUS-TERRENO-AO-ABRIR")]
    public static object? TerrenoAoAbrir(ResultBuffer argumentos)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return null;

        return TerrainCommands.CarregarDoCarimbo(documento.Editor, documento) ? true : null;
    }

    [LispFunction("CLIVUS-SOMBRAS-PELA-JANELA")]
    public static object? SombrasPelaJanela(ResultBuffer argumentos)
    {
        var valores = Valores(argumentos);
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 3 || documento is null) return null;

        var periodo = SombrasCommands.Ler(valores[0], valores[0], valores[1], valores[1], "30", valores[2], out var porque);
        if (periodo is null)
        {
            documento.Editor.WriteMessage($"\nSOMBRAS {porque}\n");
            return null;
        }

        var escopo = valores.Length >= 6 ? SombrasCommands.EscopoPelosNomes(documento.Database, valores[3], valores[4], valores[5], out porque) : ShadowScope.WholeDrawing;
        if (escopo is null)
        {
            documento.Editor.WriteMessage($"\nSOMBRAS {porque}\n");
            return null;
        }

        // O sétimo, as caixas de "Fazem sombra" (42.3): "mesas,arvores,relevo", ou parte delas.
        var queFaz = valores.Length >= 7
            ? new ShadowCasters(valores[6].Contains("mesas", StringComparison.OrdinalIgnoreCase), valores[6].Contains("arvores", StringComparison.OrdinalIgnoreCase), valores[6].Contains("relevo", StringComparison.OrdinalIgnoreCase))
            : ShadowCasters.All;

        documento.Editor.WriteMessage($"\n{SombrasCommands.GerarPelaJanela(documento, periodo, escopo, queFaz)}\n");
        return true;
    }

    /// <summary>
    /// Os outros botões da janela de sombras, fora de comando:
    /// (clivus-sombras-acao "apagar" "escopo" "áreas" "mesas"),
    /// (clivus-sombras-acao "ocultar") e (clivus-sombras-acao "mostrar").
    /// Escreve a frase e devolve T. E o que a janela faz ao abrir (42.3):
    /// (clivus-sombras-acao "tirar-pintura") devolve quantos módulos pintados
    /// pela versão antiga voltaram à cor.
    /// </summary>
    [LispFunction("CLIVUS-SOMBRAS-ACAO")]
    public static object? SombrasAcao(ResultBuffer argumentos)
    {
        var valores = Valores(argumentos);
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 1 || documento is null) return null;

        RelatoDasSombras relato;
        switch (valores[0])
        {
            case "tirar-pintura":
                return EscritaForaDeComando.Fazer(documento, () => SombrasCommands.TirarPinturaAntiga(documento.Database));

            case "ocultar":
            case "mostrar":
                relato = EscritaForaDeComando.Fazer(documento, () => SombrasCommands.Ligar(documento, valores[0] == "mostrar"));
                break;

            case "apagar" when valores.Length >= 4:
                var escopo = SombrasCommands.EscopoPelosNomes(documento.Database, valores[1], valores[2], valores[3], out var porque);
                if (escopo is null)
                {
                    documento.Editor.WriteMessage($"\nSOMBRAS {porque}\n");
                    return null;
                }

                relato = EscritaForaDeComando.Fazer(documento, () => SombrasCommands.Apagar(documento, escopo));
                break;

            default:
                documento.Editor.WriteMessage($"\nSOMBRAS ação recusada: {string.Join(" ", valores)}\n");
                return null;
        }

        documento.Editor.WriteMessage($"\n{relato}\n");
        return true;
    }

    /// <summary>
    /// A janela das sombras ou a do PVsyst desenhada num PNG, sem abrir
    /// (memória "conferir a tela renderizada"): (clivus-renderizar-janela
    /// "sombras"|"pvsyst" "caminho.png" largura). A largura é a do conteúdo,
    /// para ver se a linha quebra em vez de cortar. Devolve T, ou nil se a
    /// linha do Core Console não é STA (o WPF exige).
    /// </summary>
    [LispFunction("CLIVUS-RENDERIZAR-JANELA")]
    public static object? RenderizarJanela(ResultBuffer argumentos)
    {
        var valores = Valores(argumentos);
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 3 || documento is null || !double.TryParse(valores[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var largura)) return null;

        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            documento.Editor.WriteMessage("\nRENDER a linha não é STA; o WPF não desenha aqui.\n");
            return null;
        }

        System.Windows.Window janela = valores[0] switch
        {
            "pvsyst" => new JanelaDoPvsyst(documento, []),
            "areas" => new JanelaDeAreas(documento),
            "analises" => new JanelaDeAnalises(documento, () => { }),
            _ => new JanelaDeSombras(documento),
        };

        // A aba das análises a mostrar: (clivus-renderizar-janela "analises" "caminho.png" largura "PillarAbove").
        if (janela is JanelaDeAnalises && valores.Length >= 4 && Enum.TryParse<IndependentKind>(valores[3], out var aba)
            && ((System.Windows.Controls.DockPanel)janela.Content).Children.OfType<System.Windows.Controls.TabControl>().FirstOrDefault() is { } abas)
        {
            abas.SelectedIndex = Array.IndexOf(JanelaDeAnalises.Analises, aba);
        }
        var conteudo = (System.Windows.FrameworkElement)janela.Content;
        conteudo.Measure(new System.Windows.Size(largura, double.PositiveInfinity));
        conteudo.Arrange(new System.Windows.Rect(0, 0, largura, conteudo.DesiredSize.Height));
        conteudo.UpdateLayout();

        var (w, h) = ((int)Math.Ceiling(largura), (int)Math.Ceiling(conteudo.DesiredSize.Height));
        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new System.Windows.Rect(0, 0, w, h));
            dc.DrawRectangle(new System.Windows.Media.VisualBrush(conteudo) { Stretch = System.Windows.Media.Stretch.None, AlignmentX = System.Windows.Media.AlignmentX.Left, AlignmentY = System.Windows.Media.AlignmentY.Top }, null, new System.Windows.Rect(0, 0, w, h));
        }

        var imagem = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        imagem.Render(visual);
        var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
        png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(imagem));
        using (var arquivo = System.IO.File.Create(valores[1])) png.Save(arquivo);

        janela.Close();
        documento.Editor.WriteMessage($"\nRENDER {valores[0]} {w}x{h} em {valores[1]}\n");
        return true;
    }

    /// <summary>
    /// Os botões da janela de análises, fora de comando, pelo mesmo caminho
    /// (<see cref="OperacoesDeAnalise"/>): (clivus-analises-pela-janela "ação"
    /// "análise" "escopo" "áreas" "mesas" ["regra"|"fundo,borda"]). A ação é
    /// inserir, apagar, analisar, tirar-cores, quantificar ou aparencia; a
    /// análise, o nome do IndependentKind (PillarAbove...); o escopo, como nas
    /// sombras (TUDO, AREA...). A regra: "baixo;cima", e "F=baixo;cima",
    /// "T=baixo;cima" (as linhas do biposte) e "P" (pintar as peças),
    /// separados por espaço; vazio é sem limite. A ação analisar-como-janela
    /// monta a regra pelas caixas, como a janela (<see cref="ThresholdForm"/>,
    /// com as colunas do desenho e a regra gravada): "abaixo" e "acima" (as
    /// caixas marcadas), "P" e "Single=", "Front=", "Rear=" com o texto de cada
    /// coluna. (clivus-analises-pela-janela "linhas") diz as linhas de pilar do
    /// desenho. Escreve a frase e devolve T.
    /// </summary>
    [LispFunction("CLIVUS-ANALISES-PELA-JANELA")]
    public static object? AnalisesPelaJanela(ResultBuffer argumentos)
    {
        var valores = Valores(argumentos);
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 1 || documento is null) return null;

        var database = documento.Database;

        if (valores[0] == "linhas")
        {
            documento.Editor.WriteMessage($"\nANALISES_LINHAS {string.Join(",", AnalisesIndependentes.Linhas(database).OrderBy(l => l))} fim\n");
            return true;
        }

        if (valores.Length < 5 || !Enum.TryParse<IndependentKind>(valores[1], out var tipo))
        {
            documento.Editor.WriteMessage($"\nANALISES ação recusada: {string.Join(" ", valores)}\n");
            return null;
        }

        var escopo = SombrasCommands.EscopoPelosNomes(database, valores[2], valores[3], valores[4], out var porque);
        if (escopo is null)
        {
            documento.Editor.WriteMessage($"\nANALISES {porque}\n");
            return null;
        }

        var unidade = AnalisesIndependentesCommands.Unidade(database);
        var extra = valores.Length >= 6 ? valores[5] : string.Empty;

        string frase;
        try
        {
            frase = EscritaForaDeComando.Fazer(documento, () =>
            {
                var (mesas, onde) = OperacoesDeAnalise.Resolver(database, escopo);

                return valores[0] switch
                {
                    "inserir" => OperacoesDeAnalise.Inserir(database, tipo, unidade, mesas, onde),
                    "apagar" => OperacoesDeAnalise.Apagar(database, tipo, mesas, onde),
                    "analisar" => OperacoesDeAnalise.Analisar(database, tipo, Regra(extra), unidade, mesas, onde),
                    "analisar-como-janela" => OperacoesDeAnalise.Analisar(database, tipo, RegraDaJanela(database, tipo, extra), unidade, mesas, onde),
                    "tirar-cores" => OperacoesDeAnalise.TirarCores(database, tipo, mesas, onde),
                    "quantificar" => OperacoesDeAnalise.Quantificar(database, tipo, Regra(extra), unidade, mesas, onde),
                    "aparencia" => OperacoesDeAnalise.Aparencia(database, tipo, new AnalysisTextLook(extra.Contains("fundo", StringComparison.Ordinal), extra.Contains("borda", StringComparison.Ordinal))),
                    _ => throw new ArgumentException($"ação {valores[0]} não existe"),
                };
            });
        }
        catch (System.Exception erro)
        {
            documento.Editor.WriteMessage($"\nANALISES falhou: {erro.Message}\n");
            return null;
        }

        documento.Editor.WriteMessage($"\nANALISES {frase.Replace('\n', ' ')}\n");
        return true;
    }

    /// <summary>A regra do gancho das análises: vermelho abaixo, azul acima.</summary>
    private static ThresholdRule Regra(string texto)
    {
        static LineLimits Limites(string par)
        {
            var partes = par.Split(';');
            double? Numero(string s) => s.Length == 0 ? null : double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
            return new LineLimits(Numero(partes[0]), partes.Length > 1 ? Numero(partes[1]) : null);
        }

        var pedacos = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var monoposte = pedacos.FirstOrDefault(p => !p.StartsWith("F=", StringComparison.Ordinal) && !p.StartsWith("T=", StringComparison.Ordinal) && p != "P") is { } m ? Limites(m) : new LineLimits(null, null);

        return new ThresholdRule(monoposte.Below, RgbColor.Red, monoposte.Above, RgbColor.Blue, pedacos.Contains("P"))
        {
            Front = pedacos.FirstOrDefault(p => p.StartsWith("F=", StringComparison.Ordinal)) is { } f ? Limites(f[2..]) : null,
            Rear = pedacos.FirstOrDefault(p => p.StartsWith("T=", StringComparison.Ordinal)) is { } t ? Limites(t[2..]) : null,
        };
    }

    /// <summary>A regra pelas caixas da janela: as colunas do desenho, a regra gravada e o texto de cada coluna.</summary>
    private static ThresholdRule RegraDaJanela(Database database, IndependentKind tipo, string texto)
    {
        var pedacos = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var colunas = ThresholdForm.Columns(tipo, AnalisesIndependentes.Linhas(database));
        var textos = new Dictionary<PillarLine, (string, string)>();

        foreach (var linha in Enum.GetValues<PillarLine>())
        {
            if (pedacos.FirstOrDefault(p => p.StartsWith(linha + "=", StringComparison.Ordinal)) is not { } coluna) continue;

            var par = coluna[(linha.ToString().Length + 1)..].Split(';');
            textos[linha] = (par[0], par.Length > 1 ? par[1] : "");
        }

        var gravada = AnalisesIndependentesCommands.Regra(database, tipo);
        return ThresholdForm.Build(colunas, textos, pedacos.Contains("abaixo"), pedacos.Contains("acima"), RgbColor.Red, RgbColor.Blue, pedacos.Contains("P"), gravada, out var erro)
            ?? throw new ArgumentException(erro);
    }

    /// <summary>
    /// O texto de análise como era antes do 42.6 (revisão): (clivus-analises-
    /// texto-antigo "PillarAbove") regrava os textos dessa análise no XData
    /// versão 1, sem a linha do pilar. Devolve quantos.
    /// </summary>
    [LispFunction("CLIVUS-ANALISES-TEXTO-ANTIGO")]
    public static object? AnalisesTextoAntigo(ResultBuffer argumentos)
    {
        var valores = Valores(argumentos);
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 1 || documento is null || !Enum.TryParse<IndependentKind>(valores[0], out var tipo)) return null;

        return EscritaForaDeComando.Fazer(documento, () =>
        {
            var database = documento.Database;
            using var transacao = database.TransactionManager.StartTransaction();

            var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
            var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var antigos = 0;

            foreach (ObjectId id in espaco)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is not MText texto || LayoutXData.LoadAnalysisText(texto) is not { } identidade || identidade.Kind != tipo) continue;

                texto.UpgradeOpen();
                PluginXData.Save(transacao, texto, AnalysisTextIdentity.Tipo, 1,
                    identidade.Id.ToString("D"), identidade.Table.ToString("D"), identidade.Kind.ToString(),
                    identidade.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                antigos++;
            }

            transacao.Commit();
            return antigos;
        });
    }

    /// <summary>(clivus-sombras-modulos): os handles dos blocos dos módulos com sombra no registro dos motivos, ou nil.</summary>
    [LispFunction("CLIVUS-SOMBRAS-MODULOS")]
    public static object? SombrasModulos(ResultBuffer argumentos)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return null;

        var handles = SombrasCommands.ModulosComSombra(documento.Database);
        return handles.Count == 0 ? null : new ResultBuffer(handles.Select(h => new TypedValue((int)LispDataType.Text, h)).ToArray());
    }

    /// <summary>
    /// O desenho de antes de 31/10/2026, quando a sombra pintava o módulo
    /// (lilás, violeta e roxo pela fração) e guardava a cor de antes em
    /// SOMBRA_PINTADAS: (clivus-sombra-antiga n) pinta assim os n primeiros
    /// módulos, calado como o comando de então, para o nível 2 conferir que o
    /// Apagar sombras ainda os devolve. Devolve quantos pintou.
    /// </summary>
    [LispFunction("CLIVUS-SOMBRA-ANTIGA")]
    public static object? SombraAntiga(ResultBuffer argumentos)
    {
        var valores = Valores(argumentos);
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 1 || !int.TryParse(valores[0], out var quantos) || documento is null) return null;

        var database = documento.Database;
        return EscritaForaDeComando.Fazer(documento, () =>
        {
            double[] fracoes = [0.1, 0.4, 0.9];
            var pintadas = new List<string> { "V1" };

            using (var transacao = database.TransactionManager.StartTransaction())
            {
                var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
                foreach (ObjectId id in espaco)
                {
                    if (pintadas.Count > quantos) break;
                    if (transacao.GetObject(id, OpenMode.ForRead) is not BlockReference modulo || LayoutXData.LoadModule(modulo) is null) continue;

                    var cor = SombrasCommands.CorDaFracao(fracoes[(pintadas.Count - 1) % fracoes.Length]);
                    modulo.UpgradeOpen();
                    pintadas.Add(modulo.Handle + "=" + PecasPintadas.Texto(modulo.Color));
                    modulo.Color = Autodesk.AutoCAD.Colors.Color.FromRgb(cor.R, cor.G, cor.B);
                }

                transacao.Commit();
            }

            PluginDictionary.Save(database, "SOMBRA_PINTADAS", new ResultBuffer(pintadas.Select(p => new TypedValue((int)DxfCode.Text, p)).ToArray()));
            return pintadas.Count - 1;
        });
    }

    private static string[] Valores(ResultBuffer? argumentos) =>
        argumentos?.AsArray().Select(v => Convert.ToString(v.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray() ?? [];

    /// <summary>
    /// O Cancelar da janela de andamento das sombras (05/10/2026), sem janela:
    /// (clivus-sombras-cancelar "dd/mm/aaaa" "dd/mm/aaaa" "fuso" n) gera o
    /// período de hora em hora, das 6h às 18h, pelo caminho do botão, e
    /// "clica" em Cancelar no n-ésimo passo do andamento. Escreve a frase e
    /// devolve T.
    /// </summary>
    [LispFunction("CLIVUS-SOMBRAS-CANCELAR")]
    public static object? SombrasCancelar(ResultBuffer argumentos)
    {
        var valores = Valores(argumentos);
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 4 || documento is null) return null;

        var periodo = SombrasCommands.Ler(valores[0], valores[1], "06:00", "18:00", "60", valores[2], out var porque);
        if (periodo is null || !int.TryParse(valores[3], out var noPasso))
        {
            documento.Editor.WriteMessage($"\nSOMBRAS {porque}\n");
            return null;
        }

        var passos = 0;
        var frase = EscritaForaDeComando.Fazer(documento, () => SombrasCommands.Gerar(documento, periodo, ShadowScope.WholeDrawing, (_, texto) =>
        {
            passos++;
            if (passos == 3) documento.Editor.WriteMessage($"\nSOMBRAS_ANDAMENTO {texto}\n");
            return passos < noPasso;
        }));
        documento.Editor.WriteMessage($"\n{frase}\n");
        return true;
    }
}
#endif
