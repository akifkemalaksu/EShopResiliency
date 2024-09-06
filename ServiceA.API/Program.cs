using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using ServiceA.API.Services;
using System.Diagnostics;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<ProductService>(opt =>
{
    opt.BaseAddress = new Uri("https://localhost:7192/api/products/");
})
    .AddPolicyHandler(GetAdvanceCircuitBreakerPolicy());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("api/products/{id}", async (int id, ProductService productService) =>
{
    var result = await productService.GetProduct(id);

    return Results.Ok(result);
});

app.Run();

#region Retry Pattern
IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(message => message.StatusCode == HttpStatusCode.NotFound)
        .WaitAndRetryAsync(
            retryCount: 5,
            sleepDurationProvider: retryAttempt =>
            {
                Console.WriteLine($"Retry count: {retryAttempt}");
                return TimeSpan.FromSeconds(10);
            },
            onRetryAsync: OnRetryAsync
        );
}

Task OnRetryAsync(DelegateResult<HttpResponseMessage> arg1, TimeSpan arg2)
{
    Console.WriteLine($"Request is made again: {arg2.TotalSeconds}");

    return Task.CompletedTask;
}
#endregion

#region Circuit Breaker Pattern
IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromSeconds(10),
            onBreak: (DelegateResult<HttpResponseMessage> arg1, TimeSpan arg2) =>
            {
                Console.WriteLine("Circuit Breaker => On Break");
            },
            onHalfOpen: () =>
            {
                Console.WriteLine("Circuit Breaker => On Half Open");
            },
            onReset: () =>
            {
                Console.WriteLine("Circuit Breaker => On Reset");
            }
        );
}
#endregion

#region Advanced Circuit Breaker Pattern
IAsyncPolicy<HttpResponseMessage> GetAdvanceCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .AdvancedCircuitBreakerAsync(
            failureThreshold: 0.1,
            samplingDuration: TimeSpan.FromSeconds(30),
            minimumThroughput: 3,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (DelegateResult<HttpResponseMessage> arg1, TimeSpan arg2) =>
            {
                Console.WriteLine("Circuit Breaker => On Break");
            },
            onHalfOpen: () =>
            {
                Console.WriteLine("Circuit Breaker => On Half Open");
            },
            onReset: () =>
            {
                Console.WriteLine("Circuit Breaker => On Reset");
            }
        );
}
#endregion