using System.Threading.Tasks;

namespace Dorado.Application.Interfaces;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(string title = "Select Music Collection Folder");
}
