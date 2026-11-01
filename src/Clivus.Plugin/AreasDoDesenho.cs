using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

#if DEBUG
[assembly: CommandClass(typeof(Clivus.Plugin.AreasTestHooks))]
#endif

namespace Clivus.Plugin;

/// <summary>Uma área no desenho: a polilinha, a identidade, o contorno em planta e se o registro central a conhece.</summary>
/// <param name="Id">A polilinha da área.</param>
/// <param name="Handle">O handle dela neste arquivo.</param>
/// <param name="Identity">A identidade gravada nela (XData).</param>
/// <param name="Contorno">Os vértices em planta (<see cref="FileiraCommands.Vertices"/>).</param>
/// <param name="Registrada">Se o registro central tem este GUID com este handle.</param>
internal sealed record AreaNoDesenho(ObjectId Id, string Handle, AreaIdentity Identity, IReadOnlyList<Point3> Contorno, bool Registrada);

/// <summary>Uma linha da janela das áreas: a área (null fora das áreas e no total), o resumo, a cor e se está hachurada.</summary>
/// <param name="Pontos">Os pontos para o zoom: o contorno da área, ou os das mesas de fora, ou tudo (no total).</param>
internal sealed record LinhaDasAreas(AreaNoDesenho? Area, AreaSummaryRow Resumo, RgbColor? Cor, bool Hachurada, IReadOnlyList<Point3> Pontos);

/// <summary>A leitura do desenho para a janela das áreas.</summary>
/// <param name="Areas">As áreas, na ordem da janela (a da criação).</param>
/// <param name="Resumo">O resumo por área (<see cref="AreaSummary"/>).</param>
/// <param name="Linhas">As linhas da janela: as áreas, fora das áreas (se houver) e o total.</param>
/// <param name="Avisos">O que está errado no desenho e o usuário precisa saber (registro, cópias, hachuras órfãs).</param>
internal sealed record LeituraDasAreas(IReadOnlyList<AreaNoDesenho> Areas, AreaSummary Resumo, IReadOnlyList<LinhaDasAreas> Linhas, IReadOnlyList<string> Avisos);

/// <summary>
/// As áreas do desenho para a janela Áreas (etapa 41, Renan, 30/10/2026:
/// "quero ver elas, quero ter a opção do sistema listar o resumo de cada
/// área, de marcar cada área com rachura, do resumo sair por área").
/// <para>
/// Lê pelo XData, nunca pela camada: as áreas são as polilinhas com a
/// identidade de área (o registro central só diz quais o Regerar acha), as
/// mesas são as do <see cref="LayoutScan"/>, contadas como no Recontar
/// (<see cref="RecontarCommands.Contar(Transaction, TableParts)"/>), e a
/// conta por área é a do Core (<see cref="AreaSummary"/>).
/// </para>
/// <para>
/// Cada ação da janela é um método daqui, chamado do botão (fora de comando,
/// por <see cref="EscritaForaDeComando"/>) e, no build de teste, do gancho
/// LISP do nível 2: o mesmo caminho.
/// </para>
/// </summary>
internal static class AreasDoDesenho
{
    private static readonly RXClass ClasseDaPolilinha3d = RXObject.GetClass(typeof(Polyline3d));

    /// <summary>As áreas do desenho, pelo XData, na ordem da criação (a cor de cada uma sai da posição).</summary>
    internal static IReadOnlyList<AreaNoDesenho> Varrer(Database database)
    {
        var registradas = AreaStore.Load(database)
            .Select(r => Chave(r.Identity.Id, r.Handle))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var achadas = new List<AreaNoDesenho>();

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            // Só polilinha 3D é aberta: é o que a área é (AreaCommands.Criar).
            if (id.IsErased || id.ObjectClass != ClasseDaPolilinha3d) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Polyline3d polilinha) continue;
            if (AreaXData.Load(polilinha) is not { } identidade) continue;

