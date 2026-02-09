using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PutZige.Application.DTOs.Common;
using PutZige.Application.Common.Messages;
using PutZige.Application.Common.Constants; // Updated import for clarity
using System.Threading.Tasks;
using PutZige.API.Extensions;

namespace PutZige.API.Filters
{
    public class ValidationFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState.ToErrorsDictionary();

                context.Result = new BadRequestObjectResult(ApiResponse<object>.Error(ResponseCodes.VALIDATION_FAILED, ErrorMessages.Validation.ValidationFailed, errors, StatusCodes.Status400BadRequest));
                return;
            }

            await next();
        }
    }
}
