namespace UFV.Geo;

/// <summary>
/// Ponto no espaço, em metros.
///
/// Unidade interna do projeto: metro, double (02-arquitetura.md). Conversão
/// para outra unidade acontece na interface, nunca aqui.
/// </summary>
/// <param name="X">Leste, em metros.</param>
/// <param name="Y">Norte, em metros.</param>
/// <param name="Z">Cota, em metros.</param>
public readonly record struct Point3(double X, double Y, double Z)
{
    /// <summary>
    /// Falso se alguma coordenada for NaN ou infinita.
    ///
    /// Vale a pena perguntar: um levantamento com ponto defeituoso vira uma
    /// TinSurface com vértice NaN, e NaN não dispara nada sozinho — toda
    /// comparação com ele é falsa, então ele atravessa os testes de
    /// dentro/fora e sai do outro lado como se fosse cota.
    /// </summary>
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);

    public override string ToString() =>
        $"({X:0.###}, {Y:0.###}, {Z:0.###})";
}
