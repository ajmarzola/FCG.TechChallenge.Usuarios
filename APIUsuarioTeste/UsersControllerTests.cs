using Xunit;
using Moq;
using MediatR;
using APIUsuarios.Controllers;
using APIUsuarios.Application;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System;
using System.Threading;

public class UsersControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new UsersController(_mediatorMock.Object);

        // Simula o contexto do usuário autenticado
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("role", "ADMIN"),
            new Claim("sub", Guid.NewGuid().ToString())
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    //[Fact]
    //public async Task List_ReturnsPagedResult()
    //{
    //    var expected = new PagedResult<UserVm>();
    //    _mediatorMock.Setup(m => m.Send(It.IsAny<ListUsersQuery>(), It.IsAny<CancellationToken>()))
    //        .ReturnsAsync(expected);

    //    var result = await _controller.List();

    //    Assert.Equal(expected, result);
    //    _mediatorMock.Verify(m => m.Send(It.IsAny<ListUsersQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    //}

//    [Fact]
//    public async Task GetById_ReturnsUserVm()
//    {
//        var userId = Guid.NewGuid();
//        var expected = new UserVm();
//        _mediatorMock.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(expected);

//        var result = await _controller.GetById(userId);

//        Assert.Equal(expected, result);
//        _mediatorMock.Verify(m => m.Send(It.Is<GetUserByIdQuery>(q => q.Id == userId), It.IsAny<CancellationToken>()), Times.Once);
//    }

//    [Fact]
//    public async Task Create_ReturnsUserVm()
//    {
//        var dto = new RegisterUserDto();
//        var expected = new UserVm();
//        _mediatorMock.Setup(m => m.Send(It.IsAny<RegisterUserCommand>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(expected);

//        var result = await _controller.Create(dto);

//        Assert.Equal(expected, result);
//        _mediatorMock.Verify(m => m.Send(It.Is<RegisterUserCommand>(c => c.Dto == dto), It.IsAny<CancellationToken>()), Times.Once);
//    }

//    [Fact]
//    public async Task Update_ReturnsUserVm()
//    {
//        var userId = Guid.NewGuid();
//        var dto = new UpdateUserDto("rafaelnicoletti@hotmail.com", APIUsuarios.Domain.Role.ADMIN);
//        var expected = new UserVm();
//        _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateUserCommand>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(expected);

//        var result = await _controller.Update(userId, dto);

//        Assert.Equal(expected, result);
//        _mediatorMock.Verify(m => m.Send(It.Is<UpdateUserCommand>(c => c.Id == userId && c.Dto == dto), It.IsAny<CancellationToken>()), Times.Once);
//    }

//    [Fact]
//    public async Task ChangePassword_ReturnsNoContent()
//    {
//        var userId = Guid.NewGuid();
//        var dto = new ChangePasswordDto("","");
//        _mediatorMock.Setup(m => m.Send(It.IsAny<ChangePasswordCommand>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(Unit.Value);

//        var result = await _controller.ChangePassword(userId, dto);

//        Assert.IsType<NoContentResult>(result);
//        _mediatorMock.Verify(m => m.Send(It.Is<ChangePasswordCommand>(c => c.Id == userId && c.Dto == dto), It.IsAny<CancellationToken>()), Times.Once);
//    }

//    [Fact]
//    public async Task Delete_ReturnsNoContent()
//    {
//        var userId = Guid.NewGuid();
//        _mediatorMock.Setup(m => m.Send(It.IsAny<DeleteUserCommand>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(Unit.Value);

//        var result = await _controller.Delete(userId);

//        Assert.IsType<NoContentResult>(result);
//        _mediatorMock.Verify(m => m.Send(It.Is<DeleteUserCommand>(c => c.Id == userId), It.IsAny<CancellationToken>()), Times.Once);
//    }
}
