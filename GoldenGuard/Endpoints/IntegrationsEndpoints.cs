using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GoldenGuard.Endpoints;

public static class IntegrationsEndpoints
{
    public static IEndpointRouteBuilder MapIntegrations(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations").WithTags("Integrations");

        // ==========/rates/convert?from=USD&to=BRL&amount=100 ==========
        group.MapGet("/rates/convert", async Task<IResult> (HttpClient http, string from, string to, decimal amount) =>
        {
            try
            {
                // exchangerate.host não requer chave
                var url = $"https://api.exchangerate.host/convert?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}&amount={amount}";
                var resp = await http.GetFromJsonAsync<ExConvertResp>(url);

                if (resp is null || !resp.success)
                    return Results.BadRequest("Falha ao consultar taxa.");

                object payload = new
                {
                    from = resp.query.from,
                    to = resp.query.to,
                    amount = resp.query.amount,
                    rate = resp.info.rate,
                    result = resp.result,
                    date = resp.date
                };
                return Results.Ok(payload);
            }
            catch (Exception ex)
            {
                return Results.StatusCode(502); // Bad Gateway para erro de API externa
            }
        });

        // ==========/news?query=apostas&pageSize=5 ==========
        group.MapGet("/news", async Task<IResult> (HttpClient http, IConfiguration cfg, string? query, int pageSize = 5) =>
        {
            try
            {
                var q = string.IsNullOrWhiteSpace(query) ? "apostas" : query.Trim();
                var key = cfg["NewsApi:ApiKey"];
                if (string.IsNullOrWhiteSpace(key))
                    return Results.Ok(Array.Empty<object>());

                var url =
                    $"https://newsapi.org/v2/everything?q={Uri.EscapeDataString(q)}&language=pt&sortBy=publishedAt&pageSize={pageSize}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("X-Api-Key", key);

                using var res = await http.SendAsync(req);
                if (!res.IsSuccessStatusCode)
                    return Results.Ok(Array.Empty<object>());

                var data = await res.Content.ReadFromJsonAsync<NewsApiResp>();
                var items = (data?.articles ?? new()).Select(a => new
                {
                    title = a.title,
                    source = a.source?.name,
                    publishedAt = a.publishedAt,
                    url = a.url
                });

                return Results.Ok(items);
            }
            catch
            {
                return Results.Ok(Array.Empty<object>());
            }
        });

        return app;
    }

    // ===== DTOs p/ desserializar as APIs externas =====
    private sealed record ExConvertResp(bool success, ExQuery query, ExInfo info, decimal result, string date);
    private sealed record ExQuery(string from, string to, decimal amount);
    private sealed record ExInfo(decimal rate);

    private sealed record NewsApiResp(List<NewsArticle> articles);
    private sealed record NewsArticle(string? title, NewsSource? source, DateTime? publishedAt, string? url);
    private sealed record NewsSource(string? name);
}