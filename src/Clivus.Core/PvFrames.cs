using Clivus.Geo;

namespace Clivus.Core;

/// <summary>A face de um módulo desenhado, com a coluna e a fileira dele na mesa.</summary>
/// <param name="Column">A coluna (0 na ponta em que a mesa começa).</param>
/// <param name="Row">A fileira (0 na de baixo, a da ponta baixa do módulo).</param>
/// <param name="Corners">
/// Os quatro cantos, em metro, em volta da face (o desenho grava começo e fim
/// da borda baixa, fim e começo da borda alta). Os eixos da mesa saem das
/// colunas, das fileiras e da normal, não da ordem dos cantos; ela só escolhe
/// o comprimento numa mesa de um módulo só.
/// </param>
public sealed record PvModuleFace(int Column, int Row, IReadOnlyList<Point3> Corners);

/// <summary>
/// Uma fiada do PVsyst: o retângulo dos módulos de uma mesa (ou de um pedaço
/// dela) e o que o PVsyst precisa para enchê-lo de módulos.
/// </summary>
/// <param name="Table">O GUID da mesa.</param>
/// <param name="Name">O nome da fiada: o letreiro da mesa, com " #2", " #3" nos pedaços.</param>
/// <param name="Corners">
/// Os quatro cantos do retângulo dos módulos, em metro, na ordem do PVcase:
/// começo e fim da borda baixa, fim e começo da borda alta.
/// </param>
/// <param name="Columns">Quantos módulos ao longo da mesa.</param>
/// <param name="Rows">Quantos módulos na descida.</param>
/// <param name="ModuleWidth">O lado menor do módulo, em metro, ao milímetro.</param>
/// <param name="ModuleHeight">O lado maior do módulo, em metro, ao milímetro.</param>
/// <param name="SpacingX">O vão entre os módulos ao longo da mesa, em metro, ao milímetro.</param>
/// <param name="SpacingY">O vão entre as fileiras, em metro, ao milímetro.</param>
/// <param name="Thickness">A espessura do módulo, em metro: o fundo da caixa que o PVcase grava.</param>
public sealed record PvFrame(
    Guid Table,
    string Name,
    IReadOnlyList<Point3> Corners,
    int Columns,
    int Rows,
    double ModuleWidth,
    double ModuleHeight,
    double SpacingX,
    double SpacingY,
    double Thickness)
{
    /// <summary>Quantos módulos a fiada leva.</summary>
    public int ModuleCount => Columns * Rows;
}

/// <summary>As fiadas de uma mesa e o que aconteceu com ela.</summary>
/// <param name="Frames">As fiadas; vazia quando a mesa ficou de fora.</param>
/// <param name="OffGrid">Algum módulo estava fora da grade da mesa, e ela foi módulo a módulo.</param>
/// <param name="Problem">Por que a mesa ficou de fora, ou null.</param>
public sealed record PvTableFrames(IReadOnlyList<PvFrame> Frames, bool OffGrid, string? Problem);

/// <summary>
/// A mesa como o PVsyst a quer (Renan, 31/10/2026: "é preciso exportar como
/// mesa, exportar como módulo vai me dificultar a modelagem"): uma fiada com
/// os módulos dentro, como o PVcase exporta. O arquivo do PVcase (quatro
/// exportações do Renan, Porto Feliz e Mairinque) põe cada mesa como o
/// retângulo exato dos módulos, colunas × largura + vãos ao longo da mesa e
/// fileiras × altura + vão na descida, com a medida do módulo e os vãos ao
/// lado; o PVsyst conta os módulos por essas medidas.
///
/// O retângulo sai das faces dos módulos desenhados, não do perfil: é o que
/// está no desenho que vai. A normal da mesa sai das diagonais das faces; o
/// comprimento, das colunas (o centro da primeira à última); a descida é a
/// normal × comprimento, no sentido das fileiras; a medida do módulo e os
/// vãos, das faces projetadas nesses eixos. A mesa é rígida (regra sagrada
/// 2), então as faces ficam numa grade; uma mesa com buraco (módulo apagado)
/// vai em pedaços retangulares, e uma com módulo fora da grade vai módulo a
/// módulo, para nenhum módulo sobrar nem faltar no PVsyst. Face espremida e
/// dois módulos na mesma célula deixam a mesa de fora, com o motivo.
///
/// As medidas vão ao milímetro, como o PVcase escreve, e o retângulo é
/// refeito com elas (centrado no desenhado): assim colunas × largura + vãos
/// fecha exatamente com o lado, e o PVsyst não perde um módulo por um
/// décimo de milímetro.
/// </summary>
public static class PvFrames
{
    /// <summary>
    /// A folga da grade, em metro: um módulo a mais que isto do lugar dele, ou
    /// fora do plano da mesa, tira a mesa da grade.
    /// </summary>
    public const double GridTolerance = 0.003;

