using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Clivus.Core;

/// <summary>
/// Uma planilha do Excel (.xlsx) mínima, escrita à mão: abas com linhas de
/// texto e número. Sem biblioteca externa de propósito: o plugin roda
/// dentro do Civil 3D, e cada DLL a mais é uma DLL a mais para conflitar com
/// as da Autodesk. O formato (Office Open XML) é um zip de XML; texto vai
/// como "inlineStr", número como número, para o Excel somar.
/// <para>
/// Formatado (10/10/2026, "bem formatado, bem disposto"): cabeçalho com
/// fundo escuro e letra branca, congelado; bordas finas; número com
/// separador de milhar (inteiro sem casas, o resto com duas); linhas de total
/// em negrito com fundo; notas em itálico cinza; largura da coluna pelo
/// conteúdo. O valor gravado é o número inteiro: o formato é só a tela.
/// </para>
/// </summary>
public sealed class XlsxWriter
{
    private readonly List<(string Nome, XlsxSheet Linhas)> _abas = [];

    /// <summary>Se já há uma aba com este nome (o Excel não diferencia maiúscula).</summary>
    public bool HasSheet(string nome) => _abas.Any(a => string.Equals(a.Nome, nome, StringComparison.OrdinalIgnoreCase));

    /// <summary>Uma aba nova; devolve a lista de linhas para encher.</summary>
    /// <exception cref="ArgumentException">Nome vazio, repetido ou com caractere que o Excel recusa.</exception>
    public XlsxSheet Sheet(string nome) => Sheet(nome, primeira: false);

