namespace QuizGamePlatform.Models;

public class Area
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public ICollection<SubArea> SubAreas { get; set; } = new List<SubArea>();
}

public class SubArea
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    public int AreaId { get; set; }
    public Area? Area { get; set; }
}
