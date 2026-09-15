using CoonMeeting.Dashboard.Api.Models;

namespace CoonMeeting.Dashboard.Api.Repositories;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly LiteDbContext _db;

    public OrganizationRepository(LiteDbContext db)
    {
        _db = db;
    }

    public Task<Organization?> GetByIdAsync(string id) =>
        Task.FromResult((Organization?)_db.Organizations.FindById(id));

    public Task UpdateAsync(Organization org)
    {
        _db.Organizations.Update(org);
        return Task.CompletedTask;
    }
}
