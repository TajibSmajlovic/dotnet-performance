namespace CarvedRock.Api;
public class UserScopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserScopeMiddleware> _logger;

    public UserScopeMiddleware(RequestDelegate next, ILogger<UserScopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity is { IsAuthenticated: true })
        {
            var user = context.User;

            var subjectId = user.Claims.First(c=> c.Type == "sub")?.Value;
                        
            using (_logger.BeginScope("User:{user}, SubjectId:{subject}", user.Identity.Name, subjectId))
            {
                await _next(context);    
            }
        }
        else
        {
            await _next(context);
        }
    }
}