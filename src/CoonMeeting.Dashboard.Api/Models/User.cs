namespace CoonMeeting.Dashboard.Api.Models;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Always lowercased before storage/lookup.</summary>
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Null only in the instant between construction and being assigned an org - every persisted user has one (v1: exactly one).</summary>
    public string? OrganizationId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
