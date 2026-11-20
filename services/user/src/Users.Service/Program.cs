using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Users.Service.Contracts;
using Users.Service.Domain;
using Users.Service.Kafka;
using Users.Service.Options;

var builder = WebApplication.CreateBuilder(args);

// локальные правки (адрес брокера дома) лежат в appsettings.Local.json, он в .gitignore
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));

// тестовые пользователи с фиксированными id, чтобы заказы можно было создавать сразу
// состояние в файле: при первом запуске добавляем тестовых, дальше берём из файла
var store = new UserStore(builder.Configuration["Persistence:Path"]);
if (!store.HasData)
    foreach (var u in builder.Configuration.GetSection("Seed:Users").Get<List<SeedUser>>() ?? [])
        store.Register(u.Name, u.Email, u.Id);

builder.Services.AddSingleton(store);
builder.Services.AddSingleton<TopicReadiness>();
builder.Services.AddHostedService<TopicBootstrapService>();
builder.Services.AddSingleton<EventPublisher>();
builder.Services.AddHostedService<OrderCreatedConsumer>();

var app = builder.Build();

// брокер недоступен: отвечаем 503 вместо 500, чтобы причина была понятна
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var err = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    ctx.Response.StatusCode = err is TimeoutException or Confluent.Kafka.KafkaException ? 503 : 500;
    await ctx.Response.WriteAsJsonAsync(new { error = err is TimeoutException ? "kafka is not available, topics are not ready" : err?.Message });
}));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// регистрация пользователя: публикует user.registered
app.MapPost("/users", async (RegisterRequest req, UserStore s, EventPublisher pub, CancellationToken ct) =>
{
    try
    {
        var user = s.Register(req.Name, req.Email);
        await pub.PublishAsync(Topics.UserRegistered, user.Id.ToString(),
            new UserRegistered(Guid.NewGuid(), user.Id, user.Email, user.Name, user.RegisteredAt), ct);
        return Results.Created($"/users/{user.Id}", user);
    }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
    catch (InvalidOperationException e) { return Results.Conflict(new { error = e.Message }); }
});

// просмотр
app.MapGet("/users", (UserStore s) => Results.Ok(s.List()));

app.MapGet("/users/{id:guid}", (Guid id, UserStore s) =>
    s.Get(id) is { } u ? Results.Ok(u) : Results.NotFound(new { error = $"user {id} not found" }));

// изменение данных
app.MapPut("/users/{id:guid}", (Guid id, RegisterRequest req, UserStore s) =>
{
    try { return s.Update(id, req.Name, req.Email) is { } u ? Results.Ok(u) : Results.NotFound(new { error = $"user {id} not found" }); }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
    catch (InvalidOperationException e) { return Results.Conflict(new { error = e.Message }); }
});

app.Run();

record RegisterRequest(string Name, string Email);
record SeedUser(Guid Id, string Name, string Email);
