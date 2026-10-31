using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// O tipo de mesa que vai num objeto do cliente (40.2): o índice na lista de
/// mesas em uso, se o objeto é a área dos módulos (sem as sobras) ou a mesa
/// inteira, e quanto o objeto é mais comprido que ela (negativo: mais curto).
/// </summary>
public sealed record FieldMatch(FieldFootprint Footprint, int Kind, bool ModulesOnly, double LengthDifference = 0);

/// <summary>
/// Por onde a mesa se alinha no objeto quando os dois não têm o mesmo
/// comprimento (revisão de tela do 40.2; Renan, 30/10/2026: "respeitar qual
/// eixo: Direita, centro ou esquerda [...] se eu escolher direita, ele alinha
/// na direita e estoura a esquerda, na esquerda estoura a direita, e se eu
/// escolher centro, estoura centralizado, um pouco para cada lado"). Direita
/// e esquerda como se vê na planta com o norte para cima
/// (<see cref="FieldPlacement.RightDirection"/>).
/// </summary>
public enum FieldAlignment
{
    /// <summary>O meio da mesa no meio do objeto.</summary>
    Center,

    /// <summary>A ponta da direita da mesa na ponta da direita do objeto.</summary>
    Right,

    /// <summary>A ponta da esquerda da mesa na ponta da esquerda do objeto.</summary>
    Left,
}

/// <summary>
/// As mesas conforme os objetos (40.2): a usina processada pelo motor, o que
/// cada objeto virou e as divergências do processamento.
/// </summary>
/// <param name="LabelByObject">O rótulo da mesa (F1.2) de cada objeto que virou mesa.</param>
/// <param name="DeviationByObject">Quanto a mesa desenhada ficou do objeto, em planta, por objeto.</param>
public sealed record FieldPlant(
    ProcessedPlant Plant,
    IReadOnlyList<TableGeometry> Geometries,
    IReadOnlyList<FieldIssue> Issues,
    IReadOnlyDictionary<string, string> LabelByObject,
    IReadOnlyDictionary<string, double> DeviationByObject);

/// <summary>
/// Põe uma mesa do Clivus em cada objeto do cliente (40.2, Renan 30/10/2026:
/// "ele segue eles e faz a alocação").
///
/// A mesa é a do Clivus, sentada no terreno pelo mesmo motor da geração
/// automática: o objeto dá só o lugar em planta (o centro e o eixo da
/// fileira), nunca a cota (regra 5) nem o lado da ponta baixa, que é o do
/// azimute do projeto. Quando o objeto não tem o comprimento da mesa, a
/// diferença sobra do lado que o <see cref="FieldAlignment"/> manda.
///
/// O motor ancora a mesa no canto da ponta baixa e encurta a planta pelo giro
/// que ele escolhe para acompanhar o terreno; o ponto de alinhamento, então,
/// anda. Por isso são duas passadas: a primeira sem giro, a segunda com a
/// célula corrigida pelo que a primeira mediu. O que ainda sobra de
/// diferença sai como divergência.
/// </summary>
public static class FieldPlacement
{
    /// <summary>Acima disto a mesa desenhada conta como fora do objeto: 5 cm.</summary>
    public const double DeviationTolerance = 0.05;

    /// <summary>
    /// Quanto o objeto pode diferir da mesa e ainda ser ela: 15 % do
    /// comprimento e da largura. O que sobra é do alinhamento (Renan: "Se o
    /// objeto não tiver 100% a mesma medida da mesa, algum lugar vai
    /// estourar"); mais que isso é outra mesa, ou não é mesa (cerca, lote).
    /// </summary>
    public const double MatchTolerance = 0.15;

