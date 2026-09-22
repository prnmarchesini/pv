namespace UFV.Geo;

/// <summary>
/// Um triângulo da malha do terreno, com os três vértices em metros.
///
/// A superfície TIN é uma função de (x, y): sobre cada ponto do plano há uma
/// cota só. Por isso as contas de "o ponto está dentro?" acontecem na projeção
/// em planta, e a cota sai por interpolação entre os três vértices.
/// </summary>
public readonly record struct Triangle(Point3 A, Point3 B, Point3 C)
{
    /// <summary>Distância relativa entre dois doubles consecutivos.</summary>
    private const double EpsilonDoDouble = 2.220446049250313e-16;

    /// <summary>
    /// Quantas vezes o ruído de arredondamento um triângulo precisa ter de
    /// área para ser levado a sério.
    ///
    /// Com 1000, em coordenada UTM, um triângulo de 25 cm de lado é recusado
    /// quando tem menos de ~1,6 micrômetro de espessura. Nenhum triângulo de
    /// terreno tem espessura de micrômetro por bem: isso é ponto duplicado ou
    /// breakline quase colinear.
    /// </summary>
    private const double MargemSobreORuido = 1000.0;

    /// <summary>
    /// Dobro da área projetada em planta, com sinal. O sinal diz a orientação
    /// (positivo anti-horário); o valor com sinal é o denominador das
    /// coordenadas baricêntricas — usar o valor absoluto ali inverteria os
    /// pesos de um triângulo horário.
    /// </summary>
    public double DoubleSignedArea2D =>
        (B.Y - C.Y) * (A.X - C.X) + (C.X - B.X) * (A.Y - C.Y);

    /// <summary>Falso se algum vértice tiver NaN ou infinito.</summary>
    public bool IsFinite => A.IsFinite && B.IsFinite && C.IsFinite;

    /// <summary>
    /// Verdadeiro quando o triângulo não tem área útil em planta: os três
    /// vértices caem numa reta vista de cima, ou tão perto disso que a
    /// interpolação dentro dele não significa nada.
    ///
    /// Acontece de verdade em terreno levantado: dois pontos repetidos, uma
    /// faceta vertical como a parede de um talude, ou uma fatia entre pontos
    /// quase colineares — rotina numa malha de milhões de pontos a 25 cm.
    /// Sobre uma faceta dessas não existe "a cota", existem infinitas; quem
    /// responde é o triângulo vizinho.
    ///
    /// O limiar tem duas partes, e vale a maior:
    ///
    /// 1. Relativa à forma (escala²·1e-12), que pega o degenerado óbvio.
    /// 2. Relativa à resolução do double na coordenada em que o triângulo
    ///    está. Esta é a que importa: em UTM (y ≈ 7 400 000) o menor passo
    ///    representável é ~1 nanômetro, e um triângulo de 25 cm com essa
    ///    espessura passa folgado pelo critério de forma — o denominador fica
    ///    minúsculo, os pesos explodem, e a cota interpolada sai errada em
    ///    dezenas de metros com a consulta respondendo que deu certo.
    /// </summary>
    public bool IsDegenerate2D
    {
        get
        {
            if (!IsFinite) return true;

            var escala = Math.Max(
                Math.Max(Math.Abs(A.X - C.X), Math.Abs(B.X - C.X)),
                Math.Max(Math.Abs(A.Y - C.Y), Math.Abs(B.Y - C.Y)));

            if (escala <= 0) return true;

            // Quanto vale um "passo" de double aqui, em metros. Longe da
            // origem o passo é maior, e o mesmo triângulo é mais frágil.
            var distanciaDaOrigem = Math.Max(
                Math.Max(Math.Abs(A.X), Math.Abs(B.X)), Math.Max(Math.Abs(C.X),
                Math.Max(Math.Abs(A.Y), Math.Max(Math.Abs(B.Y), Math.Abs(C.Y)))));

            var passoDoDouble = Math.Max(distanciaDaOrigem, escala) * EpsilonDoDouble;
            var limiarAbsoluto = escala * passoDoDouble * MargemSobreORuido;
            var limiarDeForma = escala * escala * 1e-12;

            return Math.Abs(DoubleSignedArea2D) <= Math.Max(limiarAbsoluto, limiarDeForma);
        }
    }
}
