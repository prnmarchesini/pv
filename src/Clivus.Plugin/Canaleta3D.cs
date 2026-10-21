using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// O que a canaleta tem além do eixo e do gráfico (28.3, 29.1): o sólido 3D
/// da parede e, em planta, a pintura dos trechos abaixo da declividade mínima
/// (amarelo) e invertidos (vermelho), na camada de alerta (regra 4: nunca
/// falhar calado). Tudo com o GUID da canaleta no XData, refeito inteiro
/// quando ela muda.
/// </summary>
internal static class Canaleta3D
{
    internal const string Tipo3D = "DrnCanaleta3D";

    /// <summary>O dissipador e a laje da transposição (31.6), presos à canaleta como o sólido.</summary>
    internal const string TipoPeca = "DrnPeca";
    private const string TipoAlerta = "DrnAlerta";

    /// <summary>
    /// Desenha o 3D e a pintura da canaleta (nada, se a linha não é canaleta).
    /// O anterior já foi apagado por quem chama. Devolve o resumo dos
    /// alertas, ou vazio.
    /// </summary>
    /// <param name="indice">O índice da operação: guarda as vias, lidas uma vez só para todas as canaletas.</param>
    internal static string Desenhar(Transaction transacao, Database database, Tin terreno, DrainageSettings configuracao, ObjectId linhaId, IndiceDaDrenagem indice)
    {
        var polilinha = (Polyline3d)transacao.GetObject(linhaId, OpenMode.ForRead);
        var identidade = LinhasDeDrenagem.Identidade(polilinha) ?? throw new InvalidOperationException(Tr.T("A entidade não é uma linha da drenagem."));

        if (identidade.Section is not { } secao) return string.Empty;

        var linha = new LinhaNoDesenho(linhaId, identidade, LinhasDeDrenagem.Vertices(transacao, polilinha));
        var (terrenoDaLinha, canaleta) = LinhasDeDrenagem.Perfil(terreno, configuracao, linha);
        if (canaleta is null || canaleta.Points.Count < 2) return string.Empty;

        // Trecho sem terreno embaixo: o 3D ligaria o buraco com uma reta
        // inventada. Fica sem 3D, avisado (regra 4).
        if (terrenoDaLinha.OutsideCount > 0)
            return Tr.F("{0}: {1} ponto(s) fora do terreno; a canaleta ficou sem 3D até voltar inteira para dentro dele.", identidade.Name, terrenoDaLinha.OutsideCount);

        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var camada3D = LayoutLayers.GarantirDrenagem(transacao, database, LayoutLayers.DrenagemCanaleta3D);
        var guid = identidade.Id.ToString("D");

        void Marcar(Entity e, string tipo)
        {
            espaco.AppendEntity(e);
            transacao.AddNewlyCreatedDBObject(e, true);
            PluginXData.Save(transacao, e, tipo, 1, guid);
        }

        var aneis = ChannelSolid.Rings(canaleta.Points, secao);
        var solido = Solido(aneis);

        if (solido is not null)
        {
            solido.Layer = camada3D;
            Marcar(solido, Tipo3D);
        }
        else
        {
            // O modelador recusou o sólido (quina muito fechada, trecho
            // degenerado): fica a malha fechada, que mostra o mesmo 3D.
            var malha = Malha(espaco, transacao, aneis, camada3D);
            PluginXData.Save(transacao, malha, Tipo3D, 1, guid);
        }

        // 31.6: o dissipador no pé (jusante) e a laje da transposição onde a
        // canaleta cruza via. Blocos simples, com as medidas do álbum do DNIT.
        Pecas(terreno, configuracao, linha, canaleta, secao, camada3D, indice.Vias(transacao, database, configuracao.Hydrology.RoadLayers), e => Marcar(e, TipoPeca));

        // A pintura dos trechos ruins, em planta, sobre o terreno.
        var camadaAlerta = LayoutLayers.GarantirDrenagem(transacao, database, LayoutLayers.DrenagemAlerta);
        var baixos = canaleta.LowSegments.ToList();
        var invertidos = canaleta.InvertedSegments.ToList();

        foreach (var (trecho, cor) in baixos.Select(t => (t, (short)2)).Concat(invertidos.Select(t => (t, (short)1))))
        {
            var pontos = canaleta.Points.Where(p => p.Distance >= trecho.From - 1e-9 && p.Distance <= trecho.To + 1e-9).ToList();
            if (pontos.Count < 2) continue;

            var pintura = new Polyline3d { Layer = camadaAlerta, Color = Color.FromColorIndex(ColorMethod.ByAci, cor) };
            Marcar(pintura, TipoAlerta);

            foreach (var p in pontos)
            {
                var v = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Terrain + 0.05));
                pintura.AppendVertex(v);
                transacao.AddNewlyCreatedDBObject(v, true);
            }
        }

        var fundo = identidade.Mode == ChannelBottomMode.MinimumSlope && canaleta.MaxDepth > secao.UsefulHeight + 0.01
            ? Tr.F("{0}: o fundo com declividade mínima aprofunda até {1:0.00} m abaixo do terreno (a seção tem {2:0.00} m).", identidade.Name, canaleta.MaxDepth, secao.UsefulHeight)
            : string.Empty;

        if (baixos.Count == 0 && invertidos.Count == 0) return fundo;

        return (fundo.Length > 0 ? fundo + " " : string.Empty) + Tr.F("{0}: {1} trecho(s) abaixo de {2:0.0}% (amarelo) e {3} plano(s) ou subindo (vermelho), pintados na camada {4}.",
            identidade.Name, baixos.Count, configuracao.Hydrology.MinimumSlope * 100, invertidos.Count, camadaAlerta);
    }

    /// <summary>
    /// O dissipador de energia (DES: L2 de largura, comprimento 2·L1, 32 cm de
    /// altura, o topo no terreno do pé) e, em cada cruzamento com via, a laje
    /// da transposição (TSS: o comprimento do modelo atravessando a canaleta,
    /// 50 cm ao longo dela, 21 cm de espessura, o topo no terreno).
    /// </summary>
    private static void Pecas(Tin terreno, DrainageSettings configuracao,
        LinhaNoDesenho linha, ChannelProfileResult canaleta, ChannelSection secao, string camada, List<IReadOnlyList<Point3>> vias, Action<Entity> marcar)
    {
        // O dissipador só onde o Renan pôs (32.5: "coloca opção de dissipador").
        if (linha.Identidade.Dissipator != DissipatorEnd.None) Dissipador(terreno, linha.Identidade.Dissipator, canaleta, secao, camada, marcar);

        if (vias.Count == 0) return;

        var modelo = ChannelCrossing.Model(secao.TopWidth, configuracao.Hydrology.CrossingMargin);
        var laje = modelo is not null && double.TryParse(modelo[4..], out var cm) ? cm / 100 : secao.TopWidth + configuracao.Hydrology.CrossingMargin;

        foreach (var (x, y, dx, dy) in MemoriaDeDrenagem.Cruzamentos(linha.Vertices, vias))
        {
            if (!terreno.TryGetZ(x, y, out var z)) continue;
            marcar(Caixa(x, y, z - 0.105, 0.50, laje, 0.21, Math.Atan2(dy, dx), camada));
        }
    }

    /// <summary>
    /// O dissipador de energia (DES do DNIT, IPR 736, desenho 1.19) na ponta
    /// escolhida: em planta, o trapézio que abre de L1 (na canaleta) para L2 ao
    /// longo do comprimento, com o nome do modelo; em 3D, o mesmo trapézio com
    /// 32 cm de altura e o topo no terreno. Medidas do modelo do álbum que cobre
    /// a abertura; acima do maior, pela conta (L1 = B, L2 = 3·L1, 2·L1).
    /// </summary>
    private static void Dissipador(Tin terreno, DissipatorEnd ponta, ChannelProfileResult canaleta, ChannelSection secao, string camada, Action<Entity> marcar)
    {
        var pontos = canaleta.Points;
        var (l1, l2, comprimento) = EnergyDissipator.Size(secao.TopWidth);
        var modelo = EnergyDissipator.Model(secao.TopWidth);
        if (modelo is not null && int.TryParse(modelo[4..modelo.IndexOf('-')], out var cm))
            (l1, l2, comprimento) = EnergyDissipator.Size(cm / 100.0);

        // A ponta e a direção para fora da canaleta.
        var fim = ponta == DissipatorEnd.Downstream ? pontos[^1] : pontos[0];
        var vizinho = ponta == DissipatorEnd.Downstream
            ? pontos.Take(pontos.Count - 1).LastOrDefault(q => Math.Abs(q.X - fim.X) + Math.Abs(q.Y - fim.Y) > 1e-6)
            : pontos.Skip(1).FirstOrDefault(q => Math.Abs(q.X - fim.X) + Math.Abs(q.Y - fim.Y) > 1e-6);

        var (dx, dy) = (fim.X - vizinho.X, fim.Y - vizinho.Y);
        var n = Math.Sqrt(dx * dx + dy * dy);
        if (n < 1e-9) return;
        (dx, dy) = (dx / n, dy / n);
        var (px, py) = (-dy, dx);

        var (sx, sy) = (fim.X + dx * comprimento, fim.Y + dy * comprimento);
        var topo = Math.Min(fim.Terrain, terreno.TryGetZ(sx, sy, out var zs) ? zs : fim.Terrain);

        Point2d[] trapezio =
        [
            new(fim.X + px * l1 / 2, fim.Y + py * l1 / 2), new(sx + px * l2 / 2, sy + py * l2 / 2),
            new(sx - px * l2 / 2, sy - py * l2 / 2), new(fim.X - px * l1 / 2, fim.Y - py * l1 / 2),
        ];

        Polyline Contorno(double z)
        {
            var c = new Polyline { Layer = camada, Elevation = z, Closed = true };
            for (var i = 0; i < trapezio.Length; i++) c.AddVertexAt(i, trapezio[i], 0, 0, 0);
            return c;
        }

        // Em planta: o contorno um pouco acima do terreno e o modelo no meio.
        var planta = Contorno(topo + 0.02);
        planta.ColorIndex = 30;
        marcar(planta);

        var (mx, my) = (fim.X + dx * comprimento / 2, fim.Y + dy * comprimento / 2);
        var rumo = LayoutDrawer.RumoLegivel(dx, dy);
        var rotulo = new DBText
        {
            Layer = camada,
            ColorIndex = 30,
            Height = Math.Max(0.25, 0.25 * l2),
            TextString = modelo ?? Tr.T("Dissipador"),
            Rotation = rumo,
            Position = new Point3d(mx, my, topo + 0.05),
            HorizontalMode = TextHorizontalMode.TextCenter,
            VerticalMode = TextVerticalMode.TextVerticalMid,
            AlignmentPoint = new Point3d(mx, my, topo + 0.05),
        };
        marcar(rotulo);
        rotulo.AdjustAlignment(rotulo.Database ?? HostApplicationServices.WorkingDatabase);

        // Em 3D: o trapézio com 32 cm de altura, o topo no terreno.
        using var baseDoSolido = Contorno(topo - 0.32);
        var regioes = Region.CreateFromCurves([baseDoSolido]);
        try
        {
            if (regioes.Count == 0) return;

            var solido = new Solid3d { Layer = camada };
            try
            {
                solido.Extrude((Region)regioes[0], 0.32, 0);
            }
            catch
            {
                solido.Dispose();
                throw;
            }

            marcar(solido);
        }
        finally
        {
            foreach (DBObject r in regioes) r.Dispose();
        }
    }

    /// <summary>Uma caixa centrada em (x, y, z), comprida ao longo do ângulo dado.</summary>
    private static Solid3d Caixa(double x, double y, double z, double comprimento, double largura, double altura, double angulo, string camada)
    {
        // Solid3d fora do banco, largado para o coletor, derruba o AutoCAD.
        var caixa = new Solid3d { Layer = camada };
        try
        {
            caixa.CreateBox(comprimento, largura, altura);
            caixa.TransformBy(Matrix3d.Displacement(new Vector3d(x, y, z)) * Matrix3d.Rotation(angulo, Vector3d.ZAxis, Point3d.Origin));
            return caixa;
        }
        catch
        {
            caixa.Dispose();
            throw;
        }
    }

    /// <summary>
    /// O sólido: um loft regrado entre cada par de anéis vizinhos, todos
    /// unidos. Null se o modelador recusar algum pedaço.
    /// </summary>
    private static Solid3d? Solido(IReadOnlyList<IReadOnlyList<Point3>> aneis)
    {
        if (aneis.Count < 2) return null;

        Solid3d? total = null;
        var opcoes = new LoftOptionsBuilder { Ruled = true }.ToLoftOptions();

        try
        {
            for (var k = 0; k < aneis.Count - 1; k++)
            {
                using var a = Anel(aneis[k]);
                using var b = Anel(aneis[k + 1]);

                // Solid3d fora do banco, deixado para o coletor, é causa
                // conhecida de queda do AutoCAD: descartado em toda saída.
                var pedaco = new Solid3d();
                try
                {
                    pedaco.CreateLoftedSolid([a, b], [], null, opcoes);

                    if (total is null)
                    {
                        total = pedaco;
                        pedaco = null;
                    }
                    else
                    {
                        total.BooleanOperation(BooleanOperationType.BoolUnite, pedaco);
                    }
                }
                finally
                {
                    pedaco?.Dispose();
                }
            }

            return total;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("O sólido da canaleta não saiu; vai a malha.", erro);
            total?.Dispose();
            return null;
        }
    }

    /// <summary>
    /// O anel como polilinha plana no plano vertical dele: o loft recusa a
    /// polilinha 3D como seção (eNonPlanarEntity), mesmo plana.
    /// </summary>
    private static Polyline Anel(IReadOnlyList<Point3> anel)
    {
        var a = new Point3d(anel[0].X, anel[0].Y, anel[0].Z);
        var outro = anel.Select(p => new Point3d(p.X, p.Y, p.Z)).First(p => Math.Abs(p.X - a.X) + Math.Abs(p.Y - a.Y) > 1e-6);

        // A normal do plano vertical que contém o anel: horizontal, na perpendicular da largura.
        var largura = new Vector3d(outro.X - a.X, outro.Y - a.Y, 0).GetNormal();
        var normal = largura.CrossProduct(Vector3d.ZAxis).GetNormal();

        var paraOPlano = Matrix3d.WorldToPlane(normal);
        var polilinha = new Polyline { Normal = normal, Closed = true };

        for (var i = 0; i < anel.Count; i++)
        {
            var q = new Point3d(anel[i].X, anel[i].Y, anel[i].Z).TransformBy(paraOPlano);
            if (i == 0) polilinha.Elevation = q.Z;
            polilinha.AddVertexAt(i, new Point2d(q.X, q.Y), 0, 0, 0);
        }

        return polilinha;
    }

    private static PolyFaceMesh Malha(BlockTableRecord espaco, Transaction transacao, IReadOnlyList<IReadOnlyList<Point3>> aneis, string camada)
    {
        var malha = new PolyFaceMesh { Layer = camada };
        espaco.AppendEntity(malha);
        transacao.AddNewlyCreatedDBObject(malha, true);

        var triangulos = ChannelSolid.Triangles(aneis);

        // Todos os vértices antes de qualquer face: o AutoCAD recusa vértice
        // depois de face (eVertexAfterFace).
        var indice = new Dictionary<Point3, short>();
        foreach (var p in triangulos.SelectMany(t => new[] { t.A, t.B, t.C }))
        {
            if (indice.ContainsKey(p)) continue;

            var v = new PolyFaceMeshVertex(new Point3d(p.X, p.Y, p.Z));
            malha.AppendVertex(v);
            transacao.AddNewlyCreatedDBObject(v, true);
            indice[p] = (short)(indice.Count + 1);
        }

        foreach (var (a, b, c) in triangulos)
        {
            var face = new FaceRecord(indice[a], indice[b], indice[c], 0);
            malha.AppendFaceRecord(face);
            transacao.AddNewlyCreatedDBObject(face, true);
        }

        return malha;
    }
}
