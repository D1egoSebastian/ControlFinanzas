using System.IO.Compression;
using System.Text;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// Builds minimal, valid XLSX packages by hand (raw OOXML parts). Used only for shapes the ClosedXML writer cannot
/// produce, such as stored error cells and formula cells with a cached value (F-020, DD-10, C-16).
/// </summary>
internal static class XlsxHandBuilt
{
    private const string _sheetXmlHead =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>";

    private const string _sheetXmlTail = "</sheetData></worksheet>";

    public static byte[] WithFormulaCell(string formula, string cachedValue) =>
        Build("Sheet1", "<row r=\"1\"><c r=\"A1\"><f>" + formula + "</f><v>" + cachedValue + "</v></c></row>");

    public static byte[] WithErrorCell(string error) =>
        Build("Sheet1", "<row r=\"1\"><c r=\"A1\" t=\"e\"><v>" + error + "</v></c></row>");

    /// <summary>Builds a workbook whose single sheet holds the given raw <c>&lt;row&gt;</c> XML.</summary>
    public static byte[] Build(string sheetName, string rowsXml, int? dimensionRows = null)
    {
        string dimension = dimensionRows is null ? string.Empty : "<dimension ref=\"A1:A" + dimensionRows + "\"/>";
        string sheet = _sheetXmlHead.Replace("<sheetData>", dimension + "<sheetData>", StringComparison.Ordinal) + rowsXml + _sheetXmlTail;
        return Package(
            ("[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>"),
            ("_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>"),
            ("xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"" + sheetName + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>"),
            ("xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>"),
            ("xl/worksheets/sheet1.xml", sheet));
    }

    /// <summary>Zips the given (name, text) parts into a package.</summary>
    public static byte[] Package(params (string Name, string Content)[] parts)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach ((string name, string content) in parts)
            {
                ZipArchiveEntry entry = zip.CreateEntry(name);
                using Stream s = entry.Open();
                byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                s.Write(bytes, 0, bytes.Length);
            }
        }

        return ms.ToArray();
    }
}
