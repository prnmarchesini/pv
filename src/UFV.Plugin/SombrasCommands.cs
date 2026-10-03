using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using UFV.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.SombrasCommands))]

namespace UFV.Plugin;

/// <summary>O período das sombras: dias, janela de horário, passo e fuso.</summary>
internal sealed record PeriodoDeSombra(DateOnly De, DateOnly Ate, TimeOnly HoraDe, TimeOnly HoraAte, int PassoMinutos, double Fuso)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Um instante só: mesmo dia, mesma hora.</summary>
    internal bool Instante => De == Ate && HoraDe == HoraAte;

    internal IEnumerable<DateTime> Instantes() => Shading.Instants(De, Ate, HoraDe, HoraAte, TimeSpan.FromMinutes(PassoMinutos));

    internal string Descrever() =>
        Instante
            ? $"{De.ToString("dd/MM/yyyy", Brasil)} às {HoraDe.ToString("HH:mm", Brasil)}"
            : $"{De.ToString("dd/MM/yyyy", Brasil)} a {Ate.ToString("dd/MM/yyyy", Brasil)}, das {HoraDe.ToString("HH:mm", Brasil)} às {HoraAte.ToString("HH:mm", Brasil)}, de {PassoMinutos} em {PassoMinutos} min";
}

/// <summary>
/// Sombras (9.7 e 9.8). Renan, 03/10/2026: "escolho o dia e horário e gero a
/// sombra desenhando ela no CAD e marcando onde pega no módulo, ... a opção
/// de rodar por dia ou horário e vai marcar igual, mas aí sempre marca o
/// pior caso, e a mesma coisa por período, mês, ano".
///
/// Num instante: a sombra de cada objeto é desenhada no terreno e os módulos
/// que ela pega ficam com a cor da fração sombreada. Num período: cada
/// módulo fica com a cor do PIOR caso dele, e a sombra desenhada é a do
/// instante em que mais área de módulo ficou na sombra. Apagar sombras tira
/// os contornos e devolve a cor de antes de cada módulo.
/// </summary>
public static class SombrasCommands
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");
    private const string ChaveDasPintadas = "SOMBRA_PINTADAS";
    private const string TipoDaSombra = "Sombra";

    /// <summary>As faixas da marca: até 25% amarelo, até 50% laranja, acima vermelho-escuro.</summary>
    internal static RgbColor CorDaFracao(double f) =>
        f <= 0.25 ? new RgbColor(255, 220, 0) : f <= 0.5 ? new RgbColor(255, 140, 0) : new RgbColor(190, 30, 0);

    [CommandMethod(PluginInfo.ComandoSombras)]
    public static void Sombras()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        if (!UfvExtension.TemInterface())
        {
            documento.Editor.WriteMessage("\nA janela de sombras precisa da interface do Civil 3D; use UFV_SOMBRAS_AUTO.\n");
            return;
        }

        try
        {
            JanelaDeSombras.Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela de sombras.", erro);
            documento.Editor.WriteMessage($"\nNão consegui abrir as sombras: {erro.Message}\n");
        }
    }

    /// <summary>UFV_SOMBRAS_AUTO: dia inicial e final (dd/mm/aaaa), hora de e até (hh:mm), passo (min) e fuso. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoSombrasAutomatico)]
    public static void SombrasAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            string? Texto(string pergunta)
            {
                var r = editor.GetString(new PromptStringOptions($"\n{pergunta}: ") { AllowSpaces = false });
                return r.Status == PromptStatus.OK ? r.StringResult : null;
            }

            var de = Texto("Primeiro dia (dd/mm/aaaa)");
            var ate = Texto("Último dia (dd/mm/aaaa)");
            var horaDe = Texto("Hora inicial (hh:mm)");
            var horaAte = Texto("Hora final (hh:mm)");
            var passo = Texto("Passo (min)");
            var fuso = Texto("Fuso (horas, -3 em Brasília)");
            if (de is null || ate is null || horaDe is null || horaAte is null || passo is null || fuso is null) return;

            var periodo = Ler(de, ate, horaDe, horaAte, passo, fuso, out var porque);

            if (periodo is null)
            {
                editor.WriteMessage($"\nSOMBRAS {porque}\n");
                return;
            }

            editor.WriteMessage($"\n{Gerar(documento, periodo)}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha nas sombras (automático).", erro);
            editor.WriteMessage($"\nNão consegui gerar as sombras: {erro.Message}\n");
        }
    }

    [CommandMethod(PluginInfo.ComandoSombrasApagar)]
    public static void SombrasApagar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            documento.Editor.WriteMessage($"\n{Apagar(documento.Database)}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao apagar as sombras.", erro);
            documento.Editor.WriteMessage($"\nNão consegui apagar as sombras: {erro.Message}\n");
        }
    }

    /// <summary>Lê o período dos textos; null com o porquê.</summary>
    internal static PeriodoDeSombra? Ler(string de, string ate, string horaDe, string horaAte, string passo, string fuso, out string porque)
    {
        porque = string.Empty;

        if (!DateOnly.TryParseExact(de.Trim(), "d/M/yyyy", Brasil, DateTimeStyles.None, out var d0)) { porque = "O primeiro dia precisa ser dd/mm/aaaa."; return null; }
        if (!DateOnly.TryParseExact(ate.Trim(), "d/M/yyyy", Brasil, DateTimeStyles.None, out var d1)) { porque = "O último dia precisa ser dd/mm/aaaa."; return null; }
        if (!TimeOnly.TryParseExact(horaDe.Trim(), ["H:mm", "H"], Brasil, DateTimeStyles.None, out var h0)) { porque = "A hora inicial precisa ser hh:mm."; return null; }
        if (!TimeOnly.TryParseExact(horaAte.Trim(), ["H:mm", "H"], Brasil, DateTimeStyles.None, out var h1)) { porque = "A hora final precisa ser hh:mm."; return null; }
        if (!int.TryParse(passo.Trim(), NumberStyles.Integer, Brasil, out var p) || p is < 1 or > 1440) { porque = "O passo precisa ser de 1 a 1440 minutos."; return null; }
        if (!double.TryParse(fuso.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) || f is < -14 or > 14) { porque = "O fuso precisa ser de -14 a 14 horas."; return null; }
        if (d1 < d0) { porque = "O último dia vem antes do primeiro."; return null; }
        if (h1 < h0) { porque = "A hora final vem antes da inicial."; return null; }
        var instantes = Shading.CountInstants(d0, d1, h0, h1, TimeSpan.FromMinutes(p));
        if (instantes > Shading.MaxInstants)
        {
            porque = $"O período tem {instantes.ToString("N0", Brasil)} instantes; o máximo é {Shading.MaxInstants.ToString("N0", Brasil)}. Aumente o passo ou encurte o período.";
            return null;
        }

        return new PeriodoDeSombra(d0, d1, h0, h1, p, f);
    }

    /// <summary>O fuso de partida pela longitude: −3 em quase todo o Brasil.</summary>
    internal static double FusoPelaLongitude(double longitude) => Math.Clamp(Math.Round(longitude / 15), -12, 14);

    /// <summary>A localização do terreno (a mesma do resumo do terreno), ou null.</summary>
    internal static GeoLocation? Lugar(Document documento)
    {
        var terreno = TerrainCache.Get(documento);
        if (terreno is null) return null;

        return GeoStore.Read(documento.Database, TerrenoResumoCommands.Centro(terreno));
    }

    /// <summary>
    /// Gera as sombras do período: apaga as anteriores, calcula, desenha a
    /// sombra (do instante, ou do pior instante do período) e marca os
    /// módulos pelo pior caso. Devolve o relato.
    /// </summary>
    internal static string Gerar(Document documento, PeriodoDeSombra periodo)
    {
        var database = documento.Database;
        var terreno = TerrainCache.Get(documento);

        if (terreno is null && FileiraCommands.ExigirTerreno(documento.Editor, documento) is null)
            return "SOMBRAS Nenhum terreno processado neste desenho. Use o botão Terreno primeiro.";

        terreno ??= TerrainCache.Get(documento)!;

        var lugar = Lugar(documento);
        if (lugar is null || !lugar.IsValid)
            return "SOMBRAS O desenho não tem localização (latitude e longitude). Corrija em Terreno > Resumo > Localização.";

        var cilindros = new List<ShadowCylinder>();
        var faces = new List<IReadOnlyList<Point3>>();
        var modulos = new List<ObjectId>();
        var arvores = 0;

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            foreach (var (_, pe, arvore) in ArvoreCommands.Ler(transacao, database))
            {
                cilindros.AddRange(arvore.Spec.Cylinders(pe.X, pe.Y, pe.Z));
                arvores++;
            }

            (faces, modulos) = Faces(transacao, database);
        }

        Apagar(database);

        if (arvores == 0) return "SOMBRAS Não há objeto de sombra no desenho: ponha árvores em Sombreamento > Objetos > Árvore.";
        if (faces.Count == 0) return "SOMBRAS Não há módulo gerado pelo plugin no desenho.";

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        double[] fracoes;
        DateTime?[] quando;
        DateTime? desenhar;
        int comSol, instantes;

        if (periodo.Instante)
        {
            var t = periodo.De.ToDateTime(periodo.HoraDe);
            var sol = SolarCalculator.Compute(lugar.Latitude, lugar.Longitude, t, periodo.Fuso);
            comSol = sol.ElevationDegrees >= Shading.MinimumElevationDegrees ? 1 : 0;
            instantes = 1;
            fracoes = comSol == 1 ? Shading.Fractions(faces, cilindros, sol.Direction) : new double[faces.Count];
            quando = fracoes.Select(f => f > 0 ? (DateTime?)t : null).ToArray();
            desenhar = comSol == 1 ? t : null;
        }
        else
        {
            var pior = Shading.Worst(faces, cilindros, lugar.Latitude, lugar.Longitude, periodo.Fuso, periodo.Instantes());
            fracoes = pior.Fractions.ToArray();
            quando = pior.When.ToArray();
            desenhar = pior.WorstInstant;
            comSol = pior.InstantsWithSun;
            instantes = pior.Instants;
        }

        relogio.Stop();

        var contornos = 0;
        if (desenhar is { } instante)
        {
            var sol = SolarCalculator.Compute(lugar.Latitude, lugar.Longitude, instante, periodo.Fuso);
            contornos = Desenhar(database, terreno, cilindros, sol, instante);
        }

        var marcados = Marcar(database, modulos, fracoes);

        var texto = new System.Text.StringBuilder();
        texto.Append($"SOMBRAS {periodo.Descrever()} (fuso {periodo.Fuso.ToString("+0.#;-0.#;0", Brasil)}), em {lugar.Latitude.ToString("0.0000", Brasil)}°, {lugar.Longitude.ToString("0.0000", Brasil)}°: ");
        texto.Append($"{arvores} objeto(s), {faces.Count} módulo(s), {instantes} instante(s), {comSol} com sol. ");

        if (comSol == 0)
        {
            texto.Append("O sol não está acima do horizonte no período: nada a sombrear.");
            return texto.ToString();
        }

        texto.Append($"{marcados} módulo(s) pegam sombra");
        if (!periodo.Instante) texto.Append(" no pior caso");
        texto.Append($" (amarelo até 25%, laranja até 50%, vermelho acima). ");

        if (desenhar is { } d && contornos > 0)
            texto.Append($"Sombra desenhada {(periodo.Instante ? "às" : "no pior instante,")} {d.ToString("dd/MM/yyyy HH:mm", Brasil)}. ");

        texto.Append($"Conta em {relogio.Elapsed.TotalSeconds.ToString("0.0", Brasil)} s.");

        var piores = Enumerable.Range(0, fracoes.Length).Where(k => fracoes[k] > 0).OrderByDescending(k => fracoes[k]).Take(10).ToList();
        if (piores.Count > 0)
        {
            texto.Append("\n  Os piores: ");
            texto.Append(string.Join("; ", piores.Select(k => $"{Rotulo(database, modulos[k])} {(fracoes[k] * 100).ToString("0", Brasil)}% em {quando[k]!.Value.ToString("dd/MM HH:mm", Brasil)}")));
        }

        return texto.ToString();
    }

    /// <summary>As faces dos módulos (os quatro cantos) e o bloco do módulo de cada uma, na mesma ordem.</summary>
    private static (List<IReadOnlyList<Point3>> Faces, List<ObjectId> Modulos) Faces(Transaction transacao, Database database)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var classeFace = RXObject.GetClass(typeof(Face));
        var classeBloco = RXObject.GetClass(typeof(BlockReference));

        var modulos = new Dictionary<Guid, ObjectId>();
        var faces = new List<(Guid Modulo, IReadOnlyList<Point3> Cantos)>();

        foreach (ObjectId id in espaco)
        {
            if (id.ObjectClass.IsDerivedFrom(classeFace))
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is Face face && LayoutXData.LoadFace(face) is { } f)
                {
                    var cantos = Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).Select(p => new Point3(p.X, p.Y, p.Z)).ToList();
                    faces.Add((f.Module, cantos));
                }
            }
            else if (id.ObjectClass.IsDerivedFrom(classeBloco))
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is BlockReference br && LayoutXData.LoadModule(br) is { } m)
                    modulos.TryAdd(m.Id, id);
            }
        }

        var comBloco = faces.Where(f => modulos.ContainsKey(f.Modulo)).ToList();
        return (comBloco.Select(f => f.Cantos).ToList(), comBloco.Select(f => modulos[f.Modulo]).ToList());
    }

    /// <summary>Desenha a sombra de cada cilindro no terreno. Quantos contornos.</summary>
    private static int Desenhar(Database database, ProcessedTerrain terreno, IReadOnlyList<ShadowCylinder> cilindros, SunPosition sol, DateTime instante)
    {
        double? Chao(double x, double y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null;

        using var transacao = database.TransactionManager.StartTransaction();

        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var camada = LayoutLayers.Garantir(transacao, database, LayoutLayers.Sombra, new RgbColor(90, 90, 90));
        var camadaTexto = LayoutLayers.Garantir(transacao, database, LayoutLayers.SombraTexto, new RgbColor(90, 90, 90));
        var estilo = EstiloDoProjeto.PrepararTexto(transacao, database);
        var feitos = 0;

        // Uma etiqueta por objeto (a cada par tronco-copa), na ponta da sombra da copa.
        for (var i = 0; i < cilindros.Count; i++)
        {
            var contorno = Shading.ShadowOutline(cilindros[i], sol.Direction, Chao);
            if (contorno.Count < 3) continue;

            var polilinha = new Polyline3d { Closed = true, Layer = camada };
            espaco.AppendEntity(polilinha);
            transacao.AddNewlyCreatedDBObject(polilinha, true);

            foreach (var p in contorno)
            {
                var v = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z + 0.01));
                polilinha.AppendVertex(v);
                transacao.AddNewlyCreatedDBObject(v, true);
            }

            PluginXData.Save(transacao, polilinha, TipoDaSombra, 1, instante.ToString("s", CultureInfo.InvariantCulture));
            feitos++;

            if (i % 2 == 1)
            {
                var longe = contorno.MaxBy(p => (p.X - cilindros[i].X) * (p.X - cilindros[i].X) + (p.Y - cilindros[i].Y) * (p.Y - cilindros[i].Y));
                var texto = new MText
                {
                    Location = new Point3d(longe.X, longe.Y, longe.Z + 0.1),
                    TextHeight = 0.8,
                    Layer = camadaTexto,
                    Attachment = AttachmentPoint.MiddleCenter,
                    Contents = $"Sombra {instante.ToString("dd/MM HH:mm", Brasil)}",
                };
                espaco.AppendEntity(texto);
                transacao.AddNewlyCreatedDBObject(texto, true);
                estilo(texto);
                PluginXData.Save(transacao, texto, TipoDaSombra, 1, instante.ToString("s", CultureInfo.InvariantCulture));
            }
        }

        transacao.Commit();
        return feitos;
    }

    /// <summary>Pinta cada módulo com sombra pela fração dele, guardando a cor de antes. Quantos.</summary>
    private static int Marcar(Database database, IReadOnlyList<ObjectId> modulos, IReadOnlyList<double> fracoes)
    {
        using var transacao = database.TransactionManager.StartTransaction();
        var pintadas = new List<string>();

        for (var k = 0; k < modulos.Count; k++)
        {
            if (fracoes[k] <= 0 || modulos[k].IsErased) continue;
            // Camada travada não derruba a marcação (a peça é aberta mesmo assim).
            if (transacao.GetObject(modulos[k], OpenMode.ForWrite, false, true) is not Entity modulo) continue;

            var cor = CorDaFracao(fracoes[k]);
            pintadas.Add(modulo.Handle + "=" + PecasPintadas.Texto(modulo.Color));
            modulo.Color = Color.FromRgb(cor.R, cor.G, cor.B);
        }

        transacao.Commit();

        PluginDictionary.Save(database, ChaveDasPintadas, new ResultBuffer(
            pintadas.Select(p => new TypedValue((int)DxfCode.Text, p)).Prepend(new TypedValue((int)DxfCode.Text, "V1")).ToArray()));

        return pintadas.Count;
    }

    /// <summary>Apaga os contornos e etiquetas de sombra e devolve a cor de antes dos módulos marcados.</summary>
    internal static string Apagar(Database database)
    {
        var devolvidos = 0;
        var apagados = 0;

        using (var dados = PluginDictionary.Load(database, ChaveDasPintadas))
        using (var transacao = database.TransactionManager.StartTransaction())
        {
            foreach (var valor in dados?.AsArray() ?? [])
            {
                if (valor.Value is not string texto || texto == "V1") continue;

                var partes = texto.Split('=', 2);
                if (partes.Length != 2 || !long.TryParse(partes[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var numero)) continue;
                if (!database.TryGetObjectId(new Handle(numero), out var id) || id.IsErased) continue;
                if (transacao.GetObject(id, OpenMode.ForWrite, false, true) is not Entity modulo) continue;

                // Só a cor que a sombra pôs volta: se o módulo foi repintado
                // depois (Configurações, uma análise), a cor nova fica.
                if (!EDaSombra(modulo.Color)) continue;

                modulo.Color = PecasPintadas.CorDe(partes[1]);
                devolvidos++;
            }

            var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

            foreach (ObjectId id in espaco)
            {
                if (id.IsErased) continue;
                var nome = id.ObjectClass.DxfName;
                if (nome != "POLYLINE" && nome != "MTEXT") continue;

                if (transacao.GetObject(id, OpenMode.ForRead, false, true) is Entity e && PluginXData.Load(e, TipoDaSombra, 1, 1) is not null)
                {
                    transacao.GetObject(id, OpenMode.ForWrite, false, true).Erase();
                    apagados++;
                }
            }

            transacao.Commit();
        }

        PluginDictionary.Save(database, ChaveDasPintadas, new ResultBuffer(new TypedValue((int)DxfCode.Text, "V1")));

        return $"SOMBRAS {apagados} contorno(s) e etiqueta(s) de sombra apagado(s); {devolvidos} módulo(s) de volta à cor de antes.";
    }

    /// <summary>Se a cor é uma das três da marca de sombra.</summary>
    private static bool EDaSombra(Color cor) =>
        cor.ColorMethod == ColorMethod.ByColor
        && new[] { 0.1, 0.4, 0.9 }.Select(CorDaFracao).Any(c => c.R == cor.Red && c.G == cor.Green && c.B == cor.Blue);

    private static string Rotulo(Database database, ObjectId modulo)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        if (transacao.GetObject(modulo, OpenMode.ForRead) is not Entity e || LayoutXData.LoadModule(e) is not { } m) return "?";

        var mesas = LayoutScan.Tables(transacao, database);
        var letreiro = mesas.TryGetValue(m.Table, out var p) ? p.Identity?.Label ?? "?" : "?";
        return $"{letreiro} col {m.Column + 1} fil {m.Row + 1}";
    }
}
