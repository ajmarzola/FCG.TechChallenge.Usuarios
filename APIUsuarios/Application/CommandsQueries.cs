using MediatR;
using Microsoft.EntityFrameworkCore;
using APIUsuarios.Domain;
using APIUsuarios.Infrastructure.Data;
using APIUsuarios.Infrastructure.Services;

namespace APIUsuarios.Application
{
    public record RegisterUserCommand(RegisterUserDto Dto) : IRequest<UserVm>;
    public record UpdateUserCommand(Guid Id, UpdateUserDto Dto, string CurrentRole, string CurrentSub) : IRequest<UserVm>;
    public record ChangePasswordCommand(Guid Id, ChangePasswordDto Dto, string CurrentRole, string CurrentSub) : IRequest;
    public record DeleteUserCommand(Guid Id) : IRequest;
    public record LoginQuery(LoginDto Dto) : IRequest<string>;
    public record GetUserByIdQuery(Guid Id, string CurrentRole, string CurrentSub) : IRequest<UserVm?>;
    public record ListUsersQuery(int Page = 1, int PageSize = 20, string? Email = null) : IRequest<PagedResult<UserVm>>;


    public class RegisterUserHandler : IRequestHandler<RegisterUserCommand, UserVm>
    {
        private readonly UsersDbContext _db; private readonly PasswordService _pwd;
        public RegisterUserHandler(UsersDbContext db, PasswordService pwd) { _db = db; _pwd = pwd; }

        public async Task<UserVm> Handle(RegisterUserCommand r, CancellationToken ct)
        {
            var email = (r.Dto.Email ?? "").Trim().ToLowerInvariant();

            // índice único é recomendado no DbContext/Fluent API, mas cheque aqui também
            if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            {
                throw new("E-mail já cadastrado");
            }

            var now = DateTime.UtcNow;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Nome = r.Dto.Nome,
                Role = r.Dto.Role ?? Role.ALUNO,
                PasswordHash = string.Empty,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            user.PasswordHash = _pwd.Hash(user, r.Dto.Senha);

            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            return new(user.Id, user.Email, user.Nome, user.Role, user.CreatedAtUtc, user.UpdatedAtUtc);
        }
    }



    public class UpdateUserHandler : IRequestHandler<UpdateUserCommand, UserVm>
    {
        private readonly UsersDbContext _db; public UpdateUserHandler(UsersDbContext db) => _db = db;

        public async Task<UserVm> Handle(UpdateUserCommand c, CancellationToken ct)
        {
            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == c.Id, ct)
                    ?? throw new("Usuário não encontrado");

            var isAdmin = c.CurrentRole == Role.ADMIN.ToString();
            var isSelf = c.CurrentSub == u.Id.ToString();

            if (!isAdmin && !isSelf)
            {
                throw new UnauthorizedAccessException(); // veja observação abaixo sobre mapear para 403
            }

            u.Nome = c.Dto.Nome;

            // ADMIN pode alterar role, mas só se o DTO enviou um valor
            if (isAdmin && c.Dto.Role.HasValue)
            {
                u.Role = c.Dto.Role.Value;
            }

            u.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return new(u.Id, u.Email, u.Nome, u.Role, u.CreatedAtUtc, u.UpdatedAtUtc);
        }
    }



    public class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand>
    {
        private readonly UsersDbContext _db; private readonly PasswordService _pwd;
        public ChangePasswordHandler(UsersDbContext db, PasswordService pwd) { _db = db; _pwd = pwd; }

        public async Task<Unit> Handle(ChangePasswordCommand c, CancellationToken ct)
        {
            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == c.Id, ct)
                    ?? throw new("Usuário não encontrado");

            var isAdmin = c.CurrentRole == Role.ADMIN.ToString();
            var isSelf = c.CurrentSub == u.Id.ToString();

            if (!isAdmin && !isSelf)
            {
                throw new UnauthorizedAccessException();
            }

            if (!isAdmin)
            {
                if (!_pwd.Verify(u, c.Dto.SenhaAtual))
                {
                    throw new("Senha atual incorreta");
                }
            }

            u.PasswordHash = _pwd.Hash(u, c.Dto.NovaSenha);
            u.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return Unit.Value;
        }
    }



    public class DeleteUserHandler : IRequestHandler<DeleteUserCommand>
    {
        private readonly UsersDbContext _db; public DeleteUserHandler(UsersDbContext db) => _db = db;

        public async Task<Unit> Handle(DeleteUserCommand c, CancellationToken ct)
        {
            // para versões mais antigas do C#, use new object[] { c.Id }
            var u = await _db.Users.FindAsync(new object[] { c.Id }, ct);
            if (u is null)
            {
                throw new("Usuário não encontrado");
            }

            _db.Users.Remove(u);
            await _db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }



    public class LoginHandler : IRequestHandler<LoginQuery, string>
    {
        private readonly UsersDbContext _db; private readonly PasswordService _pwd; private readonly JwtTokenService _jwt;
        public LoginHandler(UsersDbContext db, PasswordService pwd, JwtTokenService jwt) { _db = db; _pwd = pwd; _jwt = jwt; }

        public async Task<string> Handle(LoginQuery q, CancellationToken ct)
        {
            var email = (q.Dto.Email ?? "").Trim().ToLowerInvariant();

            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email, ct);
            if (u is null || !_pwd.Verify(u, q.Dto.Senha))
            {
                throw new("Usuário ou senha inválidos");
            }

            return _jwt.Create(u); // 30min padrão no seu JwtTokenService
        }
    }



    public class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, UserVm?>
    {
        private readonly UsersDbContext _db; public GetUserByIdHandler(UsersDbContext db) => _db = db;

        public async Task<UserVm?> Handle(GetUserByIdQuery q, CancellationToken ct)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct);
            if (u is null)
            {
                return null;
            }

            var isAdmin = q.CurrentRole == Role.ADMIN.ToString();
            var isSelf = q.CurrentSub == u.Id.ToString();

            if (!isAdmin && !isSelf)
            {
                throw new UnauthorizedAccessException(); // veja observação abaixo
            }

            return new(u.Id, u.Email, u.Nome, u.Role, u.CreatedAtUtc, u.UpdatedAtUtc);
        }
    }

}
