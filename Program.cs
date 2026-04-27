var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
    options.ListenAnyIP(2059);
});
var app = builder.Build();

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
