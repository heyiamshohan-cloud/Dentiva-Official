using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Dentiva.Infrastructure.Files;

/// <summary>
/// CSV / Excel-compatible / JSON / structured export writers.
/// The CSV writer is RFC 4180 compliant; the Excel-compatible writer produces
/// a real .xlsx (SpreadsheetML in a zip) with no external dependency.
/// </summary>
public static class ExportWriter
{
    public static string WriteCsv(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', headers.Select(EscapeCsv)));
        sb.Append("\r\n");
        foreach (var row in rows)
        {
            sb.Append(string.Join(',', row.Select(EscapeCsv)));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    public static void WriteCsvFile(string path, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        // UTF-8 BOM keeps Excel happy with Bengali text.
        File.WriteAllText(path, WriteCsv(headers, rows), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string EscapeCsv(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        return text;
    }

    public static void WriteXlsxFile(string path, string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        using var stream = File.Create(path);
        WriteXlsx(stream, sheetName, headers, rows);
    }

    public static void WriteXlsx(Stream output, string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        using var archive = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: false);

        AddEntry(archive, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """);

        AddEntry(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);

        AddEntry(archive, "xl/workbook.xml", $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets><sheet name="{EscapeXml(sheetName)}" sheetId="1" r:id="rId1"/></sheets>
            </workbook>
            """);

        AddEntry(archive, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            </Relationships>
            """);

        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""" + "\n");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""" + "\n");
        sb.Append("<sheetData>\n");

        static string CellRef(int rowIdx, int colIdx)
        {
            var col = colIdx;
            var name = string.Empty;
            do
            {
                name = (char)('A' + col % 26) + name;
                col = col / 26 - 1;
            }
            while (col >= 0);
            return name + (rowIdx + 1);
        }

        sb.Append("<row>\n");
        for (var c = 0; c < headers.Count; c++)
        {
            sb.Append($"""<c r="{CellRef(0, c)}" t="inlineStr"><is><t>{EscapeXml(headers[c])}</t></is></c>""" + "\n");
        }

        sb.Append("</row>\n");

        for (var r = 0; r < rows.Count; r++)
        {
            sb.Append("<row>\n");
            for (var c = 0; c < rows[r].Count; c++)
            {
                var value = rows[r][c];
                var reference = CellRef(r + 1, c);
                switch (value)
                {
                    case null:
                        break;
                    case int or long or short or byte:
                        sb.Append($"""<c r="{reference}"><v>{value}</v></c>""" + "\n");
                        break;
                    case decimal or double or float:
                        sb.Append($"""<c r="{reference}"><v>{Convert.ToString(value, CultureInfo.InvariantCulture)}</v></c>""" + "\n");
                        break;
                    default:
                        sb.Append($"""<c r="{reference}" t="inlineStr"><is><t xml:space="preserve">{EscapeXml(value.ToString())}</t></is></c>""" + "\n");
                        break;
                }
            }

            sb.Append("</row>\n");
        }

        sb.Append("</sheetData>\n</worksheet>");
        AddEntry(archive, "xl/worksheets/sheet1.xml", sb.ToString());
    }

    public static void WriteJsonFile(string path, object content)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(content, options));
    }

    private static void AddEntry(System.IO.Compression.ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, System.IO.Compression.ZipArchiveLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string EscapeXml(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}

/// <summary>RFC 4180 CSV reader used by the import wizard.</summary>
public static class CsvReader
{
    public static IReadOnlyList<IReadOnlyList<string>> Read(string path)
    {
        var rows = new List<IReadOnlyList<string>>();
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var field = new StringBuilder();
        var row = new List<string>();
        var inQuotes = false;

        while (reader.Peek() >= 0)
        {
            var ch = (char)reader.Read();
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\r')
            {
                if (reader.Peek() == '\n')
                {
                    reader.Read();
                }

                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else if (ch == '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else
            {
                field.Append(ch);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
