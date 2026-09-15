namespace CoonMeeting.Dashboard.Api.Models.Dtos;

public class MeResponseDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public OrganizationRole Role { get; set; }
}
