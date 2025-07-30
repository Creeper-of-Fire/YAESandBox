namespace YAESandBox.Authentication;

public interface IPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

public class PasswordService : IPasswordService
{
    // BCrypt 会自动处理加盐（salt），非常方便
    public string HashPassword(string password) => 
        BCrypt.Net.BCrypt.HashPassword(password);

    public bool VerifyPassword(string password, string passwordHash) => 
        BCrypt.Net.BCrypt.Verify(password, passwordHash);
}