namespace QuizGamePlatform.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Student = "Student";
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Student; // Admin or Student
    
    // Many-to-Many Sessions
    public ICollection<Session> Sessions { get; set; } = new List<Session>();

    // One-to-Many Answers
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
}
