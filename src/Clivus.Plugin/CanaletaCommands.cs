using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.CanaletaCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Desenho › Canaleta (28): converter linha em canaleta (ou trocar a seção da
/// canaleta), voltar a linha e ler a seção de uma canaleta para a janela.
/// </summary>
public static class CanaletaCommands
{
    private const string Borda = "Borda";
    private const string Minima = "Minima";

    /// <summary>CLIVUS_DRN_CANALETA_CONVERTER: seção, assentamento [Borda/Minima] e a seleção.</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemCanaletaConverter, CommandFlags.UsePickSet)]
    public static void Converter()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var configuracao = DrenagemStore.Ler(documento.Database);

            var nome = editor.GetString(new PromptStringOptions(Tr.F("\nSeção da canaleta <{0}>: ", configuracao.CurrentSection)) { AllowSpaces = true });
            if (nome.Status != PromptStatus.OK) return;

            var escolhida = string.IsNullOrWhiteSpace(nome.StringResult) ? configuracao.CurrentSection : nome.StringResult.Trim();
            var secao = configuracao.Section(escolhida);
            if (secao is null)
            {
                editor.WriteMessage(Tr.F("\nCANALETA Não há a seção \"{0}\" na biblioteca ({1}).\n", escolhida, string.Join(", ", configuracao.Sections.Select(s => s.Name))));
                return;
            }

            if (secao.Problem() is { } problema)
            {
                editor.WriteMessage($"\nCANALETA {secao.Name}: {problema}\n");
                return;
            }

            var modoOpcoes = new PromptKeywordOptions(Tr.T("\nAssentamento"));
            modoOpcoes.Keywords.Add(Borda);
            modoOpcoes.Keywords.Add(Minima);
            modoOpcoes.Keywords.Default = configuracao.CurrentMode == ChannelBottomMode.MinimumSlope ? Minima : Borda;
            modoOpcoes.AllowNone = true;

            var modoResposta = editor.GetKeywords(modoOpcoes);
            if (modoResposta.Status is not (PromptStatus.OK or PromptStatus.None)) return;
            var modo = (string.IsNullOrEmpty(modoResposta.StringResult) ? modoOpcoes.Keywords.Default : modoResposta.StringResult) == Minima
                ? ChannelBottomMode.MinimumSlope : ChannelBottomMode.FollowTerrain;

            var selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.T("\nEscolha as linhas (de análise, canaletas ou polilinhas): ") },
                new SelectionFilter([new TypedValue((int)DxfCode.Start, "LINE,LWPOLYLINE,POLYLINE,ARC,SPLINE")]));
            if (selecao.Status != PromptStatus.OK) return;

            DrenagemStore.Mudar(documento.Database, d => d with { CurrentSection = secao.Name, CurrentMode = modo });

            editor.WriteMessage($"\n{Aplicar(documento, terreno.Mesh, secao, modo, selecao.Value.GetObjectIds())}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao converter em canaleta.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui fazer a canaleta: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// Cada entidade escolhida vira canaleta com a seção: a linha de análise
    /// (o GUID fica), a canaleta (troca a seção) ou uma polilinha qualquer
    /// (vira canaleta nova no mesmo traçado; a original sai, pedido explícito).
    /// </summary>
    internal static string Aplicar(Document documento, Tin terreno, ChannelSection secao, ChannelBottomMode modo, IEnumerable<ObjectId> ids)
    {
        var configuracao = DrenagemStore.Ler(documento.Database);
        var feitas = new List<string>();
        var alertas = new List<string>();

        using var transacao = documento.Database.TransactionManager.StartTransaction();

        var nomes = LinhasDeDrenagem.Todas(transacao, documento.Database).Select(l => l.Identidade.Name).ToList();
        var camada = LayoutLayers.GarantirDrenagem(transacao, documento.Database, LayoutLayers.DrenagemCanaleta);
        var indice = IndiceDaDrenagem.Montar(transacao, documento.Database);

        foreach (var id in ids)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Curve curva) continue;

            ObjectId linhaId;

            if (curva is Polyline3d p && LinhasDeDrenagem.Identidade(p) is { } identidade)
            {
                var nome = identidade.IsChannel ? identidade.Name : DrainageLineIdentity.NextName(Tr.T("Canaleta"), nomes);
                nomes.Add(nome);

                p.UpgradeOpen();
                p.Layer = camada;
                LinhasDeDrenagem.Gravar(transacao, p, identidade with { Name = nome, Section = secao, Mode = modo });
                linhaId = id;
            }
            else
            {
                // Só curva solta do usuário: o que já é do Clivus (contorno de
                // mesa, área, vala, traço do gráfico) nunca vira canaleta.
                using (var nosso = curva.GetXDataForApplication(PluginXData.Aplicativo))
                    if (nosso is not null) continue;

                var caminho = LinhaCommands.Caminho(documento.Database, id);
                if (caminho.Count < 2) continue;

                var nome = DrainageLineIdentity.NextName(Tr.T("Canaleta"), nomes);
                nomes.Add(nome);

                (linhaId, _) = LinhasDeDrenagem.Criar(transacao, documento.Database, terreno, caminho,
                    new DrainageLineIdentity { Name = nome, Section = secao, Mode = modo });

                transacao.GetObject(id, OpenMode.ForWrite).Erase();
            }

            var (frase, alerta) = LinhasDeDrenagem.Refazer(transacao, documento.Database, terreno, configuracao, linhaId, indice);
            feitas.Add(frase);
            if (alerta.Length > 0) alertas.Add(alerta);
        }

        transacao.Commit();

        if (feitas.Count == 0) return Tr.T("CANALETA Nenhuma linha escolhida.");

        var texto = Tr.F("CANALETA {0} canaleta(s) {1} ({2}): {3}", feitas.Count, secao.Name,
            modo == ChannelBottomMode.MinimumSlope ? Tr.T("fundo com declividade mínima") : Tr.T("borda no terreno"), string.Join(" ", feitas));

        return alertas.Count == 0 ? texto : texto + "\n" + Tr.F("CANALETA ATENÇÃO: {0}", string.Join(" ", alertas));
    }

    /// <summary>CLIVUS_DRN_CANALETA_DESFAZER: as canaletas escolhidas voltam a ser linha de análise.</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemCanaletaDesfazer, CommandFlags.UsePickSet)]
    public static void Desfazer()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.T("\nEscolha as canaletas que voltam a ser linha: ") },
                new SelectionFilter([new TypedValue((int)DxfCode.Start, "POLYLINE")]));
            if (selecao.Status != PromptStatus.OK) return;

            var configuracao = DrenagemStore.Ler(documento.Database);
            var voltaram = 0;

            using (var transacao = documento.Database.TransactionManager.StartTransaction())
            {
                var nomes = LinhasDeDrenagem.Todas(transacao, documento.Database).Select(l => l.Identidade.Name).ToList();
                var camada = LayoutLayers.GarantirDrenagem(transacao, documento.Database, LayoutLayers.DrenagemLinha);
                var indice = IndiceDaDrenagem.Montar(transacao, documento.Database);

                foreach (var id in selecao.Value.GetObjectIds())
                {
                    // Lê primeiro; só a canaleta é aberta para escrever (camada travada não derruba o resto).
                    if (transacao.GetObject(id, OpenMode.ForRead) is not Polyline3d lida || LinhasDeDrenagem.Identidade(lida) is not { IsChannel: true } identidade) continue;

                    var nome = DrainageLineIdentity.NextName(Tr.T("Linha"), nomes);
                    nomes.Add(nome);

                    var p = (Polyline3d)transacao.GetObject(id, OpenMode.ForWrite, false, true);
                    p.Layer = camada;
                    LinhasDeDrenagem.Gravar(transacao, p, identidade with { Name = nome, Section = null });
                    LinhasDeDrenagem.Refazer(transacao, documento.Database, terreno.Mesh, configuracao, id, indice);
                    voltaram++;
                }

                transacao.Commit();
            }

            editor.WriteMessage(Tr.F("\nCANALETA {0} canaleta(s) de volta a linha de análise (o 3D saiu).\n", voltaram));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao voltar a canaleta a linha.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui voltar a linha: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_DRN_CANALETA_LER: a seção da canaleta clicada vai para a janela (e para a linha de comando).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemCanaletaLer)]
    public static void Ler()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var r = EscolhaDeCanaleta.Pedir(documento, Tr.T("\nClique na canaleta: "), true);
        if (r.Status != PromptStatus.OK) return;

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        if (transacao.GetObject(r.Id, OpenMode.ForRead) is not Polyline3d p || LinhasDeDrenagem.Identidade(p) is not { Section: { } secao } identidade)
        {
            editor.WriteMessage(Tr.T("\nCANALETA Isso não é uma canaleta.\n"));
            return;
        }

        editor.WriteMessage(Tr.F("\nCANALETA {0}: {1}\n", identidade.Name, Descrever(secao)));
        JanelaDeCanaleta.Carregar(documento, secao, identidade.Mode);
    }

    /// <summary>As medidas da seção, numa linha.</summary>
    internal static string Descrever(ChannelSection s) => s.Kind == ChannelKind.HalfPipe
        ? Tr.F("{0}: meia-cana r {1:0.###} m, e {2:0.###} m, B {3:0.###} m; {4} (n {5:0.###}, v máx {6:0.##} m/s)", s.Name, s.Radius, s.Thickness, s.TopWidth, s.Lining, s.Manning, s.MaxVelocity)
        : Tr.F("{0}: trapezoidal b {1:0.###} m, z {2:0.###}, y {3:0.###} m, x {4:0.###} m, B {5:0.###} m, e {6:0.###} m; {7} (n {8:0.###}, v máx {9:0.##} m/s)", s.Name, s.Base, s.SideSlope, s.Height, s.SideOpening, s.TopWidth, s.Thickness, s.Lining, s.Manning, s.MaxVelocity);
}
