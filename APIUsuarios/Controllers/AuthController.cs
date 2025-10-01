using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using APIUsuarios.Application;

namespace APIUsuarios.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AuthController(IMediator mediator) => _mediator = mediator;


        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterUserDto dto)
        {
            var vm = await _mediator.Send(new RegisterUserCommand(dto));
            return Created($"/users/{vm.Id}", vm);
        }


        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var token = await _mediator.Send(new LoginQuery(dto));
            return Ok(new { token });
        }
    }
}
