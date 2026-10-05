using System.Globalization;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// Um módulo de uma mesa do desenho, como a parte elétrica o vê: o GUID do
/// bloco (<see cref="ModuleIdentity.Id"/>), a célula na mesa e os quatro
/// cantos da face superior (vazio quando quem chamou não precisa da
/// geometria, como na escolha de mesas do 11.2).
/// </summary>
public sealed record FieldModule(Guid Id, int Column, int Row, IReadOnlyList<Point3> Face);

/// <summary>
/// Uma mesa do desenho escolhida em campo para a parte elétrica (11.2, 11.6):
/// o GUID, o letreiro (F3.2), os quatro cantos do contorno (borda baixa do
/// início ao fim, depois a borda alta de volta, como o desenho grava) e os
/// módulos. Colunas e fileiras vêm dos módulos.
/// </summary>
public sealed record FieldTable(Guid Id, string Label, IReadOnlyList<Point3> Corners, IReadOnlyList<FieldModule> Modules)
{
    public int Columns => Modules.Count == 0 ? 0 : Modules.Max(m => m.Column) + 1;

    public int Rows => Modules.Count == 0 ? 0 : Modules.Max(m => m.Row) + 1;

    /// <summary>A mesa como vai na assinatura de arranjo.</summary>
    public ArrangementTable Shape => new(Columns, Rows);

    /// <summary>
    /// Por que a mesa não serve para a parte elétrica, ou null: contorno sem
    /// quatro cantos, módulo faltando ou repetido (a grade colunas × fileiras
    /// tem que estar inteira, senão o traçado de um tipo cairia num buraco).
    /// </summary>
    public string? WhyInvalid()
    {
        if (Corners.Count != 4 || Corners.Any(c => !c.IsFinite)) return Tr.F("{0}: o contorno da mesa não tem quatro cantos", Label);
        if (Modules.Count == 0) return Tr.F("{0}: a mesa não tem módulos", Label);

        var celulas = Modules.Select(m => (m.Column, m.Row)).Distinct().Count();
        if (celulas != Modules.Count || Modules.Count != Columns * Rows || Modules.Any(m => m.Column < 0 || m.Row < 0))
            return Tr.F("{0}: a mesa tem {1} módulo(s), e a grade {2}x{3} pede {4}", Label, Modules.Count, Columns, Rows, Columns * Rows);

        return PlanLength < RowDistributor.MenorMedida ? Tr.F("{0}: a borda baixa do contorno não tem comprimento", Label) : null;
    }

    /// <summary>A direção da borda baixa em planta (do início ao fim da mesa), unitária.</summary>
    public Point3 Direction
    {
        get
        {
            var dx = Corners[1].X - Corners[0].X;
            var dy = Corners[1].Y - Corners[0].Y;
            var l = Math.Sqrt(dx * dx + dy * dy);
            return new Point3(dx / l, dy / l, 0);
        }
    }

    /// <summary>O comprimento da borda baixa em planta.</summary>
    public double PlanLength => Math.Sqrt(Math.Pow(Corners[1].X - Corners[0].X, 2) + Math.Pow(Corners[1].Y - Corners[0].Y, 2));

    /// <summary>O fundo em planta: a distância da borda alta à reta da borda baixa.</summary>
    public double PlanDepth
    {
        get
        {
            var d = Direction;
            return Math.Abs(-d.Y * (Corners[3].X - Corners[0].X) + d.X * (Corners[3].Y - Corners[0].Y));
        }
    }

    /// <summary>O centro em planta.</summary>
    public Point3 Center => new(Corners.Average(c => c.X), Corners.Average(c => c.Y), 0);

    /// <summary>O módulo desta célula da mesa, ou null.</summary>
    public FieldModule? ModuleAt(int column, int row) => Modules.FirstOrDefault(m => m.Column == column && m.Row == row);
}

