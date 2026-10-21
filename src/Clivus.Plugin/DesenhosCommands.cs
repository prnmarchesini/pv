using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.DesenhosCommands))]

namespace Clivus.Plugin;

/// <summary>Desenhos (32.4 a 32.7): dissipador, caixa e Exportar canaletas.</summary>
public static class DesenhosCommands
{
    private const string Remover = "Remover";
    private const string Medidas = "Medidas";

    /// <summary>
    /// CLIVUS_DRN_DISSIPADOR: clique perto da ponta da canaleta onde vai o
    /// dissipador (a ponta mais perto do clique); [Remover] tira o da canaleta.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoDrenagemDissipador)]
    public static void Dissipador()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var remover = false;
            var r = EscolhaDeCanaleta.Pedir(documento, Tr.T("\nClique na canaleta, perto da ponta onde vai o dissipador"), true, Remover);
            if (r.Status == PromptStatus.Keyword)
            {
                remover = true;
                r = EscolhaDeCanaleta.Pedir(documento, Tr.T("\nClique na canaleta que perde o dissipador: "), true);
            }

            if (r.Status != PromptStatus.OK) return;

            var configuracao = DrenagemStore.Ler(documento.Database);
            string frase;

            using (var transacao = documento.Database.TransactionManager.StartTransaction())
            {
                if (transacao.GetObject(r.Id, OpenMode.ForRead) is not Polyline3d p || LinhasDeDrenagem.Identidade(p) is not { IsChannel: true } identidade)
                {
                    editor.WriteMessage(Tr.T("\nDISSIPADOR Isso não é uma canaleta.\n"));
                    return;
                }

                var ponta = DissipatorEnd.None;
                if (!remover)
                {
                    // A ponta mais perto do clique, na ordem do perfil (montante → jusante).
                    var linha = new LinhaNoDesenho(r.Id, identidade, LinhasDeDrenagem.Vertices(transacao, p));
                    var perfil = LinhasDeDrenagem.Perfil(terreno.Mesh, configuracao, linha).Canaleta;
                    if (perfil is null || perfil.Points.Count < 2) throw new ArgumentException(Tr.T("A canaleta não tem perfil (fora do terreno)."));

                    double D(ChannelProfilePoint q) => Math.Pow(q.X - r.Ponto.X, 2) + Math.Pow(q.Y - r.Ponto.Y, 2);
                    ponta = D(perfil.Points[^1]) <= D(perfil.Points[0]) ? DissipatorEnd.Downstream : DissipatorEnd.Upstream;
                }

                var polilinha = (Polyline3d)transacao.GetObject(r.Id, OpenMode.ForWrite, false, true);
                LinhasDeDrenagem.Gravar(transacao, polilinha, identidade with { Dissipator = ponta });
                LinhasDeDrenagem.Refazer(transacao, documento.Database, terreno.Mesh, configuracao, r.Id, IndiceDaDrenagem.Montar(transacao, documento.Database));
                transacao.Commit();

                var modelo = EnergyDissipator.Model(identidade.Section!.TopWidth) ?? Tr.T("acima do álbum");
                frase = ponta switch
                {
                    DissipatorEnd.None => Tr.F("DISSIPADOR tirado de {0}.", identidade.Name),
                    DissipatorEnd.Downstream => Tr.F("DISSIPADOR {0} na saída de {1} (abertura {2:0.00} m).", modelo, identidade.Name, identidade.Section.TopWidth),
                    _ => Tr.F("DISSIPADOR {0} na ponta de cima de {1} (abertura {2:0.00} m).", modelo, identidade.Name, identidade.Section.TopWidth),
                };
            }

