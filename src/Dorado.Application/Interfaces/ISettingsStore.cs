using Dorado.Application.Models;

namespace Dorado.Application.Interfaces;

public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
