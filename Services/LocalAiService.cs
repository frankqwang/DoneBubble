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
        var body = new { model = settings.AiModel, temperature = 0.1, max_tokens = 240, stream = false, reasoning_effort = "none", chat_template_kwargs = new { enable_thinking = false }, response_format = JsonSchemaFormat, messages = new[] { new { role = "user", content = prompt } } };
        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(settings.AiEndpoint, content, cancellationToken).ConfigureAwait(false);
        string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new(prompt, raw, null, $"LM Studio 返回 HTTP {(int)response.StatusCode}。");
        return ParseResult(prompt, raw, context.Duration, "活动");
    }
    public async Task<AiAnalysisResult> AnalyzeImageAsync(string base64Png, string contextDescription, Settings settings, CancellationToken cancellationToken = default)
    {
        string prompt = $"你是一个极简事务记录助手。根据这张用户主动提供的当前工作窗口截图和上下文，判断是否可能完成了一件事。不要猜测看不到的细节；只返回 JSON，不要 Markdown：{{\\\"done\\\":true或false,\\\"summary\\\":\\\"不超过24字的事实描述\\\",\\\"category\\\":\\\"轻\\\"或\\\"中\\\"或\\\"重\\\",\\\"confidence\\\":0到1}}。上下文：{contextDescription}。持续阅读、等待、娱乐或无法判断时 done=false。";
        var message = new object[] { new { type = "text", text = prompt }, new { type = "image_url", image_url = new { url = "data:image/png;base64," + base64Png } } };
        var body = new { model = settings.AiModel, temperature = 0.1, max_tokens = 240, stream = false, reasoning_effort = "none", chat_template_kwargs = new { enable_thinking = false }, response_format = JsonSchemaFormat, messages = new[] { new { role = "user", content = message } } };
        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(settings.AiEndpoint, content, cancellationToken).ConfigureAwait(false);
        string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new(prompt, raw, null, $"LM Studio 返回 HTTP {(int)response.StatusCode}。");
        return ParseResult(prompt, raw, TimeSpan.Zero, "画面");
    }
    private static string BuildPrompt(ActivityContext context) => $"你是一个极简事务记录助手。根据本地活动上下文判断是否可能完成了一件事。不要编造看不到的细节；控件文本只用于判断，不要复述敏感信息。只返回 JSON，不要 Markdown：{{\\\"done\\\":true或false,\\\"summary\\\":\\\"不超过24字的事实描述\\\",\\\"category\\\":\\\"轻\\\"或\\\"中\\\"或\\\"重\\\",\\\"confidence\\\":0到1}}。{context.PromptText}。持续阅读、等待、娱乐、密码输入或无法判断时 done=false。";
    private static object JsonSchemaFormat => new { type = "json_schema", json_schema = new { name = "donebubble_result", strict = true, schema = new { type = "object", properties = new { done = new { type = "boolean" }, summary = new { type = "string" }, category = new { type = "string", @enum = new[] { "轻", "中", "重" } }, confidence = new { type = "number" } }, required = new[] { "done", "summary", "category", "confidence" }, additionalProperties = false } } };
    private static AiAnalysisResult ParseResult(string prompt, string raw, TimeSpan duration, string subject)
    {
        try
        {
            using var document = JsonDocument.Parse(raw); var message = document.RootElement.GetProperty("choices")[0].GetProperty("message"); string text = message.TryGetProperty("content", out var content) ? content.GetString() ?? "" : ""; if (string.IsNullOrWhiteSpace(text) && message.TryGetProperty("reasoning_content", out var reasoning)) text = reasoning.GetString() ?? "";
            int start = text.IndexOf('{'); int end = text.LastIndexOf('}'); if (start < 0 || end <= start) return new(prompt, raw, null, $"模型输出中没有找到 JSON（{subject}）。");
            using var result = JsonDocument.Parse(text[start..(end + 1)]); var root = result.RootElement;
            if (!root.TryGetProperty("done", out var done) || !done.GetBoolean()) return new(prompt, raw, null, $"模型判断当前{subject}不像完成了一件事。");
            string summary = root.TryGetProperty("summary", out var se) ? se.GetString() ?? "完成了一段工作" : "完成了一段工作";
            string category = root.TryGetProperty("category", out var ce) ? ce.GetString() ?? "中" : "中"; if (category is not ("轻" or "中" or "重")) category = "中";
            double confidence = root.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var value) ? Math.Clamp(value, 0, 1) : .5;
            return new(prompt, raw, new ActivityCandidate(summary.Trim(), category, confidence, duration), null);
        }
        catch (Exception ex) { return new(prompt, raw, null, "解析失败：" + ex.Message); }
    }
    public void Dispose() => client.Dispose();
}