            editor.WriteMessage($"\n{frase}\n");
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage($"\nDISSIPADOR {erro.Message}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no dissipador.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui pôr o dissipador: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_DRN_CAIXA: o ponto da caixa (vários, Enter sai); [Medidas] muda as medidas, [Remover] tira uma.</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemCaixa)]
    public static void Caixa()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            while (true)
            {
                var configuracao = DrenagemStore.Ler(documento.Database);
                var b = configuracao.Box;

                var pedido = new PromptPointOptions(Tr.F("\nPonto da caixa (interna {0:0.##} × {1:0.##} m, parede {2:0.##} m; Enter sai)", b.InnerLength, b.InnerWidth, b.Wall) + " [Medidas/Remover]: ")
                {
                    AllowNone = true,
                    AppendKeywordsToMessage = false,
                };
                pedido.Keywords.Add(Medidas);
                pedido.Keywords.Add(Remover);

                var r = editor.GetPoint(pedido);
                if (r.Status == PromptStatus.None) return;

                if (r.Status == PromptStatus.Keyword && r.StringResult == Medidas)
                {
                    double? Pedir(string texto, double atual)
                    {
                        var v = editor.GetDouble(new PromptDoubleOptions(Tr.F("\n{0} <{1:0.##}>: ", texto, atual)) { AllowNone = true, AllowNegative = false, AllowZero = false, DefaultValue = atual, UseDefaultValue = true });
                        return v.Status == PromptStatus.OK ? v.Value : null;
                    }

                    var comprimento = Pedir(Tr.T("Comprimento interno (m)"), b.InnerLength);
                    var largura = comprimento is null ? null : Pedir(Tr.T("Largura interna (m)"), b.InnerWidth);
                    var parede = largura is null ? null : Pedir(Tr.T("Parede (m)"), b.Wall);
                    if (parede is null) return;

                    var nova = b with { InnerLength = comprimento!.Value, InnerWidth = largura!.Value, Wall = parede.Value };
                    if (nova.Problem() is { } problema) throw new ArgumentException(problema);

                    DrenagemStore.Mudar(documento.Database, d => d with { Box = nova });
                    continue;
                }

                if (r.Status == PromptStatus.Keyword && r.StringResult == Remover)
                {
                    var qual = editor.GetEntity(new PromptEntityOptions(Tr.T("\nClique na caixa que sai: ")));
                    if (qual.Status != PromptStatus.OK) continue;

                    using var t = documento.Database.TransactionManager.StartTransaction();
                    var apagadas = CaixasDeDrenagem.Remover(t, documento.Database, qual.ObjectId);
                    t.Commit();
                    editor.WriteMessage(apagadas > 0 ? Tr.T("\nCAIXA removida.\n") : Tr.T("\nCAIXA Isso não é uma caixa.\n"));
                    continue;
                }

                if (r.Status != PromptStatus.OK) return;

                using var transacao = documento.Database.TransactionManager.StartTransaction();
                var frase = CaixasDeDrenagem.Inserir(transacao, documento.Database, terreno.Mesh, configuracao, r.Value.X, r.Value.Y);
                transacao.Commit();
                editor.WriteMessage($"\n{frase}\n");
            }
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage($"\nCAIXA {erro.Message}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na caixa.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui pôr a caixa: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_DRN_EXPORTAR: o Excel dos metros de cada canaleta e tipo, das caixas e dos dissipadores.</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemExportar)]
    public static void Exportar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            string? caminho;
            if (ClivusExtension.TemInterface())
            {
                var nome = Path.GetFileNameWithoutExtension(documento.Name);
                if (string.IsNullOrWhiteSpace(nome)) nome = "usina";
                caminho = DialogoDeArquivo.Salvar(Tr.T("Exportar canaletas"), Tr.T("Pasta de trabalho do Excel (*.xlsx)|*.xlsx"), nome + " - canaletas.xlsx",
                    documento.IsNamedDrawing ? Path.GetDirectoryName(documento.Name) : null);
            }
            else
            {
                var r = editor.GetString(new PromptStringOptions(Tr.T("\nArquivo XLSX: ")) { AllowSpaces = true });
                caminho = r.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(r.StringResult) ? r.StringResult.Trim() : null;
            }

            if (caminho is null)
            {
                editor.WriteMessage(Tr.T("\nEXPORTAR Cancelado.\n"));
                return;
            }

            editor.WriteMessage($"\n{Gravar(documento, terreno.Mesh, caminho)}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao exportar as canaletas.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui exportar as canaletas: {0}\n", erro.Message));
        }
    }

    internal static string Gravar(Document documento, Clivus.Geo.Tin terreno, string caminho)
    {
        if (!caminho.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) caminho += ".xlsx";

        var configuracao = DrenagemStore.Ler(documento.Database);
        var linhas = new List<ChannelExportRow>();
        List<BoxRecord> caixas;

        using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
        {
            foreach (var linha in LinhasDeDrenagem.Todas(transacao, documento.Database).Where(l => l.Identidade.IsChannel).OrderBy(l => l.Identidade.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var perfil = LinhasDeDrenagem.Perfil(terreno, configuracao, linha).Canaleta;
                if (perfil is null || perfil.Points.Count < 2) continue;

                linhas.Add(new ChannelExportRow(linha.Identidade.Name, linha.Identidade.Section!, ChannelQuantities.Of(perfil, linha.Identidade.Section!), linha.Identidade.Dissipator));
            }

            caixas = CaixasDeDrenagem.Todas(transacao, documento.Database).Select(c => c.Registro).OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        var planilha = new XlsxWriter();
        foreach (var tabela in DrainageExport.Tables(linhas, caixas)) QuantityReport.Tabela(planilha, tabela);
        File.WriteAllBytes(caminho, planilha.ToBytes());

        return Tr.F("EXPORTAR {0} canaleta(s), {1:0.0} m em planta, {2} caixa(s), {3} dissipador(es), gravado em {4}",
            linhas.Count, linhas.Sum(l => l.Quantities.Length2D), caixas.Count, linhas.Count(l => l.Dissipator != DissipatorEnd.None), caminho);
    }
}
