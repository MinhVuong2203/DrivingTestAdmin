using Backend.Service.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Backend.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class UserAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public virtual async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var authService = context.HttpContext.RequestServices
                .GetRequiredService<IUserAuthService>();

            var result = await authService.AuthenticateAsync(
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
