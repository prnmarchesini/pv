using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.LocalDosInversores))]

namespace Clivus.Plugin;

/// <summary>
/// O local dos inversores (Renan, 10/10/2026): "quero selecionar os
/// inversores, aí tem um botão de escolher área, eu clico no retângulo que
/// eu fiz em campo, que vai representar uma sala, um skid etc."; e a
/// "alocação automática conforme strings", que a rota CC faz no Gerar. O
/// registro <c>INVERSORES_LOCAL</c> guarda o modo de cada inversor; a área é
/// a polilinha fechada do usuário com a marca <see cref="SiteMark"/>.
/// </summary>
public static class LocalDosInversores
{
    private const string Chave = "INVERSORES_LOCAL";
    private static readonly string OQue = Tr.N("do local dos inversores");

    /// <summary>
    /// O local de cada inversor que tem um (os outros são postos à mão), PELA
    /// GEOMETRIA (item 2 da segunda rodada de 10/10/2026, reincidência): o
    /// inversor em campo com o centro dentro de uma área é dessa área, não
    /// importa como chegou lá (Escolher área, Pôr em campo, Mover, MOVE ou
    /// COPY do AutoCAD); fora de qualquer área, deixa de ser dela. A regra é
    /// uma só (<see cref="InverterSites.Reconcile"/>, no Core) e todo leitor
    /// do local passa por aqui: a tabela, o Pôr em campo, a rota de cabos.
    /// O registro gravado só acompanha (<see cref="Sincronizar"/>).
    /// </summary>
    internal static List<InverterPlacement> Ler(Database db, out string? problema)
    {
        var c = Conferir(db);
        problema = c.Problema;
        return c.Certo;
    }

    /// <summary>
    /// Numa leitura: o registro gravado, o certo pela geometria, as áreas do
    /// desenho e o problema do registro (se não se lê). Quem lê e grava (a
    /// tabela) usa esta, para não varrer o desenho duas vezes.
    /// </summary>
    internal static (List<InverterPlacement> Gravado, List<InverterPlacement> Certo, Dictionary<Guid, (SiteMark Marca, IReadOnlyList<Point3> Contorno)> Areas, string? Problema) Conferir(Database db)
    {
        var gravado = LerGravado(db, out var problema);
        var areas = Areas(db);
        var inversores = ConfiguracaoEletricaStore.Ler(db).Setup.Inverters.Select(i => i.Id).ToList();
        var certo = InverterSites.Reconcile(gravado, inversores, Centros(db), areas.Select(a => (a.Key, a.Value.Contorno)).ToList());
        return (gravado, certo, areas, problema);
    }

    /// <summary>O registro como está gravado (sem olhar o desenho).</summary>
    internal static List<InverterPlacement> LerGravado(Database db, out string? problema)
    {
        var lido = PluginRecords.Load(db, Chave, 1, InverterPlacement.FieldCount, InverterPlacement.Parse, OQue);
        problema = lido.Problem;
        return [.. lido.Items];
    }

    /// <summary>
    /// O centro (a posição do bloco) de cada inversor em campo. Com cópias
    /// (COPY), o da referência mais antiga (menor handle): a ordem das
    /// referências do AutoCAD não é garantida e o local não pode trocar de
    /// uma leitura para outra.
    /// </summary>
    internal static Dictionary<Guid, (double X, double Y)> Centros(Database db)
    {
        var centros = new Dictionary<Guid, (double, double)>();
        using var t = db.TransactionManager.StartOpenCloseTransaction();
        foreach (var ((tipo, id), ids) in EquipamentoEmCampo.Posicionados(t, db))
        {
            if (tipo != EquipmentKind.Inverter || ids.Count == 0) continue;
            var primeiro = ids.OrderBy(x => x.Handle.Value).First();
            if (t.GetObject(primeiro, OpenMode.ForRead) is BlockReference b) centros[id] = (b.Position.X, b.Position.Y);
        }

        return centros;
    }

