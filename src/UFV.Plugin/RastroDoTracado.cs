using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace UFV.Plugin;

/// <summary>
/// Mostra na tela os trechos já traçados, enquanto o usuário desenha.
///
/// Sem isto, traçar uma área é desenhar às cegas: o AutoCAD dá o elástico do
/// último ponto até o cursor, mas nada do que já foi clicado aparece — e num
/// contorno de seis ou oito vértices ninguém lembra onde estavam os anteriores.
///
/// São gráficos transientes: existem só na tela, não entram no desenho, não
/// são salvos e não aparecem em seleção nenhuma. Some tudo quando o comando
/// termina, inclusive se ele terminar por Esc ou por erro — daí o
/// <see cref="Dispose"/>, e daí o try/finally de quem usa.
/// </summary>
internal sealed class RastroDoTracado : IDisposable
{
    /// <summary>Cor do rastro (índice de cor do AutoCAD). 3 é verde, que se enxerga no fundo escuro e no claro.</summary>
    private const int CorDoRastro = 3;

    private readonly List<Line> _linhas = [];
    private readonly IntegerCollection _viewports = [];

    private bool _descartado;

    /// <summary>Acrescenta o trecho do ponto anterior até o novo.</summary>
    internal void Acrescentar(Point3d de, Point3d para)
    {
        if (_descartado) return;

        Line? linha = null;

        try
        {
            linha = new Line(de, para) { ColorIndex = CorDoRastro };

            TransientManager.CurrentTransientManager.AddTransient(
                linha,
                TransientDrawingMode.DirectShortTerm,
                128,
                _viewports);

            // Só entra na lista depois de o AddTransient dar certo. A lista é
            // quem descarta as linhas no fim; uma linha que nunca chegou lá
            // ficaria sem dono.
            _linhas.Add(linha);
            linha = null;
        }
        catch (System.Exception erro)
        {
            linha?.Dispose();

            // Ficar sem o rastro atrapalha, mas não pode impedir o traçado:
            // o comando continua funcionando às cegas, como antes.
            RegistroDeDiagnostico.Registrar("Não consegui desenhar o rastro do traçado.", erro);
        }
    }

    /// <summary>Tira tudo da tela.</summary>
    public void Dispose()
    {
        if (_descartado) return;
        _descartado = true;

        foreach (var linha in _linhas)
        {
            try
            {
                TransientManager.CurrentTransientManager.EraseTransient(linha, _viewports);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Não consegui apagar o rastro do traçado.", erro);
            }
            finally
            {
                // Cada linha é um objeto de banco não anexado a banco nenhum:
                // se não for descartada aqui, vira memória perdida a cada área
                // traçada.
                linha.Dispose();
            }
        }

        _linhas.Clear();

        // IntegerCollection não é descartável: ao contrário da maioria das
        // coleções da API do AutoCAD, esta é gerenciada (não herda de
        // DisposableWrapper) e não tem Dispose. Conferido no compilador:
        // chamar Dispose aqui é erro CS1061.
    }
}
