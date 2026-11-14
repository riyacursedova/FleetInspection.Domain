using System.Threading.Tasks;
using FleetInspection.Domain.Models.Entities;
using FleetInspection.Domain.Models.ValueObject;

namespace FleetInspection.Application.Ports;
public sealed record LoadedInspection(VehicleInspectionLog Aggregate, long Revision);

public interface IInspectionRepository
{
    Task<LoadedInspection?> FindAsync(InspectionId id);
    void Stage(VehicleInspectionLog aggregate, long expectedRevision);
}