    /// <summary>
    /// O vão que vai quando a mesa não tem dois módulos vizinhos em sentido
    /// nenhum (uma mesa de um módulo): o padrão da fiada do PVsyst.
    /// </summary>
    public const double DefaultSpacing = 0.02;

    /// <summary>
    /// A espessura do módulo quando a mesa não diz a dela: os 35 mm dos
    /// arquivos do PVcase do Renan. Só dá o fundo da caixa, que o PVsyst não
    /// usa (os triângulos são da face de cima).
    /// </summary>
    public const double DefaultThickness = 0.035;

    /// <summary>O menor lado de uma face de módulo, em metro; menos que isto é face espremida.</summary>
    private const double MenorLado = 0.01;

    /// <summary>
    /// As fiadas de uma mesa.
    /// </summary>
    /// <param name="table">O GUID da mesa.</param>
    /// <param name="name">O letreiro da mesa (vira o nome da fiada).</param>
    /// <param name="faces">As faces dos módulos dela, como estão no desenho.</param>
    /// <param name="thickness">A espessura do módulo; não finita ou não positiva, <see cref="DefaultThickness"/>.</param>
    public static PvTableFrames FromFaces(Guid table, string name, IReadOnlyList<PvModuleFace> faces, double thickness)
    {
        ArgumentNullException.ThrowIfNull(faces);

        var nome = string.IsNullOrWhiteSpace(name) ? table.ToString("D") : name.Trim();
        var espessura = double.IsFinite(thickness) && thickness > 0 ? thickness : DefaultThickness;

        if (faces.Count == 0) return new([], false, Tr.T("a mesa não tem módulo"));

        if (faces.Any(f => f is null || f.Corners is not { Count: 4 } || !f.Corners.All(c => c.IsFinite) || f.Column < 0 || f.Row < 0))
            return new([], false, Tr.T("há face de módulo sem quatro cantos"));

        // Duas faces na mesma coluna e fileira (um desfazer pela metade) contariam
        // o módulo duas vezes no PVsyst.
        var celulas = new HashSet<(int, int)>();
        if (faces.Any(f => !celulas.Add((f.Column, f.Row))))
            return new([], false, Tr.T("há dois módulos na mesma coluna e fileira"));

        // A normal da mesa, pelas diagonais de cada face (que não dependem da
        // ordem dos cantos), virada para cima.
        var normal = Normal(faces);
        if (!Finito(normal)) return new([], false, Tr.T("há face de módulo sem área"));
        if (Math.Abs(normal.Z) < 1e-6) return new([], false, Tr.T("a mesa está de pé"));

        // Os eixos no plano: u ao longo das colunas, v subindo as fileiras (a 0 é
        // a de baixo). Sem duas colunas, u sai das fileiras; sem vizinho nenhum
        // (um módulo só), da aresta mais deitada da face. v é a normal × u: nunca
        // fica paralelo a u, qualquer que seja a ordem dos cantos.
        var porColunas = Direcao(faces, f => f.Row, f => f.Column);
        var porFileiras = Direcao(faces, f => f.Column, f => f.Row);
        var uBruto = porColunas ?? (porFileiras is { } subida ? Cruzado(subida, normal) : ArestaMaisDeitada(faces[0]));
        var u = Normalizar(Menos(uBruto, Vezes(normal, Ponto(uBruto, normal))));
        if (!Finito(u)) return new([], false, Tr.T("há face de módulo sem área"));

        var v = Cruzado(normal, u);
        if (porFileiras is { } fileirasSobem ? Ponto(v, fileirasSobem) < 0 : v.Z < 0) v = Vezes(v, -1);

        // Tudo projetado nos eixos, a partir do centro da primeira face.
        var origem = Centro(faces[0]);
        var n = faces.Count;
        var s0 = new double[n];
        var s1 = new double[n];
        var t0 = new double[n];
        var t1 = new double[n];
        var foraDoPlano = false;

        for (var i = 0; i < n; i++)
        {
            s0[i] = t0[i] = double.MaxValue;
            s1[i] = t1[i] = double.MinValue;

            foreach (var canto in faces[i].Corners)
            {
                var d = Menos(canto, origem);
                var s = Ponto(d, u);
                var t = Ponto(d, v);
                s0[i] = Math.Min(s0[i], s); s1[i] = Math.Max(s1[i], s);
                t0[i] = Math.Min(t0[i], t); t1[i] = Math.Max(t1[i], t);

                if (Math.Abs(Ponto(d, normal)) > GridTolerance) foraDoPlano = true;
            }
        }

        // Uma face espremida (um lado de menos de 1 cm) não é módulo: a mesa
        // fica de fora com o motivo, e as outras saem.
        if (Enumerable.Range(0, n).Any(i => s1[i] - s0[i] < MenorLado || t1[i] - t0[i] < MenorLado))
            return new([], false, Tr.T("há face de módulo sem área"));

        var largura = Mediana(Enumerable.Range(0, n).Select(i => s1[i] - s0[i]));
        var altura = Mediana(Enumerable.Range(0, n).Select(i => t1[i] - t0[i]));
        var vaoX = Vao(faces, f => f.Row, f => f.Column, i => s0[i], i => s1[i]);
        var vaoY = Vao(faces, f => f.Column, f => f.Row, i => t0[i], i => t1[i]);

        var colunaMinima = faces.Min(f => f.Column);
        var fileiraMinima = faces.Min(f => f.Row);
        var passoX = largura + (vaoX ?? 0);
        var passoY = altura + (vaoY ?? 0);
        var inicioX = Mediana(Enumerable.Range(0, n).Select(i => s0[i] - (faces[i].Column - colunaMinima) * passoX));
        var inicioY = Mediana(Enumerable.Range(0, n).Select(i => t0[i] - (faces[i].Row - fileiraMinima) * passoY));

        var naGrade = !foraDoPlano
            && (vaoX ?? 0) > -GridTolerance && (vaoY ?? 0) > -GridTolerance
            && Enumerable.Range(0, n).All(i =>
                Math.Abs(s0[i] - (inicioX + (faces[i].Column - colunaMinima) * passoX)) <= GridTolerance
                && Math.Abs(t0[i] - (inicioY + (faces[i].Row - fileiraMinima) * passoY)) <= GridTolerance
                && Math.Abs(s1[i] - s0[i] - largura) <= GridTolerance
                && Math.Abs(t1[i] - t0[i] - altura) <= GridTolerance);

        // Ao milímetro, como o PVcase. Sem vizinho num sentido, o vão do outro.
        var vaoXmm = Milimetro(Math.Max(0, vaoX ?? vaoY ?? DefaultSpacing));
        var vaoYmm = Milimetro(Math.Max(0, vaoY ?? vaoX ?? DefaultSpacing));
        // O retângulo em volta de um centro, pelos eixos da mesa.
        PvFrame Fiada(string nomeDaFiada, (double X, double Y, double Z) centro, int colunas, int fileiras, double larguraDoModulo, double alturaDoModulo)
        {
            var lm = Milimetro(larguraDoModulo);
            var am = Milimetro(alturaDoModulo);
            var meioS = (colunas * lm + (colunas - 1) * vaoXmm) / 2;
            var meioT = (fileiras * am + (fileiras - 1) * vaoYmm) / 2;

            Point3 Canto(double s, double t)
            {
                var p = Mais(centro, Mais(Vezes(u, s), Vezes(v, t)));
                return new Point3(p.X, p.Y, p.Z);
            }

            return new PvFrame(table, nomeDaFiada,
                [Canto(-meioS, -meioT), Canto(meioS, -meioT), Canto(meioS, meioT), Canto(-meioS, meioT)],
                colunas, fileiras, Math.Min(lm, am), Math.Max(lm, am), vaoXmm, vaoYmm, espessura);
        }

        string Nome(int k, int quantas) => quantas == 1 ? nome : $"{nome} #{k + 1}";

        if (!naGrade)
        {
            // Módulo a módulo: cada face no lugar dela (o centro dela mesma,
            // não o do plano da mesa) e com a medida dela.
            return new(Enumerable.Range(0, n)
                .Select(i => Fiada(Nome(i, n), Centro(faces[i]), 1, 1, s1[i] - s0[i], t1[i] - t0[i]))
                .ToList(), true, null);
        }

        var retangulos = Retangulos(faces);
        var fiadas = new List<PvFrame>(retangulos.Count);

        for (var k = 0; k < retangulos.Count; k++)
        {
            var (c0, c1, r0, r1) = retangulos[k];
            var sInicio = inicioX + (c0 - colunaMinima) * passoX;
            var sFim = inicioX + (c1 - colunaMinima) * passoX + largura;
            var tInicio = inicioY + (r0 - fileiraMinima) * passoY;
            var tFim = inicioY + (r1 - fileiraMinima) * passoY + altura;
            var centro = Mais(origem, Mais(Vezes(u, (sInicio + sFim) / 2), Vezes(v, (tInicio + tFim) / 2)));

            fiadas.Add(Fiada(Nome(k, retangulos.Count), centro, c1 - c0 + 1, r1 - r0 + 1, largura, altura));
        }

        return new(fiadas, false, null);
    }

