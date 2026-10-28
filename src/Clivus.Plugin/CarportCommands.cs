using System.Diagnostics;
using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.CarportCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Os comandos do carport (35.3 a 35.5, <c>plano/carport</c>):
/// <list type="bullet">
/// <item>CLIVUS_CARPORT: desenha uma fila. Escolhe o carport (o marcado na
/// Escolha das estruturas), o eixo da fila (a linha do meio da cobertura), o
/// lado para onde a água cai e quantos vãos (Enter enche o eixo). Cada pórtico
/// assenta no terreno pela cota do TIN em cada base, nunca pelo Z do clique.</item>
/// <item>CLIVUS_CARPORT_REGERAR: refaz todas as filas com o carport e o terreno de agora.</item>
/// <item>CLIVUS_CARPORT_APAGAR: apaga a fila da peça clicada.</item>
/// <item>CLIVUS_CARPORT_LISTA: a lista de material de todas as filas, em Excel.</item>
/// </list>
/// A fila fica registrada no desenho (<see cref="CarportRows"/>) e cada peça
/// leva no XData o id dela: é assim que regerar, apagar, a lista e o 3D a acham.
/// </summary>
public static class CarportCommands
{
    /// <summary>O tipo do XData das peças do carport.</summary>
    internal const string TipoXData = "Carport";
    private const int VersaoXData = 1;

    private const string CamadaAco = PluginInfo.PrefixoDeDados + "_CARPORT_ACO";
    private const string CamadaModulo = PluginInfo.PrefixoDeDados + "_CARPORT_MODULO";
    private const string CamadaBase = PluginInfo.PrefixoDeDados + "_CARPORT_BASE";
    private const string CamadaMesa = PluginInfo.PrefixoDeDados + "_CARPORT_MESA";

    // ------------------------------------------------------------------ desenhar

    [CommandMethod(PluginInfo.ComandoCarport)]
    public static void Carport()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            if (Terreno(documento) is not { } chao) return;
            if (EscolherCarport(editor, documento.Database) is not { } carport) return;

            var ucs = editor.CurrentUserCoordinateSystem;
            var inicio = editor.GetPoint(new PromptPointOptions(Tr.T("\nEixo da fila (o meio da cobertura), primeiro pórtico: ")));
            if (inicio.Status != PromptStatus.OK) return;

            var fim = editor.GetPoint(new PromptPointOptions(Tr.T("\nSentido da fila (o fim do eixo): ")) { UseBasePoint = true, BasePoint = inicio.Value, UseDashedLine = true });
            if (fim.Status != PromptStatus.OK) return;

            var lado = editor.GetPoint(new PromptPointOptions(Tr.T("\nClique do lado para onde a água cai (o lado baixo da cobertura): ")) { UseBasePoint = true, BasePoint = fim.Value, UseDashedLine = true });
            if (lado.Status != PromptStatus.OK) return;

            var opcoes = new PromptIntegerOptions(Tr.T("\nQuantos vãos? <Enter enche o eixo>: "))
            {
                AllowNone = true, AllowZero = false, AllowNegative = false, LowerLimit = 1, UpperLimit = CarportRow.MaiorNumeroDeVaos,
            };
            var vaos = editor.GetInteger(opcoes);
            if (vaos.Status is not (PromptStatus.OK or PromptStatus.None)) return;

