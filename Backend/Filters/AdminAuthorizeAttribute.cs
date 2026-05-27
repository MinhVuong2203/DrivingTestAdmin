using Backend.Service.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Backend.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class AdminAuthorizeAttribute : UserAuthorizeAttribute
    {
        public override async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var authService = context.HttpContext.RequestServices
                .GetRequiredService<IAdminAuthService>();

            var result = await authService.AuthorizeAsync(
                context.HttpContext,
                context.HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                return;
            }

            context.Result = new ObjectResult(new
            {
                message = result.Message
            })
            {
                StatusCode = result.StatusCode
            };
        }
    }
}
