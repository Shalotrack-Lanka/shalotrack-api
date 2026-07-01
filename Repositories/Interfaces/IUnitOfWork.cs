namespace ShaloTrack_API.Repositories.Interfaces;

public interface IUnitOfWork
{
    ICustomerRepository Customers { get; }
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}