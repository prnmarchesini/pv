using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// Mesas de mentira para os testes das strings (etapa 11): contorno e
/// módulos como o desenho grava (borda baixa do início ao fim, depois a alta
/// de volta), com a face de cada módulo num plano inclinado.
/// </summary>
internal static class MesasDeString
{
    /// <summary>
    /// Uma mesa com o canto da borda baixa em (x, y), correndo no ângulo
    /// dado, com colunas × fileiras módulos de largura × altura (em planta).
    /// A face sobe <paramref name="subida"/> metro por metro de fundo e
    /// <paramref name="giro"/> metro por metro ao longo da mesa, a partir de
    /// <paramref name="z0"/>.
    /// </summary>
    internal static FieldTable Mesa(
        string letreiro, double x, double y, int colunas, int fileiras,
        double angulo = 0, double largura = 1.1, double altura = 2.3, double vao = 0.02,
        double z0 = 100, double subida = 0.5, double giro = 0, Guid? id = null)
    {
        var d = new Point3(Math.Cos(angulo), Math.Sin(angulo), 0);
        var n = new Point3(-d.Y, d.X, 0);

        Point3 P(double ao, double fundo) =>
            new(x + d.X * ao + n.X * fundo, y + d.Y * ao + n.Y * fundo, z0 + subida * fundo + giro * ao);

        var comprimento = colunas * largura;
        var profundidade = fileiras * altura;

        var modulos = new List<FieldModule>();
        for (var c = 0; c < colunas; c++)
        {
            for (var r = 0; r < fileiras; r++)
            {
                var a0 = c * largura + vao / 2;
                var a1 = (c + 1) * largura - vao / 2;
                var f0 = r * altura + vao / 2;
                var f1 = (r + 1) * altura - vao / 2;
                modulos.Add(new FieldModule(Guid.NewGuid(), c, r, [P(a0, f0), P(a1, f0), P(a1, f1), P(a0, f1)]));
            }
        }

        return new FieldTable(id ?? Guid.NewGuid(), letreiro, [P(0, 0), P(comprimento, 0), P(comprimento, profundidade), P(0, profundidade)], modulos);
    }

    /// <summary>
    /// Uma fileira de mesas na reta y = <paramref name="y"/>, uma depois da
    /// outra com o vão dado, F{fileira}.1, F{fileira}.2...; cada item é (colunas, fileiras).
    /// </summary>
    internal static List<FieldTable> Fileira(int fileira, double y, double vaoEntreMesas, params (int Colunas, int Fileiras)[] mesas)
    {
        var lista = new List<FieldTable>();
        var x = 0.0;
        for (var i = 0; i < mesas.Length; i++)
        {
            lista.Add(Mesa($"F{fileira}.{i + 1}", x, y, mesas[i].Colunas, mesas[i].Fileiras));
            x += mesas[i].Colunas * 1.1 + vaoEntreMesas;
        }

        return lista;
    }
}
