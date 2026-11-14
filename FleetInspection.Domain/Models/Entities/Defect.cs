using FleetInspection.Domain.Models.ValueObject;
namespace FleetInspection.Domain.Models.Entities;

public sealed class Defect
{
    public DefectId Id { get; }
    public string Description { get; }
    public DefectSeverity Severity { get; }

    public Defect(DefectId id, string description, DefectSeverity severity)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Опис дефекту не може бути порожнім.");
        }

        Id = id;
        Description = description;
        Severity = severity;
    }
}