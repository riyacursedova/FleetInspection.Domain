namespace FleetInspection.Domain.Models.ValueObject;

// Ідентифікатор акта огляду
public sealed class InspectionId
{
    public Guid Value { get; }

    public InspectionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Ідентифікатор огляду не може бути порожнім.");
        }
        Value = value;
    }

    public static InspectionId New()
    {
        return new InspectionId(Guid.NewGuid());
    }
}

// Ідентифікатор автомобіля
public sealed class VehicleId
{
    public Guid Value { get; }

    public VehicleId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Ідентифікатор транспорту не може бути порожнім.");
        }
        Value = value;
    }
}

// Ідентифікатор механіка
public sealed class InspectorId
{
    public Guid Value { get; }

    public InspectorId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Ідентифікатор механіка не може бути порожнім.");
        }
        Value = value;
    }
}

// Ідентифікатор дефекту
public sealed class DefectId
{
    public Guid Value { get; }

    public DefectId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Ідентифікатор дефекту не може бути порожнім.");
        }
        Value = value;
    }

    public static DefectId New()
    {
        return new DefectId(Guid.NewGuid());
    }
}