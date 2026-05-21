namespace QuizGamePlatform.Models;

public class Session
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "2nd Year Students - 2026"
    
    public ICollection<User> AssignedUsers { get; set; } = new List<User>();
    public ICollection<Questionnaire> AssignedQuestionnaires { get; set; } = new List<Questionnaire>();
}
