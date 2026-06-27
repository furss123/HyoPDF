namespace HyoPDF.Core.Printing;

public static class PrintLayoutHelper
{
    public static readonly int[] SupportedPagesPerSheet = [1, 2, 4, 6, 9, 16];

    public static (int Columns, int Rows) GetGrid(int pagesPerSheet) => pagesPerSheet switch
    {
        1 => (1, 1),
        2 => (2, 1),
        4 => (2, 2),
        6 => (3, 2),
        9 => (3, 3),
        16 => (4, 4),
        _ => (1, 1)
    };

    public static List<int[]> GroupIntoSheets(IReadOnlyList<int> pageIndices, int pagesPerSheet)
    {
        var sheets = new List<int[]>();
        if (pageIndices.Count == 0)
            return sheets;

        pagesPerSheet = Math.Max(1, pagesPerSheet);
        for (var i = 0; i < pageIndices.Count; i += pagesPerSheet)
        {
            var count = Math.Min(pagesPerSheet, pageIndices.Count - i);
            var sheet = new int[count];
            for (var j = 0; j < count; j++)
                sheet[j] = pageIndices[i + j];
            sheets.Add(sheet);
        }

        return sheets;
    }
}
