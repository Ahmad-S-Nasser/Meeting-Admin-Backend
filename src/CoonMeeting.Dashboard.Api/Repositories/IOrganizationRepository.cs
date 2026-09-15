using CoonMeeting.Dashboard.Api.Models;

namespace CoonMeeting.Dashboard.Api.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(string id);
    Task UpdateAsync(Organization org);
}
