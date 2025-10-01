using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using APIUsuarios.Application;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace APIUsuarios.Controllers
{
    [ApiController]
    [Route("users")]
    public class UsersController : ControllerBase
    {
        private readonly IMediator _mediator;
        public UsersController(IMediator mediator) => _mediator = mediator;


        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "ADMIN")]
        public Task<PagedResult<UserVm>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? email = null)
        {
            return _mediator.Send(new ListUsersQuery(page, pageSize, email));
        }



        [HttpGet("{id:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<UserVm?> GetById(Guid id)
        { 
            return _mediator.Send(new GetUserByIdQuery(id, User.FindFirst("role")?.Value ?? "", User.FindFirst("sub")?.Value ?? ""));
        }


        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "ADMIN")]
        public Task<UserVm> Create(RegisterUserDto dto)
        { 
            return _mediator.Send(new RegisterUserCommand(dto));
        }


        [HttpPut("{id:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<UserVm> Update(Guid id, UpdateUserDto dto)
        { 
            return _mediator.Send(new UpdateUserCommand(id, dto, User.FindFirst("role")?.Value ?? "", User.FindFirst("sub")?.Value ?? ""));
        }


        [HttpPut("{id:guid}/password")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ChangePassword(Guid id, ChangePasswordDto dto)
        {
            await _mediator.Send(new ChangePasswordCommand(id, dto, User.FindFirst("role")?.Value ?? "", User.FindFirst("sub")?.Value ?? ""));
            return NoContent();
        }


        [HttpDelete("{id:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "ADMIN")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _mediator.Send(new DeleteUserCommand(id));
            return NoContent();
        }
    }
}
