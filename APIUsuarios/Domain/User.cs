using System.ComponentModel.DataAnnotations;

namespace APIUsuarios.Domain
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        [EmailAddress, MaxLength(160)] public required string Email { get; set; }
        [MaxLength(160)] public required string Nome { get; set; }
        public required string PasswordHash { get; set; }
        public Role Role { get; set; } = Role.ALUNO;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
