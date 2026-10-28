namespace Clivus.Core;

/// <summary>
/// Onde o vão de carport está agora no desenho: os três primeiros vértices do
/// contorno (canto baixo do começo, canto baixo do fim, canto alto do fim).
/// Só a planta conta: a cota sai do terreno quando a fila é refeita.
/// </summary>
public sealed record CarportBayNow(double V0X, double V0Y, double V1X, double V1Y, double V2X, double V2Y);

/// <summary>
/// O "Atualizar estruturas" do carport (28/10/2026, Renan: "eu movi com o
/// move, e aí preciso de uma ferramenta do tipo Atualizar estruturas"): o
/// usuário move, gira ou espelha o grupo do vão com os comandos do AutoCAD, e
/// a fila é refeita onde o vão está agora, assentada no terreno.
///
/// O deslocamento de cada vão sai do contorno dele: do contorno como a fila
/// registrada o desenha (montada de novo no mesmo lugar) para o contorno de
/// agora, com a direção e o lado de subida lidos dos dois.
/// Vãos vizinhos que se moveram juntos continuam uma fila só; o vão movido
/// sozinho, ou separado por um vão apagado, vira uma fila própria.
/// </summary>
public static class CarportFollow
{
    /// <summary>Um centímetro: dois vãos que andaram juntos levam o eixo para o mesmo lugar.</summary>
    public const double Tolerancia = 0.01;

    /// <summary>
    /// As filas que substituem esta: a mesma (mesmo id) quando todos os vãos
    /// estão lá e andaram juntos (ou não andaram); senão uma fila por trecho de
    /// vãos vizinhos que andaram juntos, cada uma com id novo de
    /// <paramref name="novoId"/>. Nenhuma, se nenhum vão sobrou.
    /// </summary>
    /// <param name="fila">A fila registrada.</param>
    /// <param name="perfil">O carport dela.</param>
    /// <param name="agora">Os vãos que estão no desenho, pelo índice na fila (0 é o primeiro).</param>
    /// <param name="novoId">De onde vem o id de cada fila nova.</param>
    /// <param name="chao">O terreno, para montar a fila onde ela está registrada (sem ele, ou fora dele, chão plano).</param>
    public static IReadOnlyList<CarportRowRecord> Rows(
        CarportRowRecord fila, CarportProfile perfil, IReadOnlyDictionary<int, CarportBayNow> agora, Func<Guid> novoId,
        Func<double, double, double?>? chao = null)
    {
        ArgumentNullException.ThrowIfNull(fila);
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(agora);
        ArgumentNullException.ThrowIfNull(novoId);

        var dx = fila.ToX - fila.FromX;
        var dy = fila.ToY - fila.FromY;
        var eixo = Math.Sqrt(dx * dx + dy * dy);
        if (!(eixo > 1e-9)) return [fila];

        var ax = dx / eixo;
        var ay = dy / eixo;
        var w = perfil.Array.BayWidth;

        // O contorno de cada vão como a fila registrada o desenha: com o
        // terreno (é o que está no desenho se ninguém mexeu) ou, se ela não
        // monta nele, em chão plano.
        var antes = Contornos(fila, perfil, chao);

        var movimentos = new SortedDictionary<int, Movimento>();
        var parada = antes.Count == fila.Bays && agora.Count == fila.Bays;
        foreach (var (v, b) in agora)
        {
            if (v < 0 || v >= fila.Bays || v >= antes.Count) continue;
            var c = antes[v];
            parada &= Perto(b.V0X, b.V0Y, c[0]) && Perto(b.V1X, b.V1Y, c[1]) && Perto(b.V2X, b.V2Y, c[2]);
            if (Movimento.De(c, b) is { } m) movimentos[v] = m;
        }

        // Ninguém mexeu: a fila fica com o eixo que tem (sem acumular arredondamento).
        if (parada) return [fila];
        if (movimentos.Count == 0) return [];

        // Os trechos: vãos de índice seguido que andaram igual.
        var trechos = new List<(int De, int Ate, Movimento M)>();
        foreach (var (v, m) in movimentos)
        {
            if (trechos.Count > 0 && trechos[^1].Ate == v - 1 && trechos[^1].M.Igual(m, fila, w, ax, ay))
                trechos[^1] = (trechos[^1].De, v, trechos[^1].M);
            else
                trechos.Add((v, v, m));
        }

        if (trechos.Count == 1 && trechos[0].De == 0 && trechos[0].Ate == fila.Bays - 1)
            return [Trecho(fila, fila.Id, 0, fila.Bays, trechos[0].M, w, ax, ay)];

        return trechos.Select(t => Trecho(fila, novoId(), t.De, t.Ate - t.De + 1, t.M, w, ax, ay)).ToList();
    }

    private static CarportRowRecord Trecho(CarportRowRecord fila, Guid id, int primeiro, int vaos, Movimento m, double w, double ax, double ay)
    {
        var (deX, deY) = m.Aplicar(fila.FromX + ax * w * primeiro, fila.FromY + ay * w * primeiro);
        var (ateX, ateY) = m.Aplicar(fila.FromX + ax * w * (primeiro + vaos), fila.FromY + ay * w * (primeiro + vaos));
        return fila with
        {
            Id = id, FromX = deX, FromY = deY, ToX = ateX, ToY = ateY, Bays = vaos,
            FallsRight = m.Espelhado ? !fila.FallsRight : fila.FallsRight,
        };
    }

    /// <summary>
    /// A fila de um vão só que desenha o vão exatamente onde este contorno está
    /// (28/10/2026, Renan: "eu copio o carport e depende como copio vai só a
    /// parte de cima"): a cópia de um vão vira fila de carport com aço. Null se
    /// o contorno não tem tamanho.
    /// </summary>
    public static CarportRowRecord? FromBay(CarportProfile perfil, string carport, CarportBayNow agora, Guid id)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(agora);

        // Um vão de referência na origem, levado para onde o contorno está.
        var referencia = new CarportRowRecord(id, carport, 0, 0, perfil.Array.BayWidth, 0, true, 1);
        var novas = Rows(referencia, perfil, new Dictionary<int, CarportBayNow> { [0] = agora }, () => id);
        return novas.Count == 1 ? novas[0] : null;
    }

    /// <summary>
    /// Junta as filas que continuam uma a outra: mesmo carport, mesma direção,
    /// mesmo lado de descida e a ponta de uma no começo da outra (a 1 cm). Os
    /// vãos copiados lado a lado voltam a ser uma fila só. A fila que fica com
    /// a junção guarda o id e a área da primeira.
    /// </summary>
    public static IReadOnlyList<CarportRowRecord> Merge(IReadOnlyList<CarportRowRecord> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);
        var lista = filas.ToList();
        bool juntou;
        do
        {
            juntou = false;
            for (var i = 0; i < lista.Count && !juntou; i++)
                for (var j = 0; j < lista.Count && !juntou; j++)
                {
                    if (i == j || !Continua(lista[i], lista[j])) continue;
                    lista[i] = lista[i] with { ToX = lista[j].ToX, ToY = lista[j].ToY, Bays = lista[i].Bays + lista[j].Bays };
                    lista.RemoveAt(j);
                    juntou = true;
                }
        }
        while (juntou);

        return lista;
    }

    /// <summary>Se <paramref name="b"/> começa onde <paramref name="a"/> acaba, na mesma linha e do mesmo jeito.</summary>
    private static bool Continua(CarportRowRecord a, CarportRowRecord b)
    {
        if (a.Carport != b.Carport || a.FallsRight != b.FallsRight) return false;
        if (Math.Abs(a.ToX - b.FromX) > Tolerancia || Math.Abs(a.ToY - b.FromY) > Tolerancia) return false;

        var (ax, ay) = (a.ToX - a.FromX, a.ToY - a.FromY);
        var (bx, by) = (b.ToX - b.FromX, b.ToY - b.FromY);
        var na = Math.Sqrt(ax * ax + ay * ay);
        var nb = Math.Sqrt(bx * bx + by * by);
        return na > 1e-9 && nb > 1e-9 && (ax * bx + ay * by) / (na * nb) > 1 - 1e-6;
    }

    /// <summary>Um milímetro: o vértice que está onde a fila o desenha não andou.</summary>
    private const double Parado = 0.001;

    private static bool Perto(double x, double y, (double X, double Y) c) => Math.Abs(x - c.X) <= Parado && Math.Abs(y - c.Y) <= Parado;

    private static List<(double X, double Y)[]> Contornos(CarportRowRecord fila, CarportProfile perfil, Func<double, double, double?>? chao)
    {
        var montada = chao is null ? null : fila.Build(perfil, chao, out _);
        montada ??= CarportRow.Build(perfil, fila.FromX, fila.FromY, fila.ToX, fila.ToY, fila.FallsRight, fila.Bays, (_, _) => 0, out _, double.MaxValue);
        if (montada is null) return [];
        return CarportReturn.Tables(fila.Id, fila.Carport, perfil, montada).Select(t => t.Corners.Select(c => (c.E, c.N)).ToArray()).ToList();
    }

    private static double Cruz(double ux, double uy, double vx, double vy) => ux * vy - uy * vx;

    /// <summary>
    /// Leva um ponto da planta de quando o vão foi desenhado para a de agora:
    /// o primeiro canto de antes vai para o primeiro vértice de agora, a
    /// direção do contorno de antes para a de agora, e o lado de subida para o
    /// lado em que o contorno sobe agora (trocado se o vão foi espelhado).
    /// </summary>
    private sealed record Movimento(double AnchorX, double AnchorY, double V0X, double V0Y, double Ax, double Ay, double Dx, double Dy, bool Espelhado)
    {
        public static Movimento? De((double X, double Y)[] c, CarportBayNow b)
        {
            var ax = c[1].X - c[0].X;
            var ay = c[1].Y - c[0].Y;
            var na = Math.Sqrt(ax * ax + ay * ay);
            var ux = b.V1X - b.V0X;
            var uy = b.V1Y - b.V0Y;
            var n = Math.Sqrt(ux * ux + uy * uy);
            if (!(n > 1e-6) || !(na > 1e-6)) return null;

            var subiaAEsquerda = Cruz(ax, ay, c[2].X - c[0].X, c[2].Y - c[0].Y) > 0;
            var sobeAgora = Cruz(ux, uy, b.V2X - b.V0X, b.V2Y - b.V0Y) > 0;
            return new Movimento(c[0].X, c[0].Y, b.V0X, b.V0Y, ax / na, ay / na, ux / n, uy / n, sobeAgora != subiaAEsquerda);
        }

        public (double X, double Y) Aplicar(double x, double y)
        {
            // Coordenadas no eixo antigo (ao longo, à esquerda) e de volta no novo.
            var px = x - AnchorX;
            var py = y - AnchorY;
            var ao = px * Ax + py * Ay;
            var lado = -px * Ay + py * Ax;
            if (Espelhado) lado = -lado;
            return (V0X + ao * Dx - lado * Dy, V0Y + ao * Dy + lado * Dx);
        }

        /// <summary>Se os dois levam a fila para o mesmo lugar (as duas pontas do eixo a menos de 1 cm).</summary>
        public bool Igual(Movimento outro, CarportRowRecord fila, double w, double ax, double ay)
        {
            if (Espelhado != outro.Espelhado) return false;
            var fim = (X: fila.FromX + ax * w * fila.Bays, Y: fila.FromY + ay * w * fila.Bays);
            foreach (var (x, y) in new[] { (fila.FromX, fila.FromY), fim })
            {
                var a = Aplicar(x, y);
                var b = outro.Aplicar(x, y);
                if (Math.Abs(a.X - b.X) > Tolerancia || Math.Abs(a.Y - b.Y) > Tolerancia) return false;
            }

            return true;
        }
    }
}
