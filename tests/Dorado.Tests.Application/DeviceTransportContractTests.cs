using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Models;

namespace Dorado.Tests.Application;

/// <summary>
/// The same contract must hold for the simulated transport and the real
/// <see cref="MtpTransport"/> over a virtual MTP client — proving the seam is
/// interchangeable ahead of hardware bring-up.
/// </summary>
public class DeviceTransportContractTests
{
    public static IEnumerable<object[]> Transports()
    {
        yield return new object[] { new SimulatedDeviceTransport("SIM-1", "Zune HD 32GB", 32L * 1024 * 1024 * 1024) };
        yield return new object[] { new MtpTransport("VIRT-1", new VirtualMtpDeviceClient()) };
    }

    [Theory]
    [MemberData(nameof(Transports))]
    public void Transport_satisfies_capacity_and_transfer_contract(IDeviceTransport transport)
    {
        Assert.False(string.IsNullOrWhiteSpace(transport.DeviceSerialNumber));
        Assert.False(string.IsNullOrWhiteSpace(transport.DeviceName));
        Assert.True(transport.TotalCapacityBytes > 0);
        Assert.True(transport.SystemBytes > 0);
        Assert.InRange(transport.FreeBytes, 1, transport.TotalCapacityBytes);

        var item = new TransferItem
        {
            Action = TransferAction.Add,
            Category = SyncCategoryType.Music,
            EntityId = Guid.NewGuid(),
            Title = "Contract Track",
            SizeBytes = 5_000_000
        };

        var freeBefore = transport.FreeBytes;
        transport.CopyToDevice(item);

        Assert.True(transport.TryGetItem(item.EntityId, out var stored));
        Assert.Equal("Contract Track", stored.Title);
        Assert.Contains(transport.GetContents(), c => c.EntityId == item.EntityId);
        Assert.True(transport.FreeBytes < freeBefore);

        transport.RemoveFromDevice(stored);
        Assert.False(transport.TryGetItem(item.EntityId, out _));
        Assert.Equal(freeBefore, transport.FreeBytes);
    }

    [Fact]
    public void Mtp_transport_reflects_virtual_client_metadata()
    {
        var client = new VirtualMtpDeviceClient("Zune 80", 80L * 1024 * 1024 * 1024);
        var transport = new MtpTransport("ZUNE-80", client);

        Assert.True(client.IsOpen);
        Assert.Equal("Zune 80", transport.DeviceName);
        Assert.Equal(80L * 1024 * 1024 * 1024, transport.TotalCapacityBytes);
    }
}
