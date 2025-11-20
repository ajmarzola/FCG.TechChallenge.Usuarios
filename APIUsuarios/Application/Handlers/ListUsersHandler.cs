using MediatR;
using Microsoft.EntityFrameworkCore;
using APIUsuarios.Infrastructure.Data;

namespace APIUsuarios.Application
{
    public class ListUsersHandler : IRequestHandler<ListUsersQuery, PagedResult<UserVm>>
    {
        private readonly UsersDbContext _db;
        public ListUsersHandler(UsersDbContext db) => _db = db;

        public async Task<PagedResult<UserVm>> Handle(ListUsersQuery q, CancellationToken ct)
        {
            // normaliza filtro de e-mail
            var email = q.Email?.Trim().ToLowerInvariant();

            var query = _db.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(u => u.Email.Contains(email));
            }

            // total antes da paginação
            var total = await query.CountAsync(ct);

            // paginação básica (page 1-based)
            var page = q.Page <= 0 ? 1 : q.Page;
            var pageSize = q.PageSize <= 0 ? 20 : q.PageSize;

            var items = await query
                .OrderBy(u => u.Email) // ou CreatedAtUtc desc, etc.
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserVm(
                    u.Id, u.Email, u.Nome, u.Role, u.CreatedAtUtc, u.UpdatedAtUtc
                ))
                .ToListAsync(ct);

            return new PagedResult<UserVm>(items, total, page, pageSize);
        }
    }
}
