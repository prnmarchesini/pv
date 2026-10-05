using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

#if DEBUG
[assembly: CommandClass(typeof(Clivus.Plugin.AreaDoTrafoTestCommands))]
#endif

namespace Clivus.Plugin;

/// <summary>
/// O hatch da área do trafo (pedido do Renan em 05/10/2026: "na aba do
/// trafo, ter um botão, para criar um hatch em torno da área que compreende
/// as strings dos inversores que compõem aquele trafo"). A cadeia é a do
/// contrato: string → inversor só no XData da string, inversor → trafo em
/// <see cref="Inverter.Transformer"/>. As pegadas são as faces dos módulos
/// das strings; o contorno vem do Core (<see cref="TransformerArea"/>).
/// <para>
/// Um hatch SOLID por ilha, na camada CLIVUS_TRAFO_AREA, com a cor do trafo
/// (a paleta dos inversores de trás para a frente, pela ordem do trafo na
/// lista: o T1 não sai com a cor do Inversor 1) e 60 % de transparência. O
/// XData (<see cref="TransformerAreaMark"/>) diz de que trafo é: gerar de
/// novo apaga o hatch antigo daquele trafo antes, e apagar o trafo o leva.
/// </para>
/// <para>
/// A cota (o hatch é plano): a do canto mais alto dos módulos da ilha mais
/// 0,30 m, a mesma escolha da marca do grupo (<see cref="GroupDrawer"/>,
/// 02/10/2026, "o hachurado tem que ficar por cima"): em vista sombreada ele
/// não some embaixo dos módulos, e acompanha as mesas, que acompanham o
/// terreno (regra 5). Por isso um hatch por ilha: cada ilha na cota dela.
/// Na ordem de desenho ele vai para a frente.
/// </para>
/// </summary>
internal static class AreaDoTrafo
{
    /// <summary>A camada dos hatches das áreas dos trafos.</summary>
    internal const string Camada = PluginInfo.PrefixoDeDados + "_TRAFO_AREA";

    /// <summary>Quanto o hatch fica acima do canto mais alto dos módulos da ilha, em metro.</summary>
    internal const double FolgaAcimaDosModulos = GroupDrawer.FolgaAcimaDasMesas;

    /// <summary>60 % transparente (o alfa do AutoCAD vai de 0, invisível, a 255, opaco).</summary>
    private const byte Alfa = 102;

    private const int Versao = 1;

    /// <summary>Uma ilha desenhada: o hatch, a cota dele, a do canto mais alto dos módulos dela, quantos módulos e a área.</summary>
    internal sealed record Ilha(ObjectId Hatch, double Cota, double CotaDosModulos, int Modulos, double Area);

    /// <summary>O que saiu de um trafo: as ilhas desenhadas, quantos hatches antigos foram apagados e a frase.</summary>
    internal sealed record Resultado(Guid Trafo, string Apelido, IReadOnlyList<Ilha> Ilhas, int Apagados, string Frase, bool Erro);

    /// <summary>
    /// O botão da aba (fora de comando): trava o documento, cala o vigia e
    /// desenha o hatch de um trafo, ou de todos (null). Devolve o resultado
    /// de cada trafo; nada de exceção sai daqui sem ser registrada por quem chama.
    /// </summary>
    internal static IReadOnlyList<Resultado> PelaJanela(Document documento, Guid? trafo)
    {
        var r = EscritaForaDeComando.Fazer(documento, () => Gerar(documento.Database, trafo));
        if (ClivusExtension.TemInterface()) AcadApp.UpdateScreen();
        return r;
    }

    /// <summary>A frase única para o recado da aba, e se é de erro (nenhum hatch saiu).</summary>
    internal static (string Frase, bool Erro) Resumir(IReadOnlyList<Resultado> resultados)
    {
        if (resultados.Count == 0) return (Tr.T("Nenhum trafo no cadastro."), true);
        if (resultados.Count == 1) return (resultados[0].Frase, resultados[0].Erro);

        var feitos = resultados.Count(r => !r.Erro);
        var linhas = new List<string> { Tr.F("Hatch de {0} de {1} trafo(s).", feitos, resultados.Count) };
        linhas.AddRange(resultados.Select(r => r.Frase));
        return (string.Join("\n", linhas), feitos == 0);
    }

