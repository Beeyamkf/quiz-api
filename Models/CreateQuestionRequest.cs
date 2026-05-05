namespace QuizAPI.Models;

public class CreateQuestionRequest
{
    public int QuizId { get; set; }
    public string Text { get; set; }
}
