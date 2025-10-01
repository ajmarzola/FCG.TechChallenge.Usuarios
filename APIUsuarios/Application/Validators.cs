using FluentValidation;

namespace APIUsuarios.Application
{
    public class RegisterUserValidator : AbstractValidator<RegisterUserDto>
    {
        public RegisterUserValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Senha).NotEmpty().MinimumLength(6);
            RuleFor(x => x.Nome).NotEmpty().MinimumLength(2).MaximumLength(160);
        }
    }


    public class LoginValidator : AbstractValidator<LoginDto>
    {
        public LoginValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Senha).NotEmpty();
        }
    }


    public class UpdateUserValidator : AbstractValidator<UpdateUserDto>
    {
        public UpdateUserValidator()
        {
            RuleFor(x => x.Nome).NotEmpty().MinimumLength(2).MaximumLength(160);
        }
    }


    public class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
    {
        public ChangePasswordValidator()
        {
            RuleFor(x => x.NovaSenha).NotEmpty().MinimumLength(6);
            RuleFor(x => x.SenhaAtual).NotEmpty();
        }
    }
}
