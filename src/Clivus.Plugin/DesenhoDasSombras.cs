using System.Globalization;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// O desenho das sombras (42.1, Renan, 31/10/2026: "QUERO VER o desenho das
/// sombras no chao, rachurada ... a represetnação da sombra até onde ela pega
/// no modulo ... O PVcase pinta de magenta as sombras, fica facil saber que é
/// sombra").
/// <para>
/// Hachura SOLID magenta, 50 % transparente, e a borda magenta, na camada das
/// sombras (que fica magenta). No chão, a mancha vai em pedaços de até 8 m,
/// cada um num plano ajustado ao terreno debaixo dele, 4 cm acima
/// (<see cref="GroundPatches"/>, regra sagrada 5); a borda segue o terreno a
/// 4 cm. Sobre a mesa, a parte do plano dos módulos na sombra, 3 cm acima
/// dele. A hachura aparece cheia no 2D Wireframe, o que a borda sozinha não
/// fazia.
/// </para>
/// <para>
/// Cada entidade leva no XData (tipo "Sombra", versão 1, os dois primeiros
/// campos como antes): o primeiro instante, onde ("Chao", "Mesa", "Texto"),
/// a área (GUID, ou <see cref="SemArea"/>), o período e a chave: o ladrilho
/// de 8 m ("x;y") no chão e na etiqueta, o GUID da mesa sobre a mesa. É por
/// ela que um cálculo ou o Apagar num escopo levam só as do escopo
/// (<see cref="ShadowScopePlan.Erases"/>); as de antes de 31/10/2026, sem
/// esses campos, são julgadas pela posição.
/// </para>
/// <para>
/// Ordem de desenho: a sombra sobre as mesas vai para a frente de tudo; a do
/// chão fica logo abaixo da primeira peça de mesa (em planta, o módulo
/// por cima dela, e o chão debaixo da mesa nem é desenhado).
/// </para>
/// </summary>
internal static class DesenhoDasSombras
{
    /// <summary>O tipo do XData (o mesmo de antes: o Apagar da Edição e o 3D leem por ele).</summary>
    internal const string Tipo = Cleanup.ShadowType;

    private const int Versao = 1;

    /// <summary>A área de quem está fora de todas.</summary>
    internal const string SemArea = "SEM_AREA";

    internal const string NoChao = "Chao";
    internal const string NaMesa = "Mesa";
    internal const string Texto = "Texto";

    /// <summary>O magenta do PVcase.</summary>
    internal static readonly RgbColor Magenta = new(255, 0, 255);

    /// <summary>O cinza com que a camada das sombras nascia até 30/10/2026.</summary>
    private static readonly RgbColor CinzaDeAntes = new(90, 90, 90);

    /// <summary>50 % transparente (o alfa do AutoCAD vai de 0, invisível, a 255, opaco).</summary>
    internal const byte Alfa = 127;

    /// <summary>A hachura sobre a mesa fica 3 cm acima do plano dos módulos (a caixa do módulo sobe 2 cm).</summary>
    internal const double AcimaDaMesa = 0.03;

    private static readonly RXClass ClasseDaHachura = RXObject.GetClass(typeof(Hatch));
    private static readonly RXClass ClasseDaPolilinha = RXObject.GetClass(typeof(Polyline3d));
    private static readonly RXClass ClasseDoTexto = RXObject.GetClass(typeof(MText));

    /// <summary>O chão de uma área do escopo e a mancha nele.</summary>
    internal sealed record ChaoDaArea(Guid? Area, IReadOnlyList<IReadOnlyList<(double X, double Y)>> Aneis);

    /// <summary>A sombra sobre uma mesa: a mesa, a área dela, o contorno (o plano) e a mancha.</summary>
    internal sealed record SombraNaMesa(Guid Mesa, Guid? Area, IReadOnlyList<Point3> Contorno, IReadOnlyList<IReadOnlyList<(double X, double Y)>> Aneis);

