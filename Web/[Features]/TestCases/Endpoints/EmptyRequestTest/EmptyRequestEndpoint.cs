using Microsoft.AspNetCore.Authorization;
using FastEndpoints;
using Web.Auth;


namespace TestCases.EmptyRequestTest;

[
    HttpGet("/test-cases/empty-request-test"),
    Authorize(Roles = Role.Admin)
]
public class EmptyRequestEndpoint : Endpoint<EmptyRequest, EmptyResponse>
{
    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
        => await HttpContext.Response.SendAsync(200, cancellation: ct);
}