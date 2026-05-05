namespace QuizAPI.Models;

public class CreateQuizRequest
{
    public string Title { get; set; }
    public int TeacherId { get; set; }
}