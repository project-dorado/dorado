using System;
using System.Threading;
using System.Threading.Tasks;
using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

public interface IMixviewService
{
    Task<MixConstellation> GenerateConstellationAsync(
        string seedName, 
        MixNodeType seedType, 
        Guid? seedId = null, 
        CancellationToken cancellationToken = default);
}