            var handle = polilinha.Handle.ToString();
            achadas.Add(new AreaNoDesenho(id, handle, identidade, FileiraCommands.Vertices(polilinha, transacao), registradas.Contains(Chave(identidade.Id, handle))));
        }

        transacao.Commit();

        return achadas
            .OrderBy(a => a.Identity.CreatedAt)
            .ThenBy(a => long.TryParse(a.Handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) ? h : long.MaxValue)
            .ToList();
    }

    /// <summary>Lê tudo o que a janela mostra: as áreas, o resumo de cada uma e as hachuras.</summary>
    internal static LeituraDasAreas Ler(Document documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        var database = documento.Database;
        var areas = Varrer(database);
        var avisos = new List<string>();

        if (AreaStore.Ler(database).Problem is { } problema)
            avisos.Add(Tr.F("ATENÇÃO: o registro das áreas tem problema ({0}). Use {1} para refazê-lo a partir do desenho.", problema, PluginInfo.ComandoReindexar));

        var foraDoRegistro = areas.Count(a => !a.Registrada);
        if (foraDoRegistro > 0) avisos.Add(Tr.F("{0} área(s) do desenho não estão no registro. Use {1}.", foraDoRegistro, PluginInfo.ComandoReindexar));

        var repetidas = areas.GroupBy(a => a.Identity.Id).Count(g => g.Count() > 1);
        if (repetidas > 0)
            avisos.Add(Tr.F("{0} área(s) com a identidade em mais de uma polilinha (a polilinha da área foi copiada): o Regerar e o Conforme objetos acham só uma delas. Rode Validar.", repetidas));

        // Área em cima de área (30/10/2026, "tinha VÁRIAS linhas sobrepostas").
        var vistas = new HashSet<ObjectId>();
        foreach (var area in areas)
        {
            if (!vistas.Add(area.Id)) continue;

            var mesmaLinha = EscolhaDeArea.Repetidas(areas, area);
            foreach (var a in mesmaLinha) vistas.Add(a.Id);

            if (mesmaLinha.Count > 1)
                avisos.Add(Tr.F("As áreas {0} são a mesma linha, uma em cima da outra: apague as que sobram (Apagar área).", string.Join(", ", mesmaLinha.Select(a => a.Identity.DisplayName))));
        }

        // O registro que aponta para área que não existe mais (apagada à mão).
        var noDesenho = areas.Select(a => Chave(a.Identity.Id, a.Handle)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sumidas = AreaStore.Load(database).Where(r => !noDesenho.Contains(Chave(r.Identity.Id, r.Handle))).Select(r => r.Identity.DisplayName).ToList();
        if (sumidas.Count > 0)
            avisos.Add(Tr.F("{0} área(s) do registro não existem mais no desenho ({1}): use {2} para limpar o registro.", sumidas.Count, string.Join(", ", sumidas), PluginInfo.ComandoReindexar));

        List<AreaTableInput> mesas;
        List<HachuraDasAreas.Marca> marcas;
        double reserva;

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var partes = LayoutScan.Tables(transacao, database);
            mesas = partes.Values.Select(p => new AreaTableInput(RecontarCommands.Contar(transacao, p), Contorno(transacao, p), AreaDaFace(transacao, p))).ToList();
            marcas = HachuraDasAreas.Marcas(transacao, database);

            // A potência de reserva da mesa sem potência gravada: a mesma regra dos grupos.
            reserva = GrupoCommands.Reserva(documento, partes);
            transacao.Commit();
        }

        var entradas = areas.Select(a => new AreaInput(a.Identity.Id, a.Identity.DisplayName, a.Contorno, CampoCommands.PelosObjetos(database, a.Contorno) is not null)).ToList();
        var resumo = AreaSummary.Build(entradas, mesas, reserva, FonteDoModulo.Simulada(database)?.Watts);

        var linhas = new List<LinhaDasAreas>();
        for (var k = 0; k < areas.Count; k++)
        {
            var dela = HachuraDasAreas.DaArea(marcas, areas[k], areas).ToList();
            var hachura = dela.FirstOrDefault(m => m.EHachura);
            linhas.Add(new LinhaDasAreas(areas[k], resumo.Areas[k], hachura?.Cor ?? AreaHatch.ColorOf(k), hachura is not null, areas[k].Contorno));
        }

        var deFora = Enumerable.Range(0, mesas.Count).Where(i => resumo.Assignment[i] is null).SelectMany(i => mesas[i].Outline).ToList();
        if (resumo.Outside is { } fora) linhas.Add(new LinhaDasAreas(null, fora, null, false, deFora));
        linhas.Add(new LinhaDasAreas(null, resumo.Total, null, false, [.. areas.SelectMany(a => a.Contorno), .. deFora]));

        var orfas = marcas.Count(m => m.EHachura && !areas.Any(a => m.Dono.Belongs(a.Identity.Id, a.Handle, areas.Select(x => (x.Identity.Id, x.Handle)))));
        if (orfas > 0) avisos.Add(Tr.F("{0} hachura(s) de área que não existe mais no desenho: o \"Tirar todas\" as apaga.", orfas));

        return new LeituraDasAreas(areas, resumo, linhas, avisos);
    }

    // ------------------------------------------------------------- ações

    /// <summary>
    /// Hachura as áreas pedidas (null: todas), trocando a hachura antiga de
    /// cada uma, com o documento travado e o vigia calado. A frase do relato.
    /// </summary>
    internal static string Hachurar(Document documento, IReadOnlyList<AreaNoDesenho> todas, IReadOnlyList<AreaNoDesenho>? alvos)
    {
        var quais = alvos ?? todas;
        if (quais.Count == 0) return Tr.T("Nenhuma área neste desenho. Use o botão Área.");

        var (resultado, semTerreno) = EscritaForaDeComando.Fazer(documento, () =>
        {
            // Sem terreno na memória (desenho reaberto), reprocessa o do carimbo, como os comandos.
            var terreno = TerrainCache.Get(documento);
            if (terreno is null && TerrainCommands.Reprocessar(documento.Editor, documento)) terreno = TerrainCache.Get(documento);

            var feito = HachuraDasAreas.Desenhar(documento.Database, todas, quais, terreno?.Mesh);
            Redesenhar(documento);
            return (feito, terreno is null);
        });

        var frase = Tr.F("ÁREAS {0} área(s) hachurada(s) na camada {1}, cada uma com a sua cor.", resultado.Feitas.Count, HachuraDasAreas.Camada);
        if (resultado.Feitas.Count > 0)
            frase += " " + Tr.F("Cota: de {0:0.00} a {1:0.00} m (acima do terreno e das mesas de cada área).", resultado.Feitas.Min(f => f.Cota), resultado.Feitas.Max(f => f.Cota));
        if (semTerreno && resultado.Feitas.Count > 0)
            frase += " " + Tr.T("ATENÇÃO: sem terreno processado, a cota veio dos vértices das áreas (assentados no terreno quando elas foram criadas).");
        foreach (var recusa in resultado.Recusas) frase += " " + recusa;

        return frase;
    }

    /// <summary>Tira a hachura de uma área, ou de todas (null). A frase do relato.</summary>
    internal static string TirarHachura(Document documento, IReadOnlyList<AreaNoDesenho> todas, AreaNoDesenho? area)
    {
        var apagadas = EscritaForaDeComando.Fazer(documento, () =>
        {
            var quantas = HachuraDasAreas.Apagar(documento.Database, todas, area);
            Redesenhar(documento);
            return quantas;
        });

        return area is null
            ? Tr.F("ÁREAS Hachuras tiradas: {0} entidade(s) apagada(s) na camada {1}.", apagadas, HachuraDasAreas.Camada)
            : apagadas == 0
                ? Tr.F("ÁREAS {0} não estava hachurada.", area.Identity.DisplayName)
                : Tr.F("ÁREAS Hachura de {0} tirada ({1} entidade(s)).", area.Identity.DisplayName, apagadas);
    }

    /// <summary>
    /// Renomeia a área (o nome que o Clivus mostra): o XData da polilinha, o
    /// registro central (se é a polilinha registrada) e o nome escrito na
    /// hachura dela. O GUID e a data ficam.
    /// </summary>
    /// <exception cref="ArgumentException">Nome vazio, longo demais ou de outra área.</exception>
    internal static string Renomear(Document documento, IReadOnlyList<AreaNoDesenho> todas, AreaNoDesenho area, string novo)
    {
        ArgumentNullException.ThrowIfNull(novo);

        var nome = novo.Trim();
        if (nome.Length == 0) throw new ArgumentException(Tr.T("Digite um nome."));
        if (nome.Length > Perguntas.MaiorNome) throw new ArgumentException(Tr.F("Nome longo demais ({0} caracteres). O limite é {1}.", nome.Length, Perguntas.MaiorNome));
        if (todas.Any(a => a.Handle != area.Handle && string.Equals(a.Identity.DisplayName, nome, StringComparison.CurrentCultureIgnoreCase)))
            throw new ArgumentException(Tr.F("Já há uma área chamada \"{0}\".", nome));

        var antes = area.Identity.DisplayName;

        EscritaForaDeComando.Fazer(documento, () =>
        {
            var database = documento.Database;
            AreaIdentity nova;

            using (var transacao = database.TransactionManager.StartTransaction())
            {
                var polilinha = (Entity)transacao.GetObject(area.Id, OpenMode.ForWrite);
                nova = (AreaXData.Load(polilinha) ?? area.Identity) with { Name = nome };
                AreaXData.Save(transacao, polilinha, nova);
                HachuraDasAreas.TrocarNome(transacao, database, area, todas, nome);
                transacao.Commit();
            }

            // O registro guarda o nome também; a cópia que ele não conhece fica fora dele.
            if (area.Registrada) AreaStore.Upsert(database, new AreaRecord(nova, area.Handle), out _);
            Redesenhar(documento);
        });

        return Tr.F("ÁREAS {0} renomeada para {1}.", antes, nome);
    }

    /// <summary>Grava o Excel do resumo por área (a aba Resumo e uma por área). A frase do relato.</summary>
    internal static string Exportar(Document documento, AreaSummary resumo, string caminho)
    {
        if (!caminho.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) caminho += ".xlsx";

        var desenho = Path.GetFileNameWithoutExtension(documento.Name);
        File.WriteAllBytes(caminho, AreaReport.Build(resumo, string.IsNullOrWhiteSpace(desenho) ? "usina" : desenho, DateTime.Now).ToBytes());

        return Tr.F("ÁREAS Resumo de {0} área(s) exportado para {1}", resumo.Areas.Count, caminho);
    }

    /// <summary>A caixa dos pontos, para o zoom; null sem ponto.</summary>
    internal static Extents3d? Limites(IReadOnlyList<Point3> pontos)
    {
        var finitos = pontos.Where(p => p.IsFinite).ToList();
        if (finitos.Count == 0) return null;

        return new Extents3d(
            new Autodesk.AutoCAD.Geometry.Point3d(finitos.Min(p => p.X), finitos.Min(p => p.Y), finitos.Min(p => p.Z)),
            new Autodesk.AutoCAD.Geometry.Point3d(finitos.Max(p => p.X), finitos.Max(p => p.Y), finitos.Max(p => p.Z)));
    }

    // ------------------------------------------------------------ leitura

    /// <summary>Os vértices da polilinha 3D como estão (sem simplificar), em 3D.</summary>
    internal static IReadOnlyList<Point3> Vertices(Transaction transacao, Polyline3d polilinha)
    {
        var pontos = new List<Point3>();

        foreach (ObjectId id in polilinha)
        {
            var vertice = (PolylineVertex3d)transacao.GetObject(id, OpenMode.ForRead);
            pontos.Add(new Point3(vertice.Position.X, vertice.Position.Y, vertice.Position.Z));
        }

        return pontos;
    }

    /// <summary>O contorno da mesa (o primeiro, se a cópia deixou dois); sem contorno, o ponto de uma peça.</summary>
    private static IReadOnlyList<Point3> Contorno(Transaction transacao, TableParts partes)
    {
        if (partes.Contour is { } id && transacao.GetObject(id, OpenMode.ForRead) is Polyline3d contorno)
            return Vertices(transacao, contorno);

        foreach (var peca in partes.All)
            if (Ponto(transacao.GetObject(peca, OpenMode.ForRead)) is { } p) return [p];

        return [];
    }

    private static Point3? Ponto(DBObject objeto)
    {
        Autodesk.AutoCAD.Geometry.Point3d? p = objeto switch
        {
            BlockReference bloco => bloco.Position,
            Face face => face.GetVertexAt(0),
            MText texto => texto.Location,
            Line linha => linha.StartPoint,
            _ => null,
        };

        return p is { } q ? new Point3(q.X, q.Y, q.Z) : null;
    }

    /// <summary>A área de um módulo da mesa: a primeira face de cima que tem área.</summary>
    private static double? AreaDaFace(Transaction transacao, TableParts partes)
    {
        foreach (var id in partes.Faces)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Face face) continue;

            var v = Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).Select(p => new Point3(p.X, p.Y, p.Z)).ToList();
            var area = AreaSummary.QuadArea(v[0], v[1], v[2], v[3]);
            if (double.IsFinite(area) && area > 0) return area;
        }

        return null;
    }

    private static string Chave(Guid id, string handle) => id.ToString("D") + "|" + handle;

    /// <summary>A tela acompanha, com o documento travado (só com interface: no Core Console não há tela).</summary>
    private static void Redesenhar(Document documento)
    {
        if (!ClivusExtension.TemInterface()) return;

        try
        {
            documento.Editor.Regen();
            AcadApp.UpdateScreen();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao redesenhar a tela depois da janela das áreas.", erro);
        }
    }
}

