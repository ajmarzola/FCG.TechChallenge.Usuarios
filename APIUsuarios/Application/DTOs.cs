using System.ComponentModel.DataAnnotations;
using APIUsuarios.Domain;

namespace APIUsuarios.Application
{
    // Criação de usuário: Role é opcional; se não vier, usar Role.ALUNO no handler.
    public record RegisterUserDto(
        [property: Required, EmailAddress] string Email,
        [property: Required, MinLength(6)] string Senha,
        [property: Required, MinLength(2), MaxLength(160)] string Nome,
        Role? Role // <- agora nullable
    );

    // Login
    public record LoginDto(
        [property: Required, EmailAddress] string Email,
        [property: Required] string Senha
    );

    // Atualização: Role opcional (só ADMIN pode alterar; handler deve checar isAdmin && Role != null)
    public record UpdateUserDto(
        [property: Required, MinLength(2), MaxLength(160)] string Nome,
        Role? Role // <- agora nullable
    );

    // Troca de senha: Admin não precisa informar SenhaAtual (validado no handler)
    public record ChangePasswordDto(
        [property: Required] string SenhaAtual,
        [property: Required, MinLength(6)] string NovaSenha
    );

    // ViewModel de retorno
    public record UserVm(
        Guid Id,
        string Email,
        string Nome,
        Role Role,
        DateTime CreatedAtUtc,
        DateTime? UpdatedAtUtc
    );

    // Paginação
    public record PagedResult<T>(
        IEnumerable<T> Items,
        int Total,
        int Page,
        int PageSize
    );
}
