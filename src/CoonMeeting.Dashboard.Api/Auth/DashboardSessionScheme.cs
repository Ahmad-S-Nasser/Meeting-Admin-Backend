namespace CoonMeeting.Dashboard.Api.Auth;

/// <summary>
/// A standard AddJwtBearer registration (see Program.cs) - just a shared name constant, no
/// custom handler needed. Entirely separate from Coon.Meeting's own ApiKey/ParticipantToken
/// schemes: this token only ever authenticates a request to the Dashboard's own backend.
/// </summary>
public static class DashboardSessionScheme
{
    public const string SchemeName = "DashboardSession";
}
