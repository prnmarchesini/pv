using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// Análise › Linhas (27.1, 27.3): o Renan desenha a linha (ou escolhe uma
/// polilinha) e o gráfico do perfil aparece pronto. "Atualizar" refaz os
/// gráficos de todas as linhas e canaletas.
/// </summary>
internal static class LinhaCommands
{
    private const string Selecionar = "Selecionar";
    private const string Atualizar = "Atualizar";

    internal static void Executar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var previa = new PreviaDaLinha();

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var primeiro = new PromptPointOptions(Tr.T("\nPrimeiro ponto da linha (A)") + " [Selecionar/Atualizar]: ");
            primeiro.Keywords.Add(Selecionar);
            primeiro.Keywords.Add(Atualizar);
            primeiro.AppendKeywordsToMessage = false;

            var resposta = editor.GetPoint(primeiro);
            List<Point3> planta;
            ObjectId original = ObjectId.Null;

            if (resposta.Status == PromptStatus.Keyword && resposta.StringResult == Atualizar)
            {
                editor.WriteMessage($"\n{AtualizarTodas(documento, terreno.Mesh)}\n");
                return;
            }

            if (resposta.Status == PromptStatus.Keyword && resposta.StringResult == Selecionar)
            {
                var escolha = new PromptEntityOptions(Tr.T("\nEscolha a polilinha (ou linha) que vira linha de análise: "));
                escolha.SetRejectMessage(Tr.T("\nSó polilinha ou linha."));
                escolha.AddAllowedClass(typeof(Curve), false);

                var entidade = editor.GetEntity(escolha);
                if (entidade.Status != PromptStatus.OK) return;

                // Só curva solta do usuário: linha da drenagem, contorno de mesa,
                // área, vala ou traço de gráfico nunca viram linha (nem somem).
                if (EDoClivus(documento.Database, entidade.ObjectId))
                {
                    editor.WriteMessage(Tr.T("\nLINHA Essa entidade é do Clivus (linha, canaleta, mesa, área ou gráfico); escolha uma polilinha sua.\n"));
                    return;
                }

                planta = Caminho(documento.Database, entidade.ObjectId);
                original = entidade.ObjectId;
            }
            else
            {
                if (resposta.Status != PromptStatus.OK) return;

                planta = [new Point3(resposta.Value.X, resposta.Value.Y, 0)];

                // Como no PLINE: o que já foi clicado fica na tela enquanto se
                // clica o resto (Renan, 21/10/2026: "não vi sendo gerado linha").
                while (true)
                {
                    var proximo = new PromptPointOptions(planta.Count < 2
                        ? Tr.T("\nPróximo ponto: ")
                        : Tr.T("\nPróximo ponto (Enter termina, B no último)") + " [Desfazer]: ")
                    {
                        UseBasePoint = true,
                        BasePoint = new Point3d(planta[^1].X, planta[^1].Y, resposta.Value.Z),
                        AllowNone = planta.Count >= 2,
                    };
                    if (planta.Count >= 2) proximo.Keywords.Add("Desfazer");
                    proximo.AppendKeywordsToMessage = false;

                    var r = editor.GetPoint(proximo);
                    if (r.Status == PromptStatus.None) break;
                    if (r.Status == PromptStatus.Keyword)
                    {
                        planta.RemoveAt(planta.Count - 1);
                        previa.Mostrar(planta, resposta.Value.Z);
                        continue;
                    }
                    if (r.Status != PromptStatus.OK) return;

                    planta.Add(new Point3(r.Value.X, r.Value.Y, 0));
                    previa.Mostrar(planta, resposta.Value.Z);
                }
            }

            previa.Apagar();

            if (planta.Count < 2)
            {
                editor.WriteMessage(Tr.T("\nLINHA A linha precisa de pelo menos dois pontos.\n"));
                return;
            }

            var configuracao = DrenagemStore.Ler(documento.Database);
            ObjectId id;
            string frase;
            ChartDrawing grafico;

            // A linha e o gráfico nascem já, ao lado da linha: o lugar do
            // gráfico escolhido depois só o muda de lugar (Esc não perde nada).
            using (var transacao = documento.Database.TransactionManager.StartTransaction())
            {
                var nomes = LinhasDeDrenagem.Todas(transacao, documento.Database).Select(l => l.Identidade.Name);
                var identidade = new DrainageLineIdentity { Name = DrainageLineIdentity.NextName(Tr.T("Linha"), nomes) };

                (id, _) = LinhasDeDrenagem.Criar(transacao, documento.Database, terreno.Mesh, planta, identidade);
                frase = LinhasDeDrenagem.DesenharPerfil(transacao, documento.Database, terreno.Mesh, configuracao, id);

                var polilinha = (Polyline3d)transacao.GetObject(id, OpenMode.ForRead);
                grafico = LinhasDeDrenagem.Grafico(terreno.Mesh, configuracao, new LinhaNoDesenho(id, LinhasDeDrenagem.Identidade(polilinha)!, LinhasDeDrenagem.Vertices(transacao, polilinha))).Grafico;

                // A polilinha escolhida virou a linha de análise (pedido explícito): sai a original.
                if (!original.IsNull) transacao.GetObject(original, OpenMode.ForWrite).Erase();

                transacao.Commit();
            }