    /// <summary>
    /// O tipo de cada pegada: a mesa em uso de tamanho mais perto (a área dos
    /// módulos ou a mesa inteira; a largura em planta ou na inclinação), até
    /// <see cref="MatchTolerance"/>; no empate, a primeira da lista. As que não
    /// chegam perto de nenhuma saem em <paramref name="issues"/>.
    /// </summary>
    public static IReadOnlyList<FieldMatch> Match(
        IEnumerable<FieldFootprint> footprints, IReadOnlyList<TableProfile> kinds, out IReadOnlyList<FieldIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(footprints);
        ArgumentNullException.ThrowIfNull(kinds);

        var achadas = new List<FieldMatch>();
        var problemas = new List<FieldIssue>();

        foreach (var f in footprints)
        {
            FieldMatch? achada = null;
            var melhor = double.PositiveInfinity;

            for (var k = 0; k < kinds.Count; k++)
            {
                var layout = kinds[k].Layout;
                var modulos = layout.Length - layout.LeftMargin - layout.RightMargin;

                // O objeto 3D visto de cima tem a largura em planta; o plano
                // pode ter sido desenhado com a da inclinação (revisão do 40.2).
                var emPlanta = layout.Depth * Math.Cos(kinds[k].TiltRadians);
                var largura = Math.Min(Math.Abs(f.Width - emPlanta), Math.Abs(f.Width - layout.Depth));
                if (largura > MatchTolerance * emPlanta) continue;

                var soModulos = Math.Abs(f.Length - modulos) <= Math.Abs(f.Length - layout.Length);
                var daMesa = soModulos ? modulos : layout.Length;
                var comprimento = Math.Abs(f.Length - daMesa);
                if (comprimento > MatchTolerance * daMesa) continue;

                if (comprimento + largura < melhor - 1e-9)
                {
                    melhor = comprimento + largura;
                    achada = new FieldMatch(f, k, soModulos, f.Length - daMesa);
                }
            }

            if (achada is not null)
            {
                achadas.Add(achada);
                continue;
            }

            problemas.Add(new FieldIssue(f.Id, FieldIssueKind.NoProfile, Tr.F(
                "{0} ({1}) tem {2:0.###} × {3:0.###} m: nenhuma mesa em uso chega perto desse tamanho, e ele não vira mesa.",
                f.Id, f.Layer, f.Length, f.Width)));
        }

        issues = problemas;
        return achadas;
    }

    /// <summary>
    /// O sentido "para a direita" de um eixo, como se vê na planta com o norte
    /// para cima: o que anda para o leste (+X); no eixo norte-sul, o que anda
    /// para o norte.
    /// </summary>
    public static (double X, double Y) RightDirection(double axis)
    {
        var (x, y) = (Math.Cos(axis), Math.Sin(axis));
        return x > 1e-9 || (Math.Abs(x) <= 1e-9 && y > 0) ? (x, y) : (-x, -y);
    }

    /// <summary>O rumo (ângulo de matemática) em palavras: norte, sul, leste, oeste e os do meio.</summary>
    public static string Rumo(double anguloDeMatematica)
    {
        var azimute = (90 - anguloDeMatematica * 180 / Math.PI) % 360;
        if (azimute < 0) azimute += 360;

        string[] nomes = [Tr.T("o norte"), Tr.T("o nordeste"), Tr.T("o leste"), Tr.T("o sudeste"), Tr.T("o sul"), Tr.T("o sudoeste"), Tr.T("o oeste"), Tr.T("o noroeste")];
        return nomes[(int)Math.Round(azimute / 45) % 8];
    }