/// <summary>Uma mesa posta em ordem ao longo da fileira (11.2, 11.6).</summary>
/// <param name="Table">A mesa.</param>
/// <param name="Reversed">
/// Se a mesa está virada (180°) em relação à primeira: as colunas e as
/// fileiras dela correm ao contrário no plano cartesiano.
/// </param>
/// <param name="Start">Onde ela começa ao longo da fileira, em metro.</param>
/// <param name="End">Onde ela termina.</param>
public sealed record OrderedTable(FieldTable Table, bool Reversed, double Start, double End)
{
    /// <summary>A célula do cartesiano (coluna, fileira) do módulo desta coluna e fileira da mesa.</summary>
    public (int Column, int Row) ToSketch(int column, int row) =>
        Reversed ? (Table.Columns - 1 - column, Table.Rows - 1 - row) : (column, row);

    /// <summary>O inverso de <see cref="ToSketch"/> (a mesma conta: virar duas vezes é não virar).</summary>
    public (int Column, int Row) FromSketch(int column, int row) => ToSketch(column, row);
}

/// <summary>
/// O desenho do plano cartesiano de um tipo de string (11.2): o tamanho de
/// cada célula de módulo (largura ao longo da fileira, altura no fundo da
/// mesa) e o vão entre uma mesa e a seguinte, em metro, como estão em campo.
/// É só para mostrar: a assinatura de arranjo é que casa com as mesas.
/// <see cref="View"/> é como a mesa de referência aparece em planta, para o
/// cartesiano mostrar o + e o − do mesmo lado que ficam em campo (05/10/2026);
/// null em tipo gravado antes disso.
/// </summary>
public sealed record ArrangementSketch(double CellWidth, double CellHeight, IReadOnlyList<double> Gaps, PlanView? View = null)
{
    /// <summary>O desenho padrão de quem ainda não tem um (módulo 1,1 × 2,3 m, mesas a 0,5 m).</summary>
    public static ArrangementSketch Default(StringArrangement arranjo) =>
        new(1.1, 2.3, Enumerable.Repeat(0.5, Math.Max(0, arranjo.Tables.Count - 1)).ToList());

    public bool IsValid => double.IsFinite(CellWidth) && CellWidth > 0 && double.IsFinite(CellHeight) && CellHeight > 0
        && Gaps.All(g => double.IsFinite(g) && g >= 0);

    /// <summary>"1.1;2.3;0.5": largura, altura e os vãos, invariante; com a vista de planta, ";v0" a ";v3" no fim.</summary>
    public string ToText() =>
        string.Join(';', new[] { CellWidth, CellHeight }.Concat(Gaps).Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)))
        + (View is { } vista ? ";v" + vista.Code.ToString(CultureInfo.InvariantCulture) : "");

    /// <summary>O inverso de <see cref="ToText"/>; null se o texto não é um desenho.</summary>
    public static ArrangementSketch? Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        var partes = texto.Split(';').ToList();
        PlanView? vista = null;
        if (partes.Count > 0 && partes[^1].StartsWith('v'))
        {
            if (!int.TryParse(partes[^1][1..], NumberStyles.None, CultureInfo.InvariantCulture, out var codigo) || PlanView.FromCode(codigo) is not { } lida) return null;
            vista = lida;
            partes.RemoveAt(partes.Count - 1);
        }

        var numeros = new List<double>();
        foreach (var parte in partes)
        {
            if (!double.TryParse(parte, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return null;
            numeros.Add(v);
        }

        if (numeros.Count < 2) return null;
        var desenho = new ArrangementSketch(numeros[0], numeros[1], numeros.Skip(2).ToList(), vista);
        return desenho.IsValid ? desenho : null;
    }

    public bool Equals(ArrangementSketch? other) =>
        other is not null && CellWidth.Equals(other.CellWidth) && CellHeight.Equals(other.CellHeight) && Gaps.SequenceEqual(other.Gaps) && View == other.View;

    public override int GetHashCode() => HashCode.Combine(CellWidth, CellHeight, Gaps.Count, View);

    /// <summary>
    /// O retângulo (x, y, largura, altura) da célula no plano cartesiano, em
    /// metro, para o arranjo dado; espelhado como a <see cref="View"/> manda
    /// (x para a direita e y para cima, como a planta na tela).
    /// </summary>
    public (double X, double Y, double Width, double Height) CellRect(StringArrangement arranjo, int table, int column, int row)
    {
        var (x, y) = SemEspelho(arranjo, table, column, row);
        if (View is { } vista && (vista.MirrorX || vista.MirrorY))
        {
            var (largura, altura) = Size(arranjo);
            if (vista.MirrorX) x = largura - x - CellWidth;
            if (vista.MirrorY) y = altura - y - CellHeight;
        }

        return (x, y, CellWidth, CellHeight);
    }

    private (double X, double Y) SemEspelho(StringArrangement arranjo, int table, int column, int row)
    {
        var x = 0.0;
        for (var t = 0; t < table; t++)
            x += arranjo.Tables[t].Columns * CellWidth + (t < Gaps.Count ? Gaps[t] : 0);

        return (x + column * CellWidth, row * CellHeight);
    }

    /// <summary>A largura e a altura do arranjo inteiro no cartesiano.</summary>
    public (double Width, double Height) Size(StringArrangement arranjo)
    {
        if (arranjo.IsEmpty) return (0, 0);

        var ultimo = SemEspelho(arranjo, arranjo.Tables.Count - 1, 0, 0);
        return (ultimo.X + arranjo.Tables[^1].Columns * CellWidth, arranjo.Tables.Max(t => t.Rows) * CellHeight);
    }
}