    /// <summary>
    /// Grava o registro como a geometria diz (só se mudou: abrir a janela
    /// não suja o desenho à toa). Registro ilegível: não grava (o problema).
    /// Quem chama trava o documento. Se gravou.
    /// </summary>
    internal static bool Sincronizar(Database db, out string? problema)
    {
        var c = Conferir(db);
        problema = c.Problema;
        if (problema is not null || InverterSites.SamePlacements(c.Gravado, c.Certo)) return false;
        Gravar(db, c.Certo);
        return true;
    }

    internal static void Gravar(Database db, IReadOnlyList<InverterPlacement> locais) =>
        PluginRecords.Save(db, Chave, 1, InverterPlacement.FieldCount, locais, p => p.ToFields());

    /// <summary>Troca o local dos inversores (null = à mão). Recusa, com o motivo, se o registro está ilegível.</summary>
    internal static string? Mudar(Database db, IReadOnlyCollection<Guid> inversores, InverterPlacementMode? modo, Guid area = default)
    {
        var lista = Ler(db, out var problema);
        if (problema is not null) return problema;
        Gravar(db, InverterPlacement.With(lista, inversores, modo, area));
        return null;
    }

    /// <summary>
    /// "Automático pelas strings" (itens 17 e 19 de 10/10/2026): os inversores
    /// passam a ser postos pela rota CC, e o retângulo de quem já estava em
    /// campo sai do desenho ("se eu já tiver inserido e clicar aqui, o sistema
    /// apaga os inversores em campo"). A frase, ou o problema se nada mudou.
    /// Quem chama trava o documento (a aba pelo Fazer, o comando por si).
    /// </summary>
    internal static (string? Frase, string? Problema) TornarAutomaticos(Database db, IReadOnlyCollection<Guid> inversores)
    {
        if (Mudar(db, inversores, InverterPlacementMode.Automatic) is { } problema) return (null, problema);

        var apagados = 0;
        foreach (var id in inversores)
            if (EquipamentoEmCampo.Apagar(db, EquipmentKind.Inverter, id) > 0) apagados++;

        var frase = Tr.F("{0} inversor(es) com alocação automática: o Gerar da rota CC põe cada um ao lado da vala, no ponto de menor cabo CC das strings dele.", inversores.Count);
        if (apagados > 0) frase += " " + Tr.F("{0} retângulo(s) que estavam em campo foram apagados.", apagados);
        return (frase, null);
    }

    /// <summary>
    /// "À mão": os inversores voltam ao Pôr em campo de sempre (a posição de
    /// agora fica). Quem está com o centro dentro de uma área continua dela
    /// (a geometria manda) e a frase diz quantos.
    /// </summary>
    internal static (string? Frase, string? Problema) TornarManuais(Database db, IReadOnlyCollection<Guid> inversores)
    {
        if (Mudar(db, inversores, null) is { } problema) return (null, problema);
        var frase = Tr.F("{0} inversor(es) de volta ao Pôr em campo à mão (a posição de agora fica).", inversores.Count);
        var naArea = Ler(db, out _).Count(l => l.Mode == InverterPlacementMode.Area && inversores.Contains(l.Inverter));
        if (naArea > 0) frase += " " + Tr.F("{0} continua(m) numa área: o centro está dentro dela (mova para fora para deixar de ser da área).", naArea);
        return (frase, null);
    }

    /// <summary>
    /// Renomeia a área (item 6 de 10/10/2026): o nome novo vai na marca da
    /// polilinha. A frase, ou o porquê de não mudar (nome vazio ou repetido,
    /// área que sumiu). Quem chama trava o documento.
    /// </summary>
    internal static (string? Frase, string? Problema) RenomearArea(Database db, Guid area, string? nome)
    {
        var areas = Areas(db);
        if (!areas.TryGetValue(area, out var atual)) return (null, Tr.T("essa área não está mais no desenho"));

        var limpo = nome?.Trim() ?? string.Empty;
        if (limpo == atual.Marca.Name) return (null, null);
        if (SiteMark.NameProblem(limpo, areas.Values.Where(a => a.Marca.Id != area).Select(a => a.Marca.Name)) is { } porque) return (null, porque);

        using var t = db.TransactionManager.StartTransaction();
        var espaco = (BlockTableRecord)t.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
        var classe = RXObject.GetClass(typeof(Curve));
        var mudou = 0;
        foreach (ObjectId id in espaco)
        {
            if (!id.ObjectClass.IsDerivedFrom(classe) || t.GetObject(id, OpenMode.ForRead) is not Curve c || Marca(c) is not { } marca || marca.Id != area) continue;
            c.UpgradeOpen();
            PluginXData.Save(t, c, SiteMark.Tipo, 1, [.. (marca with { Name = limpo }).ToFields()]);
            mudou++;
        }

        t.Commit();
        return mudou > 0 ? (Tr.F("{0} agora se chama {1}.", atual.Marca.Name, limpo), null) : (null, Tr.T("essa área não está mais no desenho"));
    }

