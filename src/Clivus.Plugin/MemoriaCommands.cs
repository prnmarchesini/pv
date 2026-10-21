using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.MemoriaCommands))]

namespace Clivus.Plugin;

/// <summary>Memória › Cálculo (31): a altura sugerida e o caminho do nível 2.</summary>
public static class MemoriaCommands
{
    /// <summary>CLIVUS_DRN_MEMORIA_ALTURA: as canaletas escolhidas ganham a altura (ou o raio) que a memória sugere.</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemMemoriaAltura, CommandFlags.UsePickSet)]
    public static void Altura()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.T("\nEscolha as canaletas que recebem a altura sugerida: ") },
                new SelectionFilter([new TypedValue((int)DxfCode.Start, "POLYLINE")]));
            if (selecao.Status != PromptStatus.OK) return;

            editor.WriteMessage($"\n{MemoriaDeDrenagem.AplicarAltura(documento, terreno.Mesh, selecao.Value.GetObjectIds())}\n");
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage($"\nMEMÓRIA {erro.Message}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao aplicar a altura sugerida.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui aplicar a altura: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_DRN_VAZAO (32.8): clique num ponto da canaleta (com a cruz, não
    /// o quadradinho de escolher objeto: em cima da canaleta estão o 3D e a
    /// pintura dela, e o quadradinho pegava esses). Passando o mouse, a vazão
    /// aparece ao lado da cruz; o clique deixa o valor escrito na tela até
    /// sair. Enter sai. Usa as bacias e os parâmetros gravados.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoDrenagemVazao)]
    public static void Vazao()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var marcas = new List<Entity>();
        var soltas = new List<Entity>();
        PointMonitorEventHandler? aoMover = null;
        CaixaDeSelecao? placar = null;
        var ultimo = string.Empty;

        // O placar grande na tela, como o kWp do grupo de módulos (pedido de 21/10/2026).
        void Placar(string texto)
        {
            placar ??= GrupoCommands.NovoPlacar();
            placar.TextoLivre = texto;
            if (!placar.IsVisible) placar.Show();
        }

        void Pôr(List<Entity> lista, Entity e)
        {
            e.ColorIndex = 6;
            TransientManager.CurrentTransientManager.AddTransient(e, TransientDrawingMode.DirectTopmost, 128, []);
            lista.Add(e);
        }

        void Tirar(List<Entity> lista)
        {
            foreach (var e in lista)
            {
                TransientManager.CurrentTransientManager.EraseTransient(e, []);
                e.Dispose();
            }

            lista.Clear();
        }

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            if (DrenagemStore.LerBacias(documento.Database) is null)
            {
                editor.WriteMessage(Tr.T("\nVAZÃO Calcule as bacias antes (Memória › Bacias).\n"));
                return;
            }

            var memoria = MemoriaDeDrenagem.Montar(documento.Database, terreno.Mesh);
            var hidrologia = DrenagemStore.Ler(documento.Database).Hydrology;

            // Cada canaleta com bacia, como um caminho do rastreio (sem gráfico).
            var canaletas = memoria.Channels
                .Where(c => c.Input.Basin is not null && c.Input.Profile.Points.Count >= 2)
                .Select(c => (Calculo: c, Caminho: new TrackedChart(c.Input.Id, c.Input.Name, (0, 0), new ChartDrawing([], [], 0, 0, 1),
                    c.Input.Profile.Points.Select(q => new TrackPoint(q.Distance, q.X, q.Y, q.Terrain, q.Bottom)).ToList())))
                .ToList();

            if (canaletas.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nVAZÃO Nenhuma canaleta com bacia: calcule as bacias de novo (Memória › Bacias).\n"));
                return;
            }

            // A canaleta mais perto do ponto, dentro da tolerância.
            (ChannelCalculation Calculo, TrackHit Ponto, double Area, double Q)? Achar(double x, double y, double tolerancia)
            {
                (ChannelCalculation, TrackHit, double, double)? melhor = null;
                var menor = double.MaxValue;

                foreach (var (calculo, caminho) in canaletas)
                {
                    if (ProfileTracker.FromPlan(caminho, x, y, tolerancia) is not { } h) continue;

                    var d = Math.Sqrt((h.PlanX - x) * (h.PlanX - x) + (h.PlanY - y) * (h.PlanY - y));
                    if (d >= menor) continue;

                    var (area, q) = DrainageMemory.FlowAt(hidrologia, calculo, h.Distance);
                    (menor, melhor) = (d, (calculo, h, area, q));
                }

                return melhor;
            }

            string Frase(ChannelCalculation c, TrackHit h, double q) =>
                Tr.F("{0} · {1:0.0} m · Q {2:0.0} l/s (fim {3:0.0})", c.Input.Name, h.Distance, q, c.FlowLitersPerSecond);

            aoMover = (_, e) =>
            {
                try
                {
                    Tirar(marcas);
                    var p = e.Context.ComputedPoint;
                    var t = RastreioDoPerfil.Pixels(15);
                    if (Achar(p.X, p.Y, t) is not { } a)
                    {
                        Placar(ultimo.Length > 0 ? ultimo : Tr.T("Vazão: passe o mouse na canaleta"));
                        return;
                    }

                    Placar(Tr.F("Vazão: {0}", Frase(a.Calculo, a.Ponto, a.Q)));

                    Pôr(marcas, new Circle(new Point3d(a.Ponto.PlanX, a.Ponto.PlanY, a.Ponto.Terrain + 0.1), Vector3d.ZAxis, t));
                    e.AppendToolTipText(Frase(a.Calculo, a.Ponto, a.Q));
                }
                catch (System.Exception erro)
                {
                    RegistroDeDiagnostico.Registrar("Falha na vazão no ponto (mouse).", erro);
                }
            };
            editor.PointMonitor += aoMover;
            Placar(Tr.T("Vazão: passe o mouse na canaleta"));

            while (true)
            {
                var r = editor.GetPoint(new PromptPointOptions(Tr.T("\nClique no ponto da canaleta (Enter sai): ")) { AllowNone = true });
                if (r.Status != PromptStatus.OK) return;

                var clique = r.Value.TransformBy(editor.CurrentUserCoordinateSystem);
                if (Achar(clique.X, clique.Y, RastreioDoPerfil.Pixels(30)) is not { } a)
                {
                    editor.WriteMessage(Tr.T("\nVAZÃO Nenhuma canaleta perto do clique.\n"));
                    continue;
                }

                var h = a.Ponto;
                ultimo = Tr.F("Vazão: {0}", Frase(a.Calculo, h, a.Q));
                Placar(ultimo);
                editor.WriteMessage(Tr.F("\nVAZÃO {0}, a {1:0.0} m da ponta de cima (de {2:0.0} m): chegam {3:N0} m² de {4:N0} m²; Q = {5:0.0} l/s ({6:0.0000} m³/s). No fim: {7:0.0} l/s.\n",
                    a.Calculo.Input.Name, h.Distance, a.Calculo.Input.Profile.Length, a.Area, a.Calculo.Input.Basin!.Area, a.Q, a.Q / 1000, a.Calculo.FlowLitersPerSecond));

                // O valor fica escrito ao lado do ponto até sair do comando.
                var altura = RastreioDoPerfil.Pixels(12);
                var z = h.Terrain + 0.1;
                Pôr(soltas, new Circle(new Point3d(h.PlanX, h.PlanY, z), Vector3d.ZAxis, 0.5 * altura));
                Pôr(soltas, new DBText
                {
                    Position = new Point3d(h.PlanX + altura, h.PlanY + altura, z),
                    Height = altura,
                    TextString = Tr.F("Q {0:0.0} l/s · {1:0.0} m", a.Q, h.Distance),
                });
            }
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage($"\nVAZÃO {erro.Message}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na vazão no ponto.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui calcular a vazão: {0}\n", erro.Message));
        }
        finally
        {
            if (aoMover is not null) editor.PointMonitor -= aoMover;
            Tirar(marcas);
            Tirar(soltas);
            placar?.Close();
        }
    }

    /// <summary>CLIVUS_DRN_MEMORIA_AUTO: o arquivo XLSX; grava o Excel e escreve cada canaleta numa linha (nível 2).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemMemoriaAutomatico)]
    public static void Automatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var resposta = editor.GetString(new PromptStringOptions(Tr.T("\nArquivo XLSX: ")) { AllowSpaces = true });
            if (resposta.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(resposta.StringResult)) return;

            editor.WriteMessage($"\n{MemoriaDeDrenagem.Exportar(documento, terreno.Mesh, resposta.StringResult.Trim())}\n");

            var inv = CultureInfo.InvariantCulture;
            var memoria = MemoriaDeDrenagem.Montar(documento.Database, terreno.Mesh);
            foreach (var c in memoria.Channels)
            {
                editor.WriteMessage(string.Format(inv,
                    "\nDRN_MEMORIA nome={0} area={1:0.##} tc={2:0.###} i={3:0.###} q={4:0.####} imin={5:0.#####} imax={6:0.#####} hmax={7:0.####} vmax={8:0.###} f={9:0.##} ysug={10:0.##} des={11} alertas={12} fim\n",
                    c.Input.Name.Replace(' ', '_'), c.Input.Basin?.Area ?? 0, c.Tc, c.Intensity, c.Flow, c.MinSlope, c.MaxSlope, c.MaxDepth, c.MaxVelocity,
                    c.Freeboard, c.SuggestedHeight, (c.DissipatorModel ?? "-").Replace(' ', '_'), c.Alerts.Count));
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_DRN_MEMORIA_AUTO.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui fazer a memória: {0}\n", erro.Message));
        }
    }
}
