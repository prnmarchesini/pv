using System.Text;

namespace Clivus.Core.Tests;

/// <summary>Passo 32.9: a base do Plúvio 2.1 e a interpolação para o local do desenho.</summary>
public class PluvioDatabaseTests
{
    /// <summary>Um arquivo no formato do Plúvio, com as estações dadas por estado.</summary>
    private static byte[] Arquivo(params (string Uf, string Nome, double K, double A, double B, double C, string Lat, string Lon)[] estacoes)
    {
        using var m = new MemoryStream();
        using var w = new BinaryWriter(m);

        void Texto(string s, int? capacidade = null)
        {
            var b = Encoding.Latin1.GetBytes(s);
            w.Write((byte)b.Length);
            w.Write(b);
            if (capacidade is { } c) w.Write(new byte[c - b.Length]);
        }

        w.Write((byte)3);
        foreach (var uf in PluvioDatabase.States)
        {
            var doEstado = estacoes.Where(e => e.Uf == uf).ToList();
            w.Write((ushort)doEstado.Count);
            foreach (var e in doEstado)
            {
                Texto(e.Nome, 68);
                w.Write(e.K);
                w.Write(e.A);
                w.Write(e.B);
                w.Write(e.C);
                Texto("SILVA et al. (1999)");
                Texto(e.Lat);
                Texto(e.Lon);
            }
        }

        w.Flush();
        return m.ToArray();
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void LeAsEstacoesComEstadoAcentoECoordenada()
    {
        var base_ = PluvioDatabase.Parse(Arquivo(
            ("AC", "Rio Branco", 1868, 0.21, 21, 0.86, "09°58'00\"", "67°49'00\""),
            ("SP", "Guararapes", 1538.817, 0.142, 25, 0.755, "21°15'30\"", "50°38'40\"")));

        Assert.Equal(2, base_.Stations.Count);
        var g = base_.Stations[1];
        Assert.Equal("SP", g.State);
        Assert.Equal("Guararapes", g.Name);
        Assert.Equal(1538.817, g.K, 9);
        Assert.Equal(21 + 15 / 60.0 + 30 / 3600.0, g.LatitudeSouth, 9);
        Assert.Equal(50 + 38 / 60.0 + 40 / 3600.0, g.LongitudeWest, 9);
        Assert.Equal("SILVA et al. (1999)", g.Source);
        Assert.Equal("AC", base_.Stations[0].State);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void ArquivoCortadoNaoViraBaseMeiaLida()
    {
        var b = Arquivo(("SP", "São José", 1999, 0.14, 25, 0.84, "20°00'00\"", "51°00'00\""));

        Assert.Throws<ArgumentException>(() => PluvioDatabase.Parse(b.AsSpan(0, b.Length - 10)));
        Assert.Throws<ArgumentException>(() => PluvioDatabase.Parse(Arquivo()));
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void EmCimaDaEstacaoEElaEPertoDelaQuaseEla()
    {
        var base_ = PluvioDatabase.Parse(Arquivo(
            ("SP", "Perto", 1000, 0.1, 10, 0.7, "21°00'00\"", "50°00'00\""),
            ("SP", "Longe", 2000, 0.2, 20, 0.9, "22°00'00\"", "50°00'00\"")));

        var em = base_.Interpolate(-21, -50);
        Assert.Equal(1000, em.K, 9);
        Assert.Single(em.Nearest);

        // A 0,1° de uma e 0,9° da outra: com expoente 10, a perto pesa (9^10)/(9^10+1).
        var quase = base_.Interpolate(-21.1, -50);
        var w = Math.Pow(9, 10) / (Math.Pow(9, 10) + 1);
        Assert.Equal(1000 * w + 2000 * (1 - w), quase.K, 6);
        Assert.Equal("Perto", quase.Nearest[0].Station.Name);
        Assert.Equal(11.1, quase.Nearest[0].DistanceKm, 0);

        // No meio: metade de cada.
        Assert.Equal(1500, base_.Interpolate(-21.5, -50).K, 6);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void AFonteDizAsEstacoesQueMaisPesaram()
    {
        var base_ = PluvioDatabase.Parse(Arquivo(
            ("SP", "Perto", 1000, 0.1, 10, 0.7, "21°00'00\"", "50°00'00\""),
            ("MS", "Longe", 2000, 0.2, 20, 0.9, "22°00'00\"", "50°00'00\"")));

        var fonte = base_.Interpolate(-21.1, -50).Source();

        Assert.StartsWith("Plúvio 2.1", fonte);
        Assert.Contains("Perto-SP 11 km (100 %)", fonte);
        Assert.Contains("Longe-MS", fonte);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void LeAsLocalidadesEInterpolaNelas()
    {
        using var m = new MemoryStream();
        using (var w = new BinaryWriter(m, Encoding.Latin1, leaveOpen: true))
        {
            void Texto(string t, int? capacidade = null)
            {
                var b = Encoding.Latin1.GetBytes(t);
                w.Write((byte)b.Length);
                w.Write(b);
                if (capacidade is { } c) w.Write(new byte[c - b.Length]);
            }

            w.Write((byte)2);
            foreach (var uf in PluvioDatabase.States)
            {
                w.Write((ushort)(uf == "SP" ? 1 : 0));
                if (uf != "SP") continue;
                Texto("Valparaíso", 68);
                Texto("21°30'00\"");
                Texto("50°00'00\"");
            }
        }

        var base_ = PluvioDatabase.Parse(Arquivo(
            ("SP", "Perto", 1000, 0.1, 10, 0.7, "21°00'00\"", "50°00'00\""),
            ("SP", "Longe", 2000, 0.2, 20, 0.9, "22°00'00\"", "50°00'00\""))).WithLocalities(m.ToArray());

        var local = Assert.Single(base_.Localities);
        Assert.Equal("Valparaíso", local.Name);
        Assert.Equal("SP", local.State);
        Assert.Equal(1500, base_.Interpolate(local).K, 6);
        Assert.Equal("SP", base_.NearestState(-21.1, -50));
        Assert.StartsWith("Plúvio 2.1, Valparaíso-SP, interpolado:", base_.Interpolate(local).Source("Valparaíso-SP"));
    }

    /// <summary>
    /// Com o Plúvio instalado, o local do memorial (21°13'59" S, 50°50'12" O)
    /// dá os quatro números do memorial de Valparaiso. Sem o Plúvio, o teste
    /// não tem o que conferir (a base é do GPRH e não vai para o repositório).
    /// </summary>
    [Fact]
    [Trait("Etapa", "32")]
    public void AValparaisoDoMemorialSaiDaBaseInstalada()
    {
        var caminho = PluvioDatabase
            .UsualPaths(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))
            .FirstOrDefault(File.Exists);
        if (caminho is null) return;

        var base_ = PluvioDatabase.Parse(File.ReadAllBytes(caminho));
        var lat = -(21 + 13 / 60.0 + 59 / 3600.0);
        var lon = -(50 + 50 / 60.0 + 12 / 3600.0);

        var v = base_.Interpolate(lat, lon);

        Assert.Equal(549, base_.Stations.Count);
        Assert.Equal(1544.886, v.K, 3);
        Assert.Equal(0.142, v.A, 3);
        Assert.Equal(24.953, v.B, 3);
        Assert.Equal(0.756, v.C, 3);
        Assert.Equal("Guararapes", v.Nearest[0].Station.Name);

        var localidades = Path.Combine(Path.GetDirectoryName(caminho)!, PluvioDatabase.LocalitiesFileName);
        if (!File.Exists(localidades)) return;

        var comLocais = base_.WithLocalities(File.ReadAllBytes(localidades));
        Assert.Equal(4937, comLocais.Localities.Count);
        Assert.Contains(comLocais.Localities, l => l.State == "SP" && l.Name == "Valparaíso");
    }
}
