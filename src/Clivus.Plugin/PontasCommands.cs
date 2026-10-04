using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.PontasCommands))]

namespace Clivus.Plugin;

/// <summary>
/// As alturas das pontas de uma mesa, escolhidas à mão (Renan, 27/09/2026:
/// "quero dizer: olha, quero essa ponta com essa altura e essa com essa, aí
/// redesenha a mesa com isso"; e "tem uma ponta que está boa, aí eu quero
/// mudar a outra, abaixar, e deixar essa ponta travada").
///
/// A ponta é a que se vê nas cotas: a PB (altura livre da ponta baixa) no
/// primeiro e no último pilar. Clicar perto de uma ponta muda só ela, com a
/// outra travada; "Duas" pede as duas; "Automatico" devolve a mesa ao motor.
/// A mesa é refeita onde está, com o mesmo GUID, e as alturas ficam gravadas
/// nela: o Recalcular as mantém, no terreno de onde a mesa estiver.
/// </summary>
public static class PontasCommands
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// A maior PB que se aceita digitar, em metro. A tela dos Parâmetros fala
    /// em cm; aqui é metro, e "15" pensado em cm viraria 15 m sem erro.
    /// </summary>
    private const double MaiorPontaBaixa = 5.0;

    /// <summary>CLIVUS_PONTAS: a mesa da peça selecionada (ou clicada).</summary>
    [CommandMethod(PluginInfo.ComandoPontas, CommandFlags.UsePickSet)]
    public static void Pontas()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var guid = RecalcularCommands.MesaDaSelecao(editor, documento)
                ?? RecalcularCommands.MesaClicada(editor, documento, "\nClique numa peça da mesa: ");
            if (guid is null) return;

            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var contexto = Preparar(editor, documento, terreno, guid.Value, FileiraCommands.PerfilDaMesa(editor, silencioso: true));
            if (contexto is null) return;

            var (primeiraHoje, ultimaHoje) = contexto.PontasHoje;
            var nomePrimeira = contexto.NomeDaPrimeira;
            var nomeUltima = contexto.NomeDaUltima;

            editor.WriteMessage(
                $"\n{contexto.Mesa.Identity!.Label}: PB {Medida(primeiraHoje)} m na ponta {nomePrimeira}, {Medida(ultimaHoje)} m na ponta {nomeUltima}"
                + (contexto.Mesa.Identity.HasManualEnds ? " (escolhidas à mão)" : string.Empty) + ".\n");

            var opcoes = new PromptPointOptions("\nClique perto da ponta que vai mudar (a outra fica travada), ou Enter para as duas [Duas/Automatico]: ")
            {
                AllowNone = true,
            };
            opcoes.Keywords.Add("Duas");
            opcoes.Keywords.Add("Automatico");

            var resposta = editor.GetPoint(opcoes);

            double? primeira = null;
            double? ultima = null;

            if (resposta.Status == PromptStatus.Keyword && resposta.StringResult == "Automatico")
            {
                DevolverAoMotor(editor, documento, terreno, contexto);
                return;
            }

            // Enter pede as duas (29/09/2026, Renan: "lado direito altura X,
            // lado esquerdo altura Y").
            if (resposta.Status == PromptStatus.None || (resposta.Status == PromptStatus.Keyword && resposta.StringResult == "Duas"))
            {
                primeira = PerguntarAltura(editor, nomePrimeira, primeiraHoje);
                if (double.IsNaN(primeira ?? 0)) return;

                ultima = PerguntarAltura(editor, nomeUltima, ultimaHoje);
                if (double.IsNaN(ultima ?? 0)) return;
            }
            else if (resposta.Status == PromptStatus.OK)
            {
                // A ponta mais perto do clique, em planta.
                var clique = resposta.Value;
                var pertoDaPrimeira = Distancia2(clique, contexto.PePrimeira) <= Distancia2(clique, contexto.PeUltima);

                if (pertoDaPrimeira)
                {
                    primeira = PerguntarAltura(editor, nomePrimeira, primeiraHoje);
                    if (double.IsNaN(primeira ?? 0)) return;
                }
                else
                {
                    ultima = PerguntarAltura(editor, nomeUltima, ultimaHoje);
                    if (double.IsNaN(ultima ?? 0)) return;
                }
            }
            else
            {
                return;
            }

            Aplicar(editor, documento, terreno, contexto, primeira, ultima);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao mudar as pontas da mesa.", erro);
            editor.WriteMessage($"\nNão consegui mudar as pontas da mesa: {erro.Message}\n");
        }
    }

    /// <summary>
    /// CLIVUS_PONTAS_AUTO: letreiro e as duas alturas pela linha de comando, sem
    /// clique. Para o nível 2. Uma altura vazia ("") fica travada.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoPontasAutomatico)]
    public static void PontasAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var letreiro = editor.GetString(new PromptStringOptions("\nLetreiro da mesa: ") { AllowSpaces = false });
            if (letreiro.Status != PromptStatus.OK) return;

            var a = editor.GetString(new PromptStringOptions("\nPB no primeiro pilar (vazio trava): ") { AllowSpaces = false });
            var b = editor.GetString(new PromptStringOptions("\nPB no último pilar (vazio trava): ") { AllowSpaces = false });
            if (a.Status != PromptStatus.OK || b.Status != PromptStatus.OK) return;

            double? primeira = NumberInput.TryParseMeasure(a.StringResult, out var va) ? va : null;
            double? ultima = NumberInput.TryParseMeasure(b.StringResult, out var vb) ? vb : null;

            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            Guid? guid;

            using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
            {
                guid = LayoutScan.Tables(transacao, documento.Database).Values
                    .FirstOrDefault(m => m.Identity?.Label == letreiro.StringResult)?.Identity?.Id;
            }

            if (guid is null)
            {
                editor.WriteMessage($"\nPONTAS Não achei a mesa {letreiro.StringResult}.\n");
                return;
            }

            var contexto = Preparar(editor, documento, terreno, guid.Value, MesaCommands.MesaDeExemplo());
            if (contexto is null) return;

            Aplicar(editor, documento, terreno, contexto, primeira, ultima);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_PONTAS_AUTO.", erro);
            editor.WriteMessage($"\nNão consegui mudar as pontas: {erro.Message}\n");
        }
    }

    /// <summary>O que o comando precisa saber da mesa antes de perguntar.</summary>
    private sealed record Contexto(
        TableParts Mesa,
        PlacedTable Celula,
        IReadOnlyList<Point3> Cantos,
        TableProfile Perfil,
        TableGeometry Geometria,
        ProjectSettings Settings,
        (double Primeira, double Ultima) PontasHoje,
        Point3d PePrimeira,
        Point3d PeUltima,
        string NomeDaPrimeira,
        string NomeDaUltima);

    private static Contexto? Preparar(Editor editor, Document documento, ProcessedTerrain terreno, Guid guid, TableProfile perfil)
    {
        var doProjeto = ConfigCommands.Inicial(documento, out var avisoDaConfig);
        if (doProjeto.EmbedmentNote(perfil.Frame) is { } notaDoT3) editor.WriteMessage($"\n  ATENÇÃO: {notaDoT3}.\n");
        var settings = doProjeto.ForTable(perfil.Frame);
        if (avisoDaConfig is not null) editor.WriteMessage($"\n  ATENÇÃO: {avisoDaConfig}\n");

        var pilares = perfil.Frame.Pillars(perfil.Layout);
        var geometria = TableGeometry.Local(perfil.Layout, pilares, perfil.Frame);

        TableParts? mesa;
        List<Point3> cantos;

        using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
        {
            LayoutScan.Tables(transacao, documento.Database).TryGetValue(guid, out mesa);

            if (mesa?.Identity is null || mesa.Contour is not { } contorno)
            {
                editor.WriteMessage("\nPONTAS A mesa não tem contorno; não há como saber onde ela está.\n");
                return null;
            }

            // Suja: o que está desenhado não é o que o motor calculou (foi
            // movida, copiada, editada), e a cota do contorno pode ter vindo
            // de um MOVE com Z, não do terreno (regra sagrada 5).
            if (mesa.Identity.Dirty)
            {
                editor.WriteMessage($"\nPONTAS {mesa.Identity.Label} está suja ({mesa.Identity.DirtyReason}): recalcule antes de mudar as pontas.\n");
                return null;
            }

            if (mesa.IsDuplicated)
            {
                editor.WriteMessage($"\nPONTAS {mesa.Identity.Label} tem contornos repetidos (mesa copiada). Apague a cópia ou use o Regerar área.\n");
                return null;
            }

            cantos = FileiraCommands.Vertices((Polyline3d)transacao.GetObject(contorno, OpenMode.ForRead), transacao).ToList();
        }

        // O perfil do tamanho da mesa desenhada, não o "atual" da biblioteca.
        var doDesenho = FileiraCommands.PerfilDaMesaDesenhada(cantos, perfil, mesa.Identity.ProfileName, documento.Database);

        if (!ReferenceEquals(doDesenho, perfil))
        {
            perfil = doDesenho;
            geometria = FileiraCommands.GeometriaDe(perfil);
        }

        PlacedTable celula;

        try
        {
            celula = TableCells.FromCorners(cantos, mesa.Identity.Label, geometria.Length, geometria.Depth * Math.Cos(perfil.TiltRadians));
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage($"\nPONTAS {mesa.Identity.Label}: {erro.Message} Use o Regerar área.\n");
            return null;
        }

        // Como a mesa está hoje: as cotas da borda baixa do contorno.
        var hoje = RowPipeline.ProcessFixed(celula, geometria, perfil.TiltRadians, terreno.Mesh, settings, cantos[0].Z, cantos[1].Z, null).Tables[0];
        var primeiro = hoje.Pillars.Pillars[0];
        var ultimo = hoje.Pillars.Pillars[^1];

        if (primeiro.LowEdgeClearance is not { } a || ultimo.LowEdgeClearance is not { } b)
        {
            editor.WriteMessage($"\nPONTAS {mesa.Identity.Label}: a ponta baixa de um pilar da ponta está fora do terreno.\n");
            return null;
        }

        var centro = new Point3((primeiro.X + ultimo.X) / 2, (primeiro.Y + ultimo.Y) / 2, 0);

        return new Contexto(
            mesa, celula, cantos, perfil, geometria, settings, (a, b),
            new Point3d(primeiro.X, primeiro.Y, 0), new Point3d(ultimo.X, ultimo.Y, 0),
            Rumo(primeiro.X - centro.X, primeiro.Y - centro.Y), Rumo(ultimo.X - centro.X, ultimo.Y - centro.Y));
    }

    private static void Aplicar(Editor editor, Document documento, ProcessedTerrain terreno, Contexto c, double? primeira, double? ultima)
    {
        var ajuste = ManualEnds.Apply(
            c.Celula, c.Geometria, c.Perfil.TiltRadians, terreno.Mesh, c.Settings, c.Cantos[0].Z, c.Cantos[1].Z, primeira, ultima);

        // Antes de apagar: as cotas da própria mesa contam (revisão do 8.8).
        var analise = LayoutDrawer.Analise.ComoODesenho(documento.Database);

        RecalcularCommands.Apagar(documento, c.Mesa);

        var guid = c.Mesa.Identity!.Id;
        var pontas = (ajuste.FirstLowEdge, ajuste.LastLowEdge);

        LayoutDrawer.Draw(
            documento.Database, ajuste.Row, c.Geometria, c.Perfil.Layout.Module, c.Perfil.TiltRadians, c.Settings.Analyses,
            _ => guid, _ => pontas, analise,
            LayoutDrawer.TiposDeMesa.DaMesa(documento.Database, c.Mesa.Identity!.ProfileName, c.Geometria, c.Perfil.Layout.Module));

        var mesa = ajuste.Row.Tables[0];

        editor.WriteMessage(
            $"\nPONTAS {c.Mesa.Identity.Label} refeita: PB {Medida(ajuste.FirstLowEdge)} m na ponta {c.NomeDaPrimeira}, "
            + $"{Medida(ajuste.LastLowEdge)} m na ponta {c.NomeDaUltima}; declividade "
            + $"{(Math.Abs(mesa.Pillars.LongitudinalTiltRadians) * 180 / Math.PI).ToString("0.#", Brasil)}°; {mesa.Pillars.Describe()}.\n");

        foreach (var aviso in ajuste.Warnings) editor.WriteMessage($"\n  ATENÇÃO: {aviso}\n");

        editor.WriteMessage("\n  As alturas ficam gravadas na mesa: o Recalcular as mantém; \"Automatico\" devolve a mesa ao motor.\n");
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    /// <summary>Tira as alturas à mão e recalcula a mesa pelo motor.</summary>
    private static void DevolverAoMotor(Editor editor, Document documento, ProcessedTerrain terreno, Contexto c)
    {
        var identidade = c.Mesa.Identity!;

        if (!identidade.HasManualEnds)
        {
            editor.WriteMessage($"\nPONTAS {identidade.Label} já é do motor; nada a devolver.\n");
            return;
        }

        using (var transacao = documento.Database.TransactionManager.StartTransaction())
        {
            var contorno = (Entity)transacao.GetObject(c.Mesa.Contour!.Value, OpenMode.ForWrite);
            LayoutXData.SaveTable(transacao, contorno, identidade with { ManualFirstLowEdge = null, ManualLastLowEdge = null });
            transacao.Commit();
        }

        RecalcularCommands.RecalcularMesas(editor, documento, terreno, [identidade.Id], c.Perfil);
    }

    /// <summary>A altura pedida; null é Enter (mantém a de hoje); NaN é cancelar.</summary>
    private static double? PerguntarAltura(Editor editor, string nome, double hoje)
    {
        while (true)
        {
            var resposta = editor.GetString(new PromptStringOptions(
                $"\nPB na ponta {nome}, em metro (hoje {Medida(hoje)}; Enter mantém): ")
            {
                AllowSpaces = false,
            });

            if (resposta.Status != PromptStatus.OK) return double.NaN;
            if (string.IsNullOrWhiteSpace(resposta.StringResult)) return null;

            if (NumberInput.TryParseMeasure(resposta.StringResult, out var valor) && double.IsFinite(valor) && valor > 0 && valor <= MaiorPontaBaixa)
                return valor;

            editor.WriteMessage($"\nIsso não é uma altura em metro entre 0 e {Medida(MaiorPontaBaixa)} (ex.: 0,45; em metro, não em cm).\n");
        }
    }

    /// <summary>O rumo de uma direção em planta, em palavras ("leste", "sudoeste").</summary>
    private static string Rumo(double dx, double dy)
    {
        string[] nomes = ["leste", "nordeste", "norte", "noroeste", "oeste", "sudoeste", "sul", "sudeste"];
        var angulo = Math.Atan2(dy, dx) * 180 / Math.PI;
        var setor = (int)Math.Round(((angulo % 360) + 360) % 360 / 45) % 8;

        return nomes[setor];
    }

    private static double Distancia2(Point3d a, Point3d b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    private static string Medida(double valor) => valor.ToString("0.00", Brasil);
}
