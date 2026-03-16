using Eventask.Domain.Entity.Users;
using Microsoft.EntityFrameworkCore;

namespace Eventask.ApiService.Repository;

public class UserRepository(EventaskContext db) : IUserRepository
{
    /// <summary>
    /// Adds a user to the tracking context. Changes will be persisted when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    public Task<User> CreateAsync(User user)
    {
        db.Users.Add(user);
        return Task.FromResult(user);
    }

    /// <summary>
    /// Marks a user as modified in the tracking context. Changes will be persisted when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    public Task<User> UpdateAsync(User user)
    {
        db.Users.Update(user);
        return Task.FromResult(user);
    }

    public async Task<User?> GetByIdAsync(Guid userId)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.UserName == username && !u.IsDeleted);
    }

    public Task<bool> ExistsByUsernameAsync(string username)
    {
        return db.Users.AnyAsync(u => u.UserName == username && !u.IsDeleted);
    }
}