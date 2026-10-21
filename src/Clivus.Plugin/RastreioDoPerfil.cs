using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

[assembly: CommandClass(typeof(Clivus.Plugin.RastreioDoPerfil))]

namespace Clivus.Plugin;

/// <summary>
/// O rastreio planta ↔ gráfico (32.2). Renan, 21/10/2026: "quero passar o
/// mouse no gráfico e ver uma seta se movendo na planta, indicando o mesmo
/// ponto do gráfico, e se eu passar o mouse na canaleta na planta, a flecha
/// aponta o mesmo ponto do gráfico [...] pode ser ligado ou desligado".
/// Escuta o movimento do mouse (PointMonitor) e desenha marcas passageiras,
/// fora do desenho. A conta é do <see cref="ProfileTracker"/>.
/// </summary>
public static class RastreioDoPerfil
{
    private const string Ligar = "Ligar";
    private const string Desligar = "Desligar";

    private static readonly Dictionary<Document, Escuta> Escutas = [];
    private static DocumentCollectionEventHandler? _aoCriar;
    private static DocumentCollectionEventHandler? _aoDestruir;

    /// <summary>Muda quando um gráfico é refeito ou apagado: o rastreio relê os gráficos.</summary>
    internal static int Versao;

    private static bool? _ligado;

    /// <summary>Se está ligado: lido do arquivo uma vez (o evento roda a cada movimento do mouse).</summary>
    internal static bool Ligado => _ligado ??= UserPreferences.Load(IdiomaDoPlugin.Arquivo).ProfileTracking;

    /// <summary>CLIVUS_DRN_RASTREIO: [Ligar/Desligar]; Enter troca.</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemRastreio)]
    public static void Comando()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var ligado = Ligado;

        var opcoes = new PromptKeywordOptions(Tr.T("\nRastreio planta ↔ gráfico"));
        opcoes.Keywords.Add(Ligar);
        opcoes.Keywords.Add(Desligar);
        opcoes.Keywords.Default = ligado ? Desligar : Ligar;
        opcoes.AllowNone = true;

        var r = editor.GetKeywords(opcoes);
        if (r.Status is not (PromptStatus.OK or PromptStatus.None)) return;

        var novo = (string.IsNullOrEmpty(r.StringResult) ? opcoes.Keywords.Default : r.StringResult) == Ligar;
        (UserPreferences.Load(IdiomaDoPlugin.Arquivo) with { ProfileTracking = novo }).Save(IdiomaDoPlugin.Arquivo);
        _ligado = novo;

        foreach (var escuta in Escutas.Values) escuta.Limpar();

