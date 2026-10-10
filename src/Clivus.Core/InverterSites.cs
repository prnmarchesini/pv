using Clivus.Geo;

namespace Clivus.Core;

/// <summary>Como o inversor é posto em campo (Renan, 10/10/2026).</summary>
public enum InverterPlacementMode
{
    /// <summary>Dentro de uma área (o retângulo que o usuário desenhou: sala, skid...).</summary>
    Area,

    /// <summary>Pelo menor cabo CC das strings dele, ao lado da vala, no Gerar da rota CC.</summary>
    Automatic,
}

/// <summary>
/// O local de um inversor: numa área (<see cref="Site"/> é o GUID da marca da
/// polilinha) ou automático pelas strings (sem área). Sem registro, o
/// inversor é posto à mão (Pôr em campo), como sempre foi.
/// </summary>
public sealed record InverterPlacement(Guid Inverter, InverterPlacementMode Mode, Guid Site)
{
    public const int FieldCount = 3;

    public IReadOnlyList<string> ToFields() => [Inverter.ToString("D"), Mode.ToString(), Site.ToString("D")];

    public static InverterPlacement? Parse(IReadOnlyList<string> c)
    {
        if (c.Count < FieldCount || !Guid.TryParse(c[0], out var inversor) || inversor == Guid.Empty) return null;
        if (!Enum.TryParse<InverterPlacementMode>(c[1], out var modo) || !Enum.IsDefined(modo) || !Guid.TryParse(c[2], out var area)) return null;
        if (modo == InverterPlacementMode.Area && area == Guid.Empty) return null;
        return new InverterPlacement(inversor, modo, modo == InverterPlacementMode.Area ? area : Guid.Empty);
    }

    /// <summary>Troca (ou tira, com <paramref name="novo"/> null) o local dos inversores <paramref name="ids"/> na lista.</summary>
    public static List<InverterPlacement> With(IEnumerable<InverterPlacement> lista, IReadOnlyCollection<Guid> ids, InverterPlacementMode? novo, Guid area = default)
    {
        var saida = lista.Where(p => !ids.Contains(p.Inverter)).ToList();
        if (novo is { } m) saida.AddRange(ids.Select(i => new InverterPlacement(i, m, m == InverterPlacementMode.Area ? area : Guid.Empty)));
        return saida;
    }
}

/// <summary>A marca de área de inversores na polilinha do usuário: o GUID e o nome ("Área 1").</summary>
public sealed record SiteMark(Guid Id, string Name)
{
    public const string Tipo = "AreaInversores";
    public const int FieldCount = 2;

    public IReadOnlyList<string> ToFields() => [Id.ToString("D"), Name];

    public static SiteMark? Parse(IReadOnlyList<string> c) =>
        c.Count >= FieldCount && Guid.TryParse(c[0], out var id) && id != Guid.Empty ? new SiteMark(id, c[1]) : null;
}

/// <summary>
/// Onde pôr os inversores (Renan, 10/10/2026): dentro de uma área, um ao
/// lado do outro; ou ao lado da vala, no ponto de menor cabo CC das strings
/// dele ("o sistema calcula o menor trajeto de cabos CC para aquele
/// inversor, conforme a posição das strings, e aloca o inversor ao lado da
/// vala; eu quero ter a liberdade de mover o inversor"). Só planta: a cota é
/// do terreno, de quem desenha.
/// </summary>
public static class InverterSites
{
    /// <summary>A folga entre dois inversores na área e entre o inversor e a vala (m).</summary>
    public const double Gap = 0.5;