            // Tudo em WCS; o Z dos cliques não entra em conta nenhuma.
            var a = inicio.Value.TransformBy(ucs);
            var b = fim.Value.TransformBy(ucs);
            var c = lado.Value.TransformBy(ucs);
            var caiADireita = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X) < 0;

            var fila = CarportRow.Build(carport.Profile, a.X, a.Y, b.X, b.Y, caiADireita,
                vaos.Status == PromptStatus.OK ? vaos.Value : null, chao, out var motivo);
            if (fila is null)
            {
                editor.WriteMessage(Tr.F("\nO carport não saiu: {0}.\n", motivo));
                return;
            }

            // O registro guarda o número de vãos que saiu: regerar não "enche" de novo.
            var registro = new CarportRowRecord(Guid.NewGuid(), carport.Name, a.X, a.Y, b.X, b.Y, caiADireita, fila.Bays.Count);
            var jaHavia = Registradas(documento.Database);
            var pecas = Desenhar(documento.Database, carport, registro.Id, fila, jaHavia.Count + 1);
            Gravar(documento.Database, [.. jaHavia, registro]);

            Relatar(editor, carport, fila, pecas);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao desenhar o carport.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui desenhar o carport: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_CARPORT_AUTO, o teste de nível 2 no Core Console (sem a janela de
    /// Configurações): cadastra o carport de referência se o desenho não tem
    /// nenhum e segue o mesmo caminho do comando, com os pontos digitados.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoCarportAutomatico)]
    public static void CarportAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        if (CarportsDoDesenho.Ler(documento.Database).Count == 0)
            CarportsDoDesenho.Gravar(documento.Database, [new DrawingCarport(CarportProfile.Reference, DrawingCarports.DefaultColor)], MesasDoDesenho.Ler(documento.Database));

        Carport();
    }

    // ------------------------------------------------------------------ usina

    /// <summary>
    /// O Usina (e o Regerar área) com carport (28/10/2026, Renan: "se em
    /// configuração eu já deixei que vou usar carport, eu faço o mesmo processo:
    /// Usina, escolho a área, o alinhamento e pronto"). Com carport marcado na
    /// Escolha das estruturas, enche a área de filas de carport pela mesma
    /// distribuição das mesas (alinhamento, lado, azimute e pitch do projeto),
    /// trocando as filas de carport que esta área já tinha. Devolve false
    /// quando não é caso de carport (o Usina segue com as mesas).
    /// </summary>
    internal static bool UsinaComCarport(
        Editor editor,
        Document documento,
        ProcessedTerrain terreno,
        (IReadOnlyList<Clivus.Geo.Point3> Vertices, string Nome) area,
        (IReadOnlyList<Clivus.Geo.Point3> Vertices, AlignmentIdentity Identidade) alinhamento)
    {
        var database = documento.Database;
        var marcados = CarportsDoDesenho.Ler(database).Where(c => c.Use).ToList();
        if (marcados.Count == 0) return false;

        // Mesa e carport marcados juntos: pergunta qual vai nesta área.
        if (MesasDoDesenho.Ler(database).Any(m => m.Use))
        {
            var carport = Tr.T("Carport");
            var mesas = Tr.T("Mesas");
            var opcoes = new PromptKeywordOptions(Tr.F("\nMesas e carport marcados. Esta área leva [{0}/{1}] <{0}>: ", carport, mesas), $"{carport} {mesas}") { AllowNone = true };
            var resposta = editor.GetKeywords(opcoes);
            if (resposta.Status == PromptStatus.Cancel) return true;
            if (resposta.Status == PromptStatus.OK && resposta.StringResult == mesas) return false;
        }

        var escolhido = marcados[0];
        var perfil = escolhido.Profile;
        var config = ConfigCommands.Inicial(documento, out var aviso).Configuration;
        if (aviso is not null) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}\n", aviso));

        editor.WriteMessage(Tr.F("\nUSINA Carport: {0}\nÁrea: {1}; alinhamento: {2}\n", perfil.Describe(), area.Nome, alinhamento.Identidade.Describe()));

        PlanLayout layout;
        try
        {
            layout = RowDistributor.Distribute(area.Vertices, alinhamento.Vertices, alinhamento.Identidade.Side,
                CarportLayout.Pitch(perfil), 0, CarportLayout.Footprint(perfil), config.UpslopeAzimuthRadians);
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage(Tr.F("\nUSINA {0}\n", erro.Message));
            return true;
        }

        var novas = CarportLayout.Rows(layout, escolhido.Name, area.Nome);
        if (novas.Count == 0)
        {
            editor.WriteMessage(Tr.T("\nUSINA Nenhuma fila de carport cabe: a área está do outro lado da linha, a linha não a atravessa, ou ela é pequena demais.\n"));
            return true;
        }

        double? Chao(double x, double y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null;

        // As filas de carport que esta área já tinha saem (como o Refazer faz com as mesas).
        var registradas = Registradas(database);
        var antigas = registradas.Where(r => r.Area == area.Nome).ToList();
        foreach (var r in antigas) Apagar(database, r.Id);
        if (antigas.Count > 0) editor.WriteMessage(Tr.F("  {0} fila(s) de carport que esta área tinha foram trocadas.\n", antigas.Count));

        var feitas = new List<CarportRowRecord>();
        int vaos = 0, modulos = 0, pecas = 0;
        foreach (var nova in novas)
        {
            var registro = nova with { Id = Guid.NewGuid() };
            var fila = registro.Build(perfil, Chao, out var motivo);
            if (fila is null)
            {
                editor.WriteMessage(Tr.F("  ATENÇÃO: uma fila de {0} vão(s) não saiu: {1}.\n", nova.Bays, motivo));
                continue;
            }

            pecas += Desenhar(database, escolhido, registro.Id, fila, registradas.Count(r => r.Area != area.Nome) + feitas.Count + 1);
            foreach (var w in fila.Warnings) editor.WriteMessage(Tr.F("  ATENÇÃO: {0}.\n", w));
            feitas.Add(registro);
            vaos += fila.Bays.Count;
            modulos += fila.Bays.Sum(b => b.Modules.Count);
        }

        Gravar(database, [.. registradas.Where(r => r.Area != area.Nome), .. feitas]);

        editor.WriteMessage(Tr.F("\nUSINA Carport \"{0}\": {1} fila(s), {2} vão(s), {3} módulos ({4:0.##} kWp); borda baixa a {5:0.###} m do chão em cada pórtico.\n",
            escolhido.Name, feitas.Count, vaos, modulos, modulos * perfil.Array.Module.PowerWatts / 1000, perfil.ClearHeight));
        editor.WriteMessage(FormattableString.Invariant($"CLIVUS_CARPORT USINA filas={feitas.Count} vaos={vaos} modulos={modulos} pecas={pecas}\n"));
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
        return true;
    }

    // ------------------------------------------------------------------ regerar

    [CommandMethod(PluginInfo.ComandoCarportRegerar)]
    public static void Regerar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            if (Terreno(documento) is not { } chao) return;
            var database = documento.Database;
            var filas = Registradas(database);
            if (filas.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nNenhuma fila de carport neste desenho.\n"));
                return;
            }

            Atualizar(editor, database, chao, _ => true);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao regerar o carport.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui regerar o carport: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// Atualizar estruturas (28/10/2026, Renan: "eu movi com o move, e aí
    /// preciso de uma ferramenta do tipo Atualizar estruturas"): cada fila
    /// escolhida vai para onde os vãos dela estão agora (MOVE, ROTATE, MIRROR
    /// do grupo do vão) e é refeita lá, assentada no terreno, com o carport de
    /// agora. As outras ficam como estão.
    /// </summary>
    private static void Atualizar(Editor editor, Database database, Func<double, double, double?> chao, Func<CarportRowRecord, bool> quais)
    {
        var filas = Registradas(database);
        var carports = CarportsDoDesenho.Ler(database);
        var agora = OndeEstaoOsVaos(database, filas);
        var comPecas = FilasComPecas(database);
        var sumidas = 0;
        var novas = new List<CarportRowRecord>();
        var escolhidas = 0;
        var feitas = 0;
        var movidas = 0;
        foreach (var registro in filas)
        {
            if (!quais(registro))
            {
                novas.Add(registro);
                continue;
            }

            escolhidas++;
            if (DrawingCarports.Find(carports, registro.Carport) is not { } carport)
            {
                editor.WriteMessage(Tr.F("\n  A fila do carport \"{0}\" ficou como estava: o carport não está mais cadastrado.\n", registro.Carport));
                novas.Add(registro);
                continue;
            }

            // Fila sem vão e sem peça nenhuma no desenho: foi apagada (ERASE,
            // Regerar área). Sai do registro; refazê-la a trazia de volta
            // (28/10/2026, Renan: "cliquei em atualizar, o trem me criou novas").
            if (agora[registro.Id].Count == 0 && !comPecas.Contains(registro.Id))
            {
                sumidas++;
                continue;
            }

            // Fila sem vão como mesa mas com o aço no desenho (desenhada antes
            // de 28/10/2026, 12:31): refeita onde está.
            var seguidas = agora[registro.Id].Count == 0
                ? [registro]
                : CarportFollow.Rows(registro, carport.Profile, agora[registro.Id], Guid.NewGuid, chao);
            var montadas = seguidas.Select(s => (Registro: s, Fila: s.Build(carport.Profile, chao, out var motivo), Motivo: motivo)).ToList();
            if (montadas.FirstOrDefault(m => m.Fila is null) is { Registro: not null } falha)
            {
                editor.WriteMessage(Tr.F("\n  A fila do carport \"{0}\" ficou como estava: {1}.\n", registro.Carport, falha.Motivo));
                novas.Add(registro);
                continue;
            }

            if (seguidas.Count != 1 || seguidas[0] != registro) movidas++;
            Apagar(database, registro.Id);
            foreach (var (s, fila, _) in montadas)
            {
                novas.Add(s);
                Relatar(editor, carport, fila!, Desenhar(database, carport, s.Id, fila!, novas.Count));
            }

            feitas++;
        }

        Gravar(database, novas);
        if (sumidas > 0)
            editor.WriteMessage(Tr.F("\n  {0} fila(s) de carport apagada(s) do desenho saíram do registro (não foram refeitas).\n", sumidas));
        editor.WriteMessage(Tr.F("\nCarport: {0} de {1} fila(s) refeitas com o carport e o terreno de agora ({2} seguiram os vãos movidos); {3} fila(s) no desenho.\n",
            feitas, escolhidas - sumidas, movidas, novas.Count));
        editor.WriteMessage(FormattableString.Invariant($"CLIVUS_CARPORT ATUALIZAR filas={novas.Count} movidas={movidas} sumidas={sumidas}\n"));
    }

    /// <summary>As filas que ainda têm alguma peça de aço ou base no desenho (pelo id da fila no XData).</summary>
    private static HashSet<Guid> FilasComPecas(Database database)
    {
        var ids = new HashSet<Guid>();
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        foreach (ObjectId id in espaco)
            if (!id.IsErased && transacao.GetObject(id, OpenMode.ForRead) is Entity e && IdDaFila(e) is { } fila) ids.Add(fila);
        return ids;
    }

    /// <summary>
    /// Onde cada vão de cada fila está agora: os três primeiros vértices do
    /// contorno. O vão sem contorno (apagado) não entra.
    /// </summary>
    private static Dictionary<Guid, Dictionary<int, CarportBayNow>> OndeEstaoOsVaos(Database database, IReadOnlyList<CarportRowRecord> filas)
    {
        var resultado = filas.ToDictionary(f => f.Id, _ => new Dictionary<int, CarportBayNow>());
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var mesas = LayoutScan.Tables(transacao, database);
        foreach (var f in filas)
            for (var v = 0; v < f.Bays; v++)
            {
                if (!mesas.TryGetValue(CarportReturn.IdDoVao(f.Id, v), out var mesa)) continue;
                if (mesa.Contour is not { } contorno) continue;
                if (transacao.GetObject(contorno, OpenMode.ForRead) is not Polyline3d polilinha) continue;

                var p = polilinha.Cast<ObjectId>().Take(3).Select(id => ((PolylineVertex3d)transacao.GetObject(id, OpenMode.ForRead)).Position).ToList();
                if (p.Count < 3) continue;
                resultado[f.Id][v] = new CarportBayNow(p[0].X, p[0].Y, p[1].X, p[1].Y, p[2].X, p[2].Y);
            }

        return resultado;
    }

    // ------------------------------------------------------------------ apagar

    [CommandMethod(PluginInfo.ComandoCarportApagar)]
    public static void ApagarFila()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            var escolha = editor.GetEntity(new PromptEntityOptions(Tr.T("\nClique numa peça da fila de carport a apagar: ")));
            if (escolha.Status != PromptStatus.OK) return;

            Guid? id;
            using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
                id = transacao.GetObject(escolha.ObjectId, OpenMode.ForRead) is Entity e ? IdDaFila(e) : null;

            if (id is not { } fila)
            {
                editor.WriteMessage(Tr.T("\nIsso não é peça de carport.\n"));
                return;
            }

            var apagadas = Apagar(documento.Database, fila);
            Gravar(documento.Database, Registradas(documento.Database).Where(r => r.Id != fila).ToList());
            editor.WriteMessage(Tr.F("\nFila de carport apagada: {0} peça(s).\n", apagadas));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao apagar o carport.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui apagar o carport: {0}\n", erro.Message));
        }
    }

    // ------------------------------------------------------------------ lista

    [CommandMethod(PluginInfo.ComandoCarportLista)]
    public static void Lista()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            if (Terreno(documento) is not { } chao) return;
            var montadas = Montadas(documento.Database, chao, editor);
            if (montadas.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nNenhuma fila de carport neste desenho.\n"));
                return;
            }

            var lista = CarportBill.Build(montadas.Select((m, i) => new CarportBillRow(Tr.F("Fila {0} — {1}", i + 1, m.Carport.Name), m.Carport.Profile, m.Fila)).ToList());
            editor.WriteMessage(Tr.F("\nCarport: {0} fila(s), {1} vão(s), {2} pórtico(s), {3} módulos ({4:0.##} kWp).\n",
                lista.Rows, lista.Bays, lista.Portals, lista.Modules, lista.Kwp));
            foreach (var l in lista.Lines)
                editor.WriteMessage(l.Length is { } c
                    ? Tr.F("  {0} {1}: {2} × {3:0.00} m\n", l.Item, l.Section, l.Quantity, c)
                    : Tr.F("  {0} {1}: {2}\n", l.Item, l.Section, l.Quantity));

            var pasta = Path.GetDirectoryName(documento.Database.Filename);
            var nome = Path.GetFileNameWithoutExtension(documento.Name) + " - carport.xlsx";
            if (!ClivusExtension.TemInterface())
            {
                editor.WriteMessage(FormattableString.Invariant($"CLIVUS_CARPORT LISTA filas={lista.Rows} vaos={lista.Bays} pilares={lista.Pillars.Count} modulos={lista.Modules}\n"));
                return;
            }

            if (DialogoDeArquivo.Salvar(Tr.T("Lista de material do carport"), Tr.T("Planilha do Excel") + " (*.xlsx)|*.xlsx", nome, pasta) is not { } caminho) return;
            File.WriteAllBytes(caminho, CarportBill.ToXlsx(lista).ToBytes());
            editor.WriteMessage(Tr.F("\nLista de material gravada em {0}\n", caminho));
            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na lista de material do carport.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui fazer a lista do carport: {0}\n", erro.Message));
        }
    }

    // ------------------------------------------------------------------ para o 3D e a lista

    /// <summary>As filas do desenho montadas de novo com o terreno de agora (as que ainda têm o carport cadastrado).</summary>
    internal static List<(DrawingCarport Carport, CarportRowResult Fila, Guid Id)> Montadas(Database database, Func<double, double, double?> chao, Editor? editor = null)
    {
        var carports = CarportsDoDesenho.Ler(database);
        var montadas = new List<(DrawingCarport, CarportRowResult, Guid)>();

        // A fila apagada do desenho (ERASE) não vai ao 3D, à lista nem ao TermoVision.
        var comPecas = FilasComPecas(database);
        foreach (var registro in Registradas(database).Where(r => comPecas.Contains(r.Id)))
        {
            if (DrawingCarports.Find(carports, registro.Carport) is not { } carport) continue;
            if (registro.Build(carport.Profile, chao, out var motivo) is { } fila) montadas.Add((carport, fila, registro.Id));
            else editor?.WriteMessage(Tr.F("\n  A fila do carport \"{0}\" não pôde ser montada: {1}.\n", registro.Carport, motivo));
        }

        return montadas;
    }

    /// <summary>
    /// Os vãos de carport entre estas mesas: os das filas registradas (pelo id
    /// do vão) e os contornos cujo perfil é o nome de um carport do desenho (a
    /// cópia de um vão, que não tem fila).
    /// </summary>
    internal static HashSet<Guid> VaosDeCarport(Database database, IReadOnlyDictionary<Guid, TableParts> mesas)
    {
        var filas = FilaDosVaos(database);
        var carports = CarportsDoDesenho.Ler(database);
        return mesas
            .Where(m => filas.ContainsKey(m.Key) || (m.Value.Identity is { } i && EVaoDeCarport(i, carports)))
            .Select(m => m.Key)
            .ToHashSet();
    }

    /// <summary>De que fila é cada vão (pelo id do vão como mesa).</summary>
    internal static Dictionary<Guid, CarportRowRecord> FilaDosVaos(Database database)
    {
        var mapa = new Dictionary<Guid, CarportRowRecord>();
        foreach (var r in Registradas(database))
            for (var v = 0; v < r.Bays; v++) mapa[CarportReturn.IdDoVao(r.Id, v)] = r;
        return mapa;
    }

    /// <summary>
    /// O Recalcular (e o Recalcular pendentes, e o Grupo › Recalcular) num vão
    /// de carport: refaz a fila inteira dele com o carport e o terreno de agora.
    /// O vão sem fila (cópia de um vão) fica como está, com o aviso.
    /// </summary>
    internal static void RegerarVaos(Editor editor, Document documento, ProcessedTerrain terreno, IReadOnlyList<Guid> vaos)
    {
        var database = documento.Database;
        var mapa = FilaDosVaos(database);
        double? Chao(double x, double y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null;

        var semFila = vaos.Count(v => !mapa.ContainsKey(v));
        if (semFila > 0)
            editor.WriteMessage(Tr.F("\n  {0} vão(s) de carport sem fila (cópia de um vão) ficaram como estavam: para carport, use Usina ou Carport › Desenhar fila.\n", semFila));

        // O vão que foi movido é refeito onde está, não onde estava.
        var filas = vaos.Where(mapa.ContainsKey).Select(v => mapa[v].Id).ToHashSet();
        if (filas.Count > 0) Atualizar(editor, database, Chao, r => filas.Contains(r.Id));
    }

    /// <summary>
    /// Depois de um Regerar área ou Apagar tudo, que apagam as mesas da área:
    /// a fila de carport que perdeu todos os vãos sai inteira (o aço e o registro).
    /// </summary>
    internal static void LimparFilasSemVao(Database database)
    {
        IReadOnlyDictionary<Guid, TableParts> mesas;
        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
            mesas = LayoutScan.Tables(transacao, database);

        var registradas = Registradas(database);
        var vazias = registradas.Where(r => !Enumerable.Range(0, r.Bays).Any(v => mesas.ContainsKey(CarportReturn.IdDoVao(r.Id, v)))).ToList();
        if (vazias.Count == 0) return;

        foreach (var r in vazias) Apagar(database, r.Id);
        Gravar(database, registradas.Except(vazias).ToList());
    }

    /// <summary>Se a mesa (pela identidade do contorno) é um vão de carport: o perfil dela é o nome de um carport do desenho.</summary>
    internal static bool EVaoDeCarport(TableIdentity mesa, IReadOnlyList<DrawingCarport> carports) =>
        mesa.ProfileName is { Length: > 0 } nome && DrawingCarports.Find(carports, nome) is not null;

    /// <summary>Os vãos de todas as filas como mesas do TermoVision (36.1).</summary>
    internal static List<TermoVisionTable> MesasDoTermoVision(Document documento)
    {
        if (TerrainCache.Get(documento) is not { } terreno) return [];
        return Montadas(documento.Database, (x, y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null)
            .SelectMany(m => CarportReturn.Tables(m.Id, m.Carport.Name, m.Carport.Profile, m.Fila, CarportMesh.CorDoAco)).ToList();
    }

    /// <summary>As faces 3D de todas as filas do desenho (a página 3D do Clivus e o servidor).</summary>
    internal static List<Scene3DFace> Faces3D(Database database, Func<double, double, double?> chao) =>
        Montadas(database, chao).SelectMany(m => CarportMesh.Faces(m.Carport.Profile, m.Fila, CarportMesh.CorDoAco, comModulos: false)).ToList();

    // ------------------------------------------------------------------ miúdos

    private static Func<double, double, double?>? Terreno(Document documento)
    {
        if (TerrainCache.Get(documento) is { } terreno)
            return (x, y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null;

        documento.Editor.WriteMessage(Tr.T("\nEscolha o terreno antes (Terreno): o carport assenta cada pilar na cota do terreno.\n"));
        return null;
    }

    private static void Relatar(Editor editor, DrawingCarport carport, CarportRowResult fila, int pecas)
    {
        var modulos = fila.Bays.Sum(v => v.Modules.Count);
        editor.WriteMessage(Tr.F(
            "\nCarport \"{0}\": {1} vão(s), {2} pórtico(s), {3} módulos ({4:0.##} kWp), {5:0.##} m de fila; borda baixa a {6:0.###} m do chão em cada pórtico.\n",
            carport.Name, fila.Bays.Count, fila.Portals.Count, modulos, modulos * carport.Profile.Array.Module.PowerWatts / 1000, fila.Length, carport.Profile.ClearHeight));
        foreach (var aviso in fila.Warnings) editor.WriteMessage(Tr.F("  ATENÇÃO: {0}.\n", aviso));
        editor.WriteMessage(FormattableString.Invariant(
            $"CLIVUS_CARPORT FILA vaos={fila.Bays.Count} porticos={fila.Portals.Count} modulos={modulos} pecas={pecas}\n"));
    }

    private static List<CarportRowRecord> Registradas(Database database)
    {
        using var dados = PluginDictionary.Load(database, CarportRows.StorageKey);
        return [.. CarportRows.Decode(dados?.AsArray().Select(v => v.Value as string ?? string.Empty).ToList())];
    }

    private static void Gravar(Database database, IReadOnlyList<CarportRowRecord> filas) =>
        PluginDictionary.Save(database, CarportRows.StorageKey, new ResultBuffer(
            CarportRows.Encode(filas).Select(c => new TypedValue((int)DxfCode.Text, c)).ToArray()));

    private static Guid? IdDaFila(Entity e) =>
        PluginXData.Load(e, TipoXData, VersaoXData, 1) is { } campos && Guid.TryParse(campos[0], out var id) ? id : null;

    /// <summary>Apaga as peças da fila; devolve quantas.</summary>
    private static int Apagar(Database database, Guid fila)
    {
        // O aço tem o id da fila; o contorno, os módulos, as faces e o que as
        // ferramentas de mesa penduraram nele (notas, tags, strings) têm o id
        // do vão como mesa.
        var vaos = Enumerable.Range(0, CarportRow.MaiorNumeroDeVaos).Select(v => CarportReturn.IdDoVao(fila, v)).ToHashSet();
        var apagadas = 0;
        using var transacao = database.TransactionManager.StartTransaction();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        foreach (ObjectId id in espaco)
        {
            if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            if (IdDaFila(e) != fila && !(LayoutScan.TableOf(e) is { } mesa && vaos.Contains(mesa))) continue;
            e.UpgradeOpen();
            e.Erase();
            apagadas++;
        }

        transacao.Commit();
        return apagadas;
    }

    /// <summary>O carport a desenhar: o único marcado, ou a escolha entre os marcados (sem marcado, entre todos).</summary>
    private static DrawingCarport? EscolherCarport(Editor editor, Database database)
    {
        var todos = CarportsDoDesenho.Ler(database, out var problemas);
        foreach (var p in problemas) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}.\n", p));

        if (todos.Count == 0)
        {
            editor.WriteMessage(Tr.T("\nNenhum carport neste desenho: cadastre um em Configurações › Estruturas › Nova estrutura... › Carport.\n"));
            return null;
        }

        var marcados = todos.Where(c => c.Use).ToList();
        var lista = marcados.Count > 0 ? marcados : todos.ToList();
        if (lista.Count == 1) return lista[0];

        for (var i = 0; i < lista.Count; i++) editor.WriteMessage($"\n  {i + 1} — {lista[i].Profile.Describe()}");
        var escolha = editor.GetInteger(new PromptIntegerOptions(Tr.F("\nQual carport (1 a {0})? <1>: ", lista.Count))
        {
            AllowNone = true, LowerLimit = 1, UpperLimit = lista.Count,
        });
        if (escolha.Status == PromptStatus.None) return lista[0];
        return escolha.Status == PromptStatus.OK ? lista[escolha.Value - 1] : null;
    }

    /// <summary>Desenha a fila em sólidos: módulos com a espessura deles, aço na cor do carport, bases em concreto.</summary>
    private static int Desenhar(Database database, DrawingCarport carport, Guid idDaFila, CarportRowResult fila, int numeroDaFila)
    {
        var corDoAco = Color.FromRgb(CarportMesh.CorDoAco.R, CarportMesh.CorDoAco.G, CarportMesh.CorDoAco.B);
        var corDoModulo = Color.FromRgb(CarportMesh.CorDoModulo.R, CarportMesh.CorDoModulo.G, CarportMesh.CorDoModulo.B);
        var espessura = carport.Profile.Array.Module.Thickness;
        var pecas = 0;

        using var transacao = database.TransactionManager.StartTransaction();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var camadaAco = LayoutLayers.Garantir(transacao, database, CamadaAco, new RgbColor(150, 160, 170));
        var camadaModulo = LayoutLayers.Garantir(transacao, database, CamadaModulo, CarportMesh.CorDoModulo);
        var camadaBase = LayoutLayers.Garantir(transacao, database, CamadaBase, CarportMesh.CorDaBase);
        var camadaMesa = LayoutLayers.Garantir(transacao, database, CamadaMesa, carport.Color);

        // Cada vão é uma "mesa" (28/10/2026, Renan: "padronizar IGUALZINHO as
        // mesas"): um grupo com o pórtico do começo (o último vão leva também o
        // do fim), as terças, os módulos e o contorno da cobertura; um clique
        // pega o vão inteiro, como na fixa.
        var pecasDoVao = Enumerable.Range(0, fila.Bays.Count).Select(_ => new List<ObjectId>()).ToList();
        int VaoDoPortico(int k) => Math.Min(k, fila.Bays.Count - 1);

        void Por(Entity entidade, string oQue, int vao)
        {
            pecasDoVao[vao].Add(espaco.AppendEntity(entidade));
            transacao.AddNewlyCreatedDBObject(entidade, true);
            PluginXData.Save(transacao, entidade, TipoXData, VersaoXData, idDaFila.ToString("D"), carport.Name, oQue,
                (vao + 1).ToString(CultureInfo.InvariantCulture));
            pecas++;
        }

        for (var k = 0; k < fila.Portals.Count; k++)
        {
            var portico = fila.Portals[k];
            foreach (var barra in portico.Bars)
                if (Barra(barra) is { } solido)
                {
                    solido.Layer = camadaAco;
                    solido.Color = corDoAco;
                    Por(solido, barra.Part.ToString(), VaoDoPortico(k));
                }

            foreach (var bloco in portico.Blocks)
            {
                var caixa = new Solid3d { Layer = camadaBase, Color = bloco.Part == CarportPart.BasePlate ? corDoAco : Color.FromColorIndex(ColorMethod.ByLayer, 256) };
                caixa.CreateBox(bloco.Size, bloco.Size, bloco.Height);
                var x = new Vector3d(bloco.Along.X, bloco.Along.Y, 0).GetNormal();
                caixa.TransformBy(Matrix3d.AlignCoordinateSystem(
                    Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis,
                    new Point3d(bloco.BottomCenter.X, bloco.BottomCenter.Y, bloco.BottomCenter.Z + bloco.Height / 2),
                    x, Vector3d.ZAxis.CrossProduct(x), Vector3d.ZAxis));
                Por(caixa, bloco.Part.ToString(), VaoDoPortico(k));
            }
        }

        for (var v = 0; v < fila.Bays.Count; v++)
        {
            var vao = fila.Bays[v];
            foreach (var terca in vao.Purlins)
                if (Barra(terca) is { } solido)
                {
                    solido.Layer = camadaAco;
                    solido.Color = corDoAco;
                    Por(solido, terca.Part.ToString(), v);
                }

        }

        // Cada vão é uma MESA para o resto do plugin (28/10/2026, Renan: "todas
        // ferramentas tem que funcionar com a mesa, o resumo, os grupos,
        // recalcular"): o contorno com a identidade de mesa (potência, rótulo,
        // o nome do carport como perfil), os módulos no bloco de módulo da fixa
        // e a face de cima, com as mesmas identidades. Grupo, Resumo, kWp,
        // numeração, strings, sombras e PVsyst leem o vão como leem a mesa.
        var modulo = carport.Profile.Array.Module;
        var blocoDoModulo = LayoutBlocks.GarantirModulo(transacao, database, modulo);
        var camadaDoModulo = LayoutLayers.Garantir(transacao, database, LayoutLayers.Modulo, new RgbColor(30, 60, 140));
        var camadaDaFace = LayoutLayers.Garantir(transacao, database, LayoutLayers.Face, new RgbColor(60, 120, 220));
        var corDoTipo = Color.FromRgb(carport.Color.R, carport.Color.G, carport.Color.B);
        var paisagem = carport.Profile.Array.Orientation == CarportOrientation.Landscape;
        var vaos = CarportReturn.Tables(idDaFila, carport.Name, carport.Profile, fila).ToList();

        for (var v = 0; v < vaos.Count && v < pecasDoVao.Count; v++)
        {
            var mesa = vaos[v];
            var cantos = mesa.Corners.Select(c => new Point3d(c.E, c.N, c.Z)).ToList();
            var identidade = new TableIdentity(
                mesa.Id, Tr.F("C{0}.{1}", numeroDaFila, v + 1), cantos[0].Z, cantos[1].Z, carport.Profile.TiltRadians,
                Marked: false, Reason: null, ModulePowerWatts: modulo.PowerWatts,
                Anchor: new Clivus.Geo.Point3(cantos[0].X, cantos[0].Y, cantos[0].Z), ProfileName: carport.Name);

            foreach (var m in fila.Bays[v].Modules)
            {
                var c = m.Corners.Select(q => new Point3d(q.X, q.Y, q.Z)).ToArray();
                var ao = (c[1] - c[0]).GetNormal();
                var sobe = (c[3] - c[0]).GetNormal();
                var normal = ao.CrossProduct(sobe).GetNormal();

                // O bloco da fixa tem a largura em X e a altura em Y: em retrato
                // X corre com a fila; em paisagem, X sobe a rampa a partir do
                // canto 1 e Y volta para o canto 0.
                var matriz = paisagem
                    ? Matrix3d.AlignCoordinateSystem(Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis, c[1], sobe, -ao, normal)
                    : Matrix3d.AlignCoordinateSystem(Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis, c[0], ao, sobe, normal);

                var bloco = new BlockReference(Point3d.Origin, blocoDoModulo) { BlockTransform = matriz, Layer = camadaDoModulo, Color = corDoTipo };
                pecasDoVao[v].Add(espaco.AppendEntity(bloco));
                transacao.AddNewlyCreatedDBObject(bloco, true);
                var idDoModulo = new ModuleIdentity(Guid.NewGuid(), mesa.Id, m.Column, m.Row, null);
                LayoutXData.SaveModule(transacao, bloco, idDoModulo);

                var face = new Face(c[0], c[1], c[2], c[3], true, true, true, true) { Layer = camadaDaFace };
                pecasDoVao[v].Add(espaco.AppendEntity(face));
                transacao.AddNewlyCreatedDBObject(face, true);
                LayoutXData.SaveFace(transacao, face, new FaceIdentity(Guid.NewGuid(), idDoModulo.Id, mesa.Id, m.Column, m.Row));
                pecas += 2;
            }

            var contorno = new Polyline3d { Closed = true, Layer = camadaMesa, Color = corDoTipo };
            pecasDoVao[v].Add(espaco.AppendEntity(contorno));
            transacao.AddNewlyCreatedDBObject(contorno, true);
            foreach (var p in cantos)
            {
                var vertice = new PolylineVertex3d(p);
                contorno.AppendVertex(vertice);
                transacao.AddNewlyCreatedDBObject(vertice, true);
            }

            LayoutXData.SaveTable(transacao, contorno, identidade);
            pecas++;

            LayoutGroups.Criar(transacao, database, pecasDoVao[v]);
        }

        transacao.Commit();
        return pecas;
    }

    /// <summary>O módulo como placa: a face de cima dos cantos, a espessura para baixo.</summary>
    private static Solid3d? Modulo(CarportModule m, double espessura)
    {
        var c = m.Corners.Select(q => new Point3d(q.X, q.Y, q.Z)).ToArray();
        var x = c[1] - c[0];
        var y = c[3] - c[0];
        if (x.Length < 1e-6 || y.Length < 1e-6) return null;

        var z = x.CrossProduct(y).GetNormal();
        if (z.Z < 0) z = -z;
        var meio = c[0] + x / 2 + y / 2 - z * (espessura / 2);

        var placa = new Solid3d();
        placa.CreateBox(x.Length, y.Length, espessura);
        var ux = x.GetNormal();
        placa.TransformBy(Matrix3d.AlignCoordinateSystem(
            Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis, meio, ux, z.CrossProduct(ux), z));
        return placa;
    }

    /// <summary>A barra como sólido: caixa do comprimento do eixo, na largura e na altura da seção.</summary>
    private static Solid3d? Barra(CarportBar barra)
    {
        var inicio = new Point3d(barra.Start.X, barra.Start.Y, barra.Start.Z);
        var fim = new Point3d(barra.End.X, barra.End.Y, barra.End.Z);
        var eixo = fim - inicio;
        if (eixo.Length < 1e-6) return null;

        var x = eixo.GetNormal();
        var altura = new Vector3d(barra.DepthDirection.X, barra.DepthDirection.Y, barra.DepthDirection.Z);
        var z = altura - x * altura.DotProduct(x);
        if (z.Length < 1e-9) z = x.GetPerpendicularVector();
        z = z.GetNormal();
        var y = z.CrossProduct(x);

        var solido = new Solid3d();
        solido.CreateBox(eixo.Length, barra.Section.Width, barra.Section.Depth);
        solido.TransformBy(Matrix3d.AlignCoordinateSystem(
            Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis, inicio + eixo / 2, x, y, z));
        return solido;
    }
}
