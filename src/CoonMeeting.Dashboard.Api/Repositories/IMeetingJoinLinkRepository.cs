using CoonMeeting.Dashboard.Api.Models;

namespace CoonMeeting.Dashboard.Api.Repositories;

public interface IMeetingJoinLinkRepository
{
    Task<MeetingJoinLink?> GetByTokenHashAsync(string tokenHash);

    /// <summary>The current non-revoked Scope=Any link for a meeting, if one exists.</summary>
    Task<MeetingJoinLink?> GetActiveAnyLinkAsync(string meetingId);

    Task InsertAsync(MeetingJoinLink link);
    Task UpdateAsync(MeetingJoinLink link);
}
