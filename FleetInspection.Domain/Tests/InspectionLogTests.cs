using System;
using FleetInspection.Domain.Models.Entities;
using FleetInspection.Domain.Models.ValueObject;
using Xunit;

namespace FleetInspection.Tests;

public class InspectionLogTests
{
    private VehicleInspectionLog CreateDraftInspection()
    {
        return new VehicleInspectionLog(
            InspectionId.New(),
            new VehicleId(Guid.NewGuid()),
            new InspectorId(Guid.NewGuid()),
            new Odometer(100000),
            new FuelLevel(80m)
        );
    }

    [Fact]
    public void Complete_Valid_Approved()
    {
        var log = CreateDraftInspection();
        log.AddDefect("Подряпина", DefectSeverity.Minor);

        log.Complete(new Odometer(100200), new FuelLevel(75m));

        Assert.Equal(InspectionStatus.ApprovedForDrive, log.Status);
    }

    [Fact]
    public void Complete_Critical_Blocked()
    {
        var log = CreateDraftInspection();
        log.AddDefect("Несправні гальма", DefectSeverity.Critical);

        log.Complete(new Odometer(100050), new FuelLevel(80m));

        Assert.Equal(InspectionStatus.FailedBlockOutbound, log.Status);
    }

    [Fact]
    public void Complete_Rollback_Throws()
    {
        var log = CreateDraftInspection();

        Assert.Throws<InvalidOperationException>(() => 
            log.Complete(new Odometer(99000), new FuelLevel(80m)));
    }

    [Fact]
    public void AddDefect_Closed_Throws()
    {
        var log = CreateDraftInspection();
        log.Complete(new Odometer(100100), new FuelLevel(80m));

        Assert.Throws<InvalidOperationException>(() => 
            log.AddDefect("Дефект", DefectSeverity.Minor));
    }
}
