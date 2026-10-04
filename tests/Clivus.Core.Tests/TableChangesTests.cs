namespace Clivus.Core.Tests;

/// <summary>
/// O livro de mudanças do vigia (7.2): acumula eventos e decide ao fim do
/// comando; e o registro de remoção vai e volta pelo formato de texto.
/// </summary>
public class TableChangesTests
{
    private static readonly DateTime Quando = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Etapa", "7")]
    public void MesaMovidaFicaSujaComOMotivoCerto()
    {
        var livro = new PendingChanges();
        var mesa = Guid.NewGuid();

        livro.Note(mesa, ChangeKind.Modified, isContour: false);
        livro.Note(mesa, ChangeKind.Modified, isContour: true);

        var decisao = livro.Resolve(Quando);

        Assert.Equal(PendingChanges.ReasonModified, decisao.Dirty[mesa]);
        Assert.Empty(decisao.Removed);
        Assert.True(livro.IsEmpty);
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void ContornoApagadoERemocaoNaoSujeira()
    {
        var livro = new PendingChanges();
        var mesa = Guid.NewGuid();

        // Apagar a mesa inteira: as peças saem como apagadas e o contorno também.
        livro.Note(mesa, ChangeKind.Erased, isContour: false);
        livro.Note(mesa, ChangeKind.Erased, isContour: true, label: "F1.2");
        livro.Note(mesa, ChangeKind.Modified, isContour: false);

        var decisao = livro.Resolve(Quando);

        Assert.Empty(decisao.Dirty);
        var removida = Assert.Single(decisao.Removed);
        Assert.Equal(mesa, removida.Id);
        Assert.Equal("F1.2", removida.Label);
        Assert.Equal(Quando, removida.When);
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void OMotivoMaisGraveVence()
    {
        var livro = new PendingChanges();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        livro.Note(a, ChangeKind.Modified, false);
        livro.Note(a, ChangeKind.Erased, false);
        livro.Note(a, ChangeKind.Modified, false);

        livro.Note(b, ChangeKind.Erased, false);
        livro.Note(b, ChangeKind.Appended, false);

        var decisao = livro.Resolve(Quando);

        Assert.Equal(PendingChanges.ReasonErased, decisao.Dirty[a]);
        Assert.Equal(PendingChanges.ReasonAppended, decisao.Dirty[b]);
        Assert.Equal(2, decisao.Dirty.Count);
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void LivroVazioDecideNadaEGuidVazioEIgnorado()
    {
        var livro = new PendingChanges();

        livro.Note(Guid.Empty, ChangeKind.Modified, false);

        Assert.True(livro.IsEmpty);
        Assert.True(livro.Resolve(Quando).IsEmpty);
    }

    /// <summary>Contorno que voltou (desfazer de um apagar): sai das removidas e a mesa conta como tocada.</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void ContornoQueVoltouSaiDasRemovidasEFicaTocado()
    {
        var livro = new PendingChanges();
        var mesa = Guid.NewGuid();

        livro.Note(mesa, ChangeKind.Restored, isContour: true, label: "F1.2");
        livro.Note(mesa, ChangeKind.Restored, isContour: false);

        var decisao = livro.Resolve(Quando);

        Assert.Equal(mesa, Assert.Single(decisao.Restored));
        Assert.Empty(decisao.Removed);
        Assert.Equal(PendingChanges.ReasonModified, decisao.Dirty[mesa]);
        Assert.True(livro.IsEmpty);

        // Apagado e voltou no mesmo comando: nem removida nem restaurada em dobro; o último vale.
        livro.Note(mesa, ChangeKind.Erased, true, "F1.2");
        livro.Note(mesa, ChangeKind.Restored, true, "F1.2");
        var segunda = livro.Resolve(Quando);
        Assert.Empty(segunda.Removed);
        Assert.Single(segunda.Restored);

        livro.Note(mesa, ChangeKind.Restored, true, "F1.2");
        livro.Note(mesa, ChangeKind.Erased, true, "F1.2");
        var terceira = livro.Resolve(Quando);
        Assert.Single(terceira.Removed);
        Assert.Empty(terceira.Restored);
    }

    /// <summary>Os comandos em que o vigia se cala: os nossos e os de desfazer.</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void OsComandosCaladosSaoOsNossosEOsDeDesfazer()
    {
        Assert.True(PluginInfo.IsSilencedCommand("CLIVUS_FILEIRA"));
        Assert.True(PluginInfo.IsSilencedCommand("clivus_estado"));
        Assert.True(PluginInfo.IsSilencedCommand("U"));
        Assert.True(PluginInfo.IsSilencedCommand("UNDO"));
        Assert.True(PluginInfo.IsSilencedCommand("REDO"));
        Assert.True(PluginInfo.IsSilencedCommand("MREDO"));
        Assert.True(PluginInfo.IsSilencedCommand("OOPS"));
        Assert.False(PluginInfo.IsSilencedCommand("MOVE"));
        Assert.False(PluginInfo.IsSilencedCommand("ERASE"));
        Assert.False(PluginInfo.IsSilencedCommand("GRIP_STRETCH"));
        Assert.False(PluginInfo.IsSilencedCommand(null));

        Assert.True(PluginInfo.IsUndoCommand("u"));
        Assert.False(PluginInfo.IsUndoCommand("CLIVUS_PENDENTE"));
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void ContornoApagadoSemLetreiroGanhaUmAviso()
    {
        var livro = new PendingChanges();
        var mesa = Guid.NewGuid();

        livro.Note(mesa, ChangeKind.Erased, isContour: true, label: " ");

        Assert.Equal("(sem letreiro)", Assert.Single(livro.Resolve(Quando).Removed).Label);
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void ARemocaoVaiEVoltaPeloTexto()
    {
        var remocao = new TableRemoval(Guid.NewGuid(), "F2.7", Quando);
        var campos = remocao.ToFields();

        Assert.Equal(TableRemoval.FieldCount, campos.Count);
        Assert.Equal(remocao, TableRemoval.Parse(campos));

        // E pelo formato de registro do plugin, como vai para o desenho.
        var texto = RecordTable.Write(1, TableRemoval.FieldCount, [remocao], r => r.ToFields());
        var lido = RecordTable.Read(texto, 1, TableRemoval.FieldCount, TableRemoval.Parse, "de remoções");

        Assert.Null(lido.Problem);
        Assert.Equal(remocao, Assert.Single(lido.Items));
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void RemocaoIlegivelDaNull()
    {
        Assert.Null(TableRemoval.Parse(["x", "F1.1", "2026-09-26T12:00:00Z"]));
        Assert.Null(TableRemoval.Parse([Guid.NewGuid().ToString("D"), " ", "2026-09-26T12:00:00Z"]));
        Assert.Null(TableRemoval.Parse([Guid.NewGuid().ToString("D"), "F1.1", "ontem"]));
        Assert.Null(TableRemoval.Parse(["só", "dois"]));
    }
}