    /// <summary>
    /// Desenha (ou refaz) o hatch da área de um trafo, ou de todos (null),
    /// numa transação. Trafo sem inversor, sem string alocada ou sem módulo
    /// no desenho não ganha hatch (e o antigo dele é apagado).
    /// </summary>
    internal static IReadOnlyList<Resultado> Gerar(Database database, Guid? trafo)
    {
        var (setup, problema) = ConfiguracaoEletricaStore.Ler(database);

        // Cadastro lido pela metade daria a área errada: nada é desenhado.
        if (problema is not null)
            return [new Resultado(trafo ?? Guid.Empty, string.Empty, [], 0, Tr.F("Nada foi desenhado: o cadastro elétrico do desenho tem registro que não deu para ler ({0}).", problema), true)];

        var alvos = trafo is { } id ? setup.Transformers.Where(t => t.Id == id).ToList() : setup.Transformers.ToList();
        if (alvos.Count == 0)
            return trafo is null ? [] : [new Resultado(trafo.Value, string.Empty, [], 0, Tr.T("Esse trafo não está mais no cadastro."), true)];

        using var transacao = database.TransactionManager.StartTransaction();

        var strings = ElectricalStore.Strings(transacao, database).Select(x => x.String).ToList();
        var pegadas = Pegadas(transacao, database);
        var doTrafo = setup.Inverters.Where(i => i.Transformer != Guid.Empty).ToDictionary(i => i.Id, i => i.Transformer);
        var antigos = Hatches(transacao, database);

        var resultados = new List<Resultado>();
        foreach (var t in alvos)
            resultados.Add(Desenhar(transacao, database, setup, t, strings, pegadas, doTrafo, antigos.Where(a => a.Trafo == t.Id).Select(a => a.Id).ToList()));

        transacao.Commit();
        return resultados;
    }

    private static Resultado Desenhar(
        Transaction transacao, Database database, ElectricalSetup setup, Transformer trafo,
        IReadOnlyList<ElectricalString> strings, IReadOnlyDictionary<Guid, (IReadOnlyList<(double X, double Y)> Contorno, double Topo)> pegadas,
        IReadOnlyDictionary<Guid, Guid> doTrafo, IReadOnlyList<ObjectId> antigos)
    {
        var apelido = trafo.Nickname;

        // Gerar de novo substitui: o antigo sai sempre, mesmo se agora não há o que desenhar.
        foreach (var id in antigos) ((Entity)transacao.GetObject(id, OpenMode.ForWrite)).Erase();
        var sobra = antigos.Count > 0 ? " " + Tr.T("O hatch antigo dele foi apagado.") : string.Empty;

        Resultado Recusa(string frase) => new(trafo.Id, apelido, [], antigos.Count, frase + sobra, true);

        var inversores = setup.Inverters.Where(i => i.Transformer == trafo.Id).Select(i => i.Id).ToHashSet();
        if (inversores.Count == 0)
            return Recusa(Tr.F("{0} não tem inversor: agrupe os inversores no skid dele (aba Inversor) antes do hatch.", apelido));

        var dele = strings.Where(s => inversores.Contains(s.Inverter)).ToList();
        if (dele.Count == 0)
            return Recusa(Tr.F("Os inversores de {0} não têm string alocada: aloque as strings (aba Inversor) antes do hatch.", apelido));

        // As pegadas do trafo e as dos módulos de strings de outros inversores
        // (de outro trafo ou ainda sem skid): o contorno não avança sobre elas.
        var modulos = dele.SelectMany(s => s.Modules).Distinct().ToList();
        var proprias = modulos.Where(pegadas.ContainsKey).ToList();
        var faltando = modulos.Count - proprias.Count;
        var meus = proprias.ToHashSet();
        var alheias = strings.Where(s => s.IsAllocated && !inversores.Contains(s.Inverter))
            .SelectMany(s => s.Modules).Where(m => !meus.Contains(m) && pegadas.ContainsKey(m)).Distinct()
            .Select(m => pegadas[m].Contorno).ToList();

        if (proprias.Count == 0)
            return Recusa(Tr.F("Os módulos das strings de {0} não estão no desenho; nada foi desenhado.", apelido));

        var ilhas = TransformerArea.Outline(proprias.Select(m => pegadas[m].Contorno).ToList(), alheias);
        if (ilhas.Count == 0)
            return Recusa(Tr.F("Não saiu contorno para {0} (os módulos não têm área em planta).", apelido));

        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var camada = LayoutLayers.Garantir(transacao, database, Camada, new RgbColor(128, 128, 128));
        var cor = Cor(setup, trafo);
        var marca = new TransformerAreaMark(trafo.Id);
        var feitas = new List<Ilha>();

        foreach (var ilha in ilhas)
        {
            var topo = ilha.Members.Max(k => pegadas[proprias[k]].Topo);
            var cota = topo + FolgaAcimaDosModulos;

            // Os padrões do banco ANTES da camada: SetDatabaseDefaults reescreve camada e cor.
            var hachura = new Hatch();
            hachura.SetDatabaseDefaults(database);
            hachura.Layer = camada;
            hachura.Color = Color.FromRgb(cor.R, cor.G, cor.B);
            hachura.Normal = Vector3d.ZAxis;
            hachura.Elevation = cota;

            espaco.AppendEntity(hachura);
            transacao.AddNewlyCreatedDBObject(hachura, true);

            hachura.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            hachura.Associative = false;
            hachura.HatchStyle = HatchStyle.Normal;
            Laco(hachura, ilha.Outer, HatchLoopTypes.Polyline | HatchLoopTypes.External);
            foreach (var buraco in ilha.Holes) Laco(hachura, buraco, HatchLoopTypes.Polyline);
            hachura.EvaluateHatch(true);
            hachura.Transparency = new Transparency(Alfa);
            PluginXData.Save(transacao, hachura, TransformerAreaMark.Tipo, Versao, trafo.Id.ToString("D"));

            feitas.Add(new Ilha(hachura.ObjectId, cota, topo, ilha.Members.Count, ilha.Area));
        }

        // Na frente dos módulos e das mesas, como a marca do grupo.
        var ordem = (DrawOrderTable)transacao.GetObject(espaco.DrawOrderTableId, OpenMode.ForWrite);
        ordem.MoveToTop(new ObjectIdCollection(feitas.Select(f => f.Hatch).ToArray()));

        var frase = Tr.F("{0}: hatch da área com {1} ilha(s), {2:#,0} m², {3} string(s) de {4} inversor(es).",
            apelido, feitas.Count, feitas.Sum(f => f.Area), dele.Count, dele.Select(s => s.Inverter).Distinct().Count());
        if (antigos.Count > 0) frase += " " + Tr.T("O anterior foi substituído.");
        if (faltando > 0) frase += " " + Tr.F("ATENÇÃO: {0} módulo(s) das strings não estão no desenho e ficaram fora.", faltando);

        return new Resultado(trafo.Id, apelido, feitas, antigos.Count, frase, false);
    }

