namespace QuizAPI.Models;

public class Quiz
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Code { get; set; }
    public int TeacherId { get; set; }
}