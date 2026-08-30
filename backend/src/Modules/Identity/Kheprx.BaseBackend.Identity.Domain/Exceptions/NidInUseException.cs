using Kheprx.BaseBackend.SharedKernel.Domain;

namespace Kheprx.BaseBackend.Identity.Domain.Exceptions;

public sealed class NidInUseException : DomainException
{
    public NidInUseException(string message) : base(message) { }
}
