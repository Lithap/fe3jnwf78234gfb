using Microsoft.EntityFrameworkCore;
using RetroRec_Server.Controllers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
    options.ListenAnyIP(2059);
});
var app = builder.Build();

static void AddColumnIfMissing(RetroRecDb db, string tableName, string addColumnSql)
{
    try
    {
        db.Database.ExecuteSqlRaw(addColumnSql);
    }
    catch (Exception ex) when (
        ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains($"no such table: {tableName}", StringComparison.OrdinalIgnoreCase))
    {
        // Existing installs may already have the column. Missing tables are
        // created below with the full current shape.
    }
}

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

    AddColumnIfMissing(db, "Accounts",
        @"ALTER TABLE ""Accounts"" ADD COLUMN ""Level"" INTEGER NOT NULL DEFAULT 1;");
    AddColumnIfMissing(db, "Accounts",
        @"ALTER TABLE ""Accounts"" ADD COLUMN ""XP"" INTEGER NOT NULL DEFAULT 0;");

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

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""UserRooms"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_UserRooms"" PRIMARY KEY AUTOINCREMENT,
            ""Name"" TEXT NOT NULL,
            ""Description"" TEXT NOT NULL,
            ""CreatorAccountId"" INTEGER NOT NULL,
            ""BaseRoomId"" INTEGER NOT NULL,
            ""UnitySceneId"" TEXT NOT NULL,
            ""ImageName"" TEXT NOT NULL,
            ""Accessibility"" INTEGER NOT NULL,
            ""IsPublished"" INTEGER NOT NULL,
            ""DataBlob"" TEXT NOT NULL,
            ""CreatedAt"" TEXT NOT NULL,
            ""ModifiedAt"" TEXT NOT NULL
        );");
    AddColumnIfMissing(db, "UserRooms",
        @"ALTER TABLE ""UserRooms"" ADD COLUMN ""Description"" TEXT NOT NULL DEFAULT '';");
    AddColumnIfMissing(db, "UserRooms",
        @"ALTER TABLE ""UserRooms"" ADD COLUMN ""BaseRoomId"" INTEGER NOT NULL DEFAULT 1;");
    AddColumnIfMissing(db, "UserRooms",
        $@"ALTER TABLE ""UserRooms"" ADD COLUMN ""UnitySceneId"" TEXT NOT NULL DEFAULT '{RRConstants.DormSceneId}';");
    AddColumnIfMissing(db, "UserRooms",
        @"ALTER TABLE ""UserRooms"" ADD COLUMN ""ImageName"" TEXT NOT NULL DEFAULT '';");
    AddColumnIfMissing(db, "UserRooms",
        @"ALTER TABLE ""UserRooms"" ADD COLUMN ""Accessibility"" INTEGER NOT NULL DEFAULT 2;");
    AddColumnIfMissing(db, "UserRooms",
        @"ALTER TABLE ""UserRooms"" ADD COLUMN ""IsPublished"" INTEGER NOT NULL DEFAULT 0;");
    AddColumnIfMissing(db, "UserRooms",
        @"ALTER TABLE ""UserRooms"" ADD COLUMN ""DataBlob"" TEXT NOT NULL DEFAULT '';");
    AddColumnIfMissing(db, "UserRooms",
        @"ALTER TABLE ""UserRooms"" ADD COLUMN ""CreatedAt"" TEXT NOT NULL DEFAULT '0001-01-01T00:00:00';");
    AddColumnIfMissing(db, "UserRooms",
        @"ALTER TABLE ""UserRooms"" ADD COLUMN ""ModifiedAt"" TEXT NOT NULL DEFAULT '0001-01-01T00:00:00';");
    db.Database.ExecuteSqlRaw(@"
        CREATE INDEX IF NOT EXISTS ""IX_UserRooms_CreatorAccountId"" ON ""UserRooms"" (""CreatorAccountId"");");
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
    await using var newBody = new MemoryStream();
    try
    {
        context.Response.Body = newBody;

        await next();

        newBody.Seek(0, SeekOrigin.Begin);
        using var responseReader = new StreamReader(newBody, leaveOpen: true);
        var responseText = await responseReader.ReadToEndAsync();
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
    }
    finally
    {
        context.Response.Body = originalBody;
    }
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
