using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Maf.Client.Chat;

public sealed class UserQuestion
{
    [Description("The question shown to the user.")]
    [JsonPropertyName("question")]
    public string Question { get; set; } = "";

    [Description("When true, the user may select more than one choice.")]
    [JsonPropertyName("canChooseMultiple")]
    public bool CanChooseMultiple { get; set; }

    [Description("When true, the user may type an answer that is not one of the choices.")]
    [JsonPropertyName("hasUserOption")]
    public bool HasUserOption { get; set; }

    [Description("Options the user can select. Each has a title and a value.")]
    [JsonPropertyName("choices")]
    public List<UserChoice> Choices { get; set; } = [];
}

public sealed class UserChoice
{
    [Description("Text shown for this option.")]
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [Description("JSON value returned when this option is selected. Any JSON value is allowed.")]
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }
}

public sealed class UserAnswer
{
    [JsonPropertyName("question")]
    public required string Question { get; init; }

    [JsonPropertyName("answer")]
    public required IReadOnlyList<JsonElement> Answer { get; init; }
}

public static class UserQuestions
{
    private static readonly JsonSerializerOptions ResultJson = new(JsonSerializerDefaults.Web);

    public static string? Problem(IReadOnlyList<UserQuestion>? questions)
    {
        if (questions is null || questions.Count == 0)
        {
            return "No questions were provided.";
        }

        foreach (UserQuestion question in questions)
        {
            if (string.IsNullOrWhiteSpace(question.Question))
            {
                return "A question is missing its text.";
            }

            List<UserChoice> choices = question.Choices ?? [];
            if (choices.Count == 0 && !question.HasUserOption)
            {
                return "A question has no choices and does not allow a typed answer.";
            }

            foreach (UserChoice choice in choices)
            {
                if (string.IsNullOrWhiteSpace(choice.Title))
                {
                    return "A choice is missing its title.";
                }

                if (choice.Value.ValueKind is JsonValueKind.Undefined)
                {
                    return "A choice is missing its value.";
                }
            }
        }

        return null;
    }

    public static IReadOnlyList<UserQuestion> Copy(IReadOnlyList<UserQuestion> questions) =>
        questions.Select(question => new UserQuestion
        {
            Question = question.Question.Trim(),
            CanChooseMultiple = question.CanChooseMultiple,
            HasUserOption = question.HasUserOption,
            Choices = (question.Choices ?? []).Select(choice => new UserChoice
            {
                Title = choice.Title.Trim(),
                Value = choice.Value.Clone(),
            }).ToList(),
        }).ToList();

    public static List<JsonElement>? Answer(UserQuestion question, IEnumerable<int> selected, string? typed)
    {
        List<JsonElement> answer = [];
        string text = typed?.Trim() ?? "";
        if (!question.CanChooseMultiple)
        {
            if (question.HasUserOption && text.Length > 0)
            {
                answer.Add(JsonSerializer.SerializeToElement(text, ResultJson));
                return answer;
            }

            int index = selected.FirstOrDefault(-1);
            if (index >= 0 && index < question.Choices.Count)
            {
                answer.Add(question.Choices[index].Value);
                return answer;
            }

            return null;
        }

        foreach (int index in selected.Distinct().Order())
        {
            if (index >= 0 && index < question.Choices.Count)
            {
                answer.Add(question.Choices[index].Value);
            }
        }

        if (question.HasUserOption && text.Length > 0)
        {
            answer.Add(JsonSerializer.SerializeToElement(text, ResultJson));
        }

        return answer.Count == 0 ? null : answer;
    }

    public static string Format(IReadOnlyList<UserAnswer> answers) =>
        JsonSerializer.Serialize(answers, ResultJson);
}
