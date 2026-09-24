namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

/// <summary>Swimmers-per-stroke count for the dashboard stroke split.</summary>
public sealed record StrokeCountRow(Guid StrokeId, string Code, string NameEn, string? NameAr, int Count);