    /// <summary>O que foi desenhado: quantas de cada, as que o AutoCAD recusou e a faixa de cotas das hachuras do chão.</summary>
    internal sealed record Feito(int HachurasNoChao, int BordasNoChao, int Mesas, int Etiquetas, int Recusadas, double? CotaMinima, double? CotaMaxima)
    {
        internal static Feito Nada { get; } = new(0, 0, 0, 0, 0, null, null);
    }

    /// <summary>Uma entidade de sombra no desenho e a marca dela (pelo XData; nas antigas, pela posição).</summary>
    internal sealed record Marca(ObjectId Id, ShadowTag Tag);

    /// <summary>
    /// Garante as camadas das sombras em magenta: nascem magenta, e a que
    /// ainda tem o cinza de antes vira magenta (cor escolhida pelo usuário fica).
    /// Devolve os nomes (sombra, texto).
    /// </summary>
    internal static (string Sombra, string Texto) Camadas(Transaction transacao, Database database)
    {
        var sombra = LayoutLayers.Garantir(transacao, database, LayoutLayers.Sombra, Magenta);
        var texto = LayoutLayers.Garantir(transacao, database, LayoutLayers.SombraTexto, Magenta);

        var tabela = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);
        foreach (var nome in new[] { sombra, texto })
        {
            var camada = (LayerTableRecord)transacao.GetObject(tabela[nome], OpenMode.ForRead);
            var cor = camada.Color;
            if (cor.ColorMethod != ColorMethod.ByColor || cor.Red != CinzaDeAntes.R || cor.Green != CinzaDeAntes.G || cor.Blue != CinzaDeAntes.B) continue;

            camada.UpgradeOpen();
            camada.Color = Color.FromRgb(Magenta.R, Magenta.G, Magenta.B);
        }

