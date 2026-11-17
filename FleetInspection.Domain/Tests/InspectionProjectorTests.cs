using System;
using System.IO;
using FleetInspection.Application.ReadModels;
using FleetInspection.Domain.Models.Entities;
using FleetInspection.Domain.Models.ValueObject;
using Xunit;

namespace FleetInspection.Tests;

public class InspectionProjectorTests
{
    private static VehicleInspectionLog CreateLog()
    {
        return new VehicleInspectionLog(
            InspectionId.New(),
            new VehicleId(Guid.NewGuid()),
            new InspectorId(Guid.NewGuid()),
            new Odometer(10000),
            new FuelLevel(80));
    }

    [Fact]
    public void UpdateInspection_ChangesStatus()
    {
        var log = CreateLog();
        var projector = new InspectionProjector();

        projector.Apply(InspectionProjector.FromAggregate(log, 1));
        Assert.Single(projector.Query("Draft"));

        log.Complete(new Odometer(10200), new FuelLevel(70));
        projector.Apply(InspectionProjector.FromAggregate(log, 2));

        Assert.Empty(projector.Query("Draft"));
        var item = Assert.Single(projector.Query("ApprovedForDrive"));
        Assert.Equal(2, item.Revision);
        Assert.Equal(10200, item.EndOdometerKm);
    }

    [Fact]
    public void DuplicateApply_IsIgnored()
    {
        var log = CreateLog();
        var projector = new InspectionProjector();

        var v1 = InspectionProjector.FromAggregate(log, 1);
        projector.Apply(v1);
        projector.Apply(v1);

        var item = Assert.Single(projector.Query());
        Assert.Equal(1, item.Revision);
    }

    [Fact]
    public void OlderRevision_IsIgnored()
    {
        var log = CreateLog();
        var projector = new InspectionProjector();

        var v1 = InspectionProjector.FromAggregate(log, 1);
        log.Complete(new Odometer(10200), new FuelLevel(70));
        var v2 = InspectionProjector.FromAggregate(log, 2);

        projector.Apply(v2);
        projector.Apply(v1);

        var item = Assert.Single(projector.Query());
        Assert.Equal(2, item.Revision);
        Assert.Equal("ApprovedForDrive", item.Status);
    }

    [Fact]
    public void RevisionConflict_ThrowsError()
    {
        var log = CreateLog();
        var projector = new InspectionProjector();

        var v1 = InspectionProjector.FromAggregate(log, 1);
        projector.Apply(v1);

        var broken = v1 with { Status = "FailedBlockOutbound" };

        var ex = Assert.Throws<InvalidDataException>(() => projector.Apply(broken));
        Assert.Equal("revision_conflict", ex.Message);
    }

    [Fact]
    public void LaggingRead_SyncsLater()
    {
        var log = CreateLog();
        var projector = new InspectionProjector();

        projector.Apply(InspectionProjector.FromAggregate(log, 1));
        log.Complete(new Odometer(10200), new FuelLevel(70));
        
        Assert.Equal("Draft", Assert.Single(projector.Query()).Status);
        
        projector.Apply(InspectionProjector.FromAggregate(log, 2));
        Assert.Equal("ApprovedForDrive", Assert.Single(projector.Query()).Status);
    }

    [Fact]
    public void Rebuild_RestoresActualState()
    {
        var log1 = CreateLog();
        var log2 = CreateLog();
        log2.Complete(new Odometer(15000), new FuelLevel(50));

        var projector = new InspectionProjector();
        projector.Apply(InspectionProjector.FromAggregate(log1, 1));

        var images = new[]
        {
            InspectionProjector.FromAggregate(log1, 1),
            InspectionProjector.FromAggregate(log2, 2)
        };

        projector.Rebuild(images);

        Assert.Equal(2, projector.Query().Count);
        Assert.Single(projector.Query("Draft"));
        Assert.Single(projector.Query("ApprovedForDrive"));
    }
}