using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.SombrasCommands))]

namespace Clivus.Plugin;

/// <summary>O período das sombras: dias (de quantos em quantos), janela de horário, passo e fuso.</summary>
internal sealed record PeriodoDeSombra(DateOnly De, DateOnly Ate, TimeOnly HoraDe, TimeOnly HoraAte, int PassoMinutos, double Fuso, int PassoDias = 1)
{
    /// <summary>Um instante só: mesmo dia, mesma hora.</summary>
    internal bool Instante => De == Ate && HoraDe == HoraAte;

    internal IEnumerable<DateTime> Instantes() => Shading.Instants(De, Ate, HoraDe, HoraAte, TimeSpan.FromMinutes(PassoMinutos), PassoDias);

    internal string Descrever() =>
        Instante
            ? Tr.F("{0:dd/MM/yyyy} às {1:HH:mm}", De, HoraDe)
            : Tr.F("{0:dd/MM/yyyy} a {1:dd/MM/yyyy}{2}, das {3:HH:mm} às {4:HH:mm}, de {5} em {5} min",
                De, Ate, PassoDias > 1 ? Tr.F(" (a cada {0} dias)", PassoDias) : string.Empty, HoraDe, HoraAte, PassoMinutos);
}

/// <summary>
/// Sombras (9.7 e 9.8). Renan, 03/10/2026: "escolho o dia e horário e gero a
/// sombra desenhando ela no CAD e marcando onde pega no módulo, ... a opção
/// de rodar por dia ou horário e vai marcar igual, mas aí sempre marca o
/// pior caso, e a mesma coisa por período, mês, ano".
///
/// Fazem sombra TODOS os elementos do desenho (03/10/2026: "a análise de
/// sombreamento tem que pegar todos elementos do desenho"): as árvores, as
/// outras mesas (a fileira da frente na de trás) e o relevo
/// (<see cref="ShadingModel"/>). Num instante: a sombra de cada objeto é
/// desenhada no terreno e os módulos que pegam sombra (de qualquer causa)
/// ficam roxos, mais escuro quanto maior a fração ("coloca outra cor no
/// sombreamento, tipo roxo"). Num período: cada
/// módulo fica com a cor do PIOR caso dele, e a sombra desenhada é a do
/// instante em que mais área de módulo ficou na sombra. Apagar sombras tira
/// os contornos e devolve a cor de antes de cada módulo.
/// </summary>
public static class SombrasCommands
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");
    private const string ChaveDasPintadas = "SOMBRA_PINTADAS";

    /// <summary>O porquê de cada módulo pintado (05/10/2026): fração, causa e quando, para o "Por que essa sombra?".</summary>
    private const string ChaveDosMotivos = "SOMBRA_MOTIVOS";
    private const string TipoDaSombra = "Sombra";

    /// <summary>
    /// As faixas da marca, em roxo (o laranja, o amarelo e o vermelho se
    /// confundiam com as cores dos tipos de mesa): até 25% lilás, até 50%
    /// violeta, acima roxo-escuro.
    /// </summary>
    internal static RgbColor CorDaFracao(double f) =>
        f <= 0.25 ? new RgbColor(215, 180, 255) : f <= 0.5 ? new RgbColor(160, 90, 230) : new RgbColor(95, 20, 160);

    private static string Legenda => Tr.T("lilás até 25% da face, violeta até 50%, roxo-escuro acima");

    private static string NomeDaCausa(ShadowCause c) => c switch
    {
        ShadowCause.Object => Tr.T("árvore"),
        ShadowCause.Table => Tr.T("mesa"),
        ShadowCause.Terrain => Tr.T("terreno"),
        _ => "-",
    };

    [CommandMethod(PluginInfo.ComandoSombras)]
    public static void Sombras()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        if (!ClivusExtension.TemInterface())
        {
            documento.Editor.WriteMessage(Tr.T("\nA janela de sombras precisa da interface do Civil 3D; use CLIVUS_SOMBRAS_AUTO.\n"));
            return;
        }

        try
        {
            JanelaDeSombras.Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela de sombras.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir as sombras: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_SOMBRAS_AUTO: dia inicial e final (dd/mm/aaaa), hora de e até (hh:mm), passo (min) e fuso. Para o nível 2.</summary>
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

            var de = Texto(Tr.T("Primeiro dia (dd/mm/aaaa)"));
            var ate = Texto(Tr.T("Último dia (dd/mm/aaaa)"));
            var horaDe = Texto(Tr.T("Hora inicial (hh:mm)"));
            var horaAte = Texto(Tr.T("Hora final (hh:mm)"));
            var passo = Texto(Tr.T("Passo (min)"));
            var fuso = Texto(Tr.T("Fuso (horas, -3 em Brasília)"));
            var dias = Texto(Tr.T("A cada quantos dias"));
            if (de is null || ate is null || horaDe is null || horaAte is null || passo is null || fuso is null || dias is null) return;

            var periodo = Ler(de, ate, horaDe, horaAte, passo, fuso, out var porque, dias);

            if (periodo is null)
            {
                editor.WriteMessage(Tr.F("\nSOMBRAS {0}\n", porque));
                return;
            }

            editor.WriteMessage($"\n{Gerar(documento, periodo)}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha nas sombras (automático).", erro);
            editor.WriteMessage(Tr.F("\nNão consegui gerar as sombras: {0}\n", erro.Message));
        }
    }

    [CommandMethod(PluginInfo.ComandoSombraPorQue)]
    public static void SombraPorQue()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var opcoes = new PromptEntityOptions(Tr.T("\nClique num módulo (ou na face dele): "));
            var escolha = editor.GetEntity(opcoes);
            if (escolha.Status != PromptStatus.OK) return;

            editor.WriteMessage($"\n{PorQueDaPeca(documento.Database, escolha.ObjectId)}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no por que da sombra.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui ver a sombra desse módulo: {0}\n", erro.Message));
        }
    }

    [CommandMethod(PluginInfo.ComandoSombraPorQueAutomatico)]
    public static void SombraPorQueAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var texto = editor.GetString(new PromptStringOptions(Tr.T("\nHandle do bloco do módulo: ")));
        if (texto.Status != PromptStatus.OK) return;

        if (!long.TryParse(texto.StringResult, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var numero)
            || !documento.Database.TryGetObjectId(new Handle(numero), out var id))
        {
            editor.WriteMessage(Tr.F("\nSOMBRA Não há entidade com o handle {0}.\n", texto.StringResult));
            return;
        }

        editor.WriteMessage($"\n{PorQueDaPeca(documento.Database, id)}\n");
    }

    /// <summary>O porquê da peça clicada: o bloco do módulo, ou a face (que aponta o módulo).</summary>
    private static string PorQueDaPeca(Database database, ObjectId peca)
    {
        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var entidade = transacao.GetObject(peca, OpenMode.ForRead) as Entity;

            if (entidade is BlockReference br && LayoutXData.LoadModule(br) is not null) return PorQue(database, peca);

            if (entidade is Face face && LayoutXData.LoadFace(face) is { } f)
            {
                var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
                foreach (ObjectId id in espaco)
                {
                    if (id.IsErased || id.ObjectClass.DxfName != "INSERT") continue;
                    if (transacao.GetObject(id, OpenMode.ForRead) is BlockReference b && LayoutXData.LoadModule(b) is { } m && m.Id == f.Module)
                        return PorQue(database, id);
                }
            }
        }

        return Tr.T("SOMBRA Isso não é um módulo do plugin: clique no módulo ou na face dele.");
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
            documento.Editor.WriteMessage(Tr.F("\nNão consegui apagar as sombras: {0}\n", erro.Message));
        }
    }

    /// <summary>Lê o período dos textos; null com o porquê.</summary>
    internal static PeriodoDeSombra? Ler(string de, string ate, string horaDe, string horaAte, string passo, string fuso, out string porque, string dias = "1")
    {
        porque = string.Empty;

        if (!int.TryParse(dias.Trim(), NumberStyles.Integer, Brasil, out var pd) || pd is < 1 or > 366) { porque = Tr.T("O passo de dias precisa ser de 1 a 366."); return null; }

        if (!DateOnly.TryParseExact(de.Trim(), "d/M/yyyy", Brasil, DateTimeStyles.None, out var d0)) { porque = Tr.T("O primeiro dia precisa ser dd/mm/aaaa."); return null; }
        if (!DateOnly.TryParseExact(ate.Trim(), "d/M/yyyy", Brasil, DateTimeStyles.None, out var d1)) { porque = Tr.T("O último dia precisa ser dd/mm/aaaa."); return null; }
        if (!TimeOnly.TryParseExact(horaDe.Trim(), ["H:mm", "H"], Brasil, DateTimeStyles.None, out var h0)) { porque = Tr.T("A hora inicial precisa ser hh:mm."); return null; }
        if (!TimeOnly.TryParseExact(horaAte.Trim(), ["H:mm", "H"], Brasil, DateTimeStyles.None, out var h1)) { porque = Tr.T("A hora final precisa ser hh:mm."); return null; }
        if (!int.TryParse(passo.Trim(), NumberStyles.Integer, Brasil, out var p) || p is < 1 or > 1440) { porque = Tr.T("O passo precisa ser de 1 a 1440 minutos."); return null; }
        if (!double.TryParse(fuso.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) || f is < -14 or > 14) { porque = Tr.T("O fuso precisa ser de -14 a 14 horas."); return null; }
        if (d1 < d0) { porque = Tr.T("O último dia vem antes do primeiro."); return null; }
        if (h1 < h0) { porque = Tr.T("A hora final vem antes da inicial."); return null; }
        var instantes = Shading.CountInstants(d0, d1, h0, h1, TimeSpan.FromMinutes(p), pd);
        if (instantes > Shading.MaxInstants)
        {
            porque = Tr.F("O período tem {0:N0} instantes; o máximo é {1:N0}. Aumente o passo ou encurte o período.", instantes, Shading.MaxInstants);
            return null;
        }

        return new PeriodoDeSombra(d0, d1, h0, h1, p, f, pd);
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
    /// <summary>O botão da janela: o mesmo cálculo, fora de comando, com o vigia calado.</summary>
    internal static string GerarPelaJanela(Document documento, PeriodoDeSombra periodo) =>
        EscritaForaDeComando.Fazer(documento, () => Gerar(documento, periodo));

    internal static string Gerar(Document documento, PeriodoDeSombra periodo)
    {
        var database = documento.Database;
        var terreno = TerrainCache.Get(documento);

        if (terreno is null && FileiraCommands.ExigirTerreno(documento.Editor, documento) is null)
            return Tr.T("SOMBRAS Nenhum terreno processado neste desenho. Use o botão Terreno primeiro.");

        terreno ??= TerrainCache.Get(documento)!;

        var lugar = Lugar(documento);
        if (lugar is null || !lugar.IsValid)
            return Tr.T("SOMBRAS O desenho não tem localização (latitude e longitude). Corrija em Terreno > Resumo > Localização.");

        var cilindros = new List<ShadowCylinder>();
        List<ShadowQuad> faces;
        List<ObjectId> modulos;
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

        if (faces.Count == 0) return Tr.T("SOMBRAS Não há módulo gerado pelo plugin no desenho.");

        var relogio = System.Diagnostics.Stopwatch.StartNew();

        // Árvores, as outras mesas e o relevo: todos os elementos do desenho.
        var modelo = new ShadingModel(faces, cilindros, (x, y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null, terreno.Mesh.MaxZ);

        double[] fracoes;
        ShadowCause[] causas;
        DateTime?[] quando;
        DateTime? desenhar;
        int comSol, instantes;

        if (periodo.Instante)
        {
            var t = periodo.De.ToDateTime(periodo.HoraDe);
            var sol = SolarCalculator.Compute(lugar.Latitude, lugar.Longitude, t, periodo.Fuso);
            var r = modelo.At(sol);
            comSol = sol.ElevationDegrees >= Shading.MinimumElevationDegrees ? 1 : 0;
            instantes = 1;
            fracoes = r.Fractions.ToArray();
            causas = r.Causes.ToArray();
            quando = fracoes.Select(f => f > 0 ? (DateTime?)t : null).ToArray();
            desenhar = comSol == 1 ? t : null;
        }
        else
        {
            var pior = modelo.Worst(lugar.Latitude, lugar.Longitude, periodo.Fuso, periodo.Instantes());
            fracoes = pior.Fractions.ToArray();
            causas = pior.Causes!.ToArray();
            quando = pior.When.ToArray();
            desenhar = pior.WorstInstant;
            comSol = pior.InstantsWithSun;
            instantes = pior.Instants;
        }

        relogio.Stop();

        // No período, os contornos do pior dia hora a hora: a cor é o pior
        // caso do período inteiro, e cada marca tem que ter a sombra que a
        // explica desenhada (05/10/2026).
        var contornos = 0;
        var horarios = new List<DateTime>();
        if (desenhar is { } instante && cilindros.Count > 0)
        {
            var candidatos = periodo.Instante ? [instante] : Shading.OutlineInstants(periodo.Instantes(), instante, TimeSpan.FromHours(1));

            foreach (var t in candidatos)
            {
                var sol = SolarCalculator.Compute(lugar.Latitude, lugar.Longitude, t, periodo.Fuso);
                if (sol.ElevationDegrees < Shading.MinimumElevationDegrees) continue;

                contornos += Desenhar(database, terreno, cilindros, sol, t);
                horarios.Add(t);
            }
        }

        var marcados = Marcar(database, modulos, fracoes, causas, quando, periodo.Descrever());

        var texto = new System.Text.StringBuilder();
        texto.Append(Tr.F("SOMBRAS {0} (fuso {1:+0.#;-0.#;0}), em {2:0.0000}°, {3:0.0000}°: ", periodo.Descrever(), periodo.Fuso, lugar.Latitude, lugar.Longitude));
        texto.Append(Tr.F("{0} árvore(s), {1} módulo(s) (as mesas e o relevo também fazem sombra), {2} instante(s), {3} com sol. ", arvores, faces.Count, instantes, comSol));

        if (comSol == 0)
        {
            texto.Append(Tr.T("O sol não está acima do horizonte no período: nada a sombrear."));
            return texto.ToString();
        }

        int Por(ShadowCause c) => Enumerable.Range(0, fracoes.Length).Count(k => fracoes[k] > 0 && causas[k] == c);

        texto.Append(periodo.Instante
            ? Tr.F("{0} módulo(s) pegam sombra: {1} por árvore, {2} por outra mesa, {3} pelo terreno ({4}). ",
                marcados, Por(ShadowCause.Object), Por(ShadowCause.Table), Por(ShadowCause.Terrain), Legenda)
            : Tr.F("{0} módulo(s) pegam sombra no pior caso: {1} por árvore, {2} por outra mesa, {3} pelo terreno ({4}). ",
                marcados, Por(ShadowCause.Object), Por(ShadowCause.Table), Por(ShadowCause.Terrain), Legenda));

        if (desenhar is { } d && contornos > 0)
            texto.Append(periodo.Instante
                ? Tr.F("Sombra das árvores desenhada às {0:dd/MM/yyyy HH:mm}, no chão e sobre as mesas ({1} contorno(s)). ", d, contornos)
                : Tr.F("Sombra das árvores desenhada no pior dia, {0:dd/MM/yyyy}, em {1} horário(s) de {2:HH:mm} a {3:HH:mm} (o pior às {4:HH:mm}), no chão e sobre as mesas ({5} contorno(s)); cada módulo tem a cor do pior caso do período inteiro, e Por que essa sombra? diz quando e o quê. ",
                    d, horarios.Count, horarios[0], horarios[^1], d, contornos));

        texto.Append(Tr.F("Conta em {0:0.0} s.", relogio.Elapsed.TotalSeconds));

        var piores = Enumerable.Range(0, fracoes.Length).Where(k => fracoes[k] > 0).OrderByDescending(k => fracoes[k]).Take(10).ToList();
        if (piores.Count > 0)
        {
            texto.Append(Tr.T("\n  Os piores: "));
            texto.Append(string.Join("; ", piores.Select(k =>
                Tr.F("{0} {1:0}% ({2}) em {3:dd/MM HH:mm}", Rotulo(database, modulos[k]), fracoes[k] * 100, NomeDaCausa(causas[k]), quando[k]!.Value))));
        }

        return texto.ToString();
    }

    /// <summary>As faces dos módulos (os quatro cantos e a mesa dona) e o bloco do módulo de cada uma, na mesma ordem.</summary>
    private static (List<ShadowQuad> Faces, List<ObjectId> Modulos) Faces(Transaction transacao, Database database)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var classeFace = RXObject.GetClass(typeof(Face));
        var classeBloco = RXObject.GetClass(typeof(BlockReference));

        var modulos = new Dictionary<Guid, ObjectId>();
        var faces = new List<(Guid Modulo, Guid Mesa, IReadOnlyList<Point3> Cantos)>();

        foreach (ObjectId id in espaco)
        {
            if (id.ObjectClass.IsDerivedFrom(classeFace))
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is Face face && LayoutXData.LoadFace(face) is { } f)
                {
                    var cantos = Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).Select(p => new Point3(p.X, p.Y, p.Z)).ToList();
                    faces.Add((f.Module, f.Table, cantos));
                }
            }
            else if (id.ObjectClass.IsDerivedFrom(classeBloco))
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is BlockReference br && LayoutXData.LoadModule(br) is { } m)
                    modulos.TryAdd(m.Id, id);
            }
        }

        // A mesa dona vira um número: a face não faz sombra na própria mesa.
        var grupos = new Dictionary<Guid, int>();
        int Grupo(Guid mesa) => grupos.TryGetValue(mesa, out var g) ? g : grupos[mesa] = grupos.Count;

        var comBloco = faces.Where(f => modulos.ContainsKey(f.Modulo)).ToList();
        return (comBloco.Select(f => new ShadowQuad(f.Cantos, Grupo(f.Mesa))).ToList(), comBloco.Select(f => modulos[f.Modulo]).ToList());
    }

    /// <summary>Desenha a sombra de cada cilindro no terreno e sobre as mesas. Quantos contornos.</summary>
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
                    Contents = Tr.F("Sombra {0:dd/MM HH:mm}", instante),
                };
                espaco.AppendEntity(texto);
                transacao.AddNewlyCreatedDBObject(texto, true);
                estilo(texto);
                PluginXData.Save(transacao, texto, TipoDaSombra, 1, instante.ToString("s", CultureInfo.InvariantCulture));
            }
        }

        // Sobre as mesas (04/10/2026: "a sombra é projetada somente na
        // superfície TIN e não considera que os módulos irão receber as
        // sombras"): a sombra de cada cilindro no plano de cada mesa,
        // recortada pelo contorno dela, um pouco acima dos módulos.
        var mesas = new List<IReadOnlyList<Point3>>();
        foreach (var mesa in LayoutScan.Tables(transacao, database).Values)
            if (mesa.Contour is { } id && transacao.GetObject(id, OpenMode.ForRead) is Polyline3d linha)
                mesas.Add(FileiraCommands.Vertices(linha, transacao));

        foreach (var cilindro in cilindros)
        {
            foreach (var mesa in mesas)
            {
                var naMesa = Shading.ShadowOnPlane(cilindro, sol.Direction, mesa);
                if (naMesa.Count < 3) continue;

                var polilinha = new Polyline3d { Closed = true, Layer = camada };
                espaco.AppendEntity(polilinha);
                transacao.AddNewlyCreatedDBObject(polilinha, true);

                foreach (var p in naMesa)
                {
                    var v = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z + 0.03));
                    polilinha.AppendVertex(v);
                    transacao.AddNewlyCreatedDBObject(v, true);
                }

                PluginXData.Save(transacao, polilinha, TipoDaSombra, 1, instante.ToString("s", CultureInfo.InvariantCulture));
                feitos++;
            }
        }

        transacao.Commit();
        return feitos;
    }

    /// <summary>Pinta cada módulo com sombra pela fração dele, guardando a cor de antes. Quantos.</summary>
    private static int Marcar(Database database, IReadOnlyList<ObjectId> modulos, IReadOnlyList<double> fracoes, IReadOnlyList<ShadowCause> causas, IReadOnlyList<DateTime?> quando, string periodo)
    {
        using var transacao = database.TransactionManager.StartTransaction();
        var pintadas = new List<string>();
        var motivos = new List<string> { "V1", periodo };

        for (var k = 0; k < modulos.Count; k++)
        {
            if (fracoes[k] <= 0 || modulos[k].IsErased) continue;
            // Camada travada não derruba a marcação (a peça é aberta mesmo assim).
            if (transacao.GetObject(modulos[k], OpenMode.ForWrite, false, true) is not Entity modulo) continue;

            var cor = CorDaFracao(fracoes[k]);
            pintadas.Add(modulo.Handle + "=" + PecasPintadas.Texto(modulo.Color));
            motivos.Add(string.Join('|', modulo.Handle.ToString(), fracoes[k].ToString("R", CultureInfo.InvariantCulture), causas[k].ToString(),
                quando[k]?.ToString("s", CultureInfo.InvariantCulture) ?? string.Empty));
            modulo.Color = Color.FromRgb(cor.R, cor.G, cor.B);
        }

        transacao.Commit();

        PluginDictionary.Save(database, ChaveDasPintadas, new ResultBuffer(
            pintadas.Select(p => new TypedValue((int)DxfCode.Text, p)).Prepend(new TypedValue((int)DxfCode.Text, "V1")).ToArray()));
        PluginDictionary.Save(database, ChaveDosMotivos, new ResultBuffer(motivos.Select(m => new TypedValue((int)DxfCode.Text, m)).ToArray()));

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
        PluginDictionary.Save(database, ChaveDosMotivos, new ResultBuffer(new TypedValue((int)DxfCode.Text, "V1")));

        return Tr.F("SOMBRAS {0} contorno(s) e etiqueta(s) de sombra apagado(s); {1} módulo(s) de volta à cor de antes.", apagados, devolvidos);
    }

    /// <summary>Se a cor é uma das três da marca de sombra.</summary>
    private static bool EDaSombra(Color cor) =>
        cor.ColorMethod == ColorMethod.ByColor
        && new[] { 0.1, 0.4, 0.9 }.Select(CorDaFracao).Concat(CoresAntigas).Any(c => c.R == cor.Red && c.G == cor.Green && c.B == cor.Blue);

    /// <summary>As cores da marca até 03/10/2026 (amarelo, laranja, vermelho): desenhos com elas ainda voltam com Apagar.</summary>
    private static readonly RgbColor[] CoresAntigas = [new(255, 220, 0), new(255, 140, 0), new(190, 30, 0)];

    /// <summary>
    /// "Por que essa sombra?" (05/10/2026): o que o último cálculo guardou
    /// para o módulo deste bloco — fração, causa e quando — ou que ele ficou
    /// sem sombra. A frase pronta para a tela.
    /// </summary>
    internal static string PorQue(Database database, ObjectId modulo)
    {
        using var dados = PluginDictionary.Load(database, ChaveDosMotivos);
        var linhas = dados?.AsArray().Select(v => v.Value as string ?? string.Empty).ToList() ?? [];

        if (linhas.Count < 2 || linhas[0] != "V1")
            return Tr.T("SOMBRA Não há cálculo de sombra guardado neste desenho: rode Sombras primeiro.");

        var rotulo = Rotulo(database, modulo);
        var handle = modulo.Handle.ToString();

        foreach (var linha in linhas.Skip(2))
        {
            var c = linha.Split('|');
            if (c.Length < 4 || c[0] != handle) continue;

            var fracao = double.TryParse(c[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0;
            var causa = Enum.TryParse<ShadowCause>(c[2], out var k) ? NomeDaCausa(k) : "?";

            return DateTime.TryParse(c[3], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
                ? Tr.F("SOMBRA {0}: {1:0}% da face na sombra, por {2}, em {3:dd/MM/yyyy HH:mm} (o pior momento dele em {4}).", rotulo, fracao * 100, causa, t, linhas[1])
                : Tr.F("SOMBRA {0}: {1:0}% da face na sombra, por {2} ({3}).", rotulo, fracao * 100, causa, linhas[1]);
        }

        return Tr.F("SOMBRA {0}: sem sombra no último cálculo ({1}).", rotulo, linhas[1]);
    }

    private static string Rotulo(Database database, ObjectId modulo)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        if (transacao.GetObject(modulo, OpenMode.ForRead) is not Entity e || LayoutXData.LoadModule(e) is not { } m) return "?";

        var mesas = LayoutScan.Tables(transacao, database);
        var letreiro = mesas.TryGetValue(m.Table, out var p) ? p.Identity?.Label ?? "?" : "?";
        return Tr.F("{0} col {1} fil {2}", letreiro, m.Column + 1, m.Row + 1);
    }
}