            editor.WriteMessage(Tr.F("\nLINHA {0} Camada {1}; o gráfico, na {2}.\n", frase, LayoutLayers.DrenagemLinha, LayoutLayers.DrenagemPerfil));

            // O quadro do gráfico segue o mouse; clique põe ali, Enter ou Esc deixa ao lado.
            if (LugarDoGrafico(editor, grafico) is { } lugar)
            {
                using var transacao = documento.Database.TransactionManager.StartTransaction();
                LinhasDeDrenagem.Refazer(transacao, documento.Database, terreno.Mesh, configuracao, id, IndiceDaDrenagem.Montar(transacao, documento.Database), lugar);
                transacao.Commit();
            }

            Enquadrar(documento, id);
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage($"\nLINHA {erro.Message}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na linha de análise da drenagem.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui fazer a linha: {0}\n", erro.Message));
        }
        finally
        {
            previa.Apagar();
        }
    }

    /// <summary>
    /// Onde vai o gráfico: com interface, o quadro dele segue o mouse até o
    /// clique; sem interface (Core Console), um ponto lido do script. Null =
    /// fica ao lado da linha.
    /// </summary>
    private static (double X, double Y)? LugarDoGrafico(Editor editor, ChartDrawing grafico)
    {
        var mensagem = Tr.T("\nOnde fica o gráfico (clique) <Enter: ao lado da linha>: ");

        if (!ClivusExtension.TemInterface())
        {
            var r = editor.GetPoint(new PromptPointOptions(mensagem) { AllowNone = true });
            return r.Status == PromptStatus.OK ? (r.Value.X, r.Value.Y) : null;
        }

        var arrasto = new QuadroDoGrafico(grafico, mensagem);
        var resultado = editor.Drag(arrasto);
        return resultado.Status == PromptStatus.OK ? (arrasto.Ponto.X, arrasto.Ponto.Y) : null;
    }

    /// <summary>Mostra a linha e o gráfico dela na tela (o gráfico pode ter ido para longe da vista).</summary>
    private static void Enquadrar(Document documento, ObjectId linha)
    {
        if (!ClivusExtension.TemInterface()) return;

        try
        {
            using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();
            var polilinha = (Polyline3d)transacao.GetObject(linha, OpenMode.ForRead);
            var caixa = polilinha.GeometricExtents;

            var indice = IndiceDaDrenagem.Montar(transacao, documento.Database);
            foreach (var id in indice.Ids(LinhasDeDrenagem.Identidade(polilinha)!.Id))
                if (transacao.GetObject(id, OpenMode.ForRead) is Entity e) caixa.AddExtents(e.GeometricExtents);

            // Um terço de folga em volta: Zoom do Local dos inversores amplia 3x.
            LocalDosInversores.Zoom(documento.Editor, caixa);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui enquadrar a linha nova.", erro);
        }
    }

    /// <summary>A prévia da linha enquanto se clica: uma polilinha passageira, fora do desenho.</summary>
    private sealed class PreviaDaLinha
    {
        private Polyline? _desenho;

        internal void Mostrar(List<Point3> planta, double z)
        {
            if (!ClivusExtension.TemInterface()) return;

            try
            {
                Apagar();
                if (planta.Count < 2) return;

                var p = new Polyline { Elevation = z, ColorIndex = 4 };
                for (var i = 0; i < planta.Count; i++) p.AddVertexAt(i, new Point2d(planta[i].X, planta[i].Y), 0, 0, 0);

                Autodesk.AutoCAD.GraphicsInterface.TransientManager.CurrentTransientManager.AddTransient(
                    p, Autodesk.AutoCAD.GraphicsInterface.TransientDrawingMode.DirectShortTerm, 128, []);
                _desenho = p;
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Não consegui mostrar a prévia da linha.", erro);
            }
        }

        internal void Apagar()
        {
            if (_desenho is null) return;

            try
            {
                Autodesk.AutoCAD.GraphicsInterface.TransientManager.CurrentTransientManager.EraseTransient(_desenho, []);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Não consegui tirar a prévia da linha.", erro);
            }

            _desenho.Dispose();
            _desenho = null;
        }
    }

    /// <summary>O quadro do gráfico preso ao mouse (canto de baixo à esquerda da moldura no cursor).</summary>
    private sealed class QuadroDoGrafico(ChartDrawing grafico, string mensagem) : DrawJig
    {
        private readonly (double MinX, double MinY, double MaxX, double MaxY) _caixa = grafico.Bounds;

        internal Point3d Ponto { get; private set; }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var opcoes = new JigPromptPointOptions(mensagem) { UserInputControls = UserInputControls.NullResponseAccepted | UserInputControls.Accept3dCoordinates };
            var r = prompts.AcquirePoint(opcoes);
            if (r.Status != PromptStatus.OK) return SamplerStatus.Cancel;
            if (r.Value.DistanceTo(Ponto) < 1e-6) return SamplerStatus.NoChange;

            Ponto = r.Value;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
        {
            var (x0, y0, x1, y1) = _caixa;
            var z = Ponto.Z;
            var cantos = new Point3dCollection
            {
                new(Ponto.X + x0, Ponto.Y + y0, z), new(Ponto.X + x1, Ponto.Y + y0, z),
                new(Ponto.X + x1, Ponto.Y + y1, z), new(Ponto.X + x0, Ponto.Y + y1, z), new(Ponto.X + x0, Ponto.Y + y0, z),
            };
            draw.Geometry.Polyline(cantos, Vector3d.ZAxis, IntPtr.Zero);

            // A moldura de verdade (do zero à largura e à altura) dentro do quadro.
            var moldura = new Point3dCollection
            {
                new(Ponto.X, Ponto.Y, z), new(Ponto.X + grafico.Width, Ponto.Y, z),
                new(Ponto.X + grafico.Width, Ponto.Y + grafico.Height, z), new(Ponto.X, Ponto.Y + grafico.Height, z), new(Ponto.X, Ponto.Y, z),
            };
            draw.Geometry.Polyline(moldura, Vector3d.ZAxis, IntPtr.Zero);

            foreach (var traco in grafico.Lines.Where(l => l.Kind == ChartLineKind.Terrain))
                draw.Geometry.Polyline(new Point3dCollection(traco.Points.Select(q => new Point3d(Ponto.X + q.X, Ponto.Y + q.Y, z)).ToArray()), Vector3d.ZAxis, IntPtr.Zero);

            return true;
        }
    }

    /// <summary>Refaz o gráfico de todas as linhas e canaletas (assentando de novo as que saíram do terreno).</summary>
    internal static string AtualizarTodas(Document documento, Tin terreno)
    {
        var configuracao = DrenagemStore.Ler(documento.Database);
        var feitas = 0;
        var problemas = new List<string>();

        using var transacao = documento.Database.TransactionManager.StartTransaction();
        var indice = IndiceDaDrenagem.Montar(transacao, documento.Database);

        foreach (var linha in LinhasDeDrenagem.Todas(transacao, documento.Database))
        {
            try
            {
                LinhasDeDrenagem.Reassentar(transacao, (Polyline3d)transacao.GetObject(linha.Id, OpenMode.ForWrite), terreno);
                var alerta = LinhasDeDrenagem.Refazer(transacao, documento.Database, terreno, configuracao, linha.Id, indice).Alerta;
                if (alerta.Length > 0) problemas.Add(alerta);
                feitas++;
            }
            catch (ArgumentException erro)
            {
                problemas.Add(erro.Message);
            }
        }

        transacao.Commit();

        return problemas.Count == 0
            ? Tr.F("LINHA {0} gráfico(s) refeito(s).", feitas)
            : Tr.F("LINHA {0} gráfico(s) refeito(s); {1} com problema: {2}", feitas, problemas.Count, string.Join("; ", problemas));
    }

    /// <summary>Se a entidade tem XData do Clivus (é de algum módulo do plugin).</summary>
    internal static bool EDoClivus(Database database, ObjectId id)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) return false;
        using var dados = e.GetXDataForApplication(PluginXData.Aplicativo);
        return dados is not null;
    }

    /// <summary>
    /// O caminho em planta de uma curva escolhida: os vértices, com os arcos
    /// divididos a cada metro (a linha da drenagem é feita de trechos retos).
    /// </summary>
    internal static List<Point3> Caminho(Database database, ObjectId id)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var curva = (Curve)transacao.GetObject(id, OpenMode.ForRead);
        var pontos = new List<Point3>();

        void Ponto(Point3d p)
        {
            if (pontos.Count > 0 && Math.Abs(pontos[^1].X - p.X) < 1e-6 && Math.Abs(pontos[^1].Y - p.Y) < 1e-6) return;
            pontos.Add(new Point3(p.X, p.Y, 0));
        }

        switch (curva)
        {
            case Polyline p:
                for (var i = 0; i < p.NumberOfVertices; i++)
                {
                    Ponto(p.GetPoint3dAt(i));

                    if (Math.Abs(p.GetBulgeAt(i)) > 1e-9 && (i < p.NumberOfVertices - 1 || p.Closed))
                    {
                        var (d0, d1) = (p.GetDistanceAtParameter(i), p.GetDistanceAtParameter(i + 1));
                        for (var d = d0 + 1; d < d1 - 1e-6; d += 1) Ponto(p.GetPointAtDist(d));
                    }
                }
                if (p.Closed) Ponto(p.GetPoint3dAt(0));
                break;

            case Polyline3d p3:
                foreach (ObjectId v in p3) Ponto(((PolylineVertex3d)transacao.GetObject(v, OpenMode.ForRead)).Position);
                break;

            case Line l:
                Ponto(l.StartPoint);
                Ponto(l.EndPoint);
                break;

            default:
                // Polilinha 2D antiga, spline, arco: amostrada a cada metro.
                var total = curva.GetDistanceAtParameter(curva.EndParam);
                for (var d = 0.0; d < total; d += 1) Ponto(curva.GetPointAtDist(d));
                Ponto(curva.EndPoint);
                break;
        }

        return pontos;
    }
}
