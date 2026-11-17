using System.Text.Json.Serialization;
using Oficina.Execucao.Api.Configuracao;
using Oficina.Execucao.Aplicacao;
using Oficina.Execucao.Infraestrutura;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AdicionarSwagger();
builder.Services.AdicionarAplicacao(builder.Configuration);
builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AdicionarJwtBearer(builder.Configuration);
builder.Services.AdicionarPoliticas();

var app = builder.Build();

// Atras do API Gateway/NLB o servico responde sob /execucao.
app.UsePathBase("/execucao");
app.UseRouting();
app.UseMiddleware<MiddlewareDeCorrelacao>();
app.UseSerilogRequestLogging();
app.UseMiddleware<MiddlewareDeExcecoes>();

// Swagger em todos os ambientes; endpoint relativo para funcionar sob o PathBase.
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("v1/swagger.json", "Oficina - Execucao Service v1"));

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
