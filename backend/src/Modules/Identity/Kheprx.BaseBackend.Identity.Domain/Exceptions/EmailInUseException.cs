using Kheprx.BaseBackend.SharedKernel.Domain;

namespace Kheprx.BaseBackend.Identity.Domain.Exceptions;

public sealed class EmailInUseException : DomainException
{
    public EmailInUseException(string message) : base(message) { }
}
