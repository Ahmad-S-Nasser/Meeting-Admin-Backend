using CoonMeeting.Dashboard.Api.Config;
using CoonMeeting.Dashboard.Api.Models;
using LiteDB;

namespace CoonMeeting.Dashboard.Api.Repositories;

/// <summary>
/// Owns the single LiteDatabase instance for the process's lifetime, same shape as Coon.Meeting's
/// own LiteDbContext - one embedded file, safe for many concurrent operations within this one
/// process, not across several.
/// </summary>
public class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _db;

    public LiteDbContext(DatabaseSettings settings)
    {
        var dir = Path.GetDirectoryName(settings.FilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        BsonMapper.Global.EnumAsInteger = false;

        // Same UTC-forcing fix as Coon.Meeting's own LiteDbContext - LiteDB otherwise converts
        // DateTime back to the server's local time zone on read.
        BsonMapper.Global.RegisterType(
            serialize: (DateTime dt) => dt.ToUniversalTime(),
            deserialize: (bson) => bson.AsDateTime.ToUniversalTime());

        _db = new LiteDatabase(settings.FilePath);

        EnsureIndexes();
    }

    public ILiteCollection<User> Users => _db.GetCollection<User>("users");
    public ILiteCollection<Organization> Organizations => _db.GetCollection<Organization>("organizations");
    public ILiteCollection<PendingInvite> PendingInvites => _db.GetCollection<PendingInvite>("pendingInvites");
    public ILiteCollection<MeetingJoinLink> MeetingJoinLinks => _db.GetCollection<MeetingJoinLink>("meetingJoinLinks");
    public ILiteCollection<MeetingSettings> MeetingSettings => _db.GetCollection<MeetingSettings>("meetingSettings");

    private void EnsureIndexes()
    {
        Users.EnsureIndex(u => u.Email, unique: true);
        PendingInvites.EnsureIndex(i => i.TokenHash, unique: true);
        MeetingJoinLinks.EnsureIndex(l => l.TokenHash, unique: true);
        MeetingJoinLinks.EnsureIndex(l => l.MeetingId);
    }

    /// <summary>
    /// Creates a user and its organization together, atomically - a User with OrganizationId set
    /// but no matching Organization (or vice versa) would be broken, so this isn't two
    /// independent inserts.
    /// </summary>
    public void CreateUserAndOrganization(User user, Organization org)
    {
        _db.BeginTrans();
        try
        {
            Organizations.Insert(org);
            Users.Insert(user);
            _db.Commit();
        }
        catch
        {
            _db.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Creates a new user, adds them to the invite's organization, and consumes the invite -
    /// atomically, same reasoning as CreateUserAndOrganization: a partial write here would
    /// leave either a user with no org membership or a member entry with no backing user.
    /// </summary>
    public void AcceptInviteForNewUser(User user, Organization org, PendingInvite invite)
    {
        _db.BeginTrans();
        try
        {
            Users.Insert(user);
            Organizations.Update(org);
            PendingInvites.Delete(invite.Id);
            _db.Commit();
        }
        catch
        {
            _db.Rollback();
            throw;
        }
    }

    /// <summary>Links an already-existing user to the invite's organization and consumes the invite - same atomicity reasoning.</summary>
    public void AcceptInviteForExistingUser(User user, Organization org, PendingInvite invite)
    {
        _db.BeginTrans();
        try
        {
            Users.Update(user);
            Organizations.Update(org);
            PendingInvites.Delete(invite.Id);
            _db.Commit();
        }
        catch
        {
            _db.Rollback();
            throw;
        }
    }

    public void Dispose() => _db.Dispose();
}
