using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using UFV.Core;
using UFV.Geo;

namespace UFV.Plugin;

/// <summary>
/// A análise de declividade da mesa (Renan, 29/09/2026: "quero uma flecha e
/// a indicação em % ou graus, eu decido qual unidade"): uma seta no plano
/// da mesa, no meio dela, apontando para onde ela desce, e o valor ao lado.
///
/// Mora na camada <see cref="LayoutLayers.SetaDeclividade"/>; cada traço e o
/// texto são notas da mesa (XData com o GUID dela), fora do grupo, como as
/// cotas: somem com a mesa no Refazer e no Recalcular e nascem de novo com
/// ela. Ligada ou não, e em que unidade, fica gravado no desenho
/// (<see cref="Ler"/>), e é o que o desenho de cada mesa consulta.
/// </summary>
internal static class SetaDeDeclividade
{
    private const string Chave = "DECLIVIDADE_SETA";

    /// <summary>Altura do texto, em metro.</summary>
    private const double AlturaDoTexto = 0.50;

    /// <summary>
    /// Quanto a seta e o texto ficam acima do plano dos módulos, na
    /// perpendicular dele. Com 5 cm na vertical e o texto deitado na
    /// horizontal, numa mesa de 17° metade do texto e da seta ficava por baixo
    /// dos módulos (Renan, 30/09/2026: "a flecha tá meio que por baixo").
    /// </summary>
    private const double Acima = 0.15;

    /// <summary>Se a análise está ligada neste desenho, e em que unidade. Nunca gravada: desligada, em porcentagem.</summary>
    internal static (bool Ligada, SlopeUnit Unidade) Ler(Database database)
    {
        try
        {
            using var dados = PluginDictionary.Load(database, Chave);
            var valores = dados?.AsArray();

            if (valores is null || valores.Length < 2) return (false, SlopeUnit.Percent);

            var ligada = valores[0].Value as string == "1";
            var unidade = SlopeLabel.Parse(valores[1].Value as string) ?? SlopeUnit.Percent;

            return (ligada, unidade);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler a análise de declividade gravada.", erro);
            return (false, SlopeUnit.Percent);
        }
    }

    /// <summary>Grava se a análise está ligada e a unidade.</summary>
    internal static void Gravar(Database database, bool ligada, SlopeUnit unidade) =>
        PluginDictionary.Save(database, Chave, new ResultBuffer(
            new TypedValue((int)DxfCode.Text, ligada ? "1" : "0"),
            new TypedValue((int)DxfCode.Text, SlopeLabel.Name(unidade))));

