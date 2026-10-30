using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// O tipo de mesa que vai num objeto do cliente (40.2): o índice na lista de
/// mesas em uso e se o objeto é a área dos módulos (sem as sobras) ou a mesa
/// inteira.
/// </summary>
public sealed record FieldMatch(FieldFootprint Footprint, int Kind, bool ModulesOnly);

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
/// automática: o objeto dá o lugar em planta (o centro, o eixo da fileira e o
/// lado da ponta baixa), nunca a cota (regra 5).
///
/// O motor ancora a mesa no canto da ponta baixa e encurta a planta pelo giro
/// que ele escolhe para acompanhar o terreno; o centro, então, anda. Por isso
/// são duas passadas: a primeira com a rampa do próprio objeto, a segunda com
/// a célula corrigida pelo que a primeira mediu. O que ainda sobra de
/// diferença sai como divergência.
/// </summary>
public static class FieldPlacement
{
    /// <summary>Acima disto a mesa desenhada conta como fora do objeto: 5 cm.</summary>
    public const double DeviationTolerance = 0.05;

    /// <summary>Quanto o azimute do projeto pode fugir da ponta baixa dos objetos: 45°.</summary>
    public const double FacingTolerance = Math.PI / 4;

    /// <summary>
    /// O tipo de cada pegada: o primeiro da lista de mesas em uso cujo
    /// tamanho (a área dos módulos ou a mesa inteira) e inclinação batem.
    /// As que não batem com nenhuma saem em <paramref name="issues"/>.
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

            for (var k = 0; k < kinds.Count && achada is null; k++)
            {
                var layout = kinds[k].Layout;
                var modulos = layout.Length - layout.LeftMargin - layout.RightMargin;

                if (Math.Abs(f.Width - layout.Depth) > FieldObjects.SizeTolerance) continue;
                if (f.Tilt is { } t && Math.Abs(t - kinds[k].TiltRadians) > FieldObjects.AngleTolerance) continue;

                if (Math.Abs(f.Length - modulos) <= FieldObjects.SizeTolerance)
                    achada = new FieldMatch(f, k, ModulesOnly: true);
                else if (Math.Abs(f.Length - layout.Length) <= FieldObjects.SizeTolerance)
                    achada = new FieldMatch(f, k, ModulesOnly: false);
            }

            if (achada is not null)
            {
                achadas.Add(achada);
                continue;
            }

            problemas.Add(new FieldIssue(f.Id, FieldIssueKind.NoProfile, Tr.F(
                "{0} ({1}) tem {2:0.###} × {3:0.###} m{4}: nenhuma mesa em uso tem esse tamanho, e ele não vira mesa.",
                f.Id, f.Layer, f.Length, f.Width, f.Tilt is { } i ? Tr.F(" a {0:0.#}°", i * 180 / Math.PI) : string.Empty)));
        }

        issues = problemas;
        return achadas;
    }

    /// <summary>
    /// A ponta baixa dos objetos contra o azimute do projeto: o motor vira a
    /// mesa para o lado da configuração, e um projeto olhando para o norte com
    /// objetos olhando para o sul daria as mesas de costas. Null quando batem,
    /// ou quando os objetos são planos (sem lado).
    /// </summary>
    public static FieldIssue? FacingIssue(FieldSurvey survey, SystemConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(configuration);

        if (survey.LowSideDirection is not { } baixo) return null;

        // O azimute (norte = 0, horário) para onde as mesas olham, no ângulo
        // de matemática (leste = 0, anti-horário).
        var olha = Math.PI / 2 - configuration.FacingAzimuthRadians;
        var diferenca = Math.Abs(Math.Atan2(Math.Sin(olha - baixo), Math.Cos(olha - baixo)));

        if (diferenca <= FacingTolerance) return null;

        return new FieldIssue("*", FieldIssueKind.ProjectFacing, Tr.F(
            "os objetos têm a ponta baixa para {0}, e o projeto olha para {1}: o Clivus viraria as mesas. Ajuste o azimute em Configurações › Parâmetros (ou pela linha de apoio) e gere de novo.",
            Rumo(baixo), Rumo(olha)));
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
    /// motor, corrige o centro e roda de novo.
    /// </summary>
    /// <param name="kinds">As mesas em uso, na ordem de prioridade (todas com a mesma inclinação).</param>
    public static FieldPlant Process(
        FieldSurvey survey,
        IReadOnlyList<TableProfile> kinds,
        Tin terrain,
        ProjectSettings settings,
        Action<int, int>? progress = null)
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
        var fileiras = FieldObjects.Rows([.. casadas.Select(c => c.Footprint)], survey.ReferenceAxis, config.MaxGapBeforeBreak);

        var geometrias = kinds.Select(k => TableGeometry.Local(k.Layout, k.Frame.Pillars(k.Layout), k.Frame)).ToList();
        var inclinacao = kinds[0].TiltRadians;

        // O lugar, em coordenadas locais, que tem que cair no centro do objeto.
        Point3 Alvo(FieldMatch c)
        {
            var g = geometrias[c.Kind];
            var layout = kinds[c.Kind].Layout;
            var x = c.ModulesOnly ? layout.LeftMargin + (layout.Length - layout.LeftMargin - layout.RightMargin) / 2 : g.Length / 2;
            return new Point3(x, g.Depth / 2, 0);
        }

        // Primeira passada: a célula pela rampa do próprio objeto.
        var celulas = new Dictionary<string, PlacedTable>();
        for (var r = 0; r < fileiras.Count; r++)
        {
            for (var n = 0; n < fileiras[r].Tables.Count; n++)
            {
                var c = porId[fileiras[r].Tables[n].Id];
                var cosGiro = c.Footprint.Length > 0 ? Math.Clamp(c.Footprint.PlanLength / c.Footprint.Length, 0.5, 1) : 1;
                celulas[c.Footprint.Id] = Celula(c, r + 1, n + 1, geometrias[c.Kind], inclinacao, Alvo(c).X * cosGiro, config.UpslopeAzimuthRadians);
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
                    erros[c.Footprint.Id] = (c.Footprint.Center.X - caiu.X, c.Footprint.Center.Y - caiu.Y);
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
                    "{0} ({1}): a mesa {2} ficou a {3:0.00} m do objeto; o motor a sentou no terreno com outra rampa (o objeto tem {4:0.#} %).",
                    id, f.Layer, celula.Label, desvio, Math.Abs(f.RowSlope ?? 0) * 100)));
            }
        }

        return new FieldPlant(usina, geometrias, problemas, rotulos, desvios);
    }

    /// <summary>
    /// A célula da mesa no objeto: o comprimento é o da mesa do Clivus (o
    /// motor exige), o eixo é o do objeto, e o canto de onde o motor parte
    /// fica onde o alvo cai no centro com o giro estimado.
    /// </summary>
    /// <param name="alvoAoLongo">Onde, ao longo do eixo em planta, o alvo fica a partir do canto de partida.</param>
    private static PlacedTable Celula(FieldMatch c, int fileira, int numero, TableGeometry g, double inclinacao, double alvoAoLongo, double subidaAzimute)
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

        // O motor parte do canto c0 quando o X local corre com o eixo, e de
        // c1 quando corre contra (TablePlacement.Montar).
        var xComEixo = (sy * dx - sx * dy) > 0;
        var (bx, by) = (f.Center.X - nx * fundo / 2, f.Center.Y - ny * fundo / 2);

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
