#if DEBUG
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.RotaDeCabosConferir))]

namespace Clivus.Plugin;

/// <summary>
/// Só no build de teste (nível 2): lê do desenho cada vala e cada cabo da
/// rota de cabos e confere a cota de cada vértice contra o TIN no mesmo XY
/// (regra universal de 10/10/2026: todo desenho respeita o TIN). A vala tem
/// que estar na cota do terreno menos a profundidade da aba; o cabo, entre o
/// fundo da vala e a base do equipamento (terreno + 0,80). Escreve os
/// vértices dos cabos em planta para o runner conferir o caminho.
/// </summary>
public static class RotaDeCabosConferir
{
    [CommandMethod(PluginInfo.ComandoRotaConferirAutomatico)]
    public static void Conferir()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            if (FileiraCommands.ExigirTerreno(editor, documento) is not { } terreno) return;
            var db = documento.Database;
            var config = RotaDeCabosStore.Configuracoes(db, out _);
            var inv = System.Globalization.CultureInfo.InvariantCulture;

            using var t = db.TransactionManager.StartOpenCloseTransaction();
            var espaco = (BlockTableRecord)t.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);

            foreach (ObjectId id in espaco)
            {
                if (t.GetObject(id, OpenMode.ForRead) is not Curve curva) continue;
                var vala = RotaDeCabosStore.Vala(curva);
                var lance = curva is Polyline3d ? RotaDeCabosStore.Lance(curva) : null;
                if (vala is null && lance is null) continue;

                var pontos = new List<Point3>();
                if (curva is Polyline3d p3)
                    foreach (ObjectId v in p3.Cast<ObjectId>().Where(v => !v.IsErased))
                    {
                        var p = ((PolylineVertex3d)t.GetObject(v, OpenMode.ForRead)).Position;
                        pontos.Add(new Point3(p.X, p.Y, p.Z));
                    }
                else
                    pontos.Add(new Point3(curva.StartPoint.X, curva.StartPoint.Y, curva.StartPoint.Z));

                var rota = vala?.Route ?? lance!.Route;
                var fundo = config[rota].Depth;
                double maiorDesvio = 0, abaixo = 0, acima = 0;
                var fora = 0;

                for (var i = 0; i < pontos.Count; i++)
                {
                    var p = pontos[i];
                    if (!terreno.Mesh.TryGetZ(p.X, p.Y, out var z))
                    {
                        fora++;
                        continue;
                    }

                    if (vala is not null) maiorDesvio = Math.Max(maiorDesvio, Math.Abs(p.Z - (z - fundo)));
                    else
                    {
                        // As pontas são o módulo da string ou a base do equipamento: só o miolo
                        // tem que ficar entre o fundo da vala e o terreno + 0,80.
                        abaixo = Math.Max(abaixo, (z - fundo) - p.Z);
                        if (i > 0 && i < pontos.Count - 1) acima = Math.Max(acima, p.Z - (z + EquipmentFootprint.FloatHeight));
                    }
                }

                if (vala is not null)
                {
                    editor.WriteMessage(string.Format(inv, "\nROTA_VALA rota={0} tipo={1} vertices={2} fora={3} desvio={4:0.0000}\n",
                        rota, curva.GetType().Name, pontos.Count, fora, maiorDesvio));
                }
                else
                {
                    editor.WriteMessage(string.Format(inv, "\nROTA_CABO rota={0} vertices={1} fora={2} abaixo={3:0.0000} acima={4:0.0000} zmin={5:0.###} planta={6}\n",
                        rota, pontos.Count, fora, abaixo, acima, pontos.Min(p => p.Z),
                        string.Join(";", pontos.Select(p => string.Format(inv, "{0:0.###},{1:0.###}", p.X, p.Y)))));
                }
            }

            // Os equipamentos em campo: a base contra o TIN no centro (+ 0,80) e a pegada da caixa em planta.
            var setup = ConfiguracaoEletricaStore.Ler(db).Setup;
            foreach (var ((tipo, guid), ids) in EquipamentoEmCampo.Posicionados(t, db))
            {
                if (ids.Count == 0 || t.GetObject(ids[0], OpenMode.ForRead) is not BlockReference b) continue;
                var tag = setup.FindEquipment(tipo, guid)?.Tag ?? "?";
                var desvio = terreno.Mesh.TryGetZ(b.Position.X, b.Position.Y, out var chao) ? Math.Abs(b.Position.Z - (chao + EquipmentFootprint.FloatHeight)) : double.NaN;
                var caixa = setup.FindEquipment(tipo, guid)?.Size;
                var (w, l) = caixa is null ? (0.0, 0.0) : (caixa.Width / 2, caixa.Length / 2);
                editor.WriteMessage(string.Format(inv, "\nROTA_EQUIP tag={0} desvio={1:0.0000} minx={2:0.###} miny={3:0.###} maxx={4:0.###} maxy={5:0.###} fim\n",
                    tag.Replace(' ', '_'), desvio, b.Position.X - w, b.Position.Y - l, b.Position.X + w, b.Position.Y + l));
            }

            // As áreas de inversores: cada vértice no terreno daquele XY.
            foreach (var (_, (marca, _)) in LocalDosInversores.Areas(db))
            {
                foreach (ObjectId id in espaco)
                {
                    if (t.GetObject(id, OpenMode.ForRead) is not Polyline3d p3 || PluginXData.Load(p3, SiteMark.Tipo, 1, SiteMark.FieldCount) is not { } c || SiteMark.Parse(c)?.Id != marca.Id) continue;
                    double maior = 0;
                    var fora = 0;
                    foreach (var v in p3.Cast<ObjectId>().Where(v => !v.IsErased))
                    {
                        var p = ((PolylineVertex3d)t.GetObject(v, OpenMode.ForRead)).Position;
                        if (terreno.Mesh.TryGetZ(p.X, p.Y, out var z)) maior = Math.Max(maior, Math.Abs(p.Z - z));
                        else fora++;
                    }

                    editor.WriteMessage(string.Format(inv, "\nROTA_AREA nome={0} fechada={1} fora={2} desvio={3:0.0000} fim\n", marca.Name.Replace(' ', '_'), p3.Closed, fora, maior));
                }
            }

            // Item 10: cada lance CC (com o destino e o handle) e, por mesa, se todos os
            // cabos das strings dela passam por um ponto comum fora dela, e de que lado.
            Lances(editor, db, t, inv);
            Acessos(editor, db, inv);

            editor.WriteMessage("\nROTA_CONFERIR_FIM\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao conferir a rota de cabos (teste).", erro);
            editor.WriteMessage($"\nROTA_CONFERIR_ERRO {erro.Message}\n");
        }
    }

    /// <summary>
    /// ROTA_LANCE: cada lance CC com o handle, a string, o destino e as pontas
    /// em planta. ROTA_ENTRADA: por mesa com cabo CC, quantos cabos saem das
    /// pontas de string dela, se há um ponto comum a todos fora dela (a entrada
    /// única) e se ele fica do lado da borda mais alta da mesa.
    /// </summary>
    private static void Lances(Autodesk.AutoCAD.EditorInput.Editor editor, Database db, Transaction t, System.Globalization.CultureInfo inv)
    {
        var leitura = LeituraDaRota.Ler(db);
        var setup = leitura.Setup;
        var strings = leitura.StringsComPontas.ToDictionary(x => x.String.Id, x => x.String);
        var porMesa = new Dictionary<Guid, (RowTable Mesa, List<HashSet<(double, double)>> Cabos)>();

        foreach (var l in RotaDeCabosStore.Lances(db).Where(l => l.Lance.Route == CableRoute.DirectCurrent))
        {
            var pontos = new List<Point3>();
            if (t.GetObject(l.Id, OpenMode.ForRead) is Polyline3d p3)
                foreach (ObjectId v in p3.Cast<ObjectId>().Where(v => !v.IsErased))
                {
                    var p = ((PolylineVertex3d)t.GetObject(v, OpenMode.ForRead)).Position;
                    pontos.Add(new Point3(p.X, p.Y, p.Z));
                }

            if (pontos.Count < 2) continue;
            var s = strings.GetValueOrDefault(l.Lance.From.Id);
            editor.WriteMessage(string.Format(inv, "\nROTA_LANCE handle={0} string={1} para={2} polaridade={3} de={4:0.###},{5:0.###} ate={6:0.###},{7:0.###} fim\n",
                l.Id.Handle, (string.IsNullOrWhiteSpace(s?.Tag) ? "?" : s.Tag).Replace(' ', '_'), (setup.FindInverter(l.Lance.To.Id)?.Name ?? "?").Replace(' ', '_'), l.Lance.Polarity,
                pontos[0].X, pontos[0].Y, pontos[^1].X, pontos[^1].Y));

            if (s is null) continue;
            var modulo = l.Lance.Polarity == CablePolarity.Negative ? s.Modules[^1] : s.Modules[0];
            if (leitura.MesaDoModulo(modulo) is not { } mesa) continue;
            if (!porMesa.TryGetValue(mesa.Id, out var dela)) porMesa[mesa.Id] = dela = (mesa, []);
            dela.Cabos.Add(pontos.Where(p => !Polygons.Contains(mesa.Corners, p.X, p.Y)).Select(p => (Math.Round(p.X, 3), Math.Round(p.Y, 3))).ToHashSet());
        }

        foreach (var (mesa, cabos) in porMesa.Values)
        {
            var comuns = new HashSet<(double X, double Y)>(cabos[0]);
            foreach (var c in cabos.Skip(1)) comuns.IntersectWith(c);

            // O ponto comum mais perto da mesa é a entrada; o lado é o da borda comprida mais perto dele.
            var c0 = mesa.Corners;
            var cx = c0.Take(4).Average(p => p.X);
            var cy = c0.Take(4).Average(p => p.Y);
            var alto = "-";
            var distancia = double.NaN;
            if (comuns.Count > 0)
            {
                var (ex, ey) = comuns.MinBy(p => (p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
                double Dist(Point3 a, Point3 b)
                {
                    var dx = b.X - a.X;
                    var dy = b.Y - a.Y;
                    return Math.Abs((ex - a.X) * dy - (ey - a.Y) * dx) / Math.Sqrt(dx * dx + dy * dy);
                }

                var pelaBaixa = Dist(c0[0], c0[1]) < Dist(c0[2], c0[3]);
                distancia = Math.Min(Dist(c0[0], c0[1]), Dist(c0[2], c0[3]));
                var baixaMaisAlta = (c0[0].Z + c0[1].Z) / 2 > (c0[2].Z + c0[3].Z) / 2;
                alto = pelaBaixa == baixaMaisAlta ? "1" : "0";
            }

            editor.WriteMessage(string.Format(inv, "\nROTA_ENTRADA mesa={0} cabos={1} comum={2} alto={3} distancia={4:0.###} fim\n", mesa.Label.Replace(' ', '_'), cabos.Count, comuns.Count > 0 ? 1 : 0, alto, distancia));
        }
    }

    /// <summary>
    /// ROTA_ACESSO (item 6 da segunda rodada de 10/10/2026): por inversor em
    /// campo, de onde vem o contorno que o motor CC usa (area ou caixa), o
    /// registro do local dele, a área do desenho onde ele está (em planta),
    /// quantas valas CC entram no contorno e quantas passam no raio, e quantos
    /// lances CC dele chegam pela vala de DENTRO da área (ou do contorno, sem
    /// área): o último vértice do cabo em cima de uma vala, antes do trecho
    /// reto até o inversor, cai dentro dela.
    /// </summary>
    private static void Acessos(Autodesk.AutoCAD.EditorInput.Editor editor, Database db, System.Globalization.CultureInfo inv)
    {
        var leitura = LeituraDaRota.Ler(db);
        var valas = RotaDeCabosStore.Valas(db)[CableRoute.DirectCurrent];
        if (valas.Count == 0) return;
        var rede = new TrenchNetwork(valas);
        var config = RotaDeCabosStore.Configuracoes(db, out _)[CableRoute.DirectCurrent];
        var lances = RotaDeCabosStore.Lances(db).Where(l => l.Lance.Route == CableRoute.DirectCurrent).ToList();
        var areas = LocalDosInversores.Areas(db);
        var locais = LocalDosInversores.Ler(db, out _);

        using var t = db.TransactionManager.StartOpenCloseTransaction();
        foreach (var i in leitura.Setup.Inverters)
        {
            if (leitura.Ponto(EquipmentKind.Inverter, i.Id) is not { } ponto) continue;
            var contorno = leitura.Contorno(EquipmentKind.Inverter, i.Id);
            var entram = contorno is { Count: >= 3 } ? rede.Entering(contorno, ponto) : [];
            var raio = rede.Within(ponto, config.Radius);
            var (marca, area) = areas.Values.FirstOrDefault(a => Polygons.Contains(a.Contorno, ponto.X, ponto.Y));
            var alvo = area ?? contorno;

            int deDentro = 0, deFora = 0;
            foreach (var l in lances.Where(l => l.Lance.To.Id == i.Id))
            {
                if (t.GetObject(l.Id, OpenMode.ForRead) is not Polyline3d p3) continue;
                var naVala = p3.Cast<ObjectId>().Where(v => !v.IsErased)
                    .Select(v => ((PolylineVertex3d)t.GetObject(v, OpenMode.ForRead)).Position)
                    .Select(p => (Point3?)new Point3(p.X, p.Y, 0))
                    .LastOrDefault(p => rede.Nearest(p!.Value, 0.01) is not null);
                if (alvo is { Count: >= 3 } && naVala is { } v0 && Polygons.Contains(alvo, v0.X, v0.Y)) deDentro++;
                else deFora++;
            }

            var local = locais.FirstOrDefault(x => x.Inverter == i.Id);
            editor.WriteMessage(string.Format(inv, "\nROTA_ACESSO inversor={0} contorno={1} local={2} na_area={3} entram={4} raio={5} chegam_de_dentro={6} chegam_de_fora={7} fim\n",
                i.Name.Replace(' ', '_'), leitura.OrigemDoContorno(i.Id), local?.Mode.ToString() ?? "-", (marca?.Name ?? "-").Replace(' ', '_'),
                entram.Count, raio.Count, deDentro, deFora));
        }
    }

    /// <summary>
    /// Só no build de teste: o "Atualizar (reconta)" da aba Resumo, pelo mesmo
    /// caminho (<see cref="RotaDeCabosTabelas.Usina"/>), escrito para o nível 2:
    /// por tipo de cabo, os circuitos, os cabos, os metros, o aviso de fora de
    /// campo, os grupos (nível, nome, subtotal) e as primeiras linhas de
    /// circuito do CC com a tag e as contas.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoRotaResumoAutomatico)]
    public static void Resumo()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        try
        {
            var usina = RotaDeCabosTabelas.Usina(documento);
            editor.WriteMessage(string.Format(inv, "\nROTA_RESUMO sumidos={0} orfaos={1}\n", usina.Sumidos, usina.Orfaos));

            for (var i = 0; i < usina.Tipos.Count; i++)
            {
                var r = usina.Tipos[i];
                var tipo = RotaDeCabosTabelas.TiposDeCabo[i].Titulo;
                editor.WriteMessage(string.Format(inv, "\nROTA_RESUMO_TIPO tipo={0} circuitos={1} cabos={2} metros={3:0.00} linhas={4} fim\n",
                    tipo, r.Circuitos.Count, r.Circuitos.Sum(c => c.Cables), r.Circuitos.Sum(c => c.CableLength), r.Tabela.Rows.Count));
                if (r.ForaDeCampo is { } fora) editor.WriteMessage($"\nROTA_RESUMO_FORA tipo={tipo} {fora}\n");

                void Grupo(CableReport.CircuitGroup g)
                {
                    editor.WriteMessage(string.Format(inv, "\nROTA_RESUMO_GRUPO tipo={0} nivel={1} nome={2} circuitos={3} cabos={4} metros={5:0.00} fim\n",
                        tipo, g.Level, g.Name.Replace(' ', '_'), g.CircuitCount, g.Cables, g.CableLength));
                    foreach (var f in g.Children) Grupo(f);
                }

                foreach (var g in r.Grupos) Grupo(g);

                // A linha do total da usina da grade (item 7 da segunda rodada) e o rodapé da tabela do Excel.
                var total = CableReport.PlantTotal(r.Grupos);
                editor.WriteMessage(string.Format(inv, "\nROTA_RESUMO_TOTAL tipo={0} circuitos={1} lances={2} cabos={3} metros={4:0.00} excel={5} fim\n",
                    tipo, total.CircuitCount, total.Runs, total.Cables, total.CableLength, (r.Tabela.Total.FirstOrDefault() as string ?? "-").Replace(' ', '_')));
            }

            // A tabela CC como vai para o Excel: o cabeçalho e as linhas de circuito com a tag.
            var cc = usina.Tipos[0].Tabela;
            editor.WriteMessage("\nROTA_RESUMO_CABECALHO " + string.Join("|", cc.Header) + "\n");
            foreach (var linha in cc.Rows.Where(l => l[1] is string tag && tag.Length > 0).Take(3))
                editor.WriteMessage("\nROTA_RESUMO_LINHA " + string.Join("|", linha.Select(TabelaNaTela.Texto)) + "\n");

            // A última linha do CSV do Exportar de cada tipo: o total da usina.
            foreach (var r in usina.Tipos)
                editor.WriteMessage("\nROTA_RESUMO_CSV_FIM " + r.Tabela.ToCsv(Tr.Culture).TrimEnd('\r', '\n').Split("\r\n")[^1] + "\n");

            editor.WriteMessage("\nROTA_RESUMO_FIM\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no resumo de cabos (teste).", erro);
            editor.WriteMessage($"\nROTA_RESUMO_ERRO {erro.Message}\n");
        }
    }
}
#endif
