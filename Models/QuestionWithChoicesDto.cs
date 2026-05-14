namespace QuizAPI.Models;

public class QuestionWithChoicesDto
{
    public int Id { get; set; }
    public string Text { get; set; }
    public List<ChoiceDto> Choices { get; set; } = new();
}

public class ChoiceDto
{
    public int Id { get; set; }
    public string Text { get; set; }
    public bool IsCorrect { get; set; }
}