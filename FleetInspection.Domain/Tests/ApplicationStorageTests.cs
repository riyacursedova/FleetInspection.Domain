using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FleetInspection.Application.Services;
using FleetInspection.Domain.Models.Entities;
using FleetInspection.Domain.Models.ValueObject;
using FleetInspection.Infrastructure.Persistence;
using Xunit;

namespace FleetInspection.Tests.Application;

public class ApplicationStorageTests
{
    private VehicleInspectionLog CreateDraftLog()
    {
        return new VehicleInspectionLog(
            InspectionId.New(),
            new VehicleId(Guid.NewGuid()),
            new InspectorId(Guid.NewGuid()),
            new Odometer(100000),
            new FuelLevel(80m)
        );
    }

    // 1. Успішне завершення огляду без критичних дефектів
    [Fact]
    public async Task CompleteInspection_Success()
    {
        var original = CreateDraftLog();
        var session = new MemorySession(original);
        var service = new InspectionAppService(session, session);
        
        await service.CompleteInspectionAsync(original.Id, new Odometer(100250), new FuelLevel(75m));
        
        var reloaded = await session.FindAsync(original.Id);
        Assert.NotNull(reloaded);
        Assert.NotSame(original, reloaded!.Aggregate);
        Assert.Equal(InspectionStatus.ApprovedForDrive, reloaded.Aggregate.Status);
        Assert.Equal(2, reloaded.Revision);
    }

    // 2. Завершення огляду за наявності критичного дефекту
    [Fact]
    public async Task CompleteInspection_CriticalDefect()
    {
        var original = CreateDraftLog();
        original.AddDefect("Тріщина гальмівного диска", DefectSeverity.Critical);
        var session = new MemorySession(original);
        var service = new InspectionAppService(session, session);
        
        await service.CompleteInspectionAsync(original.Id, new Odometer(100250), new FuelLevel(75m));
        
        var reloaded = await session.FindAsync(original.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(InspectionStatus.FailedBlockOutbound, reloaded!.Aggregate.Status);
        Assert.Equal(2, reloaded.Revision);
    }

    // 3. Порушення інваріанта
    [Fact]
    public async Task CompleteInspection_InvalidOdometer()
    {
        var original = CreateDraftLog();
        var session = new MemorySession(original);
        var service = new InspectionAppService(session, session);
        
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteInspectionAsync(original.Id, new Odometer(99500), new FuelLevel(80m)));

        var reloaded = await session.FindAsync(original.Id);
        Assert.Equal(InspectionStatus.Draft, reloaded!.Aggregate.Status);
        Assert.Equal(1, reloaded.Revision);
    }

    // 4. Конфлікт паралельного оновлення
    [Fact]
    public async Task SaveChanges_ConcurrencyConflict()
    {
        var original = CreateDraftLog();
        var session = new MemorySession(original);

        var client1 = await session.FindAsync(original.Id);
        var client2 = await session.FindAsync(original.Id);
        
        client1!.Aggregate.Complete(new Odometer(100100), new FuelLevel(80m));
        session.Stage(client1.Aggregate, client1.Revision);
        await session.SaveChangesAsync();
        
        client2!.Aggregate.Complete(new Odometer(100200), new FuelLevel(70m));
        session.Stage(client2.Aggregate, client2.Revision);
        
        await Assert.ThrowsAsync<ConcurrencyException>(() => session.SaveChangesAsync());
    }

    // 5. Спроба завершити не существубщий акт
    [Fact]
    public async Task CompleteInspection_NotFound()
    {
        var session = new MemorySession();
        var service = new InspectionAppService(session, session);
        var missingId = InspectionId.New();
        
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CompleteInspectionAsync(missingId, new Odometer(100200), new FuelLevel(75m)));
    }
}