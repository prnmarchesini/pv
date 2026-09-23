namespace UFV.Core;

/// <summary>
/// A impressão digital de uma superfície: o bastante para reconhecer que ela
/// é a mesma de antes, e para perceber quando deixou de ser.
///
/// Não existe data de modificação por objeto no desenho, então a identidade se
/// apoia em quatro coisas que mudam juntas quando alguém mexe na superfície: o
/// número de revisão que o Civil 3D mantém, a contagem de pontos, a de
/// triângulos e as cotas extremas. Uma edição que não mexa em nenhuma das
/// quatro também não muda nenhuma cota, que é o que importa aqui.
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
public sealed record SurfaceFingerprint(
    string Handle,
    string Name,
    int RevisionNumber,
    int PointCount,
    int TriangleCount,
    double MinZ,
    double MaxZ)
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
            && Math.Abs(MaxZ - outra.MaxZ) <= ToleranciaDeCota;
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
            mudancas.Add("é outra superfície");

        if (RevisionNumber != anterior.RevisionNumber)
            mudancas.Add($"foi editada (revisão {anterior.RevisionNumber} → {RevisionNumber})");

        if (PointCount != anterior.PointCount)
            mudancas.Add($"pontos: {anterior.PointCount:N0} → {PointCount:N0}");

        if (TriangleCount != anterior.TriangleCount)
            mudancas.Add($"triângulos: {anterior.TriangleCount:N0} → {TriangleCount:N0}");

        if (Math.Abs(MinZ - anterior.MinZ) > ToleranciaDeCota
            || Math.Abs(MaxZ - anterior.MaxZ) > ToleranciaDeCota)
        {
            mudancas.Add(
                $"cotas: {anterior.MinZ:0.000} a {anterior.MaxZ:0.000} m "
                + $"→ {MinZ:0.000} a {MaxZ:0.000} m");
        }

        return mudancas;
    }
}
