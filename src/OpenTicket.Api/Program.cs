using OpenTicket.Api;

var builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddApi(builder.Configuration);
}

var app = builder.Build();
{
    app.UseApi();

    if (app.Environment.IsDevelopment())
    {
        app.UseScalarUI();
        app.UseSwaggerUI();
        app.UseReDocUI();
    }

    app.Run();
}
