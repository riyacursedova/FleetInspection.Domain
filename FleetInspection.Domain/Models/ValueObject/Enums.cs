namespace FleetInspection.Domain.Models.ValueObject;

public enum DefectSeverity
{
    Minor,
    Critical
}

public enum InspectionStatus
{
    Draft,
    ApprovedForDrive,
    FailedBlockOutbound
}