    /// <summary>
    /// Os centros dos retângulos (largura em X, comprimento em Y, como o
    /// bloco do equipamento) dentro do polígono, em fileiras ao longo do lado
    /// comprido do menor retângulo que o contém, com <paramref name="gap"/>
    /// entre eles e 1 cm da borda (encostar na parede da sala vale). A caixa
    /// tem que caber inteira (nenhum lado do polígono a corta: dente de área
    /// côncava) e estar livre (<paramref name="livre"/>: fora dos inversores
    /// que já estão na área, das mesas e dos outros equipamentos). Null para o
    /// que não coube.
    /// </summary>
    public static IReadOnlyList<Point3?> InArea(IReadOnlyList<Point3> poligono, IReadOnlyList<(double Width, double Length)> tamanhos, double gap = Gap,
        Func<IReadOnlyList<(double X, double Y)>, bool>? livre = null)
    {
        ArgumentNullException.ThrowIfNull(poligono);
        ArgumentNullException.ThrowIfNull(tamanhos);
        var saida = new Point3?[tamanhos.Count];
        if (poligono.Count < 3 || tamanhos.Count == 0) return saida;

        var (centro, u, v, meioU, meioV) = MenorRetangulo(poligono);

        // Quanto cada retângulo (alinhado a X e Y) ocupa ao longo de u e de v.
        double Ao(Point3 eixo, double w, double l) => Math.Abs(w * eixo.X) + Math.Abs(l * eixo.Y);

        const double borda = 0.01;
        var linhaV = meioV - borda;  // onde começa a fileira de agora, descendo em v
        var posU = -meioU + borda;   // onde começa o próximo, ao longo de u
        var alturaDaFileira = 0.0;

        for (var i = 0; i < tamanhos.Count; i++)
        {
            var (w, l) = tamanhos[i];
            var eu = Ao(u, w, l);
            var ev = Ao(v, w, l);

            // Vaga por vaga: num polígono que não é retângulo, a vaga do menor
            // retângulo pode cair fora dele; tenta a seguinte até acabarem as fileiras.
            while (true)
            {
                if (posU + eu > meioU - borda + 1e-9)
                {
                    // Não cabe nesta fileira: a próxima.
                    linhaV -= alturaDaFileira + gap;
                    posU = -meioU + borda;
                    alturaDaFileira = 0;
                }

                if (linhaV - ev < -meioV + borda - 1e-9 || posU + eu > meioU - borda + 1e-9) break;

                var su = posU + eu / 2;
                var sv = linhaV - ev / 2;
                var p = new Point3(centro.X + u.X * su + v.X * sv, centro.Y + u.Y * su + v.Y * sv, 0);
                var cantos = EquipmentFootprint.Corners(p.X, p.Y, w, l);

                posU += eu + gap;
                alturaDaFileira = Math.Max(alturaDaFileira, ev);

                if (Dentro(poligono, cantos) && (livre is null || livre(cantos)))
                {
                    saida[i] = p;
                    break;
                }
            }
        }

        return saida;
    }

    /// <summary>Se o retângulo está inteiro dentro do polígono: os cantos dentro e nenhum lado do polígono cortando os dele.</summary>
    public static bool Dentro(IReadOnlyList<Point3> poligono, IReadOnlyList<(double X, double Y)> cantos)
    {
        if (!cantos.All(c => Polygons.Contains(poligono, c.X, c.Y))) return false;
        var caixa = cantos.Select(c => new Point3(c.X, c.Y, 0)).ToList();
        for (var i = 0; i < poligono.Count; i++)
            for (var j = 0; j < caixa.Count; j++)
                if (Polygons.SegmentsCross(poligono[i], poligono[(i + 1) % poligono.Count], caixa[j], caixa[(j + 1) % caixa.Count])) return false;
        return true;
    }

    /// <summary>Se o retângulo (cantos) e o polígono se sobrepõem em planta: um vértice (ou o centro) dentro do outro, ou lados que se cruzam.</summary>
    public static bool Overlaps(IReadOnlyList<(double X, double Y)> cantos, IReadOnlyList<Point3> poligono)
    {
        if (poligono.Count < 3) return false;
        var caixa = cantos.Select(c => new Point3(c.X, c.Y, 0)).ToList();
        if (caixa.Any(c => Polygons.Contains(poligono, c.X, c.Y)) || poligono.Any(q => Polygons.Contains(caixa, q.X, q.Y))) return true;
        if (Polygons.Contains(poligono, caixa.Average(c => c.X), caixa.Average(c => c.Y))) return true;
        for (var i = 0; i < poligono.Count; i++)
            for (var j = 0; j < caixa.Count; j++)
                if (Polygons.SegmentsCross(poligono[i], poligono[(i + 1) % poligono.Count], caixa[j], caixa[(j + 1) % caixa.Count])) return true;
        return false;
    }

    /// <summary>O menor retângulo que contém o polígono, com um lado num lado dele: centro, eixos (u o comprido) e meias medidas.</summary>
    private static (Point3 Centro, Point3 U, Point3 V, double MeioU, double MeioV) MenorRetangulo(IReadOnlyList<Point3> poligono)
    {
        (Point3, Point3, Point3, double, double)? melhor = null;
        var menorArea = double.PositiveInfinity;

        for (var i = 0; i < poligono.Count; i++)
        {
            var a = poligono[i];
            var b = poligono[(i + 1) % poligono.Count];
            var d = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            if (d < 1e-9) continue;

            var u = new Point3((b.X - a.X) / d, (b.Y - a.Y) / d, 0);
            var v = new Point3(-u.Y, u.X, 0);
            var su = poligono.Select(p => p.X * u.X + p.Y * u.Y).ToList();
            var sv = poligono.Select(p => p.X * v.X + p.Y * v.Y).ToList();
            var area = (su.Max() - su.Min()) * (sv.Max() - sv.Min());
            if (area >= menorArea) continue;

            menorArea = area;
            var cu = (su.Max() + su.Min()) / 2;
            var cv = (sv.Max() + sv.Min()) / 2;
            var centro = new Point3(u.X * cu + v.X * cv, u.Y * cu + v.Y * cv, 0);
            var (mu, mv) = ((su.Max() - su.Min()) / 2, (sv.Max() - sv.Min()) / 2);
            melhor = mu >= mv ? (centro, u, v, mu, mv) : (centro, v, new Point3(-v.X, -v.Y, 0), mv, mu);
        }

        return melhor ?? throw new ArgumentException("o polígono não tem lado", nameof(poligono));
    }

