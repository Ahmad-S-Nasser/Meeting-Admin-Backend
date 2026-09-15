using CoonMeeting.Dashboard.Api.Models;

namespace CoonMeeting.Dashboard.Api.Repositories;

public class UserRepository : IUserRepository
{
    private readonly LiteDbContext _db;

    public UserRepository(LiteDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByIdAsync(string id) =>
        Task.FromResult((User?)_db.Users.FindById(id));

    public Task<User?> GetByEmailAsync(string email) =>
        Task.FromResult((User?)_db.Users.FindOne(u => u.Email == email));

    public Task UpdateAsync(User user)
    {
        _db.Users.Update(user);
        return Task.CompletedTask;
    }
}