    /// <summary>
    /// Os módulos presentes em retângulos: colunas seguidas com as mesmas
    /// fileiras, e cada trecho seguido de fileiras dessas colunas. A mesa
    /// inteira é um retângulo só.
    /// </summary>
    private static List<(int C0, int C1, int R0, int R1)> Retangulos(IReadOnlyList<PvModuleFace> faces)
    {
        var porColuna = faces.GroupBy(f => f.Column).ToDictionary(g => g.Key, g => g.Select(f => f.Row).OrderBy(r => r).ToList());
        var minima = porColuna.Keys.Min();
        var maxima = porColuna.Keys.Max();
        var retangulos = new List<(int, int, int, int)>();
        int? inicio = null;
        List<int> fileirasDoGrupo = [];

        for (var c = minima; c <= maxima + 1; c++)
        {
            var fileiras = porColuna.TryGetValue(c, out var f) ? f : [];

            if (inicio is { } comeco && (fileiras.Count == 0 || !fileiras.SequenceEqual(fileirasDoGrupo)))
            {
                foreach (var (r0, r1) in Seguidas(fileirasDoGrupo)) retangulos.Add((comeco, c - 1, r0, r1));
                inicio = null;
            }

            if (inicio is null && fileiras.Count > 0)
            {
                inicio = c;
                fileirasDoGrupo = fileiras;
            }
        }

        return retangulos;
    }

