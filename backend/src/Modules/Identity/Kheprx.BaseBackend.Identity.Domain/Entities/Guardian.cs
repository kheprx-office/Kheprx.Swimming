namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Guardian
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public Guid RelationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NationalId { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;

    private Guardian() { } // EF Core

    public Guardian(Guid swimmerId, Guid relationId, string name, string nationalId, string phone)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        RelationId = relationId;
        Name = name.Trim();
        NationalId = nationalId.Trim();
        Phone = phone.Trim();
    }

    public void Update(string name, string nationalId, string phone)
    {
        Name = name.Trim();
        NationalId = nationalId.Trim();
        Phone = phone.Trim();
    }
}
