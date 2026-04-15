using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SnakeFrogCalendarBot.Worker.Config;

namespace SnakeFrogCalendarBot.Worker.Api;

public static class DeployEndpoints
{
    public static async Task<IResult> Handle(
        HttpRequest request,
        [FromServices] AppOptions options,
        [FromServices] MiniAppTokenService tokenService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var userId = TelegramInitDataValidator.Validate(request, options, tokenService, logger);
        if (userId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(options.GitHubDeployToken) || string.IsNullOrWhiteSpace(options.GitHubRepo))
            return Results.Problem("GitHub deploy is not configured", statusCode: 503);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.GitHubDeployToken);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SnakeFrogBot", "1.0"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var url = $"https://api.github.com/repos/{options.GitHubRepo}/actions/workflows/mini-app-deploy.yml/dispatches";
        var payload = JsonSerializer.Serialize(new { @ref = "main" });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await http.PostAsync(url, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("GitHub Actions dispatch failed: {Status} {Error}", response.StatusCode, error);
            return Results.Problem($"GitHub returned {(int)response.StatusCode}", statusCode: 502);
        }

        return Results.Accepted();
    }
}
