#if DEBUG
using System.Globalization;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.PotenciaAutoCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Só no build de teste (nível 2 dos itens 14 e 15 de 10/10/2026): escreve,
/// em números invariantes, o que a fonte única do módulo devolve para cada
/// estrutura do desenho, a potência trocada pela área, as divergências, as
/// potências gravadas nas mesas desenhadas, o kWp do recontar e do resumo
/// elétrico e se a tabela CC da rota diz por que os cálculos sumiram. Tudo
/// pelos mesmos caminhos das telas.
/// </summary>
public static class PotenciaAutoCommands
{
    [CommandMethod(PluginInfo.ComandoPotenciaAutomatico)]
    public static void Conferir()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;
        var inv = CultureInfo.InvariantCulture;

        try
        {
            var db = documento.Database;
            var fonte = FonteDoModulo.Ler(db);
            var estruturas = MesasDoDesenho.Ler(db);
            static string Nome(string t) => t.Trim().Replace(' ', '_');

            editor.WriteMessage(string.Format(inv, "\nPOTENCIA_AUTO simulada={0} eletrica={1}\n",
                fonte.Simulated is { } s ? s.Watts.ToString("0.###", inv) : "nenhuma", fonte.ElectricalEnabled ? 1 : 0));

            foreach (var e in estruturas)
            {
                var m = fonte.ElectricalFor(e.Name);
                editor.WriteMessage(string.Format(inv, "POTENCIA_ESTRUTURA nome={0} potencia={1:0.###} pan={2} voc={3}\n",
                    Nome(e.Name), e.Profile.Layout.Module.PowerWatts,
                    e.Profile.ModuleElectrical is { } pan ? pan.Pmax.ToString("0.###", inv) : "-",
                    m is null ? "-" : m.Voc.ToString("0.###", inv)));
            }

            var desenhadas = FonteDoModulo.MesasDesenhadas(db);
            foreach (var g in desenhadas.GroupBy(d => (Nome(d.ProfileName ?? "-"), d.Watts)).OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Watts))
                editor.WriteMessage(string.Format(inv, "POTENCIA_MESAS estrutura={0} watts={1} mesas={2}\n", g.Key.Item1, g.Key.Watts?.ToString("0.###", inv) ?? "-", g.Count()));

            var divergencias = fonte.Divergences(desenhadas);
            editor.WriteMessage(string.Format(inv, "POTENCIA_DIVERGENCIAS n={0}\n", divergencias.Count));
            foreach (var d in divergencias) editor.WriteMessage("POTENCIA_DIVERGENCIA " + d + "\n");

            var reserva = estruturas.Count > 0 ? estruturas[0].Profile.Layout.Module.PowerWatts : 720;
            var censo = RecontarCommands.Contar(db, reserva);
            var resumo = ResumoEletricoCommands.Ler(documento).Resumo;
            editor.WriteMessage(string.Format(inv, "POTENCIA_KWP recontar={0:0.###} modulos={1} resumo={2:0.###} resumomodulos={3}\n", censo.PowerKwp, censo.Modules, resumo.PowerKwp, resumo.Modules));

            var cc = RotaDeCabosTabelas.Montar(documento, CableRoute.DirectCurrent, out _);
            var nota = fonte.Simulated is { } simulada && cc.Count > 0 && cc[0].Notes.Contains(simulada.Reason());
            editor.WriteMessage(string.Format(inv, "POTENCIA_CC nota={0}\n", nota ? 1 : 0));
            editor.WriteMessage("POTENCIA_AUTO_FIM\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_POTENCIA_AUTO.", erro);
            editor.WriteMessage($"\nPOTENCIA_AUTO falhou: {erro.Message}\n");
        }
    }
}
#endif