    /// <summary>
    /// CLIVUS_ELETRICA_AREA_RENOMEAR, o "Renomear área de inversores…" do
    /// botão direito na polilinha da área (item 3 da segunda rodada de
    /// 10/10/2026: "o sistema deu o nome de área 1 mas não sei como mudar"):
    /// a área da seleção (ou o clique nela) e o nome novo (Enter mantém).
    /// </summary>
    [CommandMethod(PluginInfo.ComandoEletricaAreaRenomear, CommandFlags.Modal | CommandFlags.UsePickSet)]
    public static void RenomearAreaPeloDesenho()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;
        string? recado = null;

        try
        {
            var db = documento.Database;
            SiteMark? marca = null;
            var escolhidos = editor.SelectImplied();
            if (escolhidos.Status == PromptStatus.OK && escolhidos.Value is not null)
                marca = escolhidos.Value.GetObjectIds().Select(id => MarcaDe(db, id)).FirstOrDefault(m => m is not null);

            if (marca is null)
            {
                var opcoes = new PromptEntityOptions(Tr.T("\nClique na polilinha da área de inversores: "));
                opcoes.SetRejectMessage(Tr.T("\nTem que ser uma polilinha."));
                opcoes.AddAllowedClass(typeof(Polyline), false);
                opcoes.AddAllowedClass(typeof(Polyline2d), false);
                opcoes.AddAllowedClass(typeof(Polyline3d), false);
                var clicado = editor.GetEntity(opcoes);
                if (clicado.Status != PromptStatus.OK) return;
                marca = MarcaDe(db, clicado.ObjectId);
                if (marca is null)
                {
                    editor.WriteMessage(Tr.T("\nAREA Essa polilinha não é uma área de inversores (use Escolher área… na aba Inversor).\n"));
                    return;
                }
            }

            var nome = editor.GetString(new PromptStringOptions(Tr.F("\nNome novo da área <{0}>: ", marca.Name)) { AllowSpaces = true });
            if (nome.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(nome.StringResult)) return;

            var (frase, problema) = RenomearArea(db, marca.Id, nome.StringResult);
            recado = problema is null ? frase : Tr.F("Não renomeei a área: {0}.", problema);
            if (recado is not null) editor.WriteMessage("\nAREA " + recado + "\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao renomear a área de inversores pelo desenho.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui renomear a área: {0}\n", erro.Message));
        }
        finally
        {
            JanelaEletrica.Voltar(documento, recado);
        }
    }

    /// <summary>A marca de área da entidade, se ela tem uma.</summary>
    private static SiteMark? MarcaDe(Database db, ObjectId id)
    {
        using var t = db.TransactionManager.StartOpenCloseTransaction();
        return t.GetObject(id, OpenMode.ForRead) is Entity e ? Marca(e) : null;
    }

    /// <summary>As áreas de inversores do desenho: a marca e o contorno em planta.</summary>
    internal static Dictionary<Guid, (SiteMark Marca, IReadOnlyList<Point3> Contorno)> Areas(Database db)
    {
        var areas = new Dictionary<Guid, (SiteMark, IReadOnlyList<Point3>)>();
        using var t = db.TransactionManager.StartOpenCloseTransaction();
        var espaco = (BlockTableRecord)t.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
        var classe = RXObject.GetClass(typeof(Curve));

        foreach (ObjectId id in espaco)
        {
            if (!id.ObjectClass.IsDerivedFrom(classe) || t.GetObject(id, OpenMode.ForRead) is not Curve c || Marca(c) is not { } marca) continue;
            if (Contorno(c, t) is { Count: >= 3 } contorno) areas[marca.Id] = (marca, contorno);
        }

        return areas;
    }

    internal static SiteMark? Marca(Entity e) =>
        PluginXData.Load(e, SiteMark.Tipo, 1, SiteMark.FieldCount) is { } c ? SiteMark.Parse(c) : null;

    /// <summary>
    /// Os vértices de uma polilinha fechada (ou com a ponta no começo), com a
    /// cota que têm (o ponto que cair fora do terreno fica com ela, nunca
    /// zero) e os arcos divididos em trechos de até 1 m; sem repetir o
    /// primeiro no fim. Null se não é fechada.
    /// </summary>
    private static List<Point3>? Contorno(Curve c, Transaction t)
    {
        if (!c.Closed && c.StartPoint.DistanceTo(c.EndPoint) > 1e-6) return null;

        var pontos = new List<Point3>();
        switch (c)
        {
            case Polyline p:
                for (var i = 0; i < p.NumberOfVertices; i++)
                {
                    var q = p.GetPoint3dAt(i);
                    pontos.Add(new Point3(q.X, q.Y, q.Z));
                    if (p.GetBulgeAt(i) == 0 || (i == p.NumberOfVertices - 1 && !p.Closed)) continue;

                    var ate = i == p.NumberOfVertices - 1 ? p.EndParam : i + 1;
                    var partes = Math.Max(2, (int)Math.Ceiling(p.GetDistanceAtParameter(ate) - p.GetDistanceAtParameter(i)));
                    for (var k = 1; k < partes; k++)
                    {
                        var r = p.GetPointAtParameter(i + (ate - i) * k / partes);
                        pontos.Add(new Point3(r.X, r.Y, r.Z));
                    }
                }

                break;
            case Polyline3d p3:
                pontos.AddRange(p3.Cast<ObjectId>().Where(v => !v.IsErased)
                    .Select(v => ((PolylineVertex3d)t.GetObject(v, OpenMode.ForRead)).Position).Select(q => new Point3(q.X, q.Y, q.Z)));
                break;
            case Polyline2d p2:
                // Arco, spline e OCS: a própria curva dá os pontos no desenho, um a cada metro.
                var total = p2.GetDistanceAtParameter(p2.EndParam);
                var passos = Math.Max(3, (int)Math.Ceiling(total));
                for (var k = 0; k < passos; k++)
                {
                    var r = p2.GetPointAtDist(total * k / passos);
                    pontos.Add(new Point3(r.X, r.Y, r.Z));
                }

                break;
            default:
                return null;
        }

        if (pontos.Count > 1 && Math.Abs(pontos[0].X - pontos[^1].X) < 1e-6 && Math.Abs(pontos[0].Y - pontos[^1].Y) < 1e-6) pontos.RemoveAt(pontos.Count - 1);
        return pontos;
    }

    /// <summary>
    /// CLIVUS_ELETRICA_LOCAL: o modo (Area, Automatico ou Manual) e os
    /// inversores (nomes ou GUIDs separados por ";"). Área: clique na
    /// polilinha fechada; ela ganha a marca de área (a que já tinha fica) e
    /// os inversores são postos dentro dela, um ao lado do outro, na cota do
    /// terreno + 0,80. Automático: o Gerar da rota CC põe ao lado da vala.
    /// Manual: volta ao Pôr em campo de sempre (a posição de agora fica).
    /// </summary>
    [CommandMethod(PluginInfo.ComandoEletricaLocal)]
    public static void Local()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        // O que a janela mostra quando volta (item 2 da segunda rodada: o "0 de 2"
        // ia só para a linha de comando, atrás da janela, e o Renan não viu).
        (string Texto, bool Erro)? recado = null;

        try
        {
            var modo = new PromptKeywordOptions(Tr.T("\nLocal dos inversores [Area/Strings/Manual]: ")) { AllowNone = false };
            modo.Keywords.Add("Area");
            modo.Keywords.Add("Strings");
            modo.Keywords.Add("Manual");
            var qual = editor.GetKeywords(modo);
            if (qual.Status != PromptStatus.OK) return;

            var lista = editor.GetString(new PromptStringOptions(Tr.T("\nInversores (nomes separados por ;): ")) { AllowSpaces = true });
            if (lista.Status != PromptStatus.OK) return;

            var db = documento.Database;
            var (setup, _) = ConfiguracaoEletricaStore.Ler(db);
            var inversores = new List<Inverter>();
            foreach (var nome in lista.StringResult.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var achado = Guid.TryParse(nome, out var g) ? setup.FindInverter(g) : setup.Inverters.FirstOrDefault(i => string.Equals(i.Name, nome, StringComparison.CurrentCultureIgnoreCase));
                if (achado is null) editor.WriteMessage(Tr.F("\nLOCAL Não há inversor \"{0}\".\n", nome));
                else if (!inversores.Contains(achado)) inversores.Add(achado);
            }

            if (inversores.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nLOCAL Nenhum inversor; nada mudou.\n"));
                return;
            }

            var ids = inversores.Select(i => i.Id).ToList();

            if (qual.StringResult != "Area")
            {
                var (fraseDoModo, problema) = qual.StringResult == "Strings" ? TornarAutomaticos(db, ids) : TornarManuais(db, ids);
                recado = problema is null ? (fraseDoModo ?? string.Empty, false) : (Tr.F("Não gravei o local: {0}", problema), true);
                editor.WriteMessage(problema is null ? "\nLOCAL " + fraseDoModo + "\n" : Tr.F("\nLOCAL Não gravei: {0}\n", problema));
                return;
            }

            if (FileiraCommands.ExigirTerreno(editor, documento) is not { } terreno) return;

            var opcoes = new PromptEntityOptions(Tr.T("\nClique no retângulo (polilinha fechada) da área dos inversores: "));
            opcoes.SetRejectMessage(Tr.T("\nTem que ser uma polilinha."));
            opcoes.AddAllowedClass(typeof(Polyline), false);
            opcoes.AddAllowedClass(typeof(Polyline2d), false);
            opcoes.AddAllowedClass(typeof(Polyline3d), false);
            var clicado = editor.GetEntity(opcoes);
            if (clicado.Status != PromptStatus.OK) return;

            // Área nova: o nome (item 6 de 10/10/2026), com "Área N" de padrão (Enter aceita).
            string? nomeDaArea = null;
            if (MarcaDe(db, clicado.ObjectId) is null)
            {
                var existentes = Areas(db).Values.Select(a => a.Marca.Name).ToList();
                var padrao = SiteMark.NextDefaultName(existentes);
                while (true)
                {
                    var r = editor.GetString(new PromptStringOptions(Tr.F("\nNome da área <{0}>: ", padrao)) { AllowSpaces = true });
                    if (r.Status != PromptStatus.OK) return;
                    var digitado = string.IsNullOrWhiteSpace(r.StringResult) ? padrao : r.StringResult.Trim();
                    if (SiteMark.NameProblem(digitado, existentes) is { } porque)
                    {
                        editor.WriteMessage(Tr.F("\nLOCAL {0}; digite outro.\n", porque));
                        continue;
                    }

                    nomeDaArea = digitado;
                    break;
                }
            }

            // Antes de mexer no campo: o registro tem que se ler (senão o que for posto não fica gravado).
            LerGravado(db, out var ilegivel);
            if (ilegivel is not null)
            {
                recado = (Tr.F("Não gravei o local: {0}", ilegivel), true);
                editor.WriteMessage(Tr.F("\nLOCAL Não gravei: {0}\n", ilegivel));
                return;
            }

            var area = MarcarArea(db, clicado.ObjectId, terreno.Mesh, nomeDaArea, out var recusa, out var pontosFora);
            if (area is null)
            {
                if (recusa.Length > 0) recado = (recusa, true);
                editor.WriteMessage("\nLOCAL " + recusa + "\n");
                return;
            }

            if (pontosFora > 0)
                editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0} ponto(s) da área fora do terreno ficaram com a cota que tinham.\n", pontosFora));

            // As vagas ocupadas (inversores que já estão em campo, mesas, outros equipamentos)
            // ficam de fora; os que estão sendo postos agora não contam no lugar velho.
            // O Encher (item 2 da segunda rodada): a grade de sempre e, para quem
            // não coube nela, a vaga livre entre os que já estão lá, com a folga reduzida se preciso.
            var obstaculos = LeituraDaRota.Ler(db).Obstaculos(ids.ToHashSet());
            var tamanhos = inversores.Select(i => Tamanho(setup, i)).ToList();
            var cheia = InverterSites.Fill(area.Value.Contorno, tamanhos, obstaculos);
            var postos = new List<Guid>();
            for (var i = 0; i < inversores.Count; i++)
                if (cheia.Centers[i] is { } c && setup.FindEquipment(EquipmentKind.Inverter, inversores[i].Id) is { } equipamento
                    && ConfiguracaoEletricaCommands.NoTerreno(editor, db, terreno, equipamento, c.X, c.Y))
                    postos.Add(inversores[i].Id);

            // Só quem entrou na área fica com ela (item 4: a coluna Local e o botão
            // não podem divergir); quem não coube continua como estava. O registro
            // segue a geometria (o centro dentro do contorno), como em todo caminho.
            if (postos.Count > 0 && Mudar(db, postos, InverterPlacementMode.Area, area.Value.Marca.Id) is { } erro)
            {
                recado = (Tr.F("Não gravei o local: {0}", erro), true);
                editor.WriteMessage(Tr.F("\nLOCAL Não gravei: {0}\n", erro));
                return;
            }

            Sincronizar(db, out _);

            var frase = Tr.F("{0} de {1} inversor(es) postos na {2}.", postos.Count, inversores.Count, area.Value.Marca.Name);
            var reduzidos = Enumerable.Range(0, inversores.Count).Where(i => postos.Contains(inversores[i].Id) && cheia.Gaps[i] < InverterSites.Gap).ToList();
            if (reduzidos.Count > 0)
                frase += " " + Tr.F("{0} entraram com folga de {1:0.00} m (a de {2:0.00} m não cabia): {3}.",
                    reduzidos.Count, reduzidos.Min(i => cheia.Gaps[i]!.Value), InverterSites.Gap, string.Join(", ", reduzidos.Select(i => inversores[i].Name)));

            var fora = inversores.Where(i => !postos.Contains(i.Id)).ToList();
            if (fora.Count > 0)
            {
                // Por quê: as medidas da área, quantos já estão nela e a caixa que não achou vaga.
                var (comprido, curto) = InverterSites.Measures(area.Value.Contorno);
                var jaDentro = Ler(db, out _).Count(l => l.Mode == InverterPlacementMode.Area && l.Site == area.Value.Marca.Id) - postos.Count;
                var (w, l) = Tamanho(setup, fora[0]);
                frase += " " + Tr.F("ATENÇÃO: a área {0} é pequena: couberam {1} de {2}; ficaram de fora: {3}. Ela mede {4:0.00} × {5:0.00} m, já tinha {6} inversor(es) e não tem vaga livre de {7:0.00} × {8:0.00} m nem com folga de {9:0.00} m entre as caixas. Aumente o retângulo, escolha outra área ou ponha à mão.",
                    area.Value.Marca.Name, postos.Count, inversores.Count, string.Join(", ", fora.Select(i => i.Name)), comprido, curto, Math.Max(0, jaDentro), w, l, InverterSites.MinGap);
            }

            recado = (frase, fora.Count > 0);
            editor.WriteMessage("\nLOCAL " + frase + "\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao escolher o local dos inversores.", erro);
            recado = (Tr.F("Não consegui escolher o local dos inversores: {0}", erro.Message), true);
            editor.WriteMessage(Tr.F("\nNão consegui escolher o local dos inversores: {0}\n", erro.Message));
        }
        finally
        {
            JanelaEletrica.Voltar(documento, recado?.Texto, recado?.Erro ?? false);
        }
    }

    /// <summary>
    /// CLIVUS_ELETRICA_VER, o "Ver em campo" da linha (item 4 de 10/10/2026:
    /// "seleciona o inversor, o modal sai, e mostra o inversor selecionado, se
    /// eu der esc, o modal volta"): o inversor (nome ou GUID); o retângulo dele
    /// fica realçado, com zoom nele, até o Esc (ou Enter); depois fica
    /// selecionado (seleção implícita) e a janela volta. Nada é gravado.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoEletricaVer, CommandFlags.Modal | CommandFlags.Redraw | CommandFlags.NoUndoMarker)]
    public static void Ver()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;
        var realcados = new List<ObjectId>();

        try
        {
            var qual = editor.GetString(new PromptStringOptions(Tr.T("\nInversor (nome): ")) { AllowSpaces = true });
            if (qual.Status != PromptStatus.OK) return;

            var db = documento.Database;
            var (setup, _) = ConfiguracaoEletricaStore.Ler(db);
            if (setup.FindInverter(qual.StringResult) is not { } inversor)
            {
                editor.WriteMessage(Tr.F("\nINVERSOR Não há inversor \"{0}\" no cadastro.\n", qual.StringResult.Trim()));
                return;
            }

            List<ObjectId> ids;
            Extents3d? limites = null;
            using (var t = db.TransactionManager.StartOpenCloseTransaction())
            {
                ids = EquipamentoEmCampo.Posicionados(t, db).GetValueOrDefault((EquipmentKind.Inverter, inversor.Id)) ?? [];
                foreach (var id in ids)
                {
                    if (t.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
                    try
                    {
                        var x = e.GeometricExtents;
                        limites = limites is { } atual ? Juntar(atual, x) : x;
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception erro)
                    {
                        RegistroDeDiagnostico.Registrar("Retângulo do inversor sem limites; o Ver em campo não dá zoom nele.", erro);
                    }
                }
            }

            if (ids.Count == 0)
            {
                editor.WriteMessage(Tr.F("\nINVERSOR {0} não está em campo.\n", inversor.Name));
                return;
            }

            if (limites is { } caixa) Zoom(editor, caixa);

            using (var t = db.TransactionManager.StartOpenCloseTransaction())
                foreach (var id in ids)
                    if (t.GetObject(id, OpenMode.ForRead) is Entity e)
                    {
                        e.Highlight();
                        realcados.Add(id);
                    }

            editor.WriteMessage(Tr.F("\nINVERSOR {0} em campo, realçado.\n", inversor.Name));
            editor.GetString(new PromptStringOptions(Tr.T("\nEsc (ou Enter) volta à janela: ")) { AllowSpaces = false });

            Desrealcar(db, realcados);
            realcados.Clear();
            editor.SetImpliedSelection([.. ids]);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao mostrar o inversor em campo.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui mostrar o inversor em campo: {0}\n", erro.Message));
        }
        finally
        {
            Desrealcar(documento.Database, realcados);
            JanelaEletrica.Voltar(documento);
        }
    }

    private static Extents3d Juntar(Extents3d a, Extents3d b) =>
        new(new Point3d(Math.Min(a.MinPoint.X, b.MinPoint.X), Math.Min(a.MinPoint.Y, b.MinPoint.Y), Math.Min(a.MinPoint.Z, b.MinPoint.Z)),
            new Point3d(Math.Max(a.MaxPoint.X, b.MaxPoint.X), Math.Max(a.MaxPoint.Y, b.MaxPoint.Y), Math.Max(a.MaxPoint.Z, b.MaxPoint.Z)));

    private static void Desrealcar(Database db, IReadOnlyList<ObjectId> ids)
    {
        if (ids.Count == 0) return;
        try
        {
            using var t = db.TransactionManager.StartOpenCloseTransaction();
            foreach (var id in ids)
                if (!id.IsErased && t.GetObject(id, OpenMode.ForRead) is Entity e) e.Unhighlight();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao tirar o realce do inversor.", erro);
        }
    }

    /// <summary>
    /// Zoom na caixa (com folga: o retângulo ocupa um terço da tela, para ver
    /// em volta). Só com interface (no Core Console não há vista).
    /// </summary>
    private static void Zoom(Editor editor, Extents3d caixa)
    {
        if (!ClivusExtension.TemInterface()) return;

        try
        {
            using var vista = editor.GetCurrentView();
            var paraTela = (Matrix3d.Rotation(-vista.ViewTwist, vista.ViewDirection, vista.Target)
                * Matrix3d.Displacement(vista.Target - Point3d.Origin)
                * Matrix3d.PlaneToWorld(vista.ViewDirection)).Inverse();
            var a = caixa.MinPoint.TransformBy(paraTela);
            var b = caixa.MaxPoint.TransformBy(paraTela);
            var largura = Math.Max(Math.Abs(b.X - a.X), 1) * 3;
            var altura = Math.Max(Math.Abs(b.Y - a.Y), 1) * 3;
            var proporcao = vista.Width / Math.Max(vista.Height, 1e-9);
            if (largura / altura < proporcao) largura = altura * proporcao;
            else altura = largura / proporcao;

            vista.CenterPoint = new Point2d((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            vista.Width = largura;
            vista.Height = altura;
            editor.SetCurrentView(vista);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no zoom do Ver em campo.", erro);
        }
    }

    /// <summary>A largura e o comprimento do retângulo do inversor (do modelo).</summary>
    internal static (double Width, double Length) Tamanho(ElectricalSetup setup, Inverter inversor) =>
        setup.FindEquipment(EquipmentKind.Inverter, inversor.Id) is { } e ? (e.Size.Width, e.Size.Length) : (1, 1);

    /// <summary>
    /// A polilinha fechada vira área de inversores: ganha a marca (a que já
    /// tinha fica, com o mesmo GUID) e é assentada no terreno (regra
    /// universal: todo desenho respeita o TIN), virando uma Polyline3d
    /// fechada com as propriedades e o XData da antiga. Null, com o motivo, se
    /// não é fechada ou se é uma polilinha do próprio Clivus (mesa, área da
    /// usina, vala, string: não vira sala).
    /// </summary>
    private static (SiteMark Marca, IReadOnlyList<Point3> Contorno)? MarcarArea(Database db, ObjectId id, Tin terreno, string? nome, out string recusa, out int pontosFora)
    {
        recusa = string.Empty;
        pontosFora = 0;
        var existentes = Areas(db);

        using var t = db.TransactionManager.StartTransaction();
        if (t.GetObject(id, OpenMode.ForRead) is not Curve curva) return null;

        var marca = Marca(curva);
        using (var nossa = curva.GetXDataForApplication(PluginInfo.PrefixoDeDados))
        {
            if (marca is null && nossa is not null)
            {
                recusa = Tr.T("Essa polilinha é do Clivus Solar (mesa, área, vala, string...): desenhe um retângulo próprio para a sala.");
                return null;
            }
        }

        if (Contorno(curva, t) is not { Count: >= 3 } contorno)
        {
            recusa = Tr.T("A polilinha não é fechada: feche-a (ou desenhe um retângulo) e tente de novo.");
            return null;
        }

        marca ??= new SiteMark(Guid.NewGuid(), nome ?? SiteMark.NextDefaultName(existentes.Values.Select(a => a.Marca.Name).ToList()));

        // Fechada: o primeiro vértice repetido no fim, para o drapeado fechar o último lado.
        var drapeada = Draping.Along(terreno, [.. contorno, contorno[0]]);
        pontosFora = drapeada.OutsideCount;
        var noChao = drapeada.Vertices.ToList();
        if (noChao.Count > 1 && Math.Abs(noChao[0].X - noChao[^1].X) < 1e-6 && Math.Abs(noChao[0].Y - noChao[^1].Y) < 1e-6) noChao.RemoveAt(noChao.Count - 1);

        var espaco = (BlockTableRecord)t.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
        var nova = new Polyline3d { Closed = true };
        nova.SetPropertiesFrom(curva);
        espaco.AppendEntity(nova);
        t.AddNewlyCreatedDBObject(nova, true);
        foreach (var p in noChao)
        {
            var v = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z));
            nova.AppendVertex(v);
            t.AddNewlyCreatedDBObject(v, true);
        }

        using (var xdata = curva.XData) nova.XData = xdata;
        PluginXData.Save(t, nova, SiteMark.Tipo, 1, [.. marca.ToFields()]);

        curva.UpgradeOpen();
        curva.Erase();
        t.Commit();
        return (marca, contorno);
    }
}
