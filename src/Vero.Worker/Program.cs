using Vero.Application;
using Vero.Infrastructure;
using Vero.Worker;

// Composition Root do Worker: mesmas camadas da Api, montadas pelos mesmos
// métodos Add*. Só registra a análise assíncrona: não tem ML, SignalR nem controllers.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddVeroInfrastructure(
    builder.Configuration.GetConnectionString("VeroDb"),
    builder.Configuration["Encryption:Key"]);
builder.Services.AddVeroAnaliseAssincrona();

builder.Services.AddHostedService<AsyncWorker>();

var host = builder.Build();
host.Run();