#if DEBUG
/// <summary>
/// Só no build de teste: o nível 2 chama, por LISP, o mesmo caminho dos
/// botões da janela Áreas, fora de comando.
/// (clivus-areas-pela-janela "ler") escreve uma linha AREAS_LINHA por linha da
/// janela; "hachurar" nome, "hachurar-todas", "tirar" nome, "tirar-todas",
/// "renomear" nome novo e "excel" caminho escrevem AREAS_ACAO com a frase.
/// Devolve T, ou nil se a ação não andou.
/// </summary>
public static class AreasTestHooks
{
    [LispFunction("CLIVUS-AREAS-PELA-JANELA")]
    public static object? AreasPelaJanela(ResultBuffer argumentos)
    {
        var valores = argumentos?.AsArray().Select(v => Convert.ToString(v.Value, CultureInfo.InvariantCulture) ?? string.Empty).ToArray() ?? [];
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 1 || documento is null) return null;

        var editor = documento.Editor;

        try
        {
            var todas = AreasDoDesenho.Varrer(documento.Database);
            AreaNoDesenho? Area(int i) => valores.Length > i ? todas.FirstOrDefault(a => a.Identity.DisplayName == valores[i]) : null;

            string frase;
            switch (valores[0])
            {
                case "ler":
                    var leitura = AreasDoDesenho.Ler(documento);
                    var inv = CultureInfo.InvariantCulture;
                    foreach (var l in leitura.Linhas)
                    {
                        var r = l.Resumo;
                        editor.WriteMessage(string.Format(inv,
                            "\nAREAS_LINHA tipo={0} nome={1} ha={2:0.0000} mesas={3} modulos={4} kwp={5:0.000} pilares={6} metros={7:0.00} divisa={8} ocupacao={9:0.0000} objetos={10} hachura={11} fim",
                            r.Kind, r.Name, r.Hectares ?? 0, r.Census.Tables, r.Census.Modules, r.Census.PowerKwp, r.Census.Pillars, r.PillarMeters,
                            r.OnBorder, r.Occupation ?? 0, r.FollowsObjects ? 1 : 0, l.Hachurada ? 1 : 0));
                    }

                    foreach (var aviso in leitura.Avisos) editor.WriteMessage($"\nAREAS_AVISO {aviso}");
                    editor.WriteMessage("\n");
                    return true;

                case "hachurar" when Area(1) is { } area:
                    frase = AreasDoDesenho.Hachurar(documento, todas, [area]);
                    break;

                case "hachurar-todas":
                    frase = AreasDoDesenho.Hachurar(documento, todas, null);
                    break;

                case "tirar" when Area(1) is { } area:
                    frase = AreasDoDesenho.TirarHachura(documento, todas, area);
                    break;

                case "tirar-todas":
                    frase = AreasDoDesenho.TirarHachura(documento, todas, null);
                    break;

                case "renomear" when Area(1) is { } area && valores.Length > 2:
                    frase = AreasDoDesenho.Renomear(documento, todas, area, valores[2]);
                    break;

                // O Renomear pela linha da janela (31/10/2026: "nao funciona o
                // renomear. o F2 nao funciona tbm"): a janela de verdade, o menu
                // do botão direito (ou o F2), a caixa do nome e o Enter.
                // "renomear-seguidas" atual novo atual novo...: vários na mesma
                // janela, F2 e menu alternados (01/11/2026: "consigo renomear
                // apenas uma linha depois o renomear para de funcionar"); uma
                // linha AREAS_SEGUIDA por nome, ok=1 se o nome velho existia e
                // agora só o novo existe.
                case "renomear-na-linha" or "renomear-f2" or "renomear-seguidas" when valores.Length > 2 && valores.Length % 2 == 1:
                {
                    if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                    {
                        editor.WriteMessage("\nAREAS_ACAO a linha nao e STA\n");
                        return null;
                    }

                    var janela = new JanelaDeAreas(documento);
                    try
                    {
                        var conteudo = (System.Windows.FrameworkElement)janela.Content;
                        conteudo.Measure(new System.Windows.Size(1100, 800));
                        conteudo.Arrange(new System.Windows.Rect(0, 0, 1100, 800));
                        conteudo.UpdateLayout();

                        frase = string.Empty;
                        for (var i = 1; i + 1 < valores.Length; i += 2)
                        {
                            var pelaTecla = valores[0] == "renomear-f2" || (valores[0] == "renomear-seguidas" && i % 4 == 1);
                            var existia = AreasDoDesenho.Varrer(documento.Database).Any(a => a.Identity.DisplayName == valores[i]);
                            frase = janela.RenomearComoNaTela(valores[i], valores[i + 1], pelaTecla);
                            if (valores[0] != "renomear-seguidas") continue;

                            var nomes = AreasDoDesenho.Varrer(documento.Database).Select(a => a.Identity.DisplayName).ToList();
                            var gravou = existia && nomes.Contains(valores[i + 1]) && !nomes.Contains(valores[i]);
                            editor.WriteMessage($"\nAREAS_SEGUIDA {i / 2 + 1} tecla={(pelaTecla ? 1 : 0)} ok={(gravou ? 1 : 0)} {frase}\n");
                        }
                    }
                    finally
                    {
                        janela.Close();
                    }

                    break;
                }

                case "excel" when valores.Length > 1:
                    frase = AreasDoDesenho.Exportar(documento, AreasDoDesenho.Ler(documento).Resumo, valores[1]);
                    break;

                default:
                    editor.WriteMessage($"\nAREAS_ACAO recusada: {string.Join(" ", valores)}\n");
                    return null;
            }

            editor.WriteMessage($"\nAREAS_ACAO {frase}\n");
            return true;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no gancho CLIVUS-AREAS-PELA-JANELA.", erro);
            editor.WriteMessage($"\nAREAS_ACAO ERRO {erro.Message}\n");
            return null;
        }
    }
}
#endif
