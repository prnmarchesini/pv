using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.TrocarMesaCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Trocar mesa (9.2) e Regerar fileira (9.3). Renan, 03/10/2026: "clicar em
/// uma mesa e ter a opção de trocar por outra ... e escolher quantas colocar
/// no lugar ... eu só falo o lado que quero travar e o sistema refaz"; "se eu
/// trocar uma por duas é certeza que vai dar merda [no espaçamento], mas ok,
/// é problema meu, e em ambos os casos eu posso ter a opção de regerar
/// fileira, aí pelo menos tudo fica blzera".
///
/// Regerar fileira tem dois modos: <b>Manter</b> as mesas e os tipos que a
/// fileira tem e só acertar o espaçamento (o que serve depois de uma troca,
/// sem desfazê-la), ou <b>Motor</b>: a fileira nasce de novo com as mesas em
/// uso, como no Regerar área.
/// </summary>
public static class TrocarMesaCommands
{
    /// <summary>O que a janela (ou a linha de comando) escolheu.</summary>
    internal sealed record Escolha(int Tipo, int Quantas, SwapAnchor Lado, bool Reespacar);

    // ------------------------------------------------------------ trocar

    [CommandMethod(PluginInfo.ComandoTrocarMesa, CommandFlags.UsePickSet)]
    public static void Trocar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var guid = RecalcularCommands.MesaDaSelecao(editor, documento)
                ?? RecalcularCommands.MesaClicada(editor, documento, Tr.T("\nClique numa peça da mesa a trocar: "));
            if (guid is null) return;

            var mesas = MesasDoDesenho.Ler(documento.Database);

            if (mesas.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nTROCAR Este desenho não tem mesas cadastradas: cadastre em Configurações > Estruturas e salve no desenho.\n"));
                return;
            }

            var letreiro = Letreiro(documento, guid.Value);
            Escolha? escolha;

