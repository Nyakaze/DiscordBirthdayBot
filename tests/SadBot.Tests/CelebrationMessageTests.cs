using System.Net;
using System.Text;
using System.Text.Json;
using Crovus.Client;
using Crovus.Gateway;
using Crovus.Models;
using SadBot.Gifs;
using SadBot.Modules.Birthdays;

namespace SadBot.Tests;

public sealed class CelebrationMessageTests
{
    [Fact]
    public void FillsTemplatePlaceholders()
    {
        var content = CelebrationMessage.Content("Happy {age}th, {user}! Love, the {NAME} fan club", 42, "Zoe", 30);

        Assert.Equal("Happy 30th, <@42>! Love, the Zoe fan club", content);
    }

    [Fact]
    public void UsesTheDefaultTemplateWhenNoneIsSet() =>
        Assert.Equal("🎉 Happy Birthday <@42>! 🎂", CelebrationMessage.Content(null, 42, "Zoe", null));

    [Fact]
    public void OnlyAllowsPingingTheBirthdayPerson()
    {
        var request = CelebrationMessage.Build("{user} @everyone", 42, "Zoe", null,
            new Gif("https://media.giphy.com/cake.gif", "Powered by GIPHY"));

        Assert.NotNull(request.AllowedMentions);
        Assert.Equal(MentionTypes.None, request.AllowedMentions.Parse);
        Assert.Equal([new Snowflake(42)], request.AllowedMentions.Users);
        Assert.Equal("https://media.giphy.com/cake.gif", request.Embeds![0].Image?.Url);
    }

    [Fact]
    public async Task SendsAllowedMentionsToDiscord()
    {
        var handler = new CapturingHandler();
        await using var client = new CrovusClient(
            CrovusClientOptions.For("token", GatewayIntents.Guilds) with { EnableCache = false },
            httpClient: new HttpClient(handler));

        await client.Messages.SendAsync(20, CelebrationMessage.Build(null, 42, "Zoe", null, null));

        using var body = JsonDocument.Parse(handler.Body!);
        var allowed = body.RootElement.GetProperty("allowed_mentions");

        Assert.Equal(0, allowed.GetProperty("parse").GetArrayLength());
        Assert.Equal("42", allowed.GetProperty("users")[0].ToString());
        Assert.False(allowed.TryGetProperty("roles", out _));
    }

    [Fact]
    public void RejectsConflictingAllowedMentions()
    {
        var conflicting = DiscordAllowedMentions.OnlyUsers(1).Parsing(MentionTypes.Users);

        Assert.Throws<InvalidOperationException>(conflicting.Validate);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            const string message = """
                {"id":"10","channel_id":"20","content":"","author":{"id":"30","username":"sadbot"},
                 "timestamp":"2026-09-29T12:00:00+00:00","embeds":[],"attachments":[],"mentions":[]}
                """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(message, Encoding.UTF8, "application/json")
            };
        }
    }
}
