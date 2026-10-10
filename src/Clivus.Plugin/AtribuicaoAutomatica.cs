using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A atribuição automática das strings nos inversores (pedido do Renan em
/// 05/10/2026): a varredura própria dela, gravada na chave
/// "ALOCACAO_VARREDURA" (separada da "NUMERACAO_VARREDURA", que é da tag), e
/// o botão "Atribuir strings": lê as strings, os módulos e o cadastro, pede ao
/// Core quem vai para onde (<see cref="StringAutoAllocation"/>), grava o
/// vínculo no XData das strings e pinta com a cor do inversor. Quem decide é
/// o Core; aqui só se lê e se escreve.
/// </summary>
internal static class AtribuicaoAutomatica
{
    private const string ChaveDaVarredura = "ALOCACAO_VARREDURA";
    private const int VersaoDaVarredura = 1;
    private static readonly string OQueVarredura = Tr.N("da varredura da atribuição das strings");

    /// <summary>A varredura gravada, ou a padrão (de cima para baixo; na faixa, da esquerda para a direita); o problema do registro, se havia.</summary>
    internal static (AllocationScan Varredura, string? Problema) Varredura(Database database)
    {
        var lido = PluginRecords.Load<AllocationScan>(database, ChaveDaVarredura, VersaoDaVarredura, AllocationScan.FieldCount, AllocationScan.Parse, OQueVarredura);
        return (lido.Items.Count > 0 ? lido.Items[0] : AllocationScan.Default, lido.Problem);
    }

    internal static void GravarVarredura(Database database, AllocationScan varredura)
    {
        if (!varredura.IsValid) throw new ArgumentException("Varredura com os dois sentidos no mesmo eixo.", nameof(varredura));
        PluginRecords.Save(database, ChaveDaVarredura, VersaoDaVarredura, AllocationScan.FieldCount, [varredura], v => v.ToFields());
    }

    /// <summary>
    /// Atribui as strings livres na ordem da varredura gravada, enchendo os
    /// inversores na ordem da lista (14.3 automático). Grava o vínculo e a cor
    /// (e repinta as já alocadas, para um desenho de antes da cor ficar igual).
    /// Devolve o resultado e as linhas do relatório (a primeira é o resumo).
    /// </summary>
    internal static (AutoAllocationResult Resultado, IReadOnlyList<string> Linhas) Atribuir(Database database, Action<double, string>? progresso = null)
    {
        progresso?.Invoke(5, Tr.T("Lendo o cadastro e as strings..."));
        var (setup, problema) = ConfiguracaoEletricaStore.Ler(database);
        var (varredura, problemaDaVarredura) = Varredura(database);

        // Cadastro lido pela metade daria capacidade errada: nada é atribuído.
        if (problema is not null)
            return (new AutoAllocationResult([], new Dictionary<Guid, int>(), 0, 0, 0, [], []),
                [Tr.F("Nada foi atribuído: o cadastro elétrico do desenho tem registro que não deu para ler ({0}).", problema)]);

        AutoAllocationResult r;
        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var strings = ElectricalStore.Strings(transacao, database).Select(x => x.String).ToList();
            var modulos = NumeracaoDesenho.Modulos(transacao, database).ToDictionary(m => m.Key, m => new ModuleSpot(m.Value.Mesa, m.Value.Centro.X, m.Value.Centro.Y));
            progresso?.Invoke(25, Tr.T("Distribuindo as strings pela varredura..."));
            r = StringAutoAllocation.Allocate(setup.Inverters, setup.Models, strings, modulos, varredura);
        }

        progresso?.Invoke(40, Tr.T("Gravando o vínculo das strings..."));
        StringsDoDesenho.Gravar(database, r.Changed);
        progresso?.Invoke(60, Tr.T("Pintando as strings com a cor de cada inversor..."));
        CorDasStrings.Repintar(database, null, p => progresso?.Invoke(60 + 30 * p, Tr.T("Pintando as strings com a cor de cada inversor...")));

        // A pré-tag do inversor em cada string (item 8 de 10/10/2026): a cor sozinha é ruim de ver.
        progresso?.Invoke(90, Tr.T("Escrevendo a pré-tag do inversor nas strings..."));
        var preTags = PreTagDasStrings.Atualizar(database);
        progresso?.Invoke(100, Tr.T("Pronto."));

        var linhas = new List<string>();
        if (r.Changed.Count == 0)
        {
            linhas.Add(r.Leftover == 0 ? Tr.T("Nenhuma string livre para atribuir.")
                : r.Full.Count == 0 ? Tr.F("Nenhuma string atribuída: não há inversor com modelo ({0} string(s) livre(s)). Crie inversores na lista.", r.Leftover)
                : Tr.F("Nenhuma string atribuída: os inversores estão cheios ({0} string(s) livre(s) sobrando).", r.Leftover));
        }
        else
        {
            var porInversor = setup.Inverters.Where(i => r.Added.ContainsKey(i.Id)).Select(i => Tr.F("{0} +{1}", i.Name, r.Added[i.Id]));
            linhas.Add(Tr.F("{0} string(s) atribuída(s) ({1}): {2}.", r.Changed.Count, varredura.Describe(), string.Join(", ", porInversor)));
        }

        if (preTags > 0) linhas.Add(Tr.F("{0} string(s) com a pré-tag do inversor (I1, I2...); a Numeração troca pela tag de verdade.", preTags));
        if (r.Changed.Count > 0 && r.Leftover > 0) linhas.Add(Tr.F("ATENÇÃO: {0} string(s) livre(s) sobraram: os inversores encheram. Crie mais inversores ou troque o modelo.", r.Leftover));
        if (r.Full.Count > 0) linhas.Add(Tr.F("{0} inversor(es) já cheio(s), pulado(s).", r.Full.Count));
        if (r.WithoutModel.Count > 0) linhas.Add(Tr.F("ATENÇÃO: {0} inversor(es) sem modelo, pulado(s) (sem modelo não há capacidade).", r.WithoutModel.Count));
        if (r.Unplaced > 0) linhas.Add(Tr.F("ATENÇÃO: {0} string(s) livre(s) sem o primeiro módulo no desenho ficaram livres.", r.Unplaced));
        if (r.Duplicates > 0) linhas.Add(Tr.F("ATENÇÃO: {0} string(s) copiada(s) (mesmo GUID de outra) ficaram de fora; apague as cópias.", r.Duplicates));
        if (problemaDaVarredura is not null) linhas.Add(Tr.F("ATENÇÃO: {0}.", problemaDaVarredura));

        return (r, linhas);
    }
}
