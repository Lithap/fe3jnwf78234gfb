using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using RetroRec_Server;
using RetroRec_Server.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
    // Accept forwarded headers from reverse proxies (ngrok, nginx, etc.).
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
    options.ListenAnyIP(2059);
});
var app = builder.Build();

app.UseForwardedHeaders();

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

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""PlayerCheers"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PlayerCheers"" PRIMARY KEY AUTOINCREMENT,
            ""FromAccountId"" INTEGER NOT NULL,
            ""TargetAccountId"" INTEGER NOT NULL,
            ""CheerCategory"" INTEGER NOT NULL,
            ""RoomId"" INTEGER NOT NULL,
            ""Anonymous"" INTEGER NOT NULL,
            ""CreatedAt"" TEXT NOT NULL
        );");
    db.Database.ExecuteSqlRaw(@"
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PlayerCheers_FromAccountId_TargetAccountId_CheerCategory""
            ON ""PlayerCheers"" (""FromAccountId"", ""TargetAccountId"", ""CheerCategory"");");
    db.Database.ExecuteSqlRaw(@"
        CREATE INDEX IF NOT EXISTS ""IX_PlayerCheers_TargetAccountId""
            ON ""PlayerCheers"" (""TargetAccountId"");");
}

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

app.Use(async (context, next) =>
{
    Console.WriteLine($"[REQUEST] {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");

    var originalBody = context.Response.Body;
    await using var newBody = new MemoryStream();
    context.Response.Body = newBody;

    try
    {
        await next();

        newBody.Seek(0, SeekOrigin.Begin);
        using var responseReader = new StreamReader(newBody, leaveOpen: true);
        var responseText = await responseReader.ReadToEndAsync();
        newBody.Seek(0, SeekOrigin.Begin);

        if (context.Response.StatusCode == 404)
        {
            // Make missing endpoints visually impossible to miss in the console.
            // Do not read the request body here: pre-reading breaks model binding,
            // SignalR negotiate, and triggers Kestrel BadHttpRequestException when
            // Content-Length is set but the client disconnects mid-body.
            Console.WriteLine("");
            Console.WriteLine("--------------------------------------------");
            Console.WriteLine($"!!! MISSING ENDPOINT: {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
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

app.MapControllers();
app.MapHub<RecNetHub>("/hub/v1");
app.Run();
