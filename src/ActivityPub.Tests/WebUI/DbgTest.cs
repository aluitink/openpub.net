using Xunit;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Tests.WebUI;

public class DbgTest : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;
    public DbgTest(WebUIFactory f) { _factory = f; }

    [Xunit.Fact]
    public async Task Dbg_PostAndInspect()
    {
        var client = _factory.CreateClient();
        var username = $"dbg_{Guid.NewGuid().ToString("N")[..8]}";
        await client.PostAsync("/auth/register", new FormUrlEncodedContent(new Dictionary<string,string>{
            {"Username",username},{"Email",username+"@t.com"},{"DisplayName","T"},{"Password","Password123!"},{"ConfirmPassword","Password123!"}
        }));
        await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string,string>{
            {"Username",username},{"Password","Password123!"}
        }));

        var form = new MultipartFormDataContent();
        form.Add(new StringContent("DBGUNIQ"), "Content");
        var bytes = System.Text.Encoding.UTF8.GetBytes("PDF");
        var sc = new StreamContent(new MemoryStream(bytes));
        sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        sc.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data"){Name="Document",FileName="d.pdf"};
        form.Add(sc, "Document");

        var post = await client.PostAsync("/compose/post", form);
        Xunit.Assert.True(post.IsSuccessStatusCode || post.Headers.Location != null, $"post {(int)post.StatusCode}");

        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var outbox = await repo.GetActorOutboxActivitiesAsync(username, 0, 10);
        foreach (var id in outbox)
        {
            var a = await repo.GetActivityAsync(id);
            if (a != null && a.Object is ActivityPub.Core.Models.Object obj && (obj.Content ?? "").Contains("DBGUNIQ"))
            {
                System.Console.WriteLine("ACTIVITY_JSON=" + JsonSerializer.Serialize(a));
            }
        }
    }
}
