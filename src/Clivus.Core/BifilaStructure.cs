using Clivus.Geo;

namespace Clivus.Core;

/// <summary>Uma peça da estrutura bifila além dos pilares.</summary>
public enum StructurePart
{
    /// <summary>A viga do pórtico (a tesoura), na inclinação.</summary>
    Beam,

    /// <summary>A mão francesa, do pilar até a viga.</summary>
    Brace,

    /// <summary>A terça, ao longo da mesa, por cima das vigas.</summary>
    Purlin,

    /// <summary>O tirante do X de travamento, entre dois pilares da mesma linha.</summary>
    Rod,
}

/// <summary>
/// Uma barra da estrutura no mundo: o eixo de uma ponta à outra, a seção e
/// para onde a altura da seção aponta (<paramref name="DepthDirection"/>,
/// unitário e perpendicular ao eixo).
/// </summary>
/// <param name="Part">Que peça é.</param>
/// <param name="Index">
/// Viga e mão francesa: o pórtico (0 na primeira estação). Terça: a linha (0
/// a mais baixa). Tirante: o vão, contado de 1 como o projetista escreve (o vão
/// 1 fica entre o pórtico 1 e o 2).
/// </param>
/// <param name="Line">A linha de pilares da mão francesa e do tirante; <see cref="PillarLine.Single"/> nas outras.</param>
/// <param name="Start">Uma ponta do eixo (o pé, na mão francesa).</param>
/// <param name="End">A outra ponta do eixo (a cabeça, na mão francesa).</param>
/// <param name="Section">A seção.</param>
/// <param name="DepthDirection">Para onde a altura da seção aponta.</param>
public sealed record StructureBar(
    StructurePart Part,
    int Index,
    PillarLine Line,
    Point3 Start,
    Point3 End,
    BarSection Section,
    Point3 DepthDirection)
{
    /// <summary>O comprimento do eixo.</summary>
    public double Length => BifilaStructure.Distancia(Start, End);
}

/// <summary>As peças de uma mesa bifila e o que não pôde ser montado, um aviso por motivo.</summary>
public sealed record BifilaStructureResult(IReadOnlyList<StructureBar> Bars, IReadOnlyList<string> Warnings)
{
    /// <summary>Nada: a monoposte, ou a mesa sem pilar calculado.</summary>
    public static readonly BifilaStructureResult Empty = new([], []);
}

/// <summary>
/// As peças da bifila em 3D (passo 37.3), a partir da mesa assentada e dos
/// pilares já calculados: vigas, mãos francesas, terças e tirantes. Os
/// pilares não entram: eles já saem do <see cref="PillarCalculator"/>.
///
/// Debaixo da face dos módulos (o plano z = 0 da mesa) vem a terça, encostada
/// no módulo, e debaixo dela a viga: o eixo da terça fica a espessura do
/// módulo + meia terça da face, o da viga mais a terça inteira e meia viga.
/// Com as seções da Arctech isso deixa o eixo da viga 5 cm abaixo do topo do
/// pilar, como no corte.
///
/// A mão francesa é peça de catálogo: o pé fica no eixo do pilar, a
/// <see cref="BifilaFrame.FrontBraceBelowTop"/> do topo, e a cabeça é onde uma
/// barra daquele comprimento encosta no eixo da viga, do lado do outro pilar.
/// Assim a lista de material sai com o comprimento que se compra. Se ela não
/// alcança a viga, ou se o pé cairia dentro do chão, a peça não é desenhada
/// torta: vira aviso.
/// </summary>
public static class BifilaStructure
{
    /// <summary>
    /// As peças da mesa.
    /// </summary>
    /// <param name="profile">O perfil da mesa (a bifila mora no <see cref="TableFrame.Bifila"/>).</param>
    /// <param name="geometry">A geometria local da mesa, a mesma que deu os pilares.</param>
    /// <param name="pillars">Os pilares calculados, com a colocação da mesa.</param>
    public static BifilaStructureResult Build(TableProfile profile, TableGeometry geometry, TablePillars pillars)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(pillars);

