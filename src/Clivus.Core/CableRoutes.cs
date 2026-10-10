namespace Clivus.Core;

/// <summary>As rotas de cabo (roteamento, etapa 17): uma aba cada, na ordem da janela.</summary>
public enum CableRoute
{
    /// <summary>CC: string -> inversor (ou string -> combiner).</summary>
    DirectCurrent,

    /// <summary>Combiner: string -> combiner e combiner -> inversor.</summary>
    Combiner,

    /// <summary>CA: inversor -> trafo.</summary>
    AlternatingCurrent,

    /// <summary>MT: trafo -> subestação.</summary>
    MediumVoltage,
}

/// <summary>Quantos equipamentos de um tipo há no cadastro e quantos têm o retângulo em campo.</summary>
public readonly record struct EquipmentCount(int Registered, int InField);

/// <summary>
/// O que o desenho tem, para saber quais rotas são possíveis (17.1): strings
/// desenhadas e equipamentos com o retângulo em campo. A rota precisa do
/// ponto físico, então equipamento só cadastrado não conta. A combiner box
/// ainda não existe no plugin (etapa 19): por enquanto vem sempre zero.
/// </summary>
public sealed record CableRouteDrawing(int Strings, int Combiners, EquipmentCount Inverters, EquipmentCount Transformers, EquipmentCount Substations);

/// <summary>Quais abas da rota de cabos ficam habilitadas e, nas outras, o que falta (17.1).</summary>
public static class CableRoutes
{
    /// <summary>As abas na ordem do plano: CC, Combiner, CA, MT.</summary>
    public static IReadOnlyList<CableRoute> All { get; } =
        [CableRoute.DirectCurrent, CableRoute.Combiner, CableRoute.AlternatingCurrent, CableRoute.MediumVoltage];

    /// <summary>O nome da aba.</summary>
    public static string Title(CableRoute rota) => rota switch
    {
        CableRoute.DirectCurrent => Tr.T("CC"),
        CableRoute.Combiner => Tr.T("Combiner"),
        CableRoute.AlternatingCurrent => Tr.T("CA"),
        _ => Tr.T("MT"),
    };

    /// <summary>O trecho que a aba roteia.</summary>
    public static string Description(CableRoute rota) => rota switch
    {
        CableRoute.DirectCurrent => Tr.T("Corrente contínua: da string até o inversor (ou até a combiner box)."),
        CableRoute.Combiner => Tr.T("Combiner box: da string até a combiner e da combiner até o inversor."),
        CableRoute.AlternatingCurrent => Tr.T("Corrente alternada: do inversor até o transformador."),
        _ => Tr.T("Média tensão: do transformador até a subestação."),
    };

    /// <summary>
    /// O desenho pelo cadastro: cada equipamento que vai para o campo
    /// (<see cref="ElectricalSetup.Equipment"/>; a subestação compartilhada é
    /// um só, o bloco) e quantos deles têm o retângulo em <paramref name="inField"/>.
    /// Retângulo sem cadastro (COPY, UNDO, desenho copiado) não conta.
    /// </summary>
    public static CableRouteDrawing Drawing(ElectricalSetup setup, int strings, IReadOnlySet<(EquipmentKind Kind, Guid Id)> inField)
    {
        var equipamentos = setup.Equipment().ToList();

        EquipmentCount Contar(EquipmentKind tipo) => new(
            equipamentos.Count(e => e.Kind == tipo),
            equipamentos.Count(e => e.Kind == tipo && inField.Contains((e.Kind, e.Id))));

        return new CableRouteDrawing(strings, 0, Contar(EquipmentKind.Inverter), Contar(EquipmentKind.Transformer), Contar(EquipmentKind.ConsumerUnit));
    }

    /// <summary>O que falta no desenho para a rota; vazio = a aba fica habilitada.</summary>
    public static IReadOnlyList<string> Missing(CableRoute rota, CableRouteDrawing desenho)
    {
        var falta = new List<string>();

        void Strings()
        {
            if (desenho.Strings == 0) falta.Add(Tr.T("uma string desenhada (não há nenhuma)"));
        }

        void Combiner()
        {
            if (desenho.Combiners == 0) falta.Add(Tr.T("uma combiner box em campo (o cadastro de combiner ainda não existe no plugin)"));
        }

        void Equipamento(EquipmentCount n, string nenhum, Func<int, string> semCampo)
        {
            if (n.InField > 0) return;
            falta.Add(n.Registered > 0 ? semCampo(n.Registered) : nenhum);
        }

        void Inversor() => Equipamento(desenho.Inverters,
            Tr.T("um inversor (não há nenhum no cadastro)"),
            c => Tr.F("um inversor em campo ({0} no cadastro, nenhum com o retângulo em campo)", c));

        void Trafo() => Equipamento(desenho.Transformers,
            Tr.T("um transformador (não há nenhum no cadastro)"),
            c => Tr.F("um transformador em campo ({0} no cadastro, nenhum com o retângulo em campo)", c));

        switch (rota)
        {
            case CableRoute.DirectCurrent:
                Strings();
                // Com combiner em campo, o trecho CC vai até ela; sem, até o inversor.
                if (desenho.Combiners == 0) Inversor();
                break;

            case CableRoute.Combiner:
                Strings();
                Combiner();
                Inversor();
                break;

            case CableRoute.AlternatingCurrent:
                Inversor();
                Trafo();
                break;

            default:
                Trafo();
                Equipamento(desenho.Substations,
                    Tr.T("uma subestação (não há nenhuma no cadastro)"),
                    c => Tr.F("uma subestação em campo ({0} no cadastro, nenhuma com o retângulo em campo)", c));
                break;
        }

        return falta;
    }
}
