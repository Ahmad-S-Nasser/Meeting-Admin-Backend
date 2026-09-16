using System.ComponentModel.DataAnnotations;

namespace CoonMeeting.Dashboard.Api.Models.Dtos;

public class AcceptNewInviteDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;
}