    /// <summary>Os trechos seguidos de uma lista ordenada: 0 1 3 dá (0, 1) e (3, 3).</summary>
    private static IEnumerable<(int, int)> Seguidas(IReadOnlyList<int> ordenadas)
    {
        var inicio = ordenadas[0];

        for (var i = 1; i <= ordenadas.Count; i++)
        {
            if (i < ordenadas.Count && ordenadas[i] == ordenadas[i - 1] + 1) continue;

            yield return (inicio, ordenadas[i - 1]);
            if (i < ordenadas.Count) inicio = ordenadas[i];
        }
    }

    /// <summary>
    /// Uma direção da mesa: em cada linha (as faces com o mesmo <paramref name="linha"/>),
    /// a do centro da primeira ao da última por <paramref name="indice"/>; a
    /// mediana, componente a componente, para um módulo arrastado não entortar
    /// a mesa inteira. Null sem linha com duas faces.
    /// </summary>
    private static (double X, double Y, double Z)? Direcao(
        IReadOnlyList<PvModuleFace> faces, Func<PvModuleFace, int> linha, Func<PvModuleFace, int> indice)
    {
        var direcoes = new List<(double X, double Y, double Z)>();

        foreach (var grupo in faces.GroupBy(linha))
        {
            var primeira = grupo.MinBy(indice)!;
            var ultima = grupo.MaxBy(indice)!;
            if (indice(ultima) <= indice(primeira)) continue;

            var direcao = Normalizar(Menos(Centro(ultima), Centro(primeira)));
            if (Finito(direcao)) direcoes.Add(direcao);
        }

        if (direcoes.Count == 0) return null;

        return (Mediana(direcoes.Select(d => d.X)), Mediana(direcoes.Select(d => d.Y)), Mediana(direcoes.Select(d => d.Z)));
    }