    /// <summary>Uma aba nova, no fim ou (<paramref name="primeira"/>) antes de todas.</summary>
    public XlsxSheet Sheet(string nome, bool primeira)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);

        if (nome.Length > 31 || nome.IndexOfAny([':', '\\', '/', '?', '*', '[', ']']) >= 0)
            throw new ArgumentException($"O Excel não aceita a aba \"{nome}\" (até 31 caracteres, sem : \\ / ? * [ ]).", nameof(nome));

        if (_abas.Any(a => string.Equals(a.Nome, nome, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Já há uma aba \"{nome}\".", nameof(nome));

        var linhas = new XlsxSheet();
        _abas.Insert(primeira ? 0 : _abas.Count, (nome, linhas));
        return linhas;
    }

    /// <summary>O arquivo inteiro.</summary>
    public byte[] ToBytes()
    {
        if (_abas.Count == 0) throw new InvalidOperationException("Uma planilha precisa de pelo menos uma aba.");

        using var memoria = new MemoryStream();

        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            Escrever(zip, "[Content_Types].xml", TiposDeConteudo());
            Escrever(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                + "</Relationships>");
            Escrever(zip, "xl/workbook.xml", PastaDeTrabalho());
            Escrever(zip, "xl/_rels/workbook.xml.rels", RelacoesDaPasta());
            Escrever(zip, "xl/styles.xml", Estilos);

            for (var i = 0; i < _abas.Count; i++)
                Escrever(zip, $"xl/worksheets/sheet{i + 1}.xml", Aba(_abas[i].Linhas));
        }

        return memoria.ToArray();
    }

    /// <summary>A letra da coluna: 0 → A, 25 → Z, 26 → AA.</summary>
    public static string Column(int indice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(indice);

        var letras = new StringBuilder();

        for (var n = indice + 1; n > 0; n = (n - 1) / 26)
            letras.Insert(0, (char)('A' + (n - 1) % 26));

        return letras.ToString();
    }

    private string TiposDeConteudo()
    {
        var abas = string.Concat(Enumerable.Range(1, _abas.Count).Select(i =>
            $"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"));

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
            + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
            + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
            + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
            + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
            + abas
            + "</Types>";
    }

    private string PastaDeTrabalho()
    {
        var abas = string.Concat(_abas.Select((a, i) =>
            $"<sheet name=\"{SecurityElement.Escape(a.Nome)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>"));

        // Na impressão, o cabeçalho repete no topo de cada página.
        var titulos = string.Concat(_abas.Select((a, i) => (a, i))
            .Where(x => x.a.Linhas.HeaderRow >= 0 && x.a.Linhas.HeaderRow < x.a.Linhas.Count)
            .Select(x =>
            {
                var linha = (x.a.Linhas.HeaderRow + 1).ToString(CultureInfo.InvariantCulture);
                var nome = SecurityElement.Escape("'" + x.a.Nome.Replace("'", "''") + "'");
                return $"<definedName name=\"_xlnm.Print_Titles\" localSheetId=\"{x.i}\">{nome}!${linha}:${linha}</definedName>";
            }));

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" "
            + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
            + $"<sheets>{abas}</sheets>"
            + (titulos.Length > 0 ? $"<definedNames>{titulos}</definedNames>" : string.Empty)
            + "</workbook>";
    }

    private string RelacoesDaPasta()
    {
        var abas = string.Concat(Enumerable.Range(1, _abas.Count).Select(i =>
            $"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>"));

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
            + abas
            + $"<Relationship Id=\"rId{_abas.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
            + "</Relationships>";
    }

    // Os estilos (índice em cellXfs). Fontes: 0 normal, 1 negrito, 2 itálico cinza, 3 negrito branco, 4 título.
    // Preenchimentos: 2 cabeçalho (petróleo da marca), 3 total (âmbar claro), 4 grupo (cinza claro).
    // Bordas: 1 fina cinza. Números: 3 = #,##0; 4 = #,##0.00 (o Excel põe o separador da máquina).
    private const int Cabecalho = 1, Texto = 2, Inteiro = 3, Decimal = 4, TotalTexto = 5, TotalInteiro = 6, TotalDecimal = 7, Nota = 8, Titulo = 9,
        Secao = 10, SecaoInteiro = 11, SecaoDecimal = 12;

    private const string Estilos =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
        + "<fonts count=\"5\">"
        + "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>"
        + "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>"
        + "<font><i/><sz val=\"10\"/><color rgb=\"FF595959\"/><name val=\"Calibri\"/></font>"
        + "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font>"
        + "<font><b/><sz val=\"16\"/><color rgb=\"FF0F2533\"/><name val=\"Calibri\"/></font>"
        + "</fonts>"
        + "<fills count=\"5\">"
        + "<fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>"
        + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF0F2533\"/><bgColor indexed=\"64\"/></patternFill></fill>"
        + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFDEBC8\"/><bgColor indexed=\"64\"/></patternFill></fill>"
        + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFEDEFF2\"/><bgColor indexed=\"64\"/></patternFill></fill>"
        + "</fills>"
        + "<borders count=\"2\"><border/>"
        + "<border><left style=\"thin\"><color rgb=\"FFBFBFBF\"/></left><right style=\"thin\"><color rgb=\"FFBFBFBF\"/></right>"
        + "<top style=\"thin\"><color rgb=\"FFBFBFBF\"/></top><bottom style=\"thin\"><color rgb=\"FFBFBFBF\"/></bottom><diagonal/></border>"
        + "</borders>"
        + "<cellStyleXfs count=\"1\"><xf/></cellStyleXfs>"
        + "<cellXfs count=\"13\">"
        + "<xf fontId=\"0\"/>"
        + "<xf fontId=\"3\" fillId=\"2\" borderId=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"center\" wrapText=\"1\"/></xf>"
        + "<xf fontId=\"0\" borderId=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>"
        + "<xf fontId=\"0\" borderId=\"1\" numFmtId=\"3\" applyBorder=\"1\" applyNumberFormat=\"1\"/>"
        + "<xf fontId=\"0\" borderId=\"1\" numFmtId=\"4\" applyBorder=\"1\" applyNumberFormat=\"1\"/>"
        + "<xf fontId=\"1\" fillId=\"3\" borderId=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>"
        + "<xf fontId=\"1\" fillId=\"3\" borderId=\"1\" numFmtId=\"3\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyNumberFormat=\"1\"/>"
        + "<xf fontId=\"1\" fillId=\"3\" borderId=\"1\" numFmtId=\"4\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyNumberFormat=\"1\"/>"
        + "<xf fontId=\"2\" applyFont=\"1\"/>"
        + "<xf fontId=\"4\" applyFont=\"1\"/>"
        + "<xf fontId=\"1\" fillId=\"4\" borderId=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>"
        + "<xf fontId=\"1\" fillId=\"4\" borderId=\"1\" numFmtId=\"3\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyNumberFormat=\"1\"/>"
        + "<xf fontId=\"1\" fillId=\"4\" borderId=\"1\" numFmtId=\"4\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyNumberFormat=\"1\"/>"
        + "</cellXfs>"
        + "</styleSheet>";

    private static string Aba(XlsxSheet linhas)
    {
        var xml = new StringBuilder();
        var cabecalho = linhas.HeaderRow;
        var colunas = linhas.Count == 0 ? 0 : linhas.Max(l => l.Length);

        xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        xml.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

        // Impressão: a largura toda numa página (a altura corre em quantas precisar).
        xml.Append("<sheetPr><pageSetUpPr fitToPage=\"1\"/></sheetPr>");

        // O cabeçalho fica parado ao rolar.
        if (cabecalho >= 0 && cabecalho < linhas.Count)
        {
            var abaixo = "A" + (cabecalho + 2).ToString(CultureInfo.InvariantCulture);
            xml.Append(CultureInfo.InvariantCulture,
                $"<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"{cabecalho + 1}\" topLeftCell=\"{abaixo}\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        }

        var larguras = Larguras(linhas, colunas);

        // A coluna com algum número quebrado mostra todos com duas casas (112,00 ao lado de 99,50).
        var decimais = Enumerable.Range(0, colunas).Select(c => !linhas.PerCellFormat && Enumerable.Range(0, linhas.Count)
            .Where(l => l != linhas.HeaderRow && !linhas.NoteRows.Contains(l) && !linhas.TitleRows.Contains(l))
            .Any(l => c < linhas[l].Length && linhas[l][c] is double d && double.IsFinite(d) && Math.Abs(d - Math.Round(d)) >= 1e-9)).ToList();
        if (larguras.Count > 0)
        {
            xml.Append("<cols>");
            for (var c = 0; c < larguras.Count; c++)
                xml.Append(CultureInfo.InvariantCulture, $"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{larguras[c]:0.#}\" customWidth=\"1\"/>");
            xml.Append("</cols>");
        }

        xml.Append("<sheetData>");

        for (var l = 0; l < linhas.Count; l++)
        {
            var linha = linhas[l];
            var tipo = l == cabecalho ? Cabecalho
                : linhas.TotalRows.Contains(l) ? TotalTexto
                : linhas.NoteRows.Contains(l) ? Nota
                : linhas.TitleRows.Contains(l) ? Titulo
                : linhas.SectionRows.Contains(l) ? Secao
                : l < cabecalho ? 0
                : Texto;

            // O cabeçalho ganha altura para as linhas que a quebra de texto vai precisar.
            var altura = string.Empty;
            if (l == cabecalho && larguras.Count > 0)
            {
                var linhasDeTexto = linha.Select((v, c) => (int)Math.Ceiling((Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").Length / Math.Max(1, larguras[c] - 1))).DefaultIfEmpty(1).Max();
                altura = string.Create(CultureInfo.InvariantCulture, $" ht=\"{15 * Math.Max(1, linhasDeTexto) + 3}\" customHeight=\"1\"");
            }
            else if (tipo == Titulo)
            {
                altura = " ht=\"24\" customHeight=\"1\"";
            }

            xml.Append(CultureInfo.InvariantCulture, $"<row r=\"{l + 1}\"{altura}>");

            for (var c = 0; c < linha.Length; c++)
            {
                var referencia = Column(c) + (l + 1).ToString(CultureInfo.InvariantCulture);

                double? numero = linha[c] switch
                {
                    double d => double.IsFinite(d) ? d : null,
                    int or long or short or byte or float or decimal => Convert.ToDouble(linha[c], CultureInfo.InvariantCulture) is var x && double.IsFinite(x) ? x : null,
                    _ => null,
                };

                if (numero is { } n)
                {
                    var inteiro = !decimais[c] && Math.Abs(n - Math.Round(n)) < 1e-9;
                    var estilo = tipo switch
                    {
                        TotalTexto => inteiro ? TotalInteiro : TotalDecimal,
                        Secao => inteiro ? SecaoInteiro : SecaoDecimal,
                        Texto => inteiro ? Inteiro : Decimal,
                        _ => tipo,
                    };
                    var valor = linha[c] is int or long or short or byte ? Convert.ToInt64(linha[c], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : n.ToString("R", CultureInfo.InvariantCulture);
                    xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{referencia}\"{Estilo(estilo)}><v>{valor}</v></c>");
                    continue;
                }

                if (linha[c] is null or double or float or decimal)
                {
                    // Célula vazia (NaN e infinito não são número no Excel): só a borda e o fundo da linha.
                    if (tipo is Texto or TotalTexto or Secao) xml.Append($"<c r=\"{referencia}\"{Estilo(tipo)}/>");
                    continue;
                }

                // Caractere de controle não é XML válido: o Excel recusaria o arquivo inteiro por uma célula.
                var cru = new string((Convert.ToString(linha[c], CultureInfo.InvariantCulture) ?? "")
                    .Where(ch => ch >= ' ' || ch is '\t' or '\n' or '\r').ToArray());
                var texto = SecurityElement.Escape(cru) ?? "";
                xml.Append($"<c r=\"{referencia}\" t=\"inlineStr\"{Estilo(tipo)}><is><t xml:space=\"preserve\">{texto}</t></is></c>");
            }

            xml.Append("</row>");
        }

        xml.Append("</sheetData>");
        xml.Append("<pageMargins left=\"0.4\" right=\"0.4\" top=\"0.5\" bottom=\"0.5\" header=\"0.3\" footer=\"0.3\"/>");
        xml.Append("<pageSetup paperSize=\"9\" orientation=\"landscape\" fitToWidth=\"1\" fitToHeight=\"0\"/>");
        xml.Append("</worksheet>");
        return xml.ToString();
    }

    private static string Estilo(int indice) => indice == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $" s=\"{indice}\"");

    /// <summary>
    /// A largura de cada coluna pelo conteúdo (em caracteres): o maior texto
    /// ou número formatado das linhas de dado e de total; o cabeçalho conta
    /// até 18 (quebra em linhas); título e nota não contam (transbordam).
    /// </summary>
    private static List<double> Larguras(XlsxSheet linhas, int colunas)
    {
        var larguras = Enumerable.Repeat(8.0, colunas).ToList();

        for (var l = 0; l < linhas.Count; l++)
        {
            if (linhas.NoteRows.Contains(l) || linhas.TitleRows.Contains(l)) continue;

            var linha = linhas[l];
            for (var c = 0; c < linha.Length; c++)
            {
                var texto = linha[c] switch
                {
                    null => string.Empty,
                    double d => double.IsFinite(d) ? d.ToString("#,##0.00", CultureInfo.InvariantCulture) : string.Empty,
                    int or long or short or byte => Convert.ToInt64(linha[c], CultureInfo.InvariantCulture).ToString("#,##0", CultureInfo.InvariantCulture),
                    _ => Convert.ToString(linha[c], CultureInfo.InvariantCulture) ?? string.Empty,
                };

                var tamanho = texto.Split('\n').Max(p => p.Length);
                if (l == linhas.HeaderRow) tamanho = Math.Min(tamanho, 18);
                larguras[c] = Math.Max(larguras[c], Math.Min(60, tamanho + 2.5));
            }
        }

        return larguras;
    }

    private static void Escrever(ZipArchive zip, string nome, string conteudo)
    {
        var entrada = zip.CreateEntry(nome, CompressionLevel.Optimal);
        using var fluxo = entrada.Open();
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(conteudo);
        fluxo.Write(bytes, 0, bytes.Length);
    }
}

/// <summary>
/// As linhas de uma aba (cada linha uma lista de células: texto, número ou
/// vazio) e o papel de algumas delas no formato: o cabeçalho (a linha
/// <see cref="HeaderRow"/>, por padrão a primeira), as de total, de nota, de
/// título e de seção (os índices começam em 0).
/// </summary>
public sealed class XlsxSheet : List<object?[]>
{
    /// <summary>A linha do cabeçalho (fundo escuro, congelada); -1 = sem cabeçalho.</summary>
    public int HeaderRow { get; set; }

    /// <summary>
    /// O formato do número decidido célula a célula (inteiro sem casas), e não
    /// por coluna: para a aba que mistura contagem e medida na mesma coluna.
    /// </summary>
    public bool PerCellFormat { get; set; }

    public HashSet<int> TotalRows { get; } = [];

    public HashSet<int> NoteRows { get; } = [];

    public HashSet<int> TitleRows { get; } = [];

    public HashSet<int> SectionRows { get; } = [];

    /// <summary>Acrescenta a linha e a marca como total.</summary>
    public void AddTotal(object?[] linha)
    {
        TotalRows.Add(Count);
        Add(linha);
    }

    /// <summary>Acrescenta uma nota (itálico cinza, transborda pelas colunas).</summary>
    public void AddNote(string nota)
    {
        NoteRows.Add(Count);
        Add([nota]);
    }
}
