using System;
using System.Threading.Tasks;
using Serilog;
using SlackCIApp.Config;
using Slack.Webhooks;
using System.Net.Http;
using System.IO;

namespace SlackCIApp
{
    public class SlackNotifier
    {
        private readonly SlackCISettings _settings;
        private readonly ILogger _logger;
        private readonly SlackClient? _slackClient;
        private readonly HttpClient _httpClient;

        public SlackNotifier(SlackCISettings settings, ILogger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient();
            
            if (!string.IsNullOrEmpty(_settings.SlackWebhookUrl))
            {
                _slackClient = new SlackClient(_settings.SlackWebhookUrl);
            }
        }

        public async Task SendNotificationAsync(string message)
        {
            if (_slackClient == null)
            {
                _logger.Warning("Slack webhook URL not configured. Message not sent: {Message}", message);
                return;
            }

            try
            {
                await Task.Run(() => 
                {
                    var slackMessage = new SlackMessage
                    {
                        Text = message,
                        Channel = _settings.ChannelName,
                        Username = "BaseHead CI",
                        IconEmoji = ":robot_face:"
                    };
                    
                    _slackClient.Post(slackMessage);
                    _logger.Information("Sent message to Slack: {Message}", message);
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send message to Slack: {ErrorMessage}", ex.Message);
            }
        }

        public async Task<bool> UploadFileAsync(string filePath, string title = "", string initialComment = "")
        {
            if (string.IsNullOrEmpty(_settings.SlackBotToken))
            {
                _logger.Warning("Slack bot token not configured. File not uploaded: {File}", filePath);
                return false;
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.Error("File not found for upload: {Path}", filePath);
                    return false;
                }

                var fileName = Path.GetFileName(filePath);
                var fileSize = new FileInfo(filePath).Length / (1024.0 * 1024.0);
                _logger.Information("Uploading file to Slack: {File} ({Size:F2} MB)", fileName, fileSize);                // Prepare the file content
                byte[] fileBytes;
                using (var fileStream = File.OpenRead(filePath))
                {
                    using var memStream = new MemoryStream();
                    await fileStream.CopyToAsync(memStream);
                    fileBytes = memStream.ToArray();
                }

                // Create the multipart form content
                var formData = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(fileBytes);
                formData.Add(fileContent, "file", fileName);
                formData.Add(new StringContent(_settings.ChannelName.TrimStart('#')), "channel"); // Note: changed from channels to channel
                
                if (!string.IsNullOrEmpty(title))
                    formData.Add(new StringContent(title), "title");
                
                if (!string.IsNullOrEmpty(initialComment))
                    formData.Add(new StringContent(initialComment), "initial_comment");

                // Add content type for files.upload v2 API
                fileContent.Headers.Add("Content-Type", "application/octet-stream");

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://slack.com/api/files.upload"),
                    Content = formData
                };

                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.SlackBotToken);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var json = System.Text.Json.JsonDocument.Parse(responseContent);
                    if (json.RootElement.GetProperty("ok").GetBoolean())
                    {
                        _logger.Information("Successfully uploaded file to Slack: {File}", fileName);
                        return true;
                    }
                    else
                    {
                        var error = json.RootElement.GetProperty("error").GetString();
                        if (error == "missing_scope" || error == "not_allowed")
                        {
                            _logger.Error("Missing Slack permissions. Bot token needs the files:write scope");
                            await SendNotificationAsync("⚠️ Bot lacks necessary permissions. Please add `files:write` under Bot Token Scopes");
                        }
                        _logger.Error("Slack API error: {Error}", error);
                        return false;
                    }
                }
                else
                {
                    _logger.Error("Failed to upload file. Status code: {Status}", response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error uploading file to Slack: {ErrorMessage}", ex.Message);
                return false;
            }
        }
    }
}
