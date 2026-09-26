namespace UFV.Core.Tests;

/// <summary>O grupo de mesas (7.9): vai e volta pelo texto, e recusa o que não vale.</summary>
public class TableGroupTests
{
    private static readonly DateTime Quando = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Etapa", "7")]
    public void VaiEVoltaPeloTexto()
    {
        var grupo = new TableGroup(Guid.NewGuid(), "Bloco A", [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], Quando, 3);

        var campos = grupo.ToFields();
        Assert.Equal(TableGroup.FieldCount, campos.Count);

        var lido = TableGroup.Parse(campos);
        Assert.NotNull(lido);
        Assert.Equal(grupo.Id, lido.Id);
        Assert.Equal(grupo.Name, lido.Name);
        Assert.Equal(grupo.CreatedAt, lido.CreatedAt);
        Assert.Equal(grupo.Tables, lido.Tables);
        Assert.Equal(3, lido.Number);
        Assert.Equal("3\\PBloco A", lido.Caption);

        // Registro antigo, sem o número: lê com número zero.
        var antigo = TableGroup.Parse(campos.Take(4).ToList());
        Assert.NotNull(antigo);
        Assert.Equal(0, antigo.Number);
        Assert.Equal("Bloco A", antigo.Caption);
        Assert.Null(TableGroup.Parse([.. campos.Take(4), "x"]));

        // E pelo registro do plugin.
        var texto = RecordTable.Write(1, TableGroup.FieldCount, [grupo], g => g.ToFields());
        var tabela = RecordTable.Read(texto, 1, TableGroup.FieldCount, TableGroup.Parse, "de grupos");
        Assert.Null(tabela.Problem);
        Assert.Equal(grupo.Tables, Assert.Single(tabela.Items).Tables);
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void GrupoSemNomeSemMesaOuComRepeticaoNaoVale()
    {
        var mesa = Guid.NewGuid();

        Assert.False(new TableGroup(Guid.NewGuid(), " ", [mesa], Quando).IsValid);
        Assert.False(new TableGroup(Guid.NewGuid(), "A", [], Quando).IsValid);
        Assert.False(new TableGroup(Guid.NewGuid(), "A", [mesa, mesa], Quando).IsValid);
        Assert.False(new TableGroup(Guid.Empty, "A", [mesa], Quando).IsValid);
        Assert.Null(TableGroup.Parse([Guid.NewGuid().ToString("D"), "A", "x;y", "2026-09-26T12:00:00Z"]));
        Assert.Null(TableGroup.Parse(["a", "b"]));
    }
}