        return Build(profile.Layout, profile.Frame, geometry, pillars);
    }

    /// <summary>
    /// As peças da mesa pela geometria, que guarda a mesa e a estrutura de que
    /// saiu: é o caminho do desenho, que tem a geometria de cada tipo e não o
    /// perfil.
    /// </summary>
    public static BifilaStructureResult Build(TableGeometry geometry, TablePillars pillars)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(pillars);

        return geometry.Layout is { } mesa && geometry.Frame is { } estrutura
            ? Build(mesa, estrutura, geometry, pillars)
            : BifilaStructureResult.Empty;
    }

    private static BifilaStructureResult Build(TableLayout mesa, TableFrame estrutura, TableGeometry geometry, TablePillars pillars)
    {
        if (estrutura.Bifila is not { } b) return BifilaStructureResult.Empty;

        // Pilares que não são os desta geometria: não há como casar pórtico e
        // pilar. Vazio, mas dito: calado, a lista sairia com a mesa sem
        // estrutura e ninguém saberia.
        if (pillars.Pillars.Count != geometry.Pillars.Count)
            return new BifilaStructureResult([], [Tr.T("os pilares calculados não batem com a geometria da mesa: a estrutura da bifila não foi montada")]);

        var colocacao = pillars.Placement;

        Point3 Mundo(double x, double y, double z) => colocacao.Apply(new Point3(x, y, z));

        var origem = Mundo(0, 0, 0);
        var aoLongo = Unitario(Menos(Mundo(1, 0, 0), origem));
        var normal = Unitario(Menos(Mundo(0, 0, 1), origem));

        var zTerca = -(mesa.Module.Thickness + b.Purlin.Depth / 2);
        var zViga = -(mesa.Module.Thickness + b.Purlin.Depth + b.Beam.Depth / 2);

        var pecas = new List<StructureBar>();
        var avisos = new Avisos();

        // Terças: duas por fileira de módulos, uma de cada borda, de ponta a
        // ponta da estrutura.
        var fileiras = mesa.Arrangement == TableArrangement.DoubleRow ? 2 : 1;
        var linha = 0;
        for (var f = 0; f < fileiras; f++)
        {
            var y0 = f * (mesa.Module.Height + mesa.VerticalGap);

            foreach (var y in new[] { y0 + b.PurlinFromModuleEdge, y0 + mesa.Module.Height - b.PurlinFromModuleEdge })
            {
                pecas.Add(new StructureBar(
                    StructurePart.Purlin, linha++, PillarLine.Single,
                    Mundo(0, y, zTerca), Mundo(geometry.Length, y, zTerca), b.Purlin, normal));
            }
        }

        // Os pórticos, na ordem das estações (a dos pilares).
        var estacoes = geometry.Pillars.Select(p => p.Station).Distinct().ToList();
        var vigas = new List<(Point3 A, Point3 B)>(estacoes.Count);

        for (var k = 0; k < estacoes.Count; k++)
        {
            var a = Mundo(estacoes[k], geometry.RafterOffset, zViga);
            var c = Mundo(estacoes[k], geometry.RafterOffset + estrutura.RafterLength, zViga);

            vigas.Add((a, c));
            pecas.Add(new StructureBar(StructurePart.Beam, k, PillarLine.Single, a, c, b.Beam, normal));
        }

        // O pilar calculado de cada pórtico e linha. O cálculo devolve os
        // pilares na ordem da geometria, então o índice casa os dois.
        var pilar = new Dictionary<(int Portico, PillarLine Linha), PillarResult>();
        for (var i = 0; i < geometry.Pillars.Count; i++)
        {
            var peca = geometry.Pillars[i];
            pilar[(estacoes.IndexOf(peca.Station), peca.Line)] = pillars.Pillars[i];
        }

        // Pilar sem terreno embaixo (já marcado pelo cálculo): as peças dele
        // saem, mas a conferência do chão não pôde ser feita, e isso é dito.
        var semChao = false;

        // Mãos francesas.
        for (var k = 0; k < estacoes.Count; k++)
        {
            foreach (var (lado, comprimento, abaixo, fileiraY) in new[]
            {
                (PillarLine.Front, b.FrontBraceLength, b.FrontBraceBelowTop, geometry.PillarRow),
                (PillarLine.Rear, b.RearBraceLength, b.RearBraceBelowTop, geometry.RearPillarRow ?? geometry.PillarRow),
            })
            {
                if (comprimento <= 0 || !pilar.TryGetValue((k, lado), out var p)) continue;
                if (!pilar.TryGetValue((k, lado == PillarLine.Front ? PillarLine.Rear : PillarLine.Front), out var outro)) continue;

                var pe = new Point3(p.X, p.Y, p.TopZ - abaixo);
                var nome = NomeDoLado(lado);

                if (p.GroundZ is not { } chao) semChao = true;
                else if (pe.Z <= chao)
                {
                    avisos.Add(Tr.F("a mão francesa do pilar {0} pegaria dentro do chão (o pilar tem menos de {1:0.###} m acima do terreno)", nome, abaixo));
                    continue;
                }

                // O pé abaixo do fundo da viga: acima dele a peça sairia quase
                // deitada ao longo da viga, sem travar nada.
                var fundoDaViga = Mundo(estacoes[k], fileiraY, zViga).Z - b.Beam.Depth / 2;
                if (pe.Z >= fundoDaViga)
                {
                    avisos.Add(Tr.F("a mão francesa do pilar {0} pega acima do fundo da viga: desça o ponto no pilar", nome));
                    continue;
                }

                var (encosta, ponto) = OndeEncosta(pe, vigas[k].A, vigas[k].B, comprimento, paraTras: lado == PillarLine.Front);

                if (encosta == Encosto.Curta)
                {
                    avisos.Add(Tr.F("a mão francesa de {0:0.###} m do pilar {1} não alcança a viga: aumente o comprimento ou desça o ponto no pilar", comprimento, nome));
                    continue;
                }

                if (encosta == Encosto.PassaDaPonta)
                {
                    avisos.Add(Tr.F("a mão francesa de {0:0.###} m do pilar {1} passa da ponta da viga: diminua o comprimento ou suba o ponto no pilar", comprimento, nome));
                    continue;
                }

                // A cabeça do lado do outro pilar, em planta: além da face
                // deste e aquém da do outro. Fora disso a peça sai em pé,
                // colada no pilar, ou atravessa o outro.
                var paraOOutro = new Point3(outro.X - p.X, outro.Y - p.Y, 0);
                var vao = Math.Sqrt(Ponto(paraOOutro, paraOOutro));
                var avanco = vao > 0 ? Ponto(new Point3(ponto.X - p.X, ponto.Y - p.Y, 0), paraOOutro) / vao : 0;

                if (avanco <= estrutura.PillarDepth / 2)
                {
                    avisos.Add(Tr.F("a mão francesa de {0:0.###} m do pilar {1} encosta na viga colada ao pilar, quase em pé: aumente o comprimento ou desça o ponto no pilar", comprimento, nome));
                    continue;
                }

                if (avanco >= vao - estrutura.PillarDepth / 2)
                {
                    avisos.Add(Tr.F("a mão francesa de {0:0.###} m do pilar {1} passa do outro pilar: diminua o comprimento", comprimento, nome));
                    continue;
                }

                var eixo = Unitario(Menos(ponto, pe));
                pecas.Add(new StructureBar(StructurePart.Brace, k, lado, pe, ponto, b.Brace, Perpendicular(Cruz(aoLongo, eixo), eixo)));
            }
        }

        // Tirantes: o X entre os pilares das pontas de cada vão travado.
        var linhas = b.RodLine switch
        {
            BifilaRodLine.Rear => new[] { PillarLine.Rear },
            BifilaRodLine.Front => new[] { PillarLine.Front },
            BifilaRodLine.Both => new[] { PillarLine.Front, PillarLine.Rear },
            _ => [],
        };

        foreach (var lado in linhas)
        {
            foreach (var vao in b.RodSpans)
            {
                if (!pilar.TryGetValue((vao - 1, lado), out var i) || !pilar.TryGetValue((vao, lado), out var j)) continue;

                var iCima = new Point3(i.X, i.Y, i.TopZ - b.RodUpperBelowTop);
                var iBaixo = new Point3(i.X, i.Y, i.TopZ - b.RodLowerBelowTop);
                var jCima = new Point3(j.X, j.Y, j.TopZ - b.RodUpperBelowTop);
                var jBaixo = new Point3(j.X, j.Y, j.TopZ - b.RodLowerBelowTop);

                if (i.GroundZ is null || j.GroundZ is null) semChao = true;

                if ((i.GroundZ is { } ci && iBaixo.Z <= ci) || (j.GroundZ is { } cj && jBaixo.Z <= cj))
                {
                    avisos.Add(Tr.F(
                        "o tirante da linha {0}, vão {1}, pegaria dentro do chão: os pilares têm menos de {2:0.###} m acima do terreno",
                        lado == PillarLine.Front ? Tr.T("dianteira") : Tr.T("traseira"), vao, b.RodLowerBelowTop));
                    continue;
                }

                var secao = new BarSection(b.RodDiameter, b.RodDiameter);
                pecas.Add(Tirante(vao, lado, iCima, jBaixo, secao));
                pecas.Add(Tirante(vao, lado, iBaixo, jCima, secao));
            }
        }

        if (semChao) avisos.Add(Tr.T("há pilar sem terreno embaixo: as mãos francesas e os tirantes dele foram montados sem conferir o chão"));

        return new BifilaStructureResult(pecas, avisos.Lista);
    }

    /// <summary>Como a mão francesa encosta na viga.</summary>
    private enum Encosto
    {
        /// <summary>Encosta dentro da viga.</summary>
        Encosta,

        /// <summary>Curta demais: não chega no eixo da viga.</summary>
        Curta,

        /// <summary>Comprida demais: chega no eixo depois da ponta da viga.</summary>
        PassaDaPonta,
    }

    /// <summary>A distância entre dois pontos.</summary>
    internal static double Distancia(Point3 a, Point3 b)
    {
        var d = Menos(b, a);
        return Math.Sqrt(Ponto(d, d));
    }

    private static StructureBar Tirante(int vao, PillarLine lado, Point3 de, Point3 ate, BarSection secao)
    {
        var eixo = Unitario(Menos(ate, de));

        // A "altura" do tirante redondo é qualquer perpendicular; a vertical
        // projetada deixa o sólido de pé, como a peça.
        return new StructureBar(StructurePart.Rod, vao, lado, de, ate, secao, Perpendicular(new Point3(0, 0, 1), eixo));
    }

    /// <summary>
    /// Onde uma barra de comprimento <paramref name="comprimento"/> que sai de
    /// <paramref name="pe"/> encosta no eixo da viga (de <paramref name="a"/>,
    /// a ponta baixa, a <paramref name="b"/>, a alta), e se encosta mesmo.
    /// Das duas soluções, a que avança para o outro pilar (para trás, para
    /// <paramref name="b"/>, na mão francesa do dianteiro); quem confere se
    /// ela avançou o bastante, e não demais, é o chamador, que conhece os
    /// pilares.
    /// </summary>
    private static (Encosto Como, Point3 Ponto) OndeEncosta(Point3 pe, Point3 a, Point3 b, double comprimento, bool paraTras)
    {
        var d = Menos(b, a);
        var f = Menos(a, pe);

        var qa = Ponto(d, d);
        var qb = 2 * Ponto(f, d);
        var qc = Ponto(f, f) - comprimento * comprimento;
        var delta = qb * qb - 4 * qa * qc;

        if (!(qa > 0) || !(delta >= 0)) return (Encosto.Curta, default);

        var raiz = Math.Sqrt(delta);
        var u = paraTras ? (-qb + raiz) / (2 * qa) : (-qb - raiz) / (2 * qa);

        // NaN cai aqui também: a comparação ao contrário recusa o que não é número.
        if (!(u >= 0 && u <= 1)) return (Encosto.PassaDaPonta, default);

        return (Encosto.Encosta, new Point3(a.X + u * d.X, a.Y + u * d.Y, a.Z + u * d.Z));
    }

    /// <summary>A parte de <paramref name="v"/> perpendicular ao eixo, unitária; se ela some, qualquer perpendicular.</summary>
    private static Point3 Perpendicular(Point3 v, Point3 eixo)
    {
        var p = Menos(v, Vezes(eixo, Ponto(v, eixo)));
        if (Ponto(p, p) > 1e-12) return Unitario(p);

        var outro = Math.Abs(eixo.Z) < 0.9 ? new Point3(0, 0, 1) : new Point3(1, 0, 0);
        return Unitario(Cruz(eixo, outro));
    }

    private static string NomeDoLado(PillarLine lado) => lado == PillarLine.Front ? Tr.T("dianteiro") : Tr.T("traseiro");

    private static Point3 Menos(Point3 a, Point3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static Point3 Vezes(Point3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);

    private static double Ponto(Point3 a, Point3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static Point3 Cruz(Point3 a, Point3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static Point3 Unitario(Point3 a)
    {
        var n = Math.Sqrt(Ponto(a, a));
        return n > 0 ? Vezes(a, 1 / n) : a;
    }

    /// <summary>Os avisos, um por texto, na ordem em que apareceram.</summary>
    private sealed class Avisos
    {
        private readonly List<string> _lista = [];
        private readonly HashSet<string> _vistos = new(StringComparer.Ordinal);

        public IReadOnlyList<string> Lista => _lista;

        public void Add(string aviso)
        {
            if (_vistos.Add(aviso)) _lista.Add(aviso);
        }
    }
}
