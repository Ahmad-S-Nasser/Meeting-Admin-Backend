using CoonMeeting.Dashboard.Api.Models;

namespace CoonMeeting.Dashboard.Api.Services;

/// <summary>Resolves one PermissionPolicy for one caller. Pure, so every place that hands a
/// permission to a client (call-token, guest token, my-permissions) applies the identical rule.</summary>
public static class CallPermissionEvaluator
{
    public static bool Evaluate(PermissionPolicy? policy, string organizerExternalId, string? callerExternalId, bool isGuest)
    {
        if (!isGuest && callerExternalId != null &&
            string.Equals(callerExternalId, organizerExternalId, StringComparison.Ordinal))
            return true;

        return (policy?.Mode ?? PermissionMode.OrganizerOnly) switch
        {
            PermissionMode.Everyone => true,
            // A guest's id is random per visit, so it can never be individually selected.
            PermissionMode.Selected => !isGuest && callerExternalId != null &&
                                       (policy!.AllowedParticipantIds?.Contains(callerExternalId, StringComparer.Ordinal) ?? false),
            _ => false,
        };
    }
}