    /// <summary>
    /// Desenha a seta e o valor de uma mesa. <paramref name="cantos"/> são os
    /// quatro cantos do contorno na ordem do desenho: borda baixa do início
    /// ao fim (0, 1), borda alta do fim ao início (2, 3). Quantas entidades
    /// criou.
    /// </summary>
    internal static int Desenhar(
        Transaction transacao, BlockTableRecord espaco, string camada, Guid mesa, IReadOnlyList<Point3> cantos, SlopeUnit unidade,
        Action<Transaction, Entity>? marcar = null,
        Action<MText>? estilo = null)
    {
        // Sem marcador, nota da mesa (a seta do Draw); a análise de
        // declividade do 8.10 passa o seu, que grava o valor.
        marcar ??= (t, e) => LayoutXData.SaveNote(t, e, new NoteIdentity(Guid.NewGuid(), mesa));

        if (cantos.Count < 4) return 0;

        // A linha do meio da mesa, ao longo da fileira.
        var inicio = Meio(cantos[0], cantos[3]);
        var fim = Meio(cantos[1], cantos[2]);

        var emPlanta = Math.Sqrt((fim.X - inicio.X) * (fim.X - inicio.X) + (fim.Y - inicio.Y) * (fim.Y - inicio.Y));
        if (emPlanta < RowDistributor.MenorMedida) return 0;

        var desnivel = fim.Z - inicio.Z;
        var texto = SlopeLabel.Format(desnivel, emPlanta, unidade);

        // A seta desce: do alto para o baixo.
        var (alto, baixo) = desnivel > 0 ? (fim, inicio) : (inicio, fim);

        // Direção da seta (3D, no plano da mesa), a largura (da borda baixa
        // para a alta, no plano da mesa) e a normal do plano, para cima:
        // tudo é desenhado no plano da mesa, levantado na normal.
        var u = Unitario(Menos(baixo, alto));
        var w = Unitario(Menos(cantos[3], cantos[0]));
        var n = Unitario(Vetorial(Unitario(Menos(fim, inicio)), w));
        if (n.Z < 0) n = Vezes(n, -1);

        var levantar = Vezes(n, Acima);
        var comprimento = Distancia(alto, baixo);

        Point3d Ponto(Point3 p) => new(p.X + levantar.X, p.Y + levantar.Y, p.Z + levantar.Z);

        var criadas = 0;

        void Linha(Point3 a, Point3 b)
        {
            var linha = new Line(Ponto(a), Ponto(b)) { Layer = camada };
            espaco.AppendEntity(linha);
            transacao.AddNewlyCreatedDBObject(linha, true);
            marcar(transacao, linha);
            criadas++;
        }

        var centro = Meio(alto, baixo);

        if (!SlopeLabel.IsFlat(desnivel))
        {
            // O corpo: o meio da mesa, de 25% a 75% do comprimento.
            var a = Mais(alto, Vezes(u, 0.25 * comprimento));
            var b = Mais(alto, Vezes(u, 0.75 * comprimento));
            Linha(a, b);

            // A ponta: duas abas de 1,0 m para trás, abertas 0,45 m para cada lado.
            var atras = Mais(b, Vezes(u, -1.0));
            Linha(b, Mais(atras, Vezes(w, 0.45)));
            Linha(b, Mais(atras, Vezes(w, -0.45)));
        }

        // O valor, ao lado do corpo, para o lado da borda alta, deitado no
        // plano da mesa e correndo ao longo da fileira no sentido que se lê
        // em planta (nunca de cabeça para baixo).
        var ondeTexto = Mais(centro, Vezes(w, 0.9));
        var aoLongo = Unitario(Menos(fim, inicio));
        var legivel = LayoutDrawer.RumoLegivel(aoLongo.X, aoLongo.Y);
        if (Math.Abs(Math.IEEERemainder(Math.Atan2(aoLongo.Y, aoLongo.X) - legivel, 2 * Math.PI)) > 1e-6) aoLongo = Vezes(aoLongo, -1);

        var mtexto = new MText
        {
            Location = Ponto(ondeTexto),
            TextHeight = AlturaDoTexto,
            Layer = camada,
            Attachment = AttachmentPoint.MiddleCenter,
            Contents = texto,
        };

        mtexto.Normal = new Vector3d(n.X, n.Y, n.Z);
        mtexto.Direction = new Vector3d(aoLongo.X, aoLongo.Y, aoLongo.Z);

        espaco.AppendEntity(mtexto);
        transacao.AddNewlyCreatedDBObject(mtexto, true);
        estilo?.Invoke(mtexto);
        marcar(transacao, mtexto);

        return criadas + 1;
    }

    /// <summary>A camada da seta, criada ciano se ainda não existe.</summary>
    internal static string Camada(Transaction transacao, Database database) =>
        LayoutLayers.Garantir(transacao, database, LayoutLayers.SetaDeclividade, new RgbColor(0, 200, 255));

    private static Point3 Meio(Point3 a, Point3 b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);

    private static Point3 Menos(Point3 a, Point3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static Point3 Mais(Point3 a, Point3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    private static Point3 Vezes(Point3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);

    private static double Distancia(Point3 a, Point3 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    private static Point3 Unitario(Point3 a)
    {
        var n = Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
        return n < 1e-12 ? new Point3(1, 0, 0) : new Point3(a.X / n, a.Y / n, a.Z / n);
    }

    private static Point3 Vetorial(Point3 a, Point3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