    /// <summary>Um laço de polilinha fechado (o primeiro ponto repetido no fim), sem arco.</summary>
    private static void Laco(Hatch hachura, IReadOnlyList<(double X, double Y)> anel, HatchLoopTypes tipo)
    {
        var pontos = new Point2dCollection();
        var arcos = new DoubleCollection();
        foreach (var (x, y) in anel.Append(anel[0]))
        {
            pontos.Add(new Point2d(x, y));
            arcos.Add(0);
        }

        hachura.AppendLoop(tipo, pontos, arcos);
    }

    /// <summary>A cor do trafo: a paleta dos inversores de trás para a frente, pela ordem do trafo na lista.</summary>
    internal static RgbColor Cor(ElectricalSetup setup, Transformer trafo)
    {
        var paleta = InverterColors.Palette;
        var i = Math.Max(0, setup.Transformers.ToList().FindIndex(t => t.Id == trafo.Id));
        return paleta[paleta.Count - 1 - i % paleta.Count].Color;
    }

    /// <summary>
    /// As pegadas dos módulos em planta, pelo GUID do módulo: os cantos da
    /// face de cima e a cota mais alta dela; sem face, o retângulo da
    /// extensão do bloco.
    /// </summary>
    private static Dictionary<Guid, (IReadOnlyList<(double X, double Y)> Contorno, double Topo)> Pegadas(Transaction transacao, Database database)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var classeDaFace = RXObject.GetClass(typeof(Face));
        var classeDoBloco = RXObject.GetClass(typeof(BlockReference));
        var porFace = new Dictionary<Guid, (IReadOnlyList<(double X, double Y)>, double)>();
        var porBloco = new Dictionary<Guid, (IReadOnlyList<(double X, double Y)>, double)>();

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased) continue;

            if (id.ObjectClass == classeDaFace)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is not Face face || LayoutXData.LoadFace(face) is not { } f) continue;

                var cantos = Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).ToList();
                porFace.TryAdd(f.Module, (cantos.Select(p => (p.X, p.Y)).ToList(), cantos.Max(p => p.Z)));
            }
            else if (id.ObjectClass == classeDoBloco)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is not BlockReference bloco || LayoutXData.LoadModule(bloco) is not { } m) continue;

                try
                {
                    var e = bloco.GeometricExtents;
                    porBloco.TryAdd(m.Id, ([(e.MinPoint.X, e.MinPoint.Y), (e.MaxPoint.X, e.MinPoint.Y), (e.MaxPoint.X, e.MaxPoint.Y), (e.MinPoint.X, e.MaxPoint.Y)], e.MaxPoint.Z));
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    // Bloco sem extensão (vazio): fica sem pegada.
                }
            }
        }

        foreach (var (modulo, pegada) in porBloco) porFace.TryAdd(modulo, pegada);
        return porFace;
    }

    /// <summary>Os hatches de área de trafo do desenho, com o trafo de cada um.</summary>
    private static List<(ObjectId Id, Guid Trafo)> Hatches(Transaction transacao, Database database)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var classe = RXObject.GetClass(typeof(Hatch));
        var achados = new List<(ObjectId, Guid)>();

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased || id.ObjectClass != classe) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            if (PluginXData.Load(e, TransformerAreaMark.Tipo, Versao, TransformerAreaMark.FieldCount) is not { } c || !Guid.TryParse(c[0], out var trafo)) continue;
            achados.Add((id, trafo));
        }

        return achados;
    }

    /// <summary>Apaga o hatch da área do trafo (o trafo saiu do cadastro). Quantos.</summary>
    internal static int Apagar(Database database, Guid trafo)
    {
        using var transacao = database.TransactionManager.StartTransaction();
        var ids = Hatches(transacao, database).Where(h => h.Trafo == trafo).Select(h => h.Id).ToList();
        foreach (var id in ids) ((Entity)transacao.GetObject(id, OpenMode.ForWrite)).Erase();
        transacao.Commit();
        return ids.Count;
    }
}

