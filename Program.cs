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
