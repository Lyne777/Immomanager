namespace Immomanager.Web.Services;

public interface IShoppingListPdfGenerator
{
    Task<(string FileName, string Url)> GenerateAsync(int shoppingListId, CancellationToken cancellationToken = default);
}
