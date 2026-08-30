namespace Kheprx.BaseBackend.SharedKernel.Domain;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
