using HyoPDF.Core.Printing;

namespace HyoPDF.Core.Services;

public interface IPrintService
{
    void PrintDocument(PrintOptions options);
    IReadOnlyList<string> GetInstalledPrinters();
}
