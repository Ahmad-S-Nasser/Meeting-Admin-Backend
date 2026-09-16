using System.ComponentModel.DataAnnotations;

namespace CoonMeeting.Dashboard.Api.Models.Dtos;

public class CreateInviteDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