    /// <summary>
    /// A normal da mesa, para cima: a média das normais das faces, cada uma
    /// pelas duas diagonais (c2 − c0 e c3 − c1, que dão o plano qualquer que
    /// seja o canto de partida). Face espremida não entra; sem nenhuma boa, NaN.
    /// </summary>
    private static (double X, double Y, double Z) Normal(IReadOnlyList<PvModuleFace> faces)
    {
        (double X, double Y, double Z) soma = (0, 0, 0);
        var boas = 0;

        foreach (var face in faces)
        {
            var c = face.Corners;
            var n = Cruzado(Menos(c[2], c[0]), Menos(c[3], c[1]));
            if (Comprimento(n) < 1e-6) continue;

            n = Normalizar(n);
            soma = Mais(soma, n.Z < 0 ? Vezes(n, -1) : n);
            boas++;
        }

        return boas == 0 ? (double.NaN, double.NaN, double.NaN) : Normalizar(soma);
    }

    /// <summary>
    /// A aresta da face mais perto da horizontal (das duas que saem do canto 1):
    /// numa mesa inclinada, a borda baixa, que é o sentido do comprimento.
    /// </summary>
    private static (double X, double Y, double Z) ArestaMaisDeitada(PvModuleFace face)
    {
        var c = face.Corners;
        var a = Normalizar(Menos(c[1], c[0]));
        var b = Normalizar(Menos(c[2], c[1]));

        if (!Finito(a)) return b;
        if (!Finito(b)) return a;

        return Math.Abs(b.Z) < Math.Abs(a.Z) - 1e-9 ? b : a;
    }

    /// <summary>O vão entre vizinhas de uma mesma linha (a mediana), ou null sem vizinhas.</summary>
    private static double? Vao(
        IReadOnlyList<PvModuleFace> faces, Func<PvModuleFace, int> linha, Func<PvModuleFace, int> indice, Func<int, double> inicio, Func<int, double> fim)
    {
        var vaos = new List<double>();

        foreach (var grupo in Enumerable.Range(0, faces.Count).GroupBy(i => linha(faces[i])))
        {
            var porIndice = new Dictionary<int, int>();
            foreach (var i in grupo) porIndice.TryAdd(indice(faces[i]), i);

            foreach (var (k, i) in porIndice)
                if (porIndice.TryGetValue(k + 1, out var j)) vaos.Add(inicio(j) - fim(i));
        }

        return vaos.Count == 0 ? null : Mediana(vaos);
    }

    private static double Mediana(IEnumerable<double> valores)
    {
        var ordenados = valores.OrderBy(x => x).ToList();
        var meio = ordenados.Count / 2;

        return ordenados.Count % 2 == 1 ? ordenados[meio] : (ordenados[meio - 1] + ordenados[meio]) / 2;
    }

    private static double Milimetro(double metros) => Math.Round(metros, 3, MidpointRounding.AwayFromZero);

    private static (double X, double Y, double Z) Centro(PvModuleFace face) =>
        (face.Corners.Average(c => c.X), face.Corners.Average(c => c.Y), face.Corners.Average(c => c.Z));

    private static (double X, double Y, double Z) Menos(Point3 a, Point3 b) => (a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static (double X, double Y, double Z) Menos(Point3 a, (double X, double Y, double Z) b) => (a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static (double X, double Y, double Z) Menos((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static (double X, double Y, double Z) Mais((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    private static (double X, double Y, double Z) Vezes((double X, double Y, double Z) a, double k) => (a.X * k, a.Y * k, a.Z * k);

    private static double Ponto((double X, double Y, double Z) a, (double X, double Y, double Z) b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static (double X, double Y, double Z) Cruzado((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
        (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static double Comprimento((double X, double Y, double Z) a) => Math.Sqrt(Ponto(a, a));

    private static (double X, double Y, double Z) Normalizar((double X, double Y, double Z) a)
    {
        var c = Comprimento(a);
        return c <= 1e-12 ? (double.NaN, double.NaN, double.NaN) : (a.X / c, a.Y / c, a.Z / c);
    }

    private static bool Finito((double X, double Y, double Z) a) => double.IsFinite(a.X) && double.IsFinite(a.Y) && double.IsFinite(a.Z);
}