        return (sombra, texto);
    }

    /// <summary>
    /// As entidades de sombra do desenho com a marca de cada uma. As de antes
    /// de 31/10/2026 (sem área no XData) são marcadas pela posição:
    /// <paramref name="legado"/> recebe onde e o centro delas.
    /// </summary>
    internal static List<Marca> Marcas(Transaction transacao, Database database, Func<ShadowPlace, Point3, ShadowTag> legado)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var achadas = new List<Marca>();

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased) continue;

            var classe = id.ObjectClass;
            if (classe != ClasseDaHachura && classe != ClasseDaPolilinha && classe != ClasseDoTexto) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            if (PluginXData.LoadAll(e, Tipo, Versao, 1) is not { } campos) continue;

            var onde = e is MText ? ShadowPlace.Label : campos.Count > 1 && campos[1] == NaMesa ? ShadowPlace.Table : ShadowPlace.Ground;

            if (campos.Count >= 5)
            {
                Guid? area = Guid.TryParse(campos[2], out var a) ? a : null;
                Guid? mesa = onde == ShadowPlace.Table && Guid.TryParse(campos[4], out var m) ? m : null;
                (int, int)? ladrilho = onde != ShadowPlace.Table && Ladrilho(campos[4]) is { } l ? l : null;
                achadas.Add(new Marca(id, new ShadowTag(onde, area, mesa, ladrilho)));
                continue;
            }

            if (Centro(transacao, e) is { } centro) achadas.Add(new Marca(id, legado(onde, centro)));
        }

        return achadas;
    }

    /// <summary>
    /// Desenha a sombra (na transação de quem chama): no chão de cada área,
    /// os pedaços com o plano de cada um, a borda e uma etiqueta; sobre cada
    /// mesa, a hachura no plano dela e a borda.
    /// </summary>
    /// <param name="transacao">A transação de escrita.</param>
    /// <param name="database">O desenho.</param>
    /// <param name="terreno">O terreno processado.</param>
    /// <param name="chao">A mancha no chão, por área do escopo.</param>
    /// <param name="mesas">A mancha sobre cada mesa do escopo.</param>
    /// <param name="rotulo">O texto da etiqueta ("Sombra 21/06 07:00").</param>
    /// <param name="marca">O primeiro instante (ISO), o campo de sempre.</param>
    /// <param name="periodo">O período descrito.</param>
    /// <param name="pecasDasMesas">Os módulos, faces e contornos das mesas: a sombra do chão fica abaixo deles na ordem de desenho.</param>
    internal static Feito Desenhar(
        Transaction transacao, Database database, Tin terreno, IReadOnlyList<ChaoDaArea> chao, IReadOnlyList<SombraNaMesa> mesas,
        string rotulo, string marca, string periodo, IReadOnlySet<ObjectId> pecasDasMesas)
    {
        if (chao.Count == 0 && mesas.Count == 0) return Feito.Nada;

        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var (camada, camadaDoTexto) = Camadas(transacao, database);
        var estilo = EstiloDoProjeto.PrepararTexto(transacao, database);

        var doChao = new List<ObjectId>();
        var naFrente = new List<ObjectId>();
        var (hachuras, bordas, etiquetas, recusadas, comMesa) = (0, 0, 0, 0, 0);
        double? cotaMinima = null, cotaMaxima = null;

        double? Terreno(double x, double y) => terreno.TryGetZ(x, y, out var z) ? z : null;

        foreach (var parte in chao)
        {
            var area = parte.Area?.ToString("D") ?? SemArea;

            foreach (var pedaco in GroundPatches.Build(parte.Aneis, terreno))
            {
                var chave = Chave(pedaco.TileX, pedaco.TileY);
                var id = Hachura(transacao, database, espaco, camada, pedaco.Loops, pedaco.HeightAt, pedaco.Normal);
                if (id is not { } h)
                {
                    recusadas++;
                    continue;
                }

                PluginXData.Save(transacao, (Entity)transacao.GetObject(h, OpenMode.ForWrite), Tipo, Versao, marca, NoChao, area, periodo, chave);
                doChao.Add(h);
                hachuras++;

                foreach (var (x, y) in pedaco.Loops.SelectMany(l => l))
                {
                    var z = pedaco.HeightAt(x, y);
                    cotaMinima = Math.Min(cotaMinima ?? z, z);
                    cotaMaxima = Math.Max(cotaMaxima ?? z, z);
                }
            }

            // A borda no terreno, 4 cm acima, cortada nos ladrilhos como a hachura.
            var passo = Passo(parte.Aneis);
            foreach (var (tx, ty, linha) in GroundPatches.Borders(parte.Aneis))
            {
                var pontos = new List<Point3d>();
                double? ultima = null;
                foreach (var (x, y) in Densa(linha, passo))
                {
                    var z = Terreno(x, y) ?? ultima;
                    if (z is null) continue;
                    ultima = z;
                    pontos.Add(new Point3d(x, y, z.Value + GroundPatches.Clearance));
                }

                if (pontos.Count < 2) continue;

                var b = Borda(transacao, espaco, camada, pontos, fechada: false);
                PluginXData.Save(transacao, (Entity)transacao.GetObject(b, OpenMode.ForWrite), Tipo, Versao, marca, NoChao, area, periodo, Chave(tx, ty));
                doChao.Add(b);
                bordas++;
            }

            // A etiqueta na ponta mais ao norte da maior mancha da área.
            var maior = parte.Aneis.MaxBy(ShadowUnion.Area);
            if (maior is not null && maior.MaxBy(p => p.Y) is var topo && Terreno(topo.X, topo.Y) is { } zTopo)
            {
                var texto = new MText
                {
                    Location = new Point3d(topo.X, topo.Y, zTopo + 0.1),
                    TextHeight = 0.8,
                    Layer = camadaDoTexto,
                    Color = Color.FromRgb(Magenta.R, Magenta.G, Magenta.B),
                    Attachment = AttachmentPoint.BottomCenter,
                    Contents = rotulo,
                };
                espaco.AppendEntity(texto);
                transacao.AddNewlyCreatedDBObject(texto, true);
                estilo(texto);

                var (lx, ly) = GroundPatches.TileOf(topo.X, topo.Y);
                PluginXData.Save(transacao, texto, Tipo, Versao, marca, Texto, area, periodo, Chave(lx, ly));
                naFrente.Add(texto.ObjectId);
                etiquetas++;
            }
        }

        foreach (var mesa in mesas)
        {
            if (Normal(mesa.Contorno) is not { } normal) continue;

            double Cota(double x, double y) => (Shading.PlaneHeight(mesa.Contorno, x, y) ?? mesa.Contorno.Average(p => p.Z)) + AcimaDaMesa;

            var area = mesa.Area?.ToString("D") ?? SemArea;
            var chave = mesa.Mesa.ToString("D");

            var id = Hachura(transacao, database, espaco, camada, mesa.Aneis, Cota, normal);
            if (id is { } h)
            {
                PluginXData.Save(transacao, (Entity)transacao.GetObject(h, OpenMode.ForWrite), Tipo, Versao, marca, NaMesa, area, periodo, chave);
                naFrente.Add(h);
                comMesa++;
            }
            else
            {
                recusadas++;
            }

            foreach (var anel in mesa.Aneis)
            {
                var pontos = anel.Select(p => new Point3d(p.X, p.Y, Cota(p.X, p.Y))).ToList();
                if (pontos.Count < 3) continue;

                var b = Borda(transacao, espaco, camada, pontos, fechada: true);
                PluginXData.Save(transacao, (Entity)transacao.GetObject(b, OpenMode.ForWrite), Tipo, Versao, marca, NaMesa, area, periodo, chave);
                naFrente.Add(b);
            }
        }

        Ordenar(transacao, espaco, doChao, naFrente, pecasDasMesas);

        return new Feito(hachuras, bordas, comMesa, etiquetas, recusadas, cotaMinima, cotaMaxima);
    }

    /// <summary>Liga ou desliga as camadas das sombras. Quantas mudaram (as que não existem não contam).</summary>
    internal static int Ligar(Transaction transacao, Database database, bool ligadas)
    {
        var mudaram = 0;

        foreach (var nome in new[] { LayoutLayers.Sombra, LayoutLayers.SombraTexto })
        {
            var estava = LayoutLayers.EstaDesligada(transacao, database, nome);
            if (estava is null || estava == !ligadas) continue;
            if (LayoutLayers.Ligar(transacao, database, nome, ligadas)) mudaram++;
        }

        return mudaram;
    }

    // ------------------------------------------------------------ miúdos

    /// <summary>
    /// Uma hachura SOLID magenta no plano dado: a normal e a cota em cada
    /// ponto (em planta) do plano. Null se o AutoCAD recusou o contorno (a
    /// hachura incompleta sai).
    /// </summary>
    private static ObjectId? Hachura(
        Transaction transacao, Database database, BlockTableRecord espaco, string camada, IReadOnlyList<IReadOnlyList<(double X, double Y)>> lacos,
        Func<double, double, double> cota, (double X, double Y, double Z) normal)
    {
        var validos = lacos.Where(l => l.Count >= 3).ToList();
        if (validos.Count == 0) return null;

        var n = new Vector3d(normal.X, normal.Y, normal.Z).GetNormal();
        var paraOcs = Matrix3d.WorldToPlane(n);

        Hatch? hachura = null;
        try
        {
            // Os padrões do banco ANTES da camada: SetDatabaseDefaults reescreve camada e cor.
            hachura = new Hatch();
            hachura.SetDatabaseDefaults(database);
            hachura.Layer = camada;
            hachura.Color = Color.FromRgb(Magenta.R, Magenta.G, Magenta.B);
            hachura.Normal = n;

            var p0 = validos[0][0];
            hachura.Elevation = new Point3d(p0.X, p0.Y, cota(p0.X, p0.Y)).TransformBy(paraOcs).Z;

            espaco.AppendEntity(hachura);
            transacao.AddNewlyCreatedDBObject(hachura, true);

            hachura.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            hachura.Associative = false;
            hachura.HatchStyle = HatchStyle.Normal;

            // Os externos (anti-horários, como a Clipper devolve) e os furos.
            foreach (var laco in validos)
            {
                var pontos = new Point2dCollection();
                var arcos = new DoubleCollection();

                foreach (var (x, y) in laco.Append(laco[0]))
                {
                    var q = new Point3d(x, y, cota(x, y)).TransformBy(paraOcs);
                    pontos.Add(new Point2d(q.X, q.Y));
                    arcos.Add(0);
                }

                var tipo = AreaAssinada(laco) >= 0 ? HatchLoopTypes.Polyline | HatchLoopTypes.External : HatchLoopTypes.Polyline;
                hachura.AppendLoop(tipo, pontos, arcos);
            }

            hachura.EvaluateHatch(true);
            hachura.Transparency = new Transparency(Alfa);
            return hachura.ObjectId;
        }
        catch (System.Exception erro)
        {
            // Contorno que o AutoCAD não hachura: sai, e os outros pedaços seguem.
            RegistroDeDiagnostico.Registrar("Não consegui hachurar um pedaço da sombra.", erro);
            if (hachura is not null)
            {
                if (hachura.ObjectId.IsNull) hachura.Dispose();
                else if (!hachura.IsErased) hachura.Erase();
            }

            return null;
        }
    }

    private static ObjectId Borda(Transaction transacao, BlockTableRecord espaco, string camada, IEnumerable<Point3d> pontos, bool fechada)
    {
        var polilinha = new Polyline3d { Closed = fechada, Layer = camada, Color = Color.FromRgb(Magenta.R, Magenta.G, Magenta.B) };
        espaco.AppendEntity(polilinha);
        transacao.AddNewlyCreatedDBObject(polilinha, true);

        foreach (var p in pontos)
        {
            var v = new PolylineVertex3d(p);
            polilinha.AppendVertex(v);
            transacao.AddNewlyCreatedDBObject(v, true);
        }

        return polilinha.ObjectId;
    }

    /// <summary>A sombra sobre as mesas na frente de tudo; a do chão logo abaixo da primeira peça de mesa na ordem de desenho.</summary>
    private static void Ordenar(Transaction transacao, BlockTableRecord espaco, List<ObjectId> doChao, List<ObjectId> naFrente, IReadOnlySet<ObjectId> pecasDasMesas)
    {
        if (doChao.Count == 0 && naFrente.Count == 0) return;

        try
        {
            var ordem = (DrawOrderTable)transacao.GetObject(espaco.DrawOrderTableId, OpenMode.ForWrite);

            if (doChao.Count > 0)
            {
                ObjectId? primeira = null;
                if (pecasDasMesas.Count > 0)
                {
                    foreach (ObjectId id in ordem.GetFullDrawOrder(0))
                    {
                        if (!pecasDasMesas.Contains(id)) continue;
                        primeira = id;
                        break;
                    }
                }

                if (primeira is { } p) ordem.MoveBelow(new ObjectIdCollection(doChao.ToArray()), p);
                else ordem.MoveToTop(new ObjectIdCollection(doChao.ToArray()));
            }

            if (naFrente.Count > 0) ordem.MoveToTop(new ObjectIdCollection(naFrente.ToArray()));
        }
        catch (System.Exception erro)
        {
            // A ordem de desenho é aparência: sem ela, a sombra está lá do mesmo jeito.
            RegistroDeDiagnostico.Registrar("Não consegui acertar a ordem de desenho das sombras.", erro);
        }
    }

    /// <summary>A normal (para cima) do plano do contorno, pelo método de Newell; null se não há plano que se leia em planta.</summary>
    private static (double X, double Y, double Z)? Normal(IReadOnlyList<Point3> contorno)
    {
        if (contorno.Count < 3) return null;

        double nx = 0, ny = 0, nz = 0;
        for (var i = 0; i < contorno.Count; i++)
        {
            var (a, b) = (contorno[i], contorno[(i + 1) % contorno.Count]);
            nx += (a.Y - b.Y) * (a.Z + b.Z);
            ny += (a.Z - b.Z) * (a.X + b.X);
            nz += (a.X - b.X) * (a.Y + b.Y);
        }

        var comprimento = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        if (comprimento < 1e-12 || Math.Abs(nz) / comprimento < 1e-6) return null;
        if (nz < 0) (nx, ny, nz) = (-nx, -ny, -nz);

        return (nx / comprimento, ny / comprimento, nz / comprimento);
    }

    /// <summary>O passo da borda: numa usina grande a borda passa de dezenas de quilômetros, e o passo cresce.</summary>
    private static double Passo(IReadOnlyList<IReadOnlyList<(double X, double Y)>> aneis)
    {
        var perimetro = aneis.Sum(a => a.Select((p, i) => Math.Sqrt(Math.Pow(a[(i + 1) % a.Count].X - p.X, 2) + Math.Pow(a[(i + 1) % a.Count].Y - p.Y, 2))).Sum());
        return perimetro > 100_000 ? 2.0 : perimetro > 20_000 ? 1.0 : 0.5;
    }

    /// <summary>A linha aberta com um ponto a cada <paramref name="passo"/> metros, no máximo (as pontas incluídas).</summary>
    private static IEnumerable<(double X, double Y)> Densa(IReadOnlyList<(double X, double Y)> linha, double passo)
    {
        for (var i = 0; i + 1 < linha.Count; i++)
        {
            var (a, b) = (linha[i], linha[i + 1]);
            var partes = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)) / passo));
            for (var k = 0; k < partes; k++) yield return (a.X + (b.X - a.X) * k / partes, a.Y + (b.Y - a.Y) * k / partes);
        }

        if (linha.Count > 0) yield return linha[^1];
    }

    private static double AreaAssinada(IReadOnlyList<(double X, double Y)> anel)
    {
        var s = 0.0;
        for (var i = 0; i < anel.Count; i++) s += anel[i].X * anel[(i + 1) % anel.Count].Y - anel[(i + 1) % anel.Count].X * anel[i].Y;
        return s / 2;
    }

    private static string Chave(int x, int y) => x.ToString(CultureInfo.InvariantCulture) + ";" + y.ToString(CultureInfo.InvariantCulture);

    private static (int X, int Y)? Ladrilho(string texto)
    {
        var partes = texto.Split(';');
        return partes.Length == 2
            && int.TryParse(partes[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            && int.TryParse(partes[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                ? (x, y)
                : null;
    }

    /// <summary>O centro (em planta, com a cota média) de uma entidade de sombra antiga: os vértices da borda, o ponto do texto, a caixa da hachura.</summary>
    private static Point3? Centro(Transaction transacao, Entity e)
    {
        switch (e)
        {
            case Polyline3d linha:
                var pontos = FileiraCommands.Vertices(linha, transacao);
                return pontos.Count == 0 ? null : new Point3(pontos.Average(p => p.X), pontos.Average(p => p.Y), pontos.Average(p => p.Z));

            case MText texto:
                return new Point3(texto.Location.X, texto.Location.Y, texto.Location.Z);

            default:
                try
                {
                    var caixa = e.GeometricExtents;
                    return new Point3((caixa.MinPoint.X + caixa.MaxPoint.X) / 2, (caixa.MinPoint.Y + caixa.MaxPoint.Y) / 2, (caixa.MinPoint.Z + caixa.MaxPoint.Z) / 2);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    return null;
                }
        }
    }
}