    /// <summary>
    /// Uma string para o automático: para cada lado da fileira por onde ela
    /// pode sair, as batidas na vala do + e do − e quanto o cabo anda antes
    /// delas (a saída da mesa). O router escolhe o lado de menor soma (18.2):
    /// a conta aqui é a mesma.
    /// </summary>
    public sealed record StringAccess(IReadOnlyList<(TrenchPoint Hit, double Before)[]> Sides);

    /// <summary>
    /// O ponto da rede de valas de menor cabo CC para as strings: a soma, em
    /// cada string, do lado mais curto (saída da mesa até a vala e pela vala
    /// até o ponto, do + e do −). Candidatos: os nós da rede e as batidas
    /// (entre eles a soma não tem mínimo). Se as valas das strings não se
    /// ligam todas (redes separadas), vale o ponto que alcança mais strings e,
    /// entre esses, o de menor soma; Reached diz quantas. Null se nenhuma
    /// string chega à rede.
    /// </summary>
    public static (TrenchPoint Point, Point3 Direction, double Total, int Reached)? BestTrenchPoint(TrenchNetwork valas, IReadOnlyList<StringAccess> strings)
    {
        ArgumentNullException.ThrowIfNull(valas);
        ArgumentNullException.ThrowIfNull(strings);
        var uteis = strings.Where(s => s.Sides.Count > 0).ToList();
        if (uteis.Count == 0) return null;

        // Uma árvore de caminhos por batida (Dijkstra uma vez, consultada em todos os candidatos).
        var arvores = new Dictionary<(int, double, double), TrenchNetwork.TrenchTree>();
        TrenchNetwork.TrenchTree Arvore(TrenchPoint p)
        {
            var chave = (p.Edge, Math.Round(p.At.X, 6), Math.Round(p.At.Y, 6));
            if (!arvores.TryGetValue(chave, out var a)) arvores[chave] = a = valas.Tree(p);
            return a;
        }

        var candidatos = valas.Nodes().ToList();
        foreach (var s in uteis)
            foreach (var lado in s.Sides)
                foreach (var (hit, _) in lado)
                    candidatos.Add((hit, valas.Direction(hit.Edge)));

        (TrenchPoint, Point3, double, int)? melhor = null;
        foreach (var (ponto, direcao) in candidatos)
        {
            var total = 0.0;
            var alcancadas = 0;
            foreach (var s in uteis)
            {
                var desta = s.Sides.Min(lado => lado.Sum(x => x.Before + Arvore(x.Hit).Length(ponto)));
                if (double.IsInfinity(desta)) continue;
                total += desta;
                alcancadas++;
            }

            if (alcancadas == 0) continue;
            if (melhor is null || alcancadas > melhor.Value.Item4 || (alcancadas == melhor.Value.Item4 && total < melhor.Value.Item3))
                melhor = (ponto, direcao, total, alcancadas);
        }

        return melhor;
    }

    /// <summary>
    /// O centro do retângulo do inversor ao lado da vala, no ponto
    /// <paramref name="at"/>: para cada trecho que passa ali
    /// (<paramref name="direcoes"/>; num cruzamento, mais de um), dos dois
    /// lados, afastado o bastante para a caixa não ficar em cima dele, mais
    /// <paramref name="folga"/>; o primeiro lugar livre (<paramref name="livre"/>:
    /// fora das mesas, dos equipamentos e das valas). Nenhum livre: o primeiro
    /// lugar, com Free falso, para quem chama avisar.
    /// </summary>
    public static (Point3 Center, bool Free) BesideTrench(Point3 at, IReadOnlyList<Point3> direcoes, double largura, double comprimento, Func<IReadOnlyList<(double X, double Y)>, bool> livre, double folga = Gap)
    {
        Point3? primeiro = null;
        foreach (var direcao in direcoes)
        {
            var n = new Point3(-direcao.Y, direcao.X, 0);
            var afastamento = (Math.Abs(largura * n.X) + Math.Abs(comprimento * n.Y)) / 2 + folga;

            foreach (var sinal in new[] { 1.0, -1.0 })
            {
                var c = new Point3(at.X + n.X * afastamento * sinal, at.Y + n.Y * afastamento * sinal, 0);
                primeiro ??= c;
                if (livre(EquipmentFootprint.Corners(c.X, c.Y, largura, comprimento))) return (c, true);
            }
        }

        return (primeiro ?? at, false);
    }
}
