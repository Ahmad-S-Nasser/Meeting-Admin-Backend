namespace CoonMeeting.Dashboard.Api.Models.Dtos;

public class OrgMemberDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public OrganizationRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
}
