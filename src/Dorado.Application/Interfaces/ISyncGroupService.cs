using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

public interface ISyncGroupService
{
    Task<SyncGroup?> GetForDeviceAsync(string deviceSerialNumber);

    Task SaveAsync(SyncGroup group);

    Task DeleteAsync(Guid groupId);
}
