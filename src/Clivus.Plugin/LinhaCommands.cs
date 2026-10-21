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

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var primeiro = new PromptPointOptions(Tr.T("\nPrimeiro ponto da linha") + " [Selecionar/Atualizar]: ");
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

                planta = Caminho(documento.Database, entidade.ObjectId);
                original = entidade.ObjectId;
            }
            else
            {
                if (resposta.Status != PromptStatus.OK) return;

                planta = [new Point3(resposta.Value.X, resposta.Value.Y, 0)];

                while (true)
                {
                    var proximo = new PromptPointOptions(Tr.T("\nPróximo ponto (Enter termina): "))
                    {
                        UseBasePoint = true,
                        BasePoint = new Point3d(planta[^1].X, planta[^1].Y, resposta.Value.Z),
                        AllowNone = true,
                    };

                    var r = editor.GetPoint(proximo);
                    if (r.Status == PromptStatus.None) break;
                    if (r.Status != PromptStatus.OK) return;

                    planta.Add(new Point3(r.Value.X, r.Value.Y, 0));
                }
            }

            if (planta.Count < 2)
            {
                editor.WriteMessage(Tr.T("\nLINHA A linha precisa de pelo menos dois pontos.\n"));
                return;
            }

            var lugar = new PromptPointOptions(Tr.T("\nOnde fica o gráfico (canto de baixo à esquerda) <ao lado da linha>: ")) { AllowNone = true };
            var ondeResposta = editor.GetPoint(lugar);
            if (ondeResposta.Status is not (PromptStatus.OK or PromptStatus.None)) return;
            (double, double)? onde = ondeResposta.Status == PromptStatus.OK ? (ondeResposta.Value.X, ondeResposta.Value.Y) : null;

            var configuracao = DrenagemStore.Ler(documento.Database);
            string frase;

            using (var transacao = documento.Database.TransactionManager.StartTransaction())
            {
                var nomes = LinhasDeDrenagem.Todas(transacao, documento.Database).Select(l => l.Identidade.Name);
                var identidade = new DrainageLineIdentity { Name = DrainageLineIdentity.NextName(Tr.T("Linha"), nomes) };

                var (id, _) = LinhasDeDrenagem.Criar(transacao, documento.Database, terreno.Mesh, planta, identidade);
                frase = LinhasDeDrenagem.DesenharPerfil(transacao, documento.Database, terreno.Mesh, configuracao, id, onde);

                // A polilinha escolhida virou a linha de análise (pedido explícito): sai a original.
                if (!original.IsNull) transacao.GetObject(original, OpenMode.ForWrite).Erase();

                transacao.Commit();
            }

            editor.WriteMessage(Tr.F("\nLINHA {0} Camada {1}; o gráfico, na {2}.\n", frase, LayoutLayers.DrenagemLinha, LayoutLayers.DrenagemPerfil));
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
    }

    /// <summary>Refaz o gráfico de todas as linhas e canaletas (assentando de novo as que saíram do terreno).</summary>
    internal static string AtualizarTodas(Document documento, Tin terreno)
    {
        var configuracao = DrenagemStore.Ler(documento.Database);
        var feitas = 0;
        var problemas = new List<string>();

        using var transacao = documento.Database.TransactionManager.StartTransaction();

        foreach (var linha in LinhasDeDrenagem.Todas(transacao, documento.Database))
        {
            try
            {
                LinhasDeDrenagem.Reassentar(transacao, (Polyline3d)transacao.GetObject(linha.Id, OpenMode.ForWrite), terreno);
                LinhasDeDrenagem.DesenharPerfil(transacao, documento.Database, terreno, configuracao, linha.Id);
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
