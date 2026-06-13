using System.ClientModel;
using System.Globalization;
using OpenAI;
using OpenAI.Chat;

namespace Dogebot.Server.Services;

public class DengAiService : IDengAiService
{
    private const string BaseUrlEnvironmentVariableName = "DOGEBOT_DENG_AI_BASE_URL";
    private const string ApiKeyEnvironmentVariableName = "DOGEBOT_DENG_AI_API_KEY";
    private const string ModelEnvironmentVariableName = "DOGEBOT_DENG_AI_MODEL";
    private const int MaximumResponseCharacterCount = 800;
    private const int MaximumOutputTokenCount = 1000;

    private const string SystemPrompt = """
        당신의 이름은 도지봇이고, 카카오톡 봇의 가볍고 재치있는 강아지 페르소나를 가진 AI 답변 캐릭터다. 모든 답변은 친근하고 장난스럽게 하며, 충성스럽고 호기심 많은 강아지처럼 밝은 분위기를 유지하고, 문장 끝을 자연스럽게 "~멍" 말투로 마무리한다. 사용자를 비난하거나 가르치려 들지 말고, 되묻기보다 상황에 맞는 재미있는 답변을 바로 제공한다. 이 대화는 일회성으로 사용되며 이전 대화나 이후 대화를 기억하지 못한다. 따라서 "앞으로 ~하겠다", "다음부터 ~하겠다", "기억해두겠다", "계속 ~하겠다"처럼 장기 기억이나 미래의 지속 행동을 약속하는 표현을 쓰지 않는다. 답변은 공백과 줄바꿈을 포함해 반드시 800자 이내로 작성한다. 카카오톡에서는 마크다운이 지원되지 않으므로 굵게, 기울임, 제목, 목록, 인용, 코드블록, 표, 링크 형식 같은 마크다운 문법을 쓰지 말고 일반 텍스트로만 답한다.

        논란이 될 수 있는 정치, 종교, 젠더, 인종, 국적, 지역, 장애, 외모, 사건사고, 범죄, 혐오, 조롱, 선정적 주제는 편들거나 단정하지 말고 부드럽게 피한다. 특정 개인이나 집단을 공격하거나 비하하지 않는다. 위험한 행동, 불법 행위, 자해, 폭력, 개인정보 침해, 전문적 의료·법률·금융 판단은 제공하지 않는다.

        애매하거나 민감한 요청은 짧게 선을 긋고 안전한 농담이나 일상적인 대안으로 전환한다. 분위기는 귀엽고 장난스럽게 유지하되, 상대가 힘들어 보이면 다정하고 차분하게 답한다멍.
        """;

    private readonly ChatClient? _chatClient;
    private readonly ILogger<DengAiService> _logger;

    public DengAiService(ILogger<DengAiService> logger)
    {
        _logger = logger;

        var baseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariableName);
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariableName);
        var model = Environment.GetEnvironmentVariable(ModelEnvironmentVariableName);

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            logger.LogWarning("[DENG_AI] AI configuration is missing. Required environment variables: {BaseUrl}, {ApiKey}, {Model}", BaseUrlEnvironmentVariableName, ApiKeyEnvironmentVariableName, ModelEnvironmentVariableName);
            return;
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint))
        {
            logger.LogError("[DENG_AI] Invalid base URL configured in {EnvironmentVariableName}", BaseUrlEnvironmentVariableName);
            return;
        }

        _chatClient = new ChatClient(
            model: model,
            credential: new ApiKeyCredential(apiKey),
            options: new OpenAIClientOptions
            {
                Endpoint = endpoint
            });

        IsConfigured = true;
    }

    public bool IsConfigured { get; }

    public async Task<string?> GenerateReplyAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (_chatClient is null) return null;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(userMessage)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = MaximumOutputTokenCount
        };

        var completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
        var reply = completion.Value.Content.Count > 0 ? completion.Value.Content[0].Text.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(reply))
        {
            _logger.LogWarning("[DENG_AI] Empty AI response received");
            return null;
        }

        return TrimToMaximumCharacters(reply);
    }

    private static string TrimToMaximumCharacters(string message)
    {
        var textElementIndexes = StringInfo.ParseCombiningCharacters(message);
        if (textElementIndexes.Length <= MaximumResponseCharacterCount) return message;

        return message[..textElementIndexes[MaximumResponseCharacterCount]];
    }
}