        editor.WriteMessage(novo
            ? Tr.T("\nRASTREIO ligado: passe o mouse no gráfico ou na linha/canaleta.\n")
            : Tr.T("\nRASTREIO desligado.\n"));
    }

    internal static void Instalar()
    {
        if (_aoCriar is not null || !ClivusExtension.TemInterface()) return;

        var documentos = AcadApp.DocumentManager;
        _aoCriar = (_, e) => Escutar(e.Document);
        _aoDestruir = (_, e) => { if (e.Document is not null && Escutas.Remove(e.Document, out var escuta)) escuta.Soltar(); };

        documentos.DocumentCreated += _aoCriar;
        documentos.DocumentToBeDestroyed += _aoDestruir;

        foreach (Document documento in documentos) Escutar(documento);
    }

    internal static void Desinstalar()
    {
        foreach (var escuta in Escutas.Values.ToList()) escuta.Soltar();
        Escutas.Clear();

        if (_aoCriar is null) return;

        try
        {
            AcadApp.DocumentManager.DocumentCreated -= _aoCriar;
            AcadApp.DocumentManager.DocumentToBeDestroyed -= _aoDestruir;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao desligar o rastreio do perfil.", erro);
        }

        _aoCriar = null;
        _aoDestruir = null;
    }

    private static void Escutar(Document documento)
    {
        if (documento is null || Escutas.ContainsKey(documento)) return;

        try
        {
            Escutas[documento] = new Escuta(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ligar o rastreio do perfil num desenho.", erro);
        }
    }

    /// <summary>Tantos pixels em unidades do desenho, pela altura da vista.</summary>
    internal static double Pixels(double pixels)
    {
        var altura = Convert.ToDouble(AcadApp.GetSystemVariable("VIEWSIZE"), System.Globalization.CultureInfo.InvariantCulture);
        var tela = (Point2d)AcadApp.GetSystemVariable("SCREENSIZE");
        return tela.Y > 0 ? pixels * altura / tela.Y : pixels * altura / 1000;
    }

    /// <summary>Os gráficos do desenho, onde estão agora, com o caminho de cada linha (o rastreio e a Vazão no ponto).</summary>
    internal static List<TrackedChart> GraficosDoDesenho(Document documento)
    {
        var graficos = new List<TrackedChart>();

        var terreno = TerrainCache.Get(documento);
        if (terreno is null) return graficos;

        var database = documento.Database;
        var configuracao = DrenagemStore.Ler(database);

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var indice = IndiceDaDrenagem.Montar(transacao, database);

        foreach (var linha in LinhasDeDrenagem.Todas(transacao, database))
        {
            if (linha.Identidade.ChartHidden) continue;

            // Onde o gráfico está (o grupo pode ter sido arrastado), não onde foi desenhado.
            if ((LinhasDeDrenagem.LugarDoGrafico(transacao, indice, linha.Identidade.Id) ?? linha.Identidade.Chart) is not { } origem) continue;

            try
            {
                var (grafico, perfil, canaleta) = LinhasDeDrenagem.Grafico(terreno.Mesh, configuracao, linha);
                var caminho = canaleta is not null
                    ? canaleta.Points.Select(q => new TrackPoint(q.Distance, q.X, q.Y, q.Terrain, q.Bottom)).ToList()
                    : perfil.Points.Select(q => new TrackPoint(q.Distance, q.X, q.Y, q.Z, double.NaN)).ToList();

                graficos.Add(new TrackedChart(linha.Identidade.Id, linha.Identidade.Name, origem, grafico, caminho));
            }
            catch (ArgumentException)
            {
                // Linha sem perfil (fora do terreno): sem rastreio, como sem gráfico.
            }
        }

        return graficos;
    }

    private sealed class Escuta
    {
        private readonly Document _documento;
        private readonly List<Drawable> _marcas = [];
        private List<TrackedChart> _graficos = [];
        private int _versao = -1;
        private bool _sujo = true;
        private bool _falhou;

        internal Escuta(Document documento)
        {
            _documento = documento;
            _documento.Editor.PointMonitor += AoMoverOMouse;
            _documento.CommandEnded += AoTerminar;
        }

        internal void Soltar()
        {
            try
            {
                _documento.Editor.PointMonitor -= AoMoverOMouse;
                _documento.CommandEnded -= AoTerminar;
                Limpar();
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao soltar o rastreio do perfil.", erro);
            }
        }

        // U, REDO, apagar à mão: relê os gráficos no próximo movimento.
        private void AoTerminar(object? sender, CommandEventArgs e) => _sujo = true;

        internal void Limpar()
        {
            if (_marcas.Count == 0) return;

            var gerente = TransientManager.CurrentTransientManager;
            foreach (var m in _marcas)
            {
                gerente.EraseTransient(m, []);
                m.Dispose();
            }

            _marcas.Clear();
        }

        private void AoMoverOMouse(object? sender, PointMonitorEventArgs e)
        {
            try
            {
                Limpar();
                if (!Ligado) return;

                var graficos = Graficos();
                if (graficos.Count == 0) return;

                var p = e.Context.ComputedPoint;
                var tolerancia = Tolerancia();

                TrackHit? acerto = null;
                foreach (var g in graficos)
                {
                    acerto = ProfileTracker.FromChart(g, p.X, p.Y);
                    if (acerto is not null) break;
                }

                if (acerto is null)
                {
                    foreach (var g in graficos)
                    {
                        acerto = ProfileTracker.FromPlan(g, p.X, p.Y, tolerancia);
                        if (acerto is not null) break;
                    }
                }

                if (acerto is not { } h) return;

                Marcar(h, tolerancia);

                var texto = double.IsNaN(h.Bottom)
                    ? Tr.F("{0} · {1:0.0} m · terreno {2:0.00} m", h.Chart.Name, h.Distance, h.Terrain)
                    : Tr.F("{0} · {1:0.0} m · terreno {2:0.00} m · fundo {3:0.00} m", h.Chart.Name, h.Distance, h.Terrain, h.Bottom);
                e.AppendToolTipText(texto);
            }
            catch (System.Exception erro)
            {
                // Uma vez só no registro: o evento roda a cada movimento do mouse.
                if (!_falhou) RegistroDeDiagnostico.Registrar("Falha no rastreio do perfil.", erro);
                _falhou = true;
            }
        }

        /// <summary>Dez pixels em unidades do desenho.</summary>
        private static double Tolerancia() => Pixels(10);

        /// <summary>A seta em planta e a vertical no gráfico.</summary>
        private void Marcar(TrackHit h, double tamanho)
        {
            var gerente = TransientManager.CurrentTransientManager;
            var z = h.Terrain + 0.1;
            var s = 3 * tamanho;

            void Pôr(Entity e)
            {
                e.ColorIndex = 6;
                gerente.AddTransient(e, TransientDrawingMode.DirectTopmost, 128, []);
                _marcas.Add(e);
            }

            // Planta: a seta de cima apontando o ponto e um círculo nele.
            Pôr(new Line(new Point3d(h.PlanX, h.PlanY + 2.5 * s, z), new Point3d(h.PlanX, h.PlanY, z)));
            Pôr(new Line(new Point3d(h.PlanX - 0.5 * s, h.PlanY + 0.8 * s, z), new Point3d(h.PlanX, h.PlanY, z)));
            Pôr(new Line(new Point3d(h.PlanX + 0.5 * s, h.PlanY + 0.8 * s, z), new Point3d(h.PlanX, h.PlanY, z)));
            Pôr(new Circle(new Point3d(h.PlanX, h.PlanY, z), Vector3d.ZAxis, 0.4 * s));

            // Gráfico: a vertical de baixo a cima da moldura e o ponto na série.
            var oy = h.Chart.Origin.Y;
            var zg = h.Terrain;
            Pôr(new Line(new Point3d(h.ChartX, oy, zg), new Point3d(h.ChartX, oy + h.Chart.Chart.Height, zg)));
            Pôr(new Circle(new Point3d(h.ChartX, h.ChartY, zg), Vector3d.ZAxis, 0.4 * s));
        }

        /// <summary>Os gráficos do desenho, relidos quando algum foi refeito ou um comando terminou.</summary>
        private List<TrackedChart> Graficos()
        {
            if (!_sujo && _versao == Versao) return _graficos;

            _sujo = false;
            _versao = Versao;
            _graficos = GraficosDoDesenho(_documento);
            return _graficos;
        }
    }
}