    /// <summary>
    /// Processa as mesas nos objetos: casa os tipos, monta as fileiras, roda o
    /// motor, corrige o ponto de alinhamento e roda de novo.
    /// </summary>
    /// <param name="kinds">As mesas em uso, na ordem de prioridade (todas com a mesma inclinação).</param>
    /// <param name="alignment">Por onde a mesa se alinha no objeto quando os comprimentos diferem.</param>
    public static FieldPlant Process(
        FieldSurvey survey,
        IReadOnlyList<TableProfile> kinds,
        Tin terrain,
        ProjectSettings settings,
        Action<int, int>? progress = null,
        FieldAlignment alignment = FieldAlignment.Center)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(kinds);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);

        if (kinds.Count == 0) throw new ArgumentException("Sem mesa em uso.", nameof(kinds));

        var config = settings.Configuration;
        var problemas = new List<FieldIssue>();

        var aceitas = survey.Rows.SelectMany(r => r.Tables).ToList();
        var casadas = Match(aceitas, kinds, out var semMesa);
        problemas.AddRange(semMesa);

        var porId = casadas.ToDictionary(c => c.Footprint.Id);
        var fileiras = FieldObjects.RowsByAxis([.. casadas.Select(c => c.Footprint)], config.MaxGapBeforeBreak);

        var geometrias = kinds.Select(k => TableGeometry.Local(k.Layout, k.Frame.Pillars(k.Layout), k.Frame)).ToList();
        var inclinacao = kinds[0].TiltRadians;
        var (sx, sy) = (Math.Sin(config.UpslopeAzimuthRadians), Math.Cos(config.UpslopeAzimuthRadians));

        // O motor parte do canto c0 quando o X local corre com o eixo, e de
        // c1 quando corre contra (TablePlacement.Montar).
        bool XComEixo(FieldFootprint f) => (sy * f.AxisVector.X - sx * f.AxisVector.Y) > 0;

        // A direita é uma só para a usina, a do eixo da maioria: com ela por
        // pegada, numa fileira norte-sul o ruído de desenho (89,99° e 90,01°)
        // trocava a ponta de uma mesa para a outra (revisão do 38.9). Cada
        // pegada anda no sentido do seu eixo mais perto dela.
        var direita = RightDirection(survey.ReferenceAxis);
        (double X, double Y) DireitaDe(FieldFootprint f)
        {
            var (ax, ay) = f.AxisVector;
            return ax * direita.X + ay * direita.Y >= 0 ? (ax, ay) : (-ax, -ay);
        }

        // O lugar, em coordenadas locais, que tem que cair no ponto de
        // alinhamento do objeto: o meio, ou a ponta da direita ou da esquerda
        // (da área dos módulos, quando o objeto é ela).
        Point3 Alvo(FieldMatch c)
        {
            var g = geometrias[c.Kind];
            var layout = kinds[c.Kind].Layout;
            var (inicio, fim) = c.ModulesOnly ? (layout.LeftMargin, g.Length - layout.RightMargin) : (0.0, g.Length);

            var (rx, ry) = DireitaDe(c.Footprint);
            var (ax, ay) = c.Footprint.AxisVector;
            var xLocalParaADireita = XComEixo(c.Footprint) == (rx * ax + ry * ay > 0);

            var x = alignment switch
            {
                FieldAlignment.Right => xLocalParaADireita ? fim : inicio,
                FieldAlignment.Left => xLocalParaADireita ? inicio : fim,
                _ => (inicio + fim) / 2,
            };

            return new Point3(x, g.Depth / 2, 0);
        }

        // O ponto de alinhamento do objeto, em planta.
        Point3 NoObjeto(FieldFootprint f)
        {
            var (rx, ry) = DireitaDe(f);
            var lado = alignment switch { FieldAlignment.Right => 1, FieldAlignment.Left => -1, _ => 0 };
            return new Point3(f.Center.X + lado * rx * f.Length / 2, f.Center.Y + lado * ry * f.Length / 2, 0);
        }

        // Primeira passada: a célula sem giro.
        var celulas = new Dictionary<string, PlacedTable>();
        for (var r = 0; r < fileiras.Count; r++)
        {
            for (var n = 0; n < fileiras[r].Tables.Count; n++)
            {
                var c = porId[fileiras[r].Tables[n].Id];
                celulas[c.Footprint.Id] = Celula(c, r + 1, n + 1, geometrias[c.Kind], inclinacao, Alvo(c).X, NoObjeto(c.Footprint), XComEixo(c.Footprint), config.UpslopeAzimuthRadians);
            }
        }

        var modulos = kinds.Select(k => k.Layout.ModuleCount).ToList();
        var potencias = kinds.Select(k => k.Layout.Module.PowerWatts).ToList();

        ProcessedPlant Rodar(Action<int, int>? avanco) => PlantPipeline.ProcessAll(
            PlanLayout.FromRows([.. fileiras.Select((f, r) => new PlanRow(r + 1, [.. f.Tables.Select(t => celulas[t.Id])]))]),
            geometrias, inclinacao, modulos, potencias, terrain, settings, avanco);

        // O que a passada mediu: onde o alvo de cada mesa caiu, em planta.
        Dictionary<string, (double X, double Y)> Erros(ProcessedPlant usina)
        {
            var erros = new Dictionary<string, (double, double)>();

            for (var r = 0; r < fileiras.Count; r++)
            {
                for (var n = 0; n < fileiras[r].Tables.Count; n++)
                {
                    var c = porId[fileiras[r].Tables[n].Id];
                    var caiu = usina.Rows[r].Tables[n].Pillars.Placement.Apply(Alvo(c));
                    var alvo = NoObjeto(c.Footprint);
                    erros[c.Footprint.Id] = (alvo.X - caiu.X, alvo.Y - caiu.Y);
                }
            }

            return erros;
        }

        var primeira = Rodar(null);

        // Segunda passada: a célula andada do erro medido.
        foreach (var (id, (ex, ey)) in Erros(primeira))
            celulas[id] = Andar(celulas[id], ex, ey);

        var usina = Rodar(progress);

        var rotulos = new Dictionary<string, string>();
        var desvios = new Dictionary<string, double>();

        foreach (var (id, (ex, ey)) in Erros(usina))
        {
            var celula = celulas[id];
            var desvio = Math.Sqrt(ex * ex + ey * ey);
            rotulos[id] = celula.Label;
            desvios[id] = desvio;

            if (desvio > DeviationTolerance)
            {
                var f = porId[id].Footprint;
                problemas.Add(new FieldIssue(id, FieldIssueKind.Deviation, Tr.F(
                    "{0} ({1}): a mesa {2} ficou a {3:0.00} m do lugar no objeto; o motor a sentou no terreno com a rampa dele.",
                    id, f.Layer, celula.Label, desvio)));
            }
        }

        problemas.AddRange(Sobrepostas(usina, fileiras, porId, geometrias));

        return new FieldPlant(usina, geometrias, problemas, rotulos, desvios);
    }

    /// <summary>Acima disto, duas mesas vizinhas contam como uma em cima da outra: 1 cm.</summary>
    public const double OverlapTolerance = 0.01;

    /// <summary>
    /// As mesas que ficaram uma em cima da outra na fileira (revisão do 38.9):
    /// com o objeto mais curto que a mesa, a diferença sobra para o lado do
    /// alinhamento, e se o vão entre os objetos for menor que ela, a mesa
    /// entra na vizinha. Em planta, pelo comprimento inteiro de cada mesa ao
    /// longo do eixo da fileira.
    /// </summary>
    private static IEnumerable<FieldIssue> Sobrepostas(
        ProcessedPlant usina, IReadOnlyList<FieldRow> fileiras, IReadOnlyDictionary<string, FieldMatch> porId, IReadOnlyList<TableGeometry> geometrias)
    {
        for (var r = 0; r < fileiras.Count && r < usina.Rows.Count; r++)
        {
            var mesas = usina.Rows[r].Tables;
            if (mesas.Count < 2) continue;

            var (ux, uy) = porId[fileiras[r].Tables[0].Id].Footprint.AxisVector;

            // O trecho que cada mesa ocupa ao longo do eixo, com o objeto dela.
            var trechos = new List<(double De, double Ate, string Id, string Rotulo)>();
            for (var n = 0; n < mesas.Count && n < fileiras[r].Tables.Count; n++)
            {
                var g = geometrias[porId[fileiras[r].Tables[n].Id].Kind];
                var ao = new[] { new Point3(0, 0, 0), new Point3(g.Length, 0, 0), new Point3(g.Length, g.Depth, 0), new Point3(0, g.Depth, 0) }
                    .Select(mesas[n].Pillars.Placement.Apply)
                    .Select(p => p.X * ux + p.Y * uy)
                    .ToList();
                trechos.Add((ao.Min(), ao.Max(), fileiras[r].Tables[n].Id, mesas[n].Cell.Label));
            }

            trechos.Sort((a, b) => a.De.CompareTo(b.De));

            for (var n = 1; n < trechos.Count; n++)
            {
                var emCima = trechos[n - 1].Ate - trechos[n].De;
                if (emCima <= OverlapTolerance) continue;

                var f = porId[trechos[n].Id].Footprint;
                yield return new FieldIssue(trechos[n].Id, FieldIssueKind.TableOverlap, Tr.F(
                    "{0} ({1}): a mesa {2} entra {3:0.000} m na mesa {4}: os objetos são mais curtos que a mesa e o vão entre eles não cabe a diferença. Troque o alinhamento ou confira os objetos.",
                    trechos[n].Id, f.Layer, trechos[n].Rotulo, emCima, trechos[n - 1].Rotulo));
            }
        }
    }

    /// <summary>
    /// A célula da mesa no objeto: o comprimento é o da mesa do Clivus (o
    /// motor exige), o eixo é o do objeto, e o canto de onde o motor parte
    /// fica onde o alvo cai no ponto de alinhamento do objeto.
    /// </summary>
    /// <param name="alvoAoLongo">Onde, ao longo do eixo em planta, o alvo fica a partir do canto de partida.</param>
    /// <param name="noObjeto">O ponto de alinhamento do objeto, em planta (o meio dele na largura).</param>
    /// <param name="xComEixo">Se o X local da mesa corre no sentido do eixo do objeto.</param>
    private static PlacedTable Celula(
        FieldMatch c, int fileira, int numero, TableGeometry g, double inclinacao, double alvoAoLongo, Point3 noObjeto, bool xComEixo, double subidaAzimute)
    {
        var f = c.Footprint;
        var (dx, dy) = f.AxisVector;
        var (sx, sy) = (Math.Sin(subidaAzimute), Math.Cos(subidaAzimute));

        // A normal da célula do lado da subida da configuração: é a que o
        // motor escolhe (RowOrientation.Resolve).
        var (nx, ny) = (-dy, dx);
        if (nx * sx + ny * sy < 0) (nx, ny) = (-nx, -ny);

        var comprimento = g.Length;
        var fundo = g.Depth * Math.Cos(inclinacao);

        var (bx, by) = (noObjeto.X - nx * fundo / 2, noObjeto.Y - ny * fundo / 2);

        var (c0x, c0y) = xComEixo
            ? (bx - dx * alvoAoLongo, by - dy * alvoAoLongo)
            : (bx + dx * alvoAoLongo - dx * comprimento, by + dy * alvoAoLongo - dy * comprimento);

        Point3[] cantos =
        [
            new(c0x, c0y, 0),
            new(c0x + dx * comprimento, c0y + dy * comprimento, 0),
            new(c0x + dx * comprimento + nx * fundo, c0y + dy * comprimento + ny * fundo, 0),
            new(c0x + nx * fundo, c0y + ny * fundo, 0),
        ];

        return new PlacedTable(fileira, numero, cantos[0], f.Axis, comprimento, fundo, cantos, PartlyOutside: false, Kind: c.Kind);
    }

    /// <summary>A mesma célula andada em planta.</summary>
    private static PlacedTable Andar(PlacedTable celula, double ex, double ey)
    {
        var cantos = celula.Corners.Select(p => new Point3(p.X + ex, p.Y + ey, p.Z)).ToList();
        return celula with { Origin = cantos[0], Corners = cantos };
    }
}
