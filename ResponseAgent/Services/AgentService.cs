using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace ResponseAgent.Services;

public interface IAgentService
{
    Task<string> GenerateResponseAsync(string questionText, CancellationToken cancellationToken = default);
    Task<string> ReviewResponseAsync(string responseText, CancellationToken cancellationToken = default);
    Task<string> RewriteResponseAsync(string responseText, CancellationToken cancellationToken = default);
    Task<string> ChatAsync(string userMessage, string context, CancellationToken cancellationToken = default);
}

public class AgentService : IAgentService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentService> _logger;

    public AgentService(IConfiguration configuration, ILogger<AgentService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GenerateResponseAsync(
        string questionText,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["FoundryAgent:Endpoint"];
        var agentName = _configuration["FoundryAgent:AgentId"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogError("Foundry Agent �G���h�|�C���g���ݒ肳��Ă��܂���");
            return "�G���[: Foundry Agent �G���h�|�C���g���ݒ肳��Ă��܂���Bappsettings.json ���m�F���Ă��������B";
        }

        if (string.IsNullOrWhiteSpace(agentName))
        {
            _logger.LogError("Agent �����ݒ肳��Ă��܂���");
            return "�G���[: Agent �����ݒ肳��Ă��܂���Bappsettings.json ���m�F���Ă��������B";
        }

        try
        {
            // AIProjectClient ���쐬 (App Service �ł̓}�l�[�W�hID���g�p)
            var credential = new DefaultAzureCredential();
            var projectClient = new AIProjectClient(new Uri(endpoint), credential);

            // ��b���쐬
            var conversationResult = projectClient.OpenAI.Conversations.CreateProjectConversation();
            var conversation = conversationResult.Value;
            
            _logger.LogInformation("��b���쐬���܂���: {ConversationId}", conversation.Id);

            // ProjectResponsesClient ���擾
            var responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
                defaultAgent: agentName,
                defaultConversationId: conversation.Id);

            _logger.LogInformation("�G�[�W�F���g '{AgentName}' �Ƀ��b�Z�[�W�𑗐M��...", agentName);

            // �G�[�W�F���g�Ƀ��b�Z�[�W�𑗐M
            var responseResult = await Task.Run(() => responsesClient.CreateResponse(questionText), cancellationToken);
            var response = responseResult.Value;

            // �����e�L�X�g���擾
            var responseText = response.GetOutputText();

            _logger.LogInformation("�G�[�W�F���g���牞������M���܂���");

            return responseText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI�G�[�W�F���g���s���ɃG���[���������܂���");
            return $"�G���[: {ex.Message}\n\n�ݒ���m�F���Ă�������:\n- �G���h�|�C���g: {endpoint}\n- �G�[�W�F���g��: {agentName}";
        }
    }

    public async Task<string> ReviewResponseAsync(
        string responseText,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["FoundryAgent:Endpoint"];
        var reviewAgentName = _configuration["FoundryAgent:ReviewAgentId"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogError("Foundry Agent �G���h�|�C���g���ݒ肳��Ă��܂���");
            return "�G���[: Foundry Agent �G���h�|�C���g���ݒ肳��Ă��܂���Bappsettings.json ���m�F���Ă��������B";
        }

        if (string.IsNullOrWhiteSpace(reviewAgentName))
        {
            _logger.LogError("Review Agent �����ݒ肳��Ă��܂���");
            return "�G���[: Review Agent �����ݒ肳��Ă��܂���Bappsettings.json �� ReviewAgentId ���m�F���Ă��������B";
        }

        try
        {
            // AIProjectClient ���쐬 (App Service �ł̓}�l�[�W�hID���g�p)
            var credential = new DefaultAzureCredential();
            var projectClient = new AIProjectClient(new Uri(endpoint), credential);

            // ��b���쐬
            var conversationResult = projectClient.OpenAI.Conversations.CreateProjectConversation();
            var conversation = conversationResult.Value;
            
            _logger.LogInformation("���r���[�p��b���쐬���܂���: {ConversationId}", conversation.Id);

            // ProjectResponsesClient ���擾�i���r���[�G�[�W�F���g�p�j
            var responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
                defaultAgent: reviewAgentName,
                defaultConversationId: conversation.Id);

            _logger.LogInformation("���r���[�G�[�W�F���g '{ReviewAgentName}' �Ƀ��b�Z�[�W�𑗐M��...", reviewAgentName);

            // ���r���[�˗����b�Z�[�W���쐬
            var reviewRequest = $"�ȉ��̓��وĂ����r���[���Ă�������:\n\n{responseText}";

            // �G�[�W�F���g�Ƀ��b�Z�[�W�𑗐M
            var responseResult = await Task.Run(() => responsesClient.CreateResponse(reviewRequest), cancellationToken);
            var response = responseResult.Value;

            // �����e�L�X�g���擾
            var reviewText = response.GetOutputText();

            _logger.LogInformation("���r���[�G�[�W�F���g���牞������M���܂���");

            return reviewText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "���r���[�G�[�W�F���g���s���ɃG���[���������܂���");
            return $"�G���[: {ex.Message}\n\n�ݒ���m�F���Ă�������:\n- �G���h�|�C���g: {endpoint}\n- ���r���[�G�[�W�F���g��: {reviewAgentName}";
        }
    }

    public async Task<string> RewriteResponseAsync(
        string responseText,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["FoundryAgent:Endpoint"];
        var rewriteAgentName = _configuration["FoundryAgent:RewriteAgentId"] ?? "Answer-Rewrite";

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogError("Foundry Agent エンドポイントが設定されていません");
            return $"エラー: Foundry Agent エンドポイントが設定されていません。appsettings.json を確認してください。";
        }

        if (string.IsNullOrWhiteSpace(rewriteAgentName))
        {
            _logger.LogError("Rewrite Agent 名が設定されていません");
            return $"エラー: Rewrite Agent 名が設定されていません。appsettings.json を確認してください。";
        }

        try
        {
            var credential = new DefaultAzureCredential();
            var projectClient = new AIProjectClient(new Uri(endpoint), credential);

            // 会話を作成
            var conversationResult = projectClient.OpenAI.Conversations.CreateProjectConversation();
            var conversation = conversationResult.Value;
            
            _logger.LogInformation("修正用の会話を作成しました: {ConversationId}", conversation.Id);

            // ProjectResponsesClient を取得（修正エージェント用）
            var responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
                defaultAgent: rewriteAgentName,
                defaultConversationId: conversation.Id);

            _logger.LogInformation("修正エージェント '{RewriteAgentName}' にメッセージを送信中...", rewriteAgentName);

            // 修正リクエストメッセージを作成
            var rewriteRequest = $"以下のAI生成テキストを改善・修正してください。修正内容は元のテキストとの差分を含めて提示してください:\n\n{responseText}";

            // エージェントにメッセージを送信
            var responseResult = await Task.Run(() => responsesClient.CreateResponse(rewriteRequest), cancellationToken);
            var response = responseResult.Value;

            // 修正されたテキストを取得
            var rewrittenText = response.GetOutputText();

            _logger.LogInformation("修正エージェントから応答を受け取りました");

            return rewrittenText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修正エージェント処理中にエラーが発生しました");
            return $"エラー: {ex.Message}\n\n設定を確認してください:\n- エンドポイント: {endpoint}\n- 修正エージェント名: {rewriteAgentName}";
        }
    }

    public async Task<string> ChatAsync(
        string userMessage,
        string context,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["FoundryAgent:Endpoint"];
        var chatAgentName = _configuration["FoundryAgent:ChatAgentId"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogError("Foundry Agent �G���h�|�C���g���ݒ肳��Ă��܂���");
            return "�G���[: Foundry Agent �G���h�|�C���g���ݒ肳��Ă��܂���B";
        }

        if (string.IsNullOrWhiteSpace(chatAgentName))
        {
            _logger.LogError("Chat Agent �����ݒ肳��Ă��܂���");
            return "�G���[: Chat Agent �����ݒ肳��Ă��܂���Bappsettings.json �� ChatAgentId ���m�F���Ă��������B";
        }

        try
        {
            var credential = new DefaultAzureCredential();
            var projectClient = new AIProjectClient(new Uri(endpoint), credential);

            var conversationResult = projectClient.OpenAI.Conversations.CreateProjectConversation();
            var conversation = conversationResult.Value;
            
            _logger.LogInformation("�`���b�g�p��b���쐬���܂���: {ConversationId}", conversation.Id);

            var responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
                defaultAgent: chatAgentName,
                defaultConversationId: conversation.Id);

            _logger.LogInformation("�`���b�g�G�[�W�F���g '{ChatAgentName}' �Ƀ��b�Z�[�W�𑗐M��...", chatAgentName);

            // �R���e�L�X�g���܂߂����b�Z�[�W���쐬
            var messageWithContext = string.IsNullOrWhiteSpace(context)
                ? userMessage
                : $"�y��ʏ�̏��z\n{context}\n\n�y���[�U�[�̎���z\n{userMessage}";

            var responseResult = await Task.Run(() => responsesClient.CreateResponse(messageWithContext), cancellationToken);
            var response = responseResult.Value;

            var chatResponse = response.GetOutputText();

            _logger.LogInformation("�`���b�g�G�[�W�F���g���牞������M���܂���");

            return chatResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "�`���b�g�G�[�W�F���g���s���ɃG���[���������܂���");
            return $"�G���[: {ex.Message}";
        }
    }
}








