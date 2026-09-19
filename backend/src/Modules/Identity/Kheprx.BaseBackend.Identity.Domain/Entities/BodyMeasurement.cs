namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class BodyMeasurement
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public DateOnly MeasuredAt { get; private set; }
    public decimal RightArmCm { get; private set; }
    public decimal LeftArmCm { get; private set; }
    public decimal RightLegCm { get; private set; }
    public decimal LeftLegCm { get; private set; }
    public decimal TorsoCm { get; private set; }
    public decimal BustDiameterCm { get; private set; }
    public decimal WaistDiameterCm { get; private set; }

    private BodyMeasurement() { } // EF Core

    public BodyMeasurement(Guid swimmerId, decimal rightArmCm, decimal leftArmCm, decimal rightLegCm,
        decimal leftLegCm, decimal torsoCm, decimal bustDiameterCm, decimal waistDiameterCm)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        MeasuredAt = DateOnly.FromDateTime(DateTime.UtcNow);
        RightArmCm = rightArmCm;
        LeftArmCm = leftArmCm;
        RightLegCm = rightLegCm;
        LeftLegCm = leftLegCm;
        TorsoCm = torsoCm;
        BustDiameterCm = bustDiameterCm;
        WaistDiameterCm = waistDiameterCm;
    }
}