/// <summary>
/// Como uma mesa aparece em planta, com o norte para cima (05/10/2026, Renan:
/// "no configurador da string as pontas estão do lado esquerdo, mas na planta
/// elas ficam do lado direito"). No cartesiano a coluna 0 fica à esquerda e a
/// fileira 0 (borda baixa) embaixo; <see cref="MirrorX"/> quando, em planta,
/// as colunas correm para a esquerda (oeste), e <see cref="MirrorY"/> quando
/// a borda alta fica ao sul da baixa.
/// </summary>
public readonly record struct PlanView(bool MirrorX, bool MirrorY)
{
    private const double Quase = 1e-9;

    /// <summary>0 a 3: 1 espelha o x, 2 espelha o y.</summary>
    public int Code => (MirrorX ? 1 : 0) + (MirrorY ? 2 : 0);

    public static PlanView? FromCode(int code) => code is >= 0 and <= 3 ? new PlanView((code & 1) != 0, (code & 2) != 0) : null;

    /// <summary>
    /// A vista de uma mesa pelos cantos do contorno (borda baixa do início ao
    /// fim, depois a alta de volta). Mesa em fileira norte-sul (colunas
    /// correndo no y) espelha o x quando as colunas correm para o sul.
    /// </summary>
    public static PlanView Of(IReadOnlyList<Point3> corners)
    {
        ArgumentNullException.ThrowIfNull(corners);
        if (corners.Count < 4) return default;

        var dx = corners[1].X - corners[0].X;
        var dy = corners[1].Y - corners[0].Y;
        // Para a borda alta: a normal da borda baixa do lado do 4º canto.
        var lado = -dy * (corners[3].X - corners[0].X) + dx * (corners[3].Y - corners[0].Y);
        var hx = lado >= 0 ? -dy : dy;
        var hy = lado >= 0 ? dx : -dx;
        var escala = Math.Max(Math.Sqrt(dx * dx + dy * dy), Quase);

        var espelhaX = dx / escala < -Quase || (Math.Abs(dx / escala) <= Quase && dy < 0);
        var espelhaY = hy / escala < -Quase || (Math.Abs(hy / escala) <= Quase && hx < 0);
        return new PlanView(espelhaX, espelhaY);
    }
}

/// <summary>
/// As mesas escolhidas em campo postas em ordem ao longo da fileira (11.2):
/// a assinatura de arranjo sai dessa ordem, e o cartesiano mostra as mesas
/// lado a lado com o vão de campo.
/// </summary>
public static class StringFieldTables
{
    /// <summary>Duas mesas são da mesma fileira se as direções diferem menos que 1°.</summary>
    private static readonly double CossenoDoParalelo = Math.Cos(RowNumbering.SameAzimuthRadians);

