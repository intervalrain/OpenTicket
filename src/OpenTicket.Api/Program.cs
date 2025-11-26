using OpenTicket.Api;

var builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddApi(builder.Configuration);
}

var app = builder.Build();
{
    app.UseApi();
    app.Run();
}
