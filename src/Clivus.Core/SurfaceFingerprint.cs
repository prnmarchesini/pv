namespace Clivus.Core;

/// <summary>
/// A impressão digital de uma superfície: o bastante para reconhecer que ela
/// é a mesma de antes, e para perceber quando deixou de ser.
///
/// Não existe data de modificação por objeto no desenho, então a identidade se
/// apoia em coisas que mudam quando alguém mexe na superfície: o número de
/// revisão que o Civil 3D mantém, a contagem de pontos, a de triângulos, as
/// cotas extremas e a CAIXA ENVOLVENTE em planta.
///
/// A caixa entrou depois, e por um caso real: o Renan moveu a superfície no
/// desenho e o carimbo continuou dizendo "atual". Mover em X e Y desloca cada
/// ponto do terreno — toda cota consultada passa a sair de um lugar
/// diferente — mas não muda contagem nenhuma nem as cotas extremas. Sem a
/// caixa, o deslocamento passava batido, e é o tipo de coisa que acontece
/// quando se ajusta a topografia à planta do projeto.
/// </summary>
/// <param name="Handle">
/// O identificador permanente da superfície dentro do arquivo. Nasce com ela e
/// vive com ela; layer não serve para isso (ver 02-arquitetura.md).
/// </param>
/// <param name="Name">Nome da superfície, só para a mensagem ao usuário.</param>
/// <param name="RevisionNumber">Contador que o Civil 3D incrementa a cada edição.</param>
/// <param name="PointCount">Pontos da superfície.</param>
/// <param name="TriangleCount">Triângulos da superfície.</param>
/// <param name="MinZ">Cota mais baixa, em metros.</param>
/// <param name="MaxZ">Cota mais alta, em metros.</param>
/// <param name="MinX">Extremo oeste da superfície, em metros.</param>
/// <param name="MinY">Extremo sul da superfície, em metros.</param>
/// <param name="MaxX">Extremo leste da superfície, em metros.</param>
/// <param name="MaxY">Extremo norte da superfície, em metros.</param>
public sealed record SurfaceFingerprint(
    string Handle,
    string Name,
    int RevisionNumber,
    int PointCount,
    int TriangleCount,
    double MinZ,
    double MaxZ,
    double MinX,
    double MinY,
    double MaxX,
    double MaxY)
{
    /// <summary>Um milímetro: a mesma tolerância dos verificadores de regra sagrada.</summary>
    private const double ToleranciaDeCota = 0.001;

    /// <summary>
    /// Se esta impressão digital é a mesma da outra, ignorando o nome.
    ///
    /// O nome fica de fora de propósito: renomear a superfície não muda cota
    /// nenhuma, e invalidar o cálculo por causa disso só ensinaria o usuário a
    /// desconfiar do aviso.
    /// </summary>
    public bool MatchesTerrainOf(SurfaceFingerprint outra)
    {
        ArgumentNullException.ThrowIfNull(outra);

        return string.Equals(Handle, outra.Handle, StringComparison.OrdinalIgnoreCase)
            && RevisionNumber == outra.RevisionNumber
            && PointCount == outra.PointCount
            && TriangleCount == outra.TriangleCount
            && Math.Abs(MinZ - outra.MinZ) <= ToleranciaDeCota
            && Math.Abs(MaxZ - outra.MaxZ) <= ToleranciaDeCota
            && Math.Abs(MinX - outra.MinX) <= ToleranciaDeCota
            && Math.Abs(MinY - outra.MinY) <= ToleranciaDeCota
            && Math.Abs(MaxX - outra.MaxX) <= ToleranciaDeCota
            && Math.Abs(MaxY - outra.MaxY) <= ToleranciaDeCota;
    }

    /// <summary>
    /// O que mudou em relação à outra, em português, para a mensagem ao
    /// usuário. Vazio quando o terreno é o mesmo.
    /// </summary>
    public IReadOnlyList<string> DescribeChangesFrom(SurfaceFingerprint anterior)
    {
        ArgumentNullException.ThrowIfNull(anterior);

        var mudancas = new List<string>();

        if (!string.Equals(Handle, anterior.Handle, StringComparison.OrdinalIgnoreCase))
            mudancas.Add(Tr.T("é outra superfície"));

        if (RevisionNumber != anterior.RevisionNumber)
            mudancas.Add(Tr.F("foi editada (revisão {0} → {1})", anterior.RevisionNumber, RevisionNumber));

        if (PointCount != anterior.PointCount)
            mudancas.Add(Tr.F("pontos: {0:N0} → {1:N0}", anterior.PointCount, PointCount));

        if (TriangleCount != anterior.TriangleCount)
            mudancas.Add(Tr.F("triângulos: {0:N0} → {1:N0}", anterior.TriangleCount, TriangleCount));

        if (Math.Abs(MinZ - anterior.MinZ) > ToleranciaDeCota
            || Math.Abs(MaxZ - anterior.MaxZ) > ToleranciaDeCota)
        {
            mudancas.Add(
                Tr.F("cotas: {0:0.000} a {1:0.000} m → {2:0.000} a {3:0.000} m", anterior.MinZ, anterior.MaxZ, MinZ, MaxZ));
        }

        if (Math.Abs(MinX - anterior.MinX) > ToleranciaDeCota
            || Math.Abs(MinY - anterior.MinY) > ToleranciaDeCota
            || Math.Abs(MaxX - anterior.MaxX) > ToleranciaDeCota
            || Math.Abs(MaxY - anterior.MaxY) > ToleranciaDeCota)
        {
            // Deslocamento puro merece um nome próprio: é a edição que mais
            // engana, porque o terreno continua com a mesma cara e todos os
            // números de tamanho continuam iguais.
            var deslocou =
                Math.Abs((MaxX - MinX) - (anterior.MaxX - anterior.MinX)) <= ToleranciaDeCota
                && Math.Abs((MaxY - MinY) - (anterior.MaxY - anterior.MinY)) <= ToleranciaDeCota;

            mudancas.Add(deslocou
                ? Tr.F("foi movida ({0:+0.000;-0.000} m em X, {1:+0.000;-0.000} m em Y)", MinX - anterior.MinX, MinY - anterior.MinY)
                : Tr.T("mudou de posição ou de tamanho em planta"));
        }

        return mudancas;
    }
}
