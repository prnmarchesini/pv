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
            _ => new JanelaDeSombras(documento),
        };
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