#if DEBUG
/// <summary>
/// Só no build de teste: CLIVUS_ELETRICA_JANELA_HATCH_AUTO &lt;trafo | *&gt;
/// chama o MESMO caminho do botão "Hatch da área" ("*": "Hatch de todos")
/// da aba Transformador, no contexto da aplicação (Session), fora de comando
/// do documento, como o clique da janela solta. As linhas começam por
/// "ELETRICA HATCH" e saem para o nível 2 ler.
/// </summary>
public static class AreaDoTrafoTestCommands
{
    [CommandMethod(PluginInfo.ComandoEletricaJanelaHatchAutomatico, CommandFlags.Session)]
    public static void HatchPelaJanela()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var r = editor.GetString(new PromptStringOptions("\nTrafo (apelido, * = todos): ") { AllowSpaces = true });
            if (r.Status != PromptStatus.OK) return;

            Guid? trafo = null;
            if (r.StringResult.Trim() != "*")
            {
                var setup = ConfiguracaoEletricaStore.Ler(documento.Database).Setup;
                if (setup.Transformers.FirstOrDefault(t => ElectricalSetup.SameName(t.Nickname, r.StringResult)) is not { } t)
                {
                    editor.WriteMessage($"\nELETRICA HATCH recusado: trafo {r.StringResult} nao existe\n");
                    return;
                }

                trafo = t.Id;
            }

            var resultados = AreaDoTrafo.PelaJanela(documento, trafo);
            var (frase, erro) = AreaDoTrafo.Resumir(resultados);
            editor.WriteMessage($"\nELETRICA HATCH erro={erro} {frase.Replace('\n', ' ')}\n");

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var x in resultados)
                foreach (var i in x.Ilhas)
                    editor.WriteMessage(string.Format(inv, "ELETRICA HATCH_ILHA trafo={0} handle={1} cota={2:0.000} topo={3:0.000} modulos={4} area={5:0.00} fim\n",
                        x.Apelido, i.Hatch.Handle, i.Cota, i.CotaDosModulos, i.Modulos, i.Area));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_ELETRICA_JANELA_HATCH_AUTO.", erro);
            editor.WriteMessage($"\nELETRICA HATCH ERRO {erro.Message}\n");
        }
    }
}
#endif
