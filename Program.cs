using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
    options.ListenAnyIP(2059);
});
var app = builder.Build();

// Ensure the SQLite schema exists on startup.
//
// EnsureCreated() only creates the schema if the database file is missing —
// for servers with an existing retrorec.db (which is everyone who's been
// running RetroRec for more than five minutes), it sees the file already
// exists and SKIPS bootstrapping entirely, so any newly-added tables like
// Bios / FriendRelationships never get created. The friend-request flow
// then crashes with `SqliteException: no such table: FriendRelationships`.
//
// Workaround: call EnsureCreated() to handle the brand-new-DB case, then
// follow up with CREATE TABLE IF NOT EXISTS for every new table we add
// after the initial schema. This is idempotent and works for both fresh
// installs and existing DBs without needing EF migrations infrastructure.
using (var scope = app.Services.CreateScope())
{
    using var db = new RetroRecDb();
    db.Database.EnsureCreated();

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Bios"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Bios"" PRIMARY KEY AUTOINCREMENT,
            ""AccountId"" INTEGER NOT NULL,
            ""Bio"" TEXT NOT NULL,
            ""UpdatedAt"" TEXT NOT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE INDEX IF NOT EXISTS ""IX_Bios_AccountId"" ON ""Bios"" (""AccountId"");");

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""FriendRelationships"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_FriendRelationships"" PRIMARY KEY AUTOINCREMENT,
            ""SenderId"" INTEGER NOT NULL,
            ""TargetId"" INTEGER NOT NULL,
            ""CreatedAt"" TEXT NOT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE INDEX IF NOT EXISTS ""IX_FriendRelationships_SenderId_TargetId""
            ON ""FriendRelationships"" (""SenderId"", ""TargetId"");");

    // Patch existing tables for columns added after the initial deploy.
    //
    // Why this is needed: existing servers were created with older versions
    // of the Account / UserRoom schemas. EF doesn't auto-migrate column
    // additions, so when a controller queries (for example) UserRoom.IsPublished
    // and the DB doesn't have that column, SQLite throws "no such column",
    // the global exception handler swallows it and returns "200 OK {}", and
    // the client renders an empty list — i.e. "no rooms anywhere".
    //
    // Each AddColumnIfMissing is idempotent: it inspects pragma table_info
    // first and only ALTERs when the column is actually absent, so it's
    // safe to run on every boot regardless of which version of the DB the
    // server was originally provisioned from.
    AddColumnIfMissing(db, "Accounts", "Level",        "INTEGER NOT NULL DEFAULT 1");
    AddColumnIfMissing(db, "Accounts", "XP",           "INTEGER NOT NULL DEFAULT 0");

    AddColumnIfMissing(db, "UserRooms", "Description",       "TEXT NOT NULL DEFAULT ''");
    AddColumnIfMissing(db, "UserRooms", "BaseRoomId",        "INTEGER NOT NULL DEFAULT 0");
    AddColumnIfMissing(db, "UserRooms", "UnitySceneId",      "TEXT NOT NULL DEFAULT ''");
    AddColumnIfMissing(db, "UserRooms", "ImageName",         "TEXT NOT NULL DEFAULT ''");
    AddColumnIfMissing(db, "UserRooms", "Accessibility",     "INTEGER NOT NULL DEFAULT 2");
    AddColumnIfMissing(db, "UserRooms", "IsPublished",       "INTEGER NOT NULL DEFAULT 0");
    AddColumnIfMissing(db, "UserRooms", "DataBlob",          "TEXT NOT NULL DEFAULT ''");
    // ModifiedAt has no NOT NULL default that makes sense for SQLite ALTER —
    // it permits any value but must be present on the row. Existing rows
    // get an empty string, which EF reads as DateTime.MinValue. Saving a
    // room subsequently overwrites it with DateTime.UtcNow.
    AddColumnIfMissing(db, "UserRooms", "ModifiedAt",        "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00.0000000'");

    static void AddColumnIfMissing(RetroRecDb db, string table, string column, string definition)
    {
        try
        {
            using var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) conn.Open();

            bool exists = false;
            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = $"PRAGMA table_info(\"{table}\");";
                using var reader = pragma.ExecuteReader();
                // PRAGMA table_info returns rows shaped:
                //   cid | name | type | notnull | dflt_value | pk
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), column, StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (exists) return;

            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};";
            alter.ExecuteNonQuery();
            Console.WriteLine($"[migration] added {table}.{column}");
        }
        catch (Exception ex)
        {
            // Most likely the table doesn't exist yet (fresh install). That's
            // fine — EnsureCreated already built the new shape, no patching
            // needed. Log so we can spot real problems but don't fail boot.
            Console.WriteLine($"[migration] {table}.{column} skipped: {ex.Message}");
        }
    }
}

app.Use(async (context, next) =>
{
    Console.WriteLine($"[REQUEST] {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");

    // Buffer the request body so we can log it if this turns out to be a 404.
    // EnableBuffering must be called BEFORE we read, so controllers can re-read it afterwards.
    context.Request.EnableBuffering();
    string requestBody = "";
    if (context.Request.ContentLength > 0)
    {
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        requestBody = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
    }

    var originalBody = context.Response.Body;
    using var newBody = new MemoryStream();
    context.Response.Body = newBody;

    await next();

    newBody.Seek(0, SeekOrigin.Begin);
    var responseText = await new StreamReader(newBody).ReadToEndAsync();
    newBody.Seek(0, SeekOrigin.Begin);

    if (context.Response.StatusCode == 404)
    {
        // Make missing endpoints visually impossible to miss in the console.
        Console.WriteLine("");
        Console.WriteLine("--------------------------------------------");
        Console.WriteLine($"!!! MISSING ENDPOINT: {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
        if (!string.IsNullOrWhiteSpace(requestBody))
            Console.WriteLine($"Request Body: {requestBody}");
        Console.WriteLine("--------------------------------------------");
        Console.WriteLine("");
    }
    else
    {
        Console.WriteLine($"API Response: {context.Request.Method} {context.Request.Path} - {context.Response.StatusCode}");
        if (!string.IsNullOrEmpty(responseText) && responseText.Length < 500)
            Console.WriteLine($"Body: {responseText}");
        else if (responseText.Length >= 500)
            Console.WriteLine($"Body: [{responseText.Length} chars, truncated]");
    }

    await newBody.CopyToAsync(originalBody);
});

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (error != null)
            Console.WriteLine($"[CRASH] {error.Error.GetType().Name}: {error.Error.Message}\n{error.Error.StackTrace}");
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("{}");
    });
});

app.MapControllers();
app.MapHub<RecNetHub>("/hub/v1");
app.Run();
