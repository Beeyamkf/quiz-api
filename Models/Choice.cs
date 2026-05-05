namespace QuizAPI.Models;

public class Choice
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Text { get; set; }
    public bool IsCorrect { get; set; }
}