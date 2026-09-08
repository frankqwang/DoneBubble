using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DoneBubble.Models;
namespace DoneBubble.Services;
public sealed class LocalAiService : IDisposable
{
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(25) };
    public async Task<ActivityCandidate?> JudgeAsync(ActivityContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        return (await AnalyzeAsync(context, settings, cancellationToken).ConfigureAwait(false)).Candidate;
    }
    public async Task<AiAnalysisResult> AnalyzeAsync(ActivityContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.AiEndpoint)) return new(BuildPrompt(context), "", null, "没有配置 LM Studio 地址。");
        string prompt = BuildPrompt(context);
        var body = new { model = settings.AiModel, temperature = 0.1, max_tokens = 160, stream = false, messages = new[] { new { role = "user", content = prompt } } };
        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(settings.AiEndpoint, content, cancellationToken).ConfigureAwait(false);
        string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new(prompt, raw, null, $"LM Studio 返回 HTTP {(int)response.StatusCode}。");
        using var document = JsonDocument.Parse(raw);
        string text = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        int start = text.IndexOf('{'); int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return new(prompt, raw, null, "模型输出中没有找到 JSON。");
        using var result = JsonDocument.Parse(text[start..(end + 1)]);
        var root = result.RootElement;
        if (!root.TryGetProperty("done", out var done) || !done.GetBoolean()) return new(prompt, raw, null, "模型判断当前活动不像完成了一件事。");
        string summary = root.TryGetProperty("summary", out var summaryElement) ? summaryElement.GetString() ?? "完成了一段工作" : "完成了一段工作";
        string category = root.TryGetProperty("category", out var categoryElement) ? categoryElement.GetString() ?? "中" : "中";
        if (category is not ("轻" or "中" or "重")) category = "中";
        double confidence = root.TryGetProperty("confidence", out var confidenceElement) && confidenceElement.TryGetDouble(out var value) ? Math.Clamp(value, 0, 1) : .5;
        return new(prompt, raw, new ActivityCandidate(summary.Trim(), category, confidence, context.Duration), null);
    }
    private static string BuildPrompt(ActivityContext context) => $"你是一个极简事务记录助手。根据本地活动上下文判断是否可能完成了一件事。不要编造看不到的细节；控件文本只用于判断，不要复述敏感信息。只返回 JSON，不要 Markdown：{{\\\"done\\\":true或false,\\\"summary\\\":\\\"不超过24字的事实描述\\\",\\\"category\\\":\\\"轻\\\"或\\\"中\\\"或\\\"重\\\",\\\"confidence\\\":0到1}}。{context.PromptText}。持续阅读、等待、娱乐、密码输入或无法判断时 done=false。";
    public void Dispose() => client.Dispose();
}
