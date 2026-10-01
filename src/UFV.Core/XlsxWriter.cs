using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace UFV.Core;

/// <summary>
/// Uma planilha do Excel (.xlsx) mínima, escrita à mão: abas com linhas de
/// texto e número. Sem biblioteca externa de propósito: o plugin roda
/// dentro do Civil 3D, e cada DLL a mais é uma DLL a mais para conflitar com
/// as da Autodesk. O formato (Office Open XML) é um zip de XML; texto vai
/// como "inlineStr", número como número, para o Excel somar.
/// </summary>
public sealed class XlsxWriter
{
    private readonly List<(string Nome, List<object?[]> Linhas)> _abas = [];

    /// <summary>Uma aba nova; devolve a lista de linhas para encher.</summary>
    /// <exception cref="ArgumentException">Nome vazio, repetido ou com caractere que o Excel recusa.</exception>
    public List<object?[]> Sheet(string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);

        if (nome.Length > 31 || nome.IndexOfAny([':', '\\', '/', '?', '*', '[', ']']) >= 0)
            throw new ArgumentException($"O Excel não aceita a aba \"{nome}\" (até 31 caracteres, sem : \\ / ? * [ ]).", nameof(nome));

        if (_abas.Any(a => string.Equals(a.Nome, nome, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Já há uma aba \"{nome}\".", nameof(nome));

        var linhas = new List<object?[]>();
        _abas.Add((nome, linhas));
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
            Escrever(zip, "xl/styles.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                + "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>"
                + "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>"
                + "<borders count=\"1\"><border/></borders>"
                + "<cellStyleXfs count=\"1\"><xf/></cellStyleXfs>"
                + "<cellXfs count=\"2\"><xf fontId=\"0\"/><xf fontId=\"1\" applyFont=\"1\"/></cellXfs>"
                + "</styleSheet>");

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

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" "
            + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
            + $"<sheets>{abas}</sheets></workbook>";
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

    private static string Aba(List<object?[]> linhas)
    {
        var xml = new StringBuilder();

        xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        xml.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

        for (var l = 0; l < linhas.Count; l++)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<row r=\"{l + 1}\">");

            var linha = linhas[l];

            for (var c = 0; c < linha.Length; c++)
            {
                var referencia = Column(c) + (l + 1).ToString(CultureInfo.InvariantCulture);

                // A primeira linha de cada aba é o cabeçalho, em negrito.
                var estilo = l == 0 ? " s=\"1\"" : "";

                switch (linha[c])
                {
                    case null:
                        break;

                    case double d when double.IsFinite(d):
                        xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{referencia}\"{estilo}><v>{d.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                        break;

                    case int n:
                        xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{referencia}\"{estilo}><v>{n}</v></c>");
                        break;

                    case double:
                        // NaN e infinito não são número no Excel: célula vazia.
                        break;

                    default:
                        var texto = SecurityElement.Escape(Convert.ToString(linha[c], CultureInfo.InvariantCulture)) ?? "";
                        xml.Append($"<c r=\"{referencia}\" t=\"inlineStr\"{estilo}><is><t xml:space=\"preserve\">{texto}</t></is></c>");
                        break;
                }
            }

            xml.Append("</row>");
        }

        xml.Append("</sheetData></worksheet>");
        return xml.ToString();
    }

    private static void Escrever(ZipArchive zip, string nome, string conteudo)
    {
        var entrada = zip.CreateEntry(nome, CompressionLevel.Optimal);
        using var fluxo = entrada.Open();
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(conteudo);
        fluxo.Write(bytes, 0, bytes.Length);
    }
}
