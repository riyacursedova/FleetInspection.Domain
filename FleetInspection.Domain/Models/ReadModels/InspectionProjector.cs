using System;
using System.Collections.Generic;
using System.IO;
using FleetInspection.Domain.Models.Entities;

namespace FleetInspection.Application.ReadModels;

public sealed record InspectionReadModel(
    Guid InspectionId,
    long Revision,
    string Status,
    Guid VehicleId,
    int StartOdometerKm,
    int? EndOdometerKm,
    decimal? EndFuelPercent);

public sealed class InspectionProjector
{
    private Dictionary<Guid, InspectionReadModel> _rows = new();

    public static InspectionReadModel FromAggregate(VehicleInspectionLog log, long revision)
    {
        return new InspectionReadModel(
            log.Id.Value,
            revision,
            log.Status.ToString(),
            log.VehicleId.Value,
            log.StartOdometer.Kilometers,
            log.EndOdometer?.Kilometers,
            log.EndFuelLevel?.Percentage);
    }

    public void Apply(InspectionReadModel image)
    {
        if (image.Revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(image));
        }

        if (_rows.TryGetValue(image.InspectionId, out var old))
        {
            if (image.Revision < old.Revision)
            {
                return;
            }

            if (image.Revision == old.Revision)
            {
                if (image != old)
                {
                    throw new InvalidDataException("revision_conflict");
                }
                return;
            }
        }

        _rows[image.InspectionId] = image;
    }

    public IReadOnlyList<InspectionReadModel> Query(string? status = null)
    {
        var result = new List<InspectionReadModel>();

        foreach (var row in _rows.Values)
        {
            if (status == null || row.Status == status)
            {
                result.Add(row);
            }
        }

        result.Sort((a, b) => a.InspectionId.CompareTo(b.InspectionId));
        return result;
    }

    public void Rebuild(IEnumerable<InspectionReadModel> currentImages)
    {
        var next = new InspectionProjector();

        foreach (var image in currentImages)
        {
            next.Apply(image);
        }

        _rows = next._rows;
    }
}