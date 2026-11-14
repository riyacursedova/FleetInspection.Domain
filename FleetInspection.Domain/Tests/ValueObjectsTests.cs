using System;
using FleetInspection.Domain.Models.ValueObject;
using Xunit;

namespace FleetInspection.Tests;

public class ValueObjectsTests
{
    [Fact]
    public void Odometer_Valid_Success()
    {
        var odometer = new Odometer(150000);
        Assert.Equal(150000, odometer.Kilometers);
    }

    [Fact]
    public void Odometer_Negative_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Odometer(-10));
    }

    [Fact]
    public void Fuel_Valid_Success()
    {
        var fuel = new FuelLevel(75.5m);
        Assert.Equal(75.5m, fuel.Percentage);
    }

    [Fact]
    public void Fuel_Invalid_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FuelLevel(105m));
    }

    [Fact]
    public void Identifiers_Same_AreEqual()
    {
        var sameGuid = Guid.NewGuid();
        var id1 = new VehicleId(sameGuid);
        var id2 = new VehicleId(sameGuid);

        Assert.Equal(id1.Value, id2.Value);
    }
}