    /// <summary>
    /// Põe as mesas em ordem ao longo da fileira. A referência é a mesa de
    /// menor letreiro (F1.1 antes de F1.2), e as outras correm no sentido da
    /// borda baixa dela. Recusa (null e o porquê) mesas que não estão na
    /// mesma fileira: não paralelas, ou fora da reta (mais de meio fundo de
    /// mesa de lado).
    /// </summary>
    public static IReadOnlyList<OrderedTable>? Order(IReadOnlyList<FieldTable> mesas, out string? problema)
    {
        ArgumentNullException.ThrowIfNull(mesas);

        problema = null;
        if (mesas.Count == 0)
        {
            problema = Tr.T("nenhuma mesa do plugin na seleção");
            return null;
        }

        foreach (var mesa in mesas)
        {
            if (mesa.WhyInvalid() is { } porque)
            {
                problema = porque;
                return null;
            }
        }

        var referencia = mesas.OrderBy(m => ChaveDoLetreiro(m.Label)).ThenBy(m => m.Label, StringComparer.Ordinal).ThenBy(m => m.Id).First();
        var d = referencia.Direction;
        var n = new Point3(-d.Y, d.X, 0);
        var c0 = referencia.Center;
        // Meio fundo da MAIOR mesa: uma 1V ao lado de uma 2V, bordas baixas
        // alinhadas, tem o centro a meio fundo da 1V de distância (revisão
        // da etapa 11); a fileira vizinha fica a um passo inteiro.
        var tolerancia = 0.5 * mesas.Max(m => m.PlanDepth) + 1e-6;

        var postas = new List<OrderedTable>();
        foreach (var mesa in mesas)
        {
            var dm = mesa.Direction;
            var cosseno = dm.X * d.X + dm.Y * d.Y;

            if (Math.Abs(cosseno) < CossenoDoParalelo)
            {
                problema = Tr.F("{0} e {1} não estão na mesma fileira (direções diferentes)", referencia.Label, mesa.Label);
                return null;
            }

            var c = mesa.Center;
            var lado = (c.X - c0.X) * n.X + (c.Y - c0.Y) * n.Y;
            if (Math.Abs(lado) > tolerancia)
            {
                problema = Tr.F("{0} e {1} não estão na mesma fileira", referencia.Label, mesa.Label);
                return null;
            }

            var ao = (c.X - c0.X) * d.X + (c.Y - c0.Y) * d.Y;
            postas.Add(new OrderedTable(mesa, cosseno < 0, ao - mesa.PlanLength / 2, ao + mesa.PlanLength / 2));
        }

        return postas.OrderBy(p => p.Start).ThenBy(p => p.Table.Id).ToList();
    }

    /// <summary>A assinatura de arranjo e o desenho do cartesiano das mesas em ordem.</summary>
    public static (StringArrangement Arrangement, ArrangementSketch Sketch) Describe(IReadOnlyList<OrderedTable> ordem)
    {
        ArgumentNullException.ThrowIfNull(ordem);
        if (ordem.Count == 0) throw new ArgumentException("Sem mesa.", nameof(ordem));

        var arranjo = new StringArrangement(ordem.Select(o => o.Table.Shape).ToList());
        var primeira = ordem[0].Table;
        var vaos = ordem.Zip(ordem.Skip(1), (a, b) => Math.Round(Math.Max(0, b.Start - a.End), 3)).ToList();
        // A vista da mesa de referência (a não virada): é a ela que o cartesiano segue.
        var referencia = (ordem.FirstOrDefault(o => !o.Reversed) ?? ordem[0]).Table;

        // Ao milímetro: é o que vai gravado no desenho.
        return (arranjo, new ArrangementSketch(Math.Round(primeira.PlanLength / primeira.Columns, 3), Math.Round(primeira.PlanDepth / primeira.Rows, 3), vaos, PlanView.Of(referencia.Corners)));
    }

    /// <summary>(fileira, número) do letreiro, para ordenar; letreiro ilegível vai para o fim.</summary>
    internal static (int, int) ChaveDoLetreiro(string label) =>
        TableCells.TryParseLabel(label, out var fileira, out var numero) ? (fileira, numero) : (int.MaxValue, int.MaxValue);
}