            if (ClivusExtension.TemInterface())
            {
                var janela = new JanelaDeTroca(mesas, letreiro);
                if (AcadApp.ShowModalWindow(janela) != true || janela.Escolhida is null) return;
                escolha = janela.Escolhida;
            }
            else
            {
                escolha = PerguntarNaLinha(editor, mesas);
                if (escolha is null) return;
            }

            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            Executar(editor, documento, terreno, guid.Value, mesas[escolha.Tipo], escolha.Quantas, escolha.Lado, escolha.Reespacar);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao trocar a mesa.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui trocar a mesa: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_TROCAR_MESA_AUTO: letreiro, número do tipo na lista do desenho, quantas, lado e se reespaça. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoTrocarMesaAutomatico)]
    public static void TrocarAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var letreiro = editor.GetString(new PromptStringOptions(Tr.T("\nLetreiro da mesa: ")) { AllowSpaces = false });
            if (letreiro.Status != PromptStatus.OK) return;

            var mesas = MesasDoDesenho.Ler(documento.Database);
            var escolha = PerguntarNaLinha(editor, mesas);
            if (escolha is null) return;

            var guid = PeloLetreiro(documento, letreiro.StringResult);

            if (guid is null)
            {
                editor.WriteMessage(Tr.F("\nTROCAR Não há mesa com o letreiro \"{0}\".\n", letreiro.StringResult));
                return;
            }

            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            Executar(editor, documento, terreno, guid.Value, mesas[escolha.Tipo], escolha.Quantas, escolha.Lado, escolha.Reespacar);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao trocar a mesa (automático).", erro);
            editor.WriteMessage(Tr.F("\nNão consegui trocar a mesa: {0}\n", erro.Message));
        }
    }

    /// <summary>Tipo, quantidade, lado e reespaçar pela linha de comando.</summary>
    private static Escolha? PerguntarNaLinha(Editor editor, IReadOnlyList<DrawingTable> mesas)
    {
        if (mesas.Count == 0)
        {
            editor.WriteMessage(Tr.T("\nTROCAR Este desenho não tem mesas cadastradas.\n"));
            return null;
        }

        for (var i = 0; i < mesas.Count; i++)
            editor.WriteMessage(Tr.F("\n  {0}: {1} ({2} módulos)", i + 1, mesas[i].Name, mesas[i].Profile.Layout.ModuleCount));

        var tipo = editor.GetInteger(new PromptIntegerOptions(Tr.F("\nMesa nova (1 a {0}): ", mesas.Count)) { LowerLimit = 1, UpperLimit = mesas.Count });
        if (tipo.Status != PromptStatus.OK) return null;

        var quantas = editor.GetInteger(new PromptIntegerOptions(Tr.T("\nQuantas no lugar (1 a 5): ")) { LowerLimit = 1, UpperLimit = 5, DefaultValue = 1, UseDefaultValue = true });
        if (quantas.Status != PromptStatus.OK) return null;

        var lado = new PromptKeywordOptions(Tr.T("\nLado travado [Inicio/Fim]: "), "Inicio Fim") { AllowNone = false };
        var respostaLado = editor.GetKeywords(lado);
        if (respostaLado.Status != PromptStatus.OK) return null;

        var reespacar = new PromptKeywordOptions(Tr.T("\nReespaçar a fileira depois? [Sim/Nao]: "), "Sim Nao") { AllowNone = false };
        var respostaReespacar = editor.GetKeywords(reespacar);
        if (respostaReespacar.Status != PromptStatus.OK) return null;

        return new Escolha(
            tipo.Value - 1, quantas.Value,
            respostaLado.StringResult == "Fim" ? SwapAnchor.End : SwapAnchor.Start,
            respostaReespacar.StringResult == "Sim");
    }

    /// <summary>
    /// Troca a mesa: as novas no lugar dela, encostadas no lado travado,
    /// assentadas no terreno; a antiga sai. Diz quanto passou da vizinha e,
    /// se pedido, reespaça a fileira mantendo as mesas.
    /// </summary>
    internal static bool Executar(
        Editor editor, Document documento, ProcessedTerrain terreno, Guid guid, DrawingTable nova, int quantas, SwapAnchor lado, bool reespacar)
    {
        var database = documento.Database;
        var lida = Ler(documento);

        if (!lida.Mesas.TryGetValue(guid, out var mesa) || mesa.Identity is null || !lida.Cantos.TryGetValue(guid, out var cantos))
        {
            editor.WriteMessage(Tr.T("\nTROCAR A mesa não tem contorno; não há como saber onde ela está.\n"));
            return false;
        }

        if (mesa.IsDuplicated)
        {
            editor.WriteMessage(Tr.F("\nTROCAR {0} tem contornos repetidos (mesa copiada). Apague a cópia ou use o Regerar área.\n", mesa.Identity.Label));
            return false;
        }

        var celula = Celula(cantos, mesa.Identity, database, nova.Profile);
        if (celula is null)
        {
            editor.WriteMessage(Tr.F("\nTROCAR {0}: o contorno não descreve uma mesa. Use o Regerar área.\n", mesa.Identity.Label));
            return false;
        }

        var outras = lida.Cantos.Where(c => c.Key != guid).Select(c => (IReadOnlyList<Point3>)c.Value);
        var vao = TableSwap.RoomBeyond(celula, outras, lado);

        var perfil = nova.Profile;
        var geometria = FileiraCommands.GeometriaDe(perfil);
        var doProjeto = ConfigCommands.Inicial(documento, out var aviso);
        if (aviso is not null) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}\n", aviso));
        var settings = doProjeto.ForTable(perfil.Frame);

        var pegada = new TableFootprint(geometria.Length, geometria.Depth * Math.Cos(perfil.TiltRadians));
        var troca = TableSwap.Plan(celula, pegada, kind: 0, quantas, settings.Configuration.TableGap, lado, vao);

        var fileira = RowPipeline.ProcessRow(new PlanRow(celula.Row, troca.Tables), geometria, perfil.TiltRadians, terreno.Mesh, settings);

        // Como o desenho está (cores, cotas, seta), lido antes de mexer.
        var analise = LayoutDrawer.Analise.ComoODesenho(database);

        // Desenha antes de apagar: se o desenho falhar (camada travada), a
        // mesa antiga continua lá.
        var desenho = LayoutDrawer.Draw(
            database, fileira, geometria, perfil.Layout.Module, perfil.TiltRadians, settings.Analyses, analisar: analise,
            tipos: LayoutDrawer.TiposDeMesa.DaMesa(database, nova.Name, geometria, perfil.Layout.Module));

        RecalcularCommands.Apagar(documento, mesa);

        foreach (var a in fileira.Warnings) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}\n", a));

        var magenta = desenho.Marked > 0 ? Tr.F(", {0} com módulo dentro da terra (magenta)", desenho.Marked) : string.Empty;
        var novas = string.Join(", ", troca.Tables.Select(t => t.Label));

        editor.WriteMessage(lado == SwapAnchor.Start
            ? Tr.F("\nTROCAR {0} virou {1} × {2} ({3}), travada no início: {4} mesa(s), {5} módulo(s){6}.\n", mesa.Identity.Label, quantas, nova.Name, novas, desenho.Tables, desenho.Modules, magenta)
            : Tr.F("\nTROCAR {0} virou {1} × {2} ({3}), travada no fim: {4} mesa(s), {5} módulo(s){6}.\n", mesa.Identity.Label, quantas, nova.Name, novas, desenho.Tables, desenho.Modules, magenta));

        AvisarForaDaArea(editor, database, troca.Tables, Tr.T("TROCAR"));

        if (troca.Overflows && !reespacar)
        {
            editor.WriteMessage(
                Tr.F("  ATENÇÃO: as mesas novas passam {0:0.00} m do espaço até a vizinha (com o espaçamento de {1:0.00} m). Use Regerar fileira > Manter para acertar.\n", troca.Overflow, settings.Configuration.TableGap));
        }

        if (reespacar) Regerar(editor, documento, terreno, troca.Tables[0].Label, troca.Tables[0].Corners, manter: true, alinhamento: null);

        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
        return true;
    }

    // ------------------------------------------------------ regerar fileira

    [CommandMethod(PluginInfo.ComandoRegerarFileira, CommandFlags.UsePickSet)]
    public static void RegerarFileira()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var guid = RecalcularCommands.MesaDaSelecao(editor, documento)
                ?? RecalcularCommands.MesaClicada(editor, documento, Tr.T("\nClique numa peça de uma mesa da fileira: "));
            if (guid is null) return;

            var modo = new PromptKeywordOptions(
                Tr.T("\nRegerar a fileira [Manter as mesas e acertar o espaçamento/Motor com as mesas em uso] <Manter>: "), "Manter Motor")
            { AllowNone = true };
            var resposta = editor.GetKeywords(modo);
            if (resposta.Status != PromptStatus.OK && resposta.Status != PromptStatus.None) return;

            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var lida = Ler(documento);
            if (!lida.Cantos.TryGetValue(guid.Value, out var cantos) || lida.Mesas[guid.Value].Identity is not { } identidade)
            {
                editor.WriteMessage(Tr.T("\nREGERAR FILEIRA A mesa não tem contorno; não há como saber onde ela está.\n"));
                return;
            }

            Regerar(editor, documento, terreno, identidade.Label, cantos, manter: resposta.StringResult != "Motor", alinhamento: null);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao regerar a fileira.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui regerar a fileira: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_REGERAR_FILEIRA_AUTO: letreiro e modo (Manter/Motor), o primeiro alinhamento. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoRegerarFileiraAutomatico)]
    public static void RegerarFileiraAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var letreiro = editor.GetString(new PromptStringOptions(Tr.T("\nLetreiro de uma mesa da fileira: ")) { AllowSpaces = false });
            if (letreiro.Status != PromptStatus.OK) return;

            var modo = editor.GetKeywords(new PromptKeywordOptions(Tr.T("\nModo [Manter/Motor]: "), "Manter Motor"));
            if (modo.Status != PromptStatus.OK) return;

            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var guid = PeloLetreiro(documento, letreiro.StringResult);
            var lida = Ler(documento);

            if (guid is null || !lida.Cantos.TryGetValue(guid.Value, out var cantos))
            {
                editor.WriteMessage(Tr.F("\nREGERAR FILEIRA Não há mesa com o letreiro \"{0}\".\n", letreiro.StringResult));
                return;
            }

            var alinhamentos = AlignmentStore.Load(documento.Database);
            var alinhamento = alinhamentos.Count > 0 ? FileiraCommands.LerAlinhamento(documento.Database, alinhamentos[0]) : null;

            Regerar(editor, documento, terreno, letreiro.StringResult, cantos, manter: modo.StringResult == "Manter", alinhamento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao regerar a fileira (automático).", erro);
            editor.WriteMessage(Tr.F("\nNão consegui regerar a fileira: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// Regera a fileira da mesa de cantos <paramref name="cantosDaMesa"/>.
    /// Manter: as mesas da fileira (a faixa dela, dentro da mesma área)
    /// reespaçadas, cada uma com o tipo dela. Motor: a área é planejada de
    /// novo e só a fileira que passa pela mesa é desenhada.
    /// </summary>
    private static void Regerar(
        Editor editor, Document documento, ProcessedTerrain terreno, string letreiro, IReadOnlyList<Point3> cantosDaMesa, bool manter,
        (IReadOnlyList<Point3> Vertices, AlignmentIdentity Identidade)? alinhamento)
    {
        var database = documento.Database;
        var centro = new Point3(cantosDaMesa.Average(p => p.X), cantosDaMesa.Average(p => p.Y), 0);

        var area = AreaDe(database, centro);

        if (area is null)
        {
            editor.WriteMessage(Tr.F("\nREGERAR FILEIRA A mesa {0} não está dentro de nenhuma área registrada.\n", letreiro));
            return;
        }

        var doProjeto = ConfigCommands.Inicial(documento, out _);
        var config = doProjeto.Configuration;
        var lida = Ler(documento);

        // A faixa da fileira: o lado da mesa clicada, meia distância entre
        // fileiras para cada lado; e dentro da mesma área.
        var a = cantosDaMesa[0];
        var dx = cantosDaMesa[1].X - a.X;
        var dy = cantosDaMesa[1].Y - a.Y;
        var n = Math.Sqrt(dx * dx + dy * dy);
        if (n < 1e-9) return;
        var (nx, ny) = (-dy / n, dx / n);
        var fundo = (cantosDaMesa[3].X - a.X) * nx + (cantosDaMesa[3].Y - a.Y) * ny;
        if (fundo < 0) (nx, ny, fundo) = (-nx, -ny, -fundo);

        double Lado(Point3 p) => (p.X - a.X) * nx + (p.Y - a.Y) * ny;

        var meio = fundo / 2;
        var daFileira = lida.Cantos
            .Where(c =>
            {
                var cx = c.Value.Average(p => p.X);
                var cy = c.Value.Average(p => p.Y);
                return Math.Abs(Lado(new Point3(cx, cy, 0)) - meio) < config.Pitch / 2
                    && Polygons.Contains(area.Value.Vertices, cx, cy);
            })
            .Select(c => c.Key)
            .ToList();

        if (manter)
        {
            Reespacar(editor, documento, terreno, lida, daFileira, letreiro);
        }
        else
        {
            // A fileira do motor precisa cair na mesma faixa: sem isso, a
            // fileira vizinha do plano seria desenhada por cima de outra.
            bool NaFaixa(ProcessedRow fileira) =>
                fileira.Tables.Any(m => Math.Abs(Lado(new Point3(m.Cell.Corners.Average(p => p.X), m.Cell.Corners.Average(p => p.Y), 0)) - meio) < config.Pitch / 2);

            PeloMotor(editor, documento, terreno, area.Value, alinhamento, lida, daFileira, centro, letreiro, NaFaixa);
        }
    }

    /// <summary>Reespaça as mesas da fileira, cada uma com o tipo dela.</summary>
    private static void Reespacar(Editor editor, Document documento, ProcessedTerrain terreno, Leitura lida, IReadOnlyList<Guid> daFileira, string letreiro)
    {
        var database = documento.Database;
        var doDesenho = MesasDoDesenho.Ler(database);
        var padrao = doDesenho.Count > 0 ? doDesenho[0].Profile : MesaCommands.MesaDeExemplo();

        // Os tipos que a fileira tem, na ordem em que aparecem.
        var tipos = new List<DrawingTable>();
        var celulas = new List<PlacedTable>();
        var partes = new List<TableParts>();

        // Mesa da faixa que não dá para ler (copiada, sem identidade): nada é
        // feito, para não desenhar por cima de algo que ficaria.
        var ruins = daFileira.Count(g => lida.Mesas[g].Identity is null || lida.Mesas[g].IsDuplicated);
        if (ruins > 0)
        {
            editor.WriteMessage(Tr.F("\nREGERAR FILEIRA {0} mesa(s) da fileira de {1} sem identidade ou com contorno repetido (copiada). Use Validar ou o Regerar área. Nada foi mexido.\n", ruins, letreiro));
            return;
        }

        foreach (var guid in daFileira)
        {
            var mesa = lida.Mesas[guid];

            var perfil = FileiraCommands.PerfilDaMesaDesenhada(lida.Cantos[guid], padrao, mesa.Identity!.ProfileName, database);
            var registro = DrawingTables.Find(doDesenho, perfil.Name) ?? new DrawingTable(perfil, new RgbColor(150, 150, 150), Use: true);

            var k = tipos.FindIndex(t => ReferenceEquals(t.Profile, registro.Profile) || t.Name == registro.Name);
            if (k < 0)
            {
                tipos.Add(registro);
                k = tipos.Count - 1;
            }

            var celula = Celula(lida.Cantos[guid], mesa.Identity!, database, perfil);
            if (celula is null)
            {
                editor.WriteMessage(Tr.F("\nREGERAR FILEIRA O contorno de {0} não descreve uma mesa. Use o Regerar área. Nada foi mexido.\n", mesa.Identity!.Label));
                return;
            }

            celulas.Add(celula with { Kind = k });
            partes.Add(mesa);
        }

        if (celulas.Count == 0)
        {
            editor.WriteMessage(Tr.F("\nREGERAR FILEIRA Não achei as mesas da fileira de {0}.\n", letreiro));
            return;
        }

        // Inclinações diferentes na mesma fileira não têm conta comum (o fundo
        // em planta e a cota dependem dela): recusado, dito.
        if (tipos.Select(t => Math.Round(t.Profile.TiltRadians, 6)).Distinct().Count() > 1)
        {
            editor.WriteMessage(Tr.F("\nREGERAR FILEIRA A fileira de {0} tem mesas de inclinações diferentes ({1}). Use o Regerar área. Nada foi mexido.\n", letreiro, string.Join(", ", tipos.Select(t => t.Name))));
            return;
        }

        var (desenhoDosTipos, pegadas, _) = UsinaCommands.Tipos(tipos);
        var doProjeto = ConfigCommands.Inicial(documento, out _);
        var settings = doProjeto.ForTable(tipos[0].Profile.Frame);
        var tilt = tipos[0].Profile.TiltRadians;

        var reespacadas = TableSwap.Respace(celulas, pegadas, settings.Configuration.TableGap, settings.Configuration.MaxGapBeforeBreak);
        var fileira = RowPipeline.ProcessRow(
            new PlanRow(reespacadas[0].Row, reespacadas), reespacadas.Select(c => desenhoDosTipos.Geometrias[c.Kind]).ToList(), tilt, terreno.Mesh, settings);

        var analise = LayoutDrawer.Analise.ComoODesenho(database);
        var guids = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var p in partes) guids.TryAdd(p.Identity!.Label, p.Identity.Id);

        // Mesmo GUID por letreiro: grupos e quantificações seguem a mesa. As
        // antigas são apagadas antes, porque têm os mesmos GUIDs.
        RecalcularCommands.Apagar(documento, partes);

        var desenho = LayoutDrawer.Draw(
            database, fileira, desenhoDosTipos.Geometrias[0], tipos[0].Profile.Layout.Module, tilt, settings.Analyses,
            idDaMesa: m => guids.TryGetValue(m.Label, out var g) ? g : Guid.NewGuid(), analisar: analise, tipos: desenhoDosTipos);

        AvisarForaDaArea(editor, database, reespacadas, Tr.T("REGERAR FILEIRA"));

        foreach (var a in fileira.Warnings) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}\n", a));

        editor.WriteMessage(
            Tr.F("\nREGERAR FILEIRA {0}: {1} mesa(s) reespaçada(s), mantidos os tipos ({2}); {3} módulo(s){4}.\n", fileira.Row.Number, desenho.Tables, string.Join(", ", tipos.Select(t => t.Name)), desenho.Modules, (desenho.Marked > 0 ? Tr.F(", {0} com módulo dentro da terra (magenta)", desenho.Marked) : string.Empty)));
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    /// <summary>A área planejada de novo; só a fileira que passa pela mesa é desenhada.</summary>
    private static void PeloMotor(
        Editor editor, Document documento, ProcessedTerrain terreno, (IReadOnlyList<Point3> Vertices, string Nome) area,
        (IReadOnlyList<Point3> Vertices, AlignmentIdentity Identidade)? alinhamento, Leitura lida, IReadOnlyList<Guid> daFileira,
        Point3 centro, string letreiro, Func<ProcessedRow, bool> naFaixa)
    {
        alinhamento ??= FileiraCommands.EscolherAlinhamento(editor, documento);
        if (alinhamento is null) return;

        var plano = UsinaCommands.Planejar(
            editor, documento, terreno, area, alinhamento.Value, FileiraCommands.PerfilDaMesa(editor, silencioso: true), avisarSeJaHaMesas: false);

        if (plano is null)
        {
            editor.WriteMessage(Tr.T("\nREGERAR FILEIRA Nada foi apagado.\n"));
            return;
        }

        // A fileira do plano mais perto da mesa clicada.
        ProcessedRow? escolhida = null;
        var melhor = double.PositiveInfinity;

        foreach (var fileira in plano.Usina.Rows)
        {
            foreach (var mesa in fileira.Tables)
            {
                var c = mesa.Cell.Corners;
                var d = Math.Pow(c.Average(p => p.X) - centro.X, 2) + Math.Pow(c.Average(p => p.Y) - centro.Y, 2);
                if (d < melhor) (melhor, escolhida) = (d, fileira);
            }
        }

        if (escolhida is null || !naFaixa(escolhida))
        {
            editor.WriteMessage(Tr.F("\nREGERAR FILEIRA O motor não pôs fileira na faixa de {0} (a configuração mudou a distância entre fileiras?). Use o Regerar área. Nada foi apagado.\n", letreiro));
            return;
        }

        // Desenha antes de apagar: se o desenho falhar, a fileira antiga fica.
        var desenho = LayoutDrawer.Draw(
            documento.Database, escolhida, plano.Geometria, plano.Perfil.Layout.Module, plano.Perfil.TiltRadians, plano.Settings.Analyses,
            analisar: LayoutDrawer.Analise.Nada, tipos: plano.Tipos);

        RecalcularCommands.Apagar(documento, daFileira.Select(g => lida.Mesas[g]).ToList());

        foreach (var a in escolhida.Warnings) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}\n", a));

        editor.WriteMessage(
            Tr.F("\nREGERAR FILEIRA {0} pelo motor: {1} mesa(s) apagada(s), {2} desenhada(s), {3} módulo(s){4}.\n", escolhida.Row.Number, daFileira.Count, desenho.Tables, desenho.Modules, (desenho.Marked > 0 ? Tr.F(", {0} com módulo dentro da terra (magenta)", desenho.Marked) : string.Empty)));
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    /// <summary>Avisa as mesas que saíram da área (a troca e o reespaçar não olham o contorno dela).</summary>
    private static void AvisarForaDaArea(Editor editor, Database database, IReadOnlyList<PlacedTable> mesas, string prefixo)
    {
        if (mesas.Count == 0) return;

        var c = mesas[0].Corners;
        if (AreaDe(database, new Point3(c.Average(p => p.X), c.Average(p => p.Y), 0)) is not { } area) return;

        var fora = mesas.Where(m => m.Corners.Any(p => !Polygons.Contains(area.Vertices, p.X, p.Y))).Select(m => m.Label).ToList();

        if (fora.Count > 0)
            editor.WriteMessage(fora.Count == 1
                ? Tr.F("  ATENÇÃO: {0} {1} passa da borda da área {2}.\n", prefixo, string.Join(", ", fora), area.Nome)
                : Tr.F("  ATENÇÃO: {0} {1} passam da borda da área {2}.\n", prefixo, string.Join(", ", fora), area.Nome));
    }

    // ------------------------------------------------------------- leitura

    private sealed record Leitura(IReadOnlyDictionary<Guid, TableParts> Mesas, IReadOnlyDictionary<Guid, List<Point3>> Cantos);

    /// <summary>As mesas do desenho e os cantos do contorno de cada uma.</summary>
    private static Leitura Ler(Document documento)
    {
        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        var mesas = LayoutScan.Tables(transacao, documento.Database);
        var cantos = new Dictionary<Guid, List<Point3>>();

        foreach (var (guid, partes) in mesas)
        {
            if (partes.Contour is not { } id || id.IsErased) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Polyline3d polilinha) continue;

            var vertices = FileiraCommands.Vertices(polilinha, transacao).ToList();
            if (vertices.Count == 4) cantos[guid] = vertices;
        }

        return new Leitura(mesas, cantos);
    }

    /// <summary>A célula da mesa desenhada, pelo perfil dela; contorno deformado cai no que está desenhado.</summary>
    private static PlacedTable? Celula(IReadOnlyList<Point3> cantos, TableIdentity identidade, Database database, TableProfile padrao)
    {
        var perfil = FileiraCommands.PerfilDaMesaDesenhada(cantos, padrao, identidade.ProfileName, database);
        var geometria = FileiraCommands.GeometriaDe(perfil);

        try
        {
            return TableCells.FromCorners(cantos, identidade.Label, geometria.Length, geometria.Depth * Math.Cos(perfil.TiltRadians));
        }
        catch (ArgumentException)
        {
            try
            {
                return TableCells.FromDrawnCorners(cantos, identidade.Label);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    private static (IReadOnlyList<Point3> Vertices, string Nome)? AreaDe(Database database, Point3 ponto)
    {
        foreach (var registro in AreaStore.Load(database))
        {
            var area = FileiraCommands.LerArea(database, registro.Handle);
            if (area is { } a && Polygons.Contains(a.Vertices, ponto.X, ponto.Y)) return a;
        }

        return null;
    }

    private static string Letreiro(Document documento, Guid guid)
    {
        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();
        return LayoutScan.Tables(transacao, documento.Database).TryGetValue(guid, out var m) ? m.Identity?.Label ?? "?" : "?";
    }

    private static Guid? PeloLetreiro(Document documento, string letreiro)
    {
        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        return LayoutScan.Tables(transacao, documento.Database).Values
            .FirstOrDefault(m => string.Equals(m.Identity?.Label, letreiro.Trim(), StringComparison.OrdinalIgnoreCase))?.Identity?.Id;
    }
}
