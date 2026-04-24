using System.Text;
using System.Text.RegularExpressions;

public static class TerrainTileNaming
{
    private static readonly Regex TileLabelRegex = new Regex(@"^(?<col>[A-Za-z]+)(?<row>\d+)$", RegexOptions.Compiled);

    public static string ResolvePattern(string pattern, int row, int col)
    {
        return pattern
            .Replace("{tile}", GetTileLabel(row, col))
            .Replace("{colLetter}", GetColumnLetter(col))
            .Replace("{row1}", (row + 1).ToString())
            .Replace("{row}", row.ToString())
            .Replace("{col1}", (col + 1).ToString())
            .Replace("{col}", col.ToString());
    }

    public static string GetTileLabel(int row, int col)
    {
        return $"{GetColumnLetter(col)}{row + 1}";
    }

    public static string GetColumnLetter(int columnIndex)
    {
        var index = columnIndex;
        var builder = new StringBuilder();

        do
        {
            var remainder = index % 26;
            builder.Insert(0, (char)('A' + remainder));
            index = (index / 26) - 1;
        }
        while (index >= 0);

        return builder.ToString();
    }

    public static bool TryParseTileLabel(string value, out int row, out int col)
    {
        row = -1;
        col = -1;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = TileLabelRegex.Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        var columnLetters = match.Groups["col"].Value.ToUpperInvariant();
        var rowText = match.Groups["row"].Value;
        if (!int.TryParse(rowText, out var parsedRow1) || parsedRow1 <= 0)
        {
            return false;
        }

        var parsedCol = 0;
        for (var i = 0; i < columnLetters.Length; i++)
        {
            parsedCol = (parsedCol * 26) + (columnLetters[i] - 'A' + 1);
        }

        row = parsedRow1 - 1;
        col = parsedCol - 1;
        return row >= 0 && col >= 0;
    }
}
