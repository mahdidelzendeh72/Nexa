using System.Text.Json;
using Maf.Client.Chat;
using Microsoft.Extensions.AI;

namespace Maf.Client.Chat.Tools;

public static class AskUserAgentTool
{
    public const string Name = "ask_user";

    public const string DismissedResult = "کاربر به سؤال‌ها پاسخ نداد.";

    public const string Description =
        "Ask the user questions and wait until they answer. Call this once, with every question, when the user must choose an option or give an answer that is not already known. " +
        "Each item needs question, canChooseMultiple, hasUserOption, and choices. A choice has title, shown to the user, and value, any JSON value returned unchanged. " +
        "Set hasUserOption to true to show a text box for an answer that is not one of the choices. " +
        "The result is a JSON array of objects with question and answer. answer is always an array of the selected values and any typed text.";

    public static AITool Create(Func<IReadOnlyList<UserQuestion>, CancellationToken, Task<IReadOnlyList<UserAnswer>?>> ask) =>
        AIFunctionFactory.Create(
            (IReadOnlyList<UserQuestion> questions, CancellationToken cancellationToken) =>
                AskAsync(ask, questions, cancellationToken),
            Name,
            Description);

    private static async Task<JsonElement> AskAsync(
        Func<IReadOnlyList<UserQuestion>, CancellationToken, Task<IReadOnlyList<UserAnswer>?>> ask,
        IReadOnlyList<UserQuestion> questions,
        CancellationToken cancellationToken)
    {
        if (UserQuestions.Problem(questions) is string problem)
        {
            return JsonSerializer.SerializeToElement(problem);
        }

        IReadOnlyList<UserAnswer>? answers = await ask(UserQuestions.Copy(questions), cancellationToken);
        return answers is null
            ? JsonSerializer.SerializeToElement(DismissedResult)
            : JsonSerializer.Deserialize<JsonElement>(UserQuestions.Format(answers));
    }
}
