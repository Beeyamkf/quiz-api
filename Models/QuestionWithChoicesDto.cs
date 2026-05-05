namespace QuizAPI.Models;

public class QuestionWithChoicesDto
{
    public int Id { get; set; }
    public string Text { get; set; }
    public List<ChoiceDto> Choices { get; set; }
}

public class ChoiceDto
{
    public int Id { get; set; }
    public string Text { get; set; }
}