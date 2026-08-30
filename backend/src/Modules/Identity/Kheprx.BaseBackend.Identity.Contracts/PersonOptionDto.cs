namespace Kheprx.BaseBackend.Identity.Contracts;

// Minimal person option (id = users.Id + display name) for the create-project
// contractor/worker pickers. Id is the UserId — matches the projects join tables
// (project_moqaweleen.MoqawelId / project_workers.WorkerId → users.Id).
public sealed record PersonOptionDto(Guid Id, string FullName);
