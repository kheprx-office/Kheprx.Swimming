using Kheprx.BaseBackend.SharedKernel.Domain;

namespace Kheprx.BaseBackend.Identity.Domain.Exceptions;

public sealed class InvalidUserException : DomainException
{
    public InvalidUserException(string message) : base(message) { }
}
