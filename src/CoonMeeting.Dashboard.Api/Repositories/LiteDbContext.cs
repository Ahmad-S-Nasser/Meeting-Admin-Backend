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

    private void EnsureIndexes()
    {
        Users.EnsureIndex(u => u.Email, unique: true);
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

    public void Dispose() => _db.Dispose();
}
