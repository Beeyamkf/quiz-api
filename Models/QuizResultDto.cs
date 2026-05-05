namespace QuizAPI.Models;

public class QuizResultDto
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public int Score { get; set; }
    public DateTime CreatedAt { get; set; }
}