using ollama;
using OllamaChat.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOllamaChatInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapChatEndpoints();

app.Run();
