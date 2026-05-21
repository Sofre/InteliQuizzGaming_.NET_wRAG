namespace QuizGamePlatform.Models;

public class Questionnaire
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Customization UI info
    public string ThemeColor { get; set; } = "#3b82f6"; // Default blue
    public string BackgroundColor { get; set; } = "#ffffff";
    
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    
    // Many-to-Many Sessions
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}

public class Question
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    
    // JSON or comma separated options
    public string Options { get; set; } = string.Empty; 
    public string CorrectAnswer { get; set; } = string.Empty;
    
    // Points given for correctness
    public int Points { get; set; } = 10;
    
    public int QuestionnaireId { get; set; }
    public Questionnaire? Questionnaire { get; set; }
    
    // Admin-defined sub-area for this question
    public int? SubAreaId { get; set; }
    public SubArea? SubArea { get; set; }
}
