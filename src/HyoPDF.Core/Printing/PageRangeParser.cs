namespace HyoPDF.Core.Printing;

public static class PageRangeParser
{
    public static int[] Parse(string? input, int pageCount)
    {
        if (pageCount <= 0)
            throw new InvalidOperationException("Document has no pages.");

        if (string.IsNullOrWhiteSpace(input))
            return Enumerable.Range(0, pageCount).ToArray();

        var indices = new SortedSet<int>();

        foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Contains('-', StringComparison.Ordinal))
            {
                var bounds = part.Split('-', StringSplitOptions.TrimEntries);
                if (bounds.Length != 2 ||
                    !int.TryParse(bounds[0], out var start) ||
                    !int.TryParse(bounds[1], out var end))
                    throw new FormatException($"Invalid page range segment: '{part}'.");

                if (start < 1 || end < 1 || start > end)
                    throw new FormatException($"Invalid page range segment: '{part}'.");

                for (var page = start; page <= end; page++)
                {
                    if (page > pageCount)
                        throw new ArgumentOutOfRangeException(nameof(input), $"Page {page} exceeds document page count ({pageCount}).");
                    indices.Add(page - 1);
                }
            }
            else
            {
                if (!int.TryParse(part, out var page) || page < 1)
                    throw new FormatException($"Invalid page number: '{part}'.");

                if (page > pageCount)
                    throw new ArgumentOutOfRangeException(nameof(input), $"Page {page} exceeds document page count ({pageCount}).");

                indices.Add(page - 1);
            }
        }

        if (indices.Count == 0)
            throw new FormatException("Page range is empty.");

        return indices.ToArray();
    }
}
