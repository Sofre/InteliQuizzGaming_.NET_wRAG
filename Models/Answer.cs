namespace QuizGamePlatform.Models;

public class Answer
{
    public int Id { get; set; }
    
    public int UserId { get; set; }
    public User? User { get; set; }
    
    public int QuestionId { get; set; }
    public Question? Question { get; set; }
    
    // The choice the user made
    public string Choice { get; set; } = string.Empty;
    
    // Gamification properties
    public bool IsSkipped { get; set; }
    public bool IsCorrect { get; set; }
    public int PointsEarned { get; set; }
    public int TimeTakenSeconds { get; set; } // Timer logic
    
    // User-overriden / assigned subarea
    public int? UserAssignedSubAreaId { get; set; }
    public SubArea? UserAssignedSubArea { get; set; }
}
