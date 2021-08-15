using System.Threading.Tasks;

namespace KindlerBot.Conversion
{
    internal record BookInfo(string Title, string Author);

    internal interface ICalibreCli
    {
        Task<CalibreResult<BookInfo>> GetBookInfo(string filePath);
        Task<CalibreResult> ExportCover(string filePath, string coverOutputPath);
        Task<CalibreResult> ConvertBook(string sourcePath, string destPath);
        Task<CalibreResult> SendBookToEmail(string filePath, string email);
    }
}
