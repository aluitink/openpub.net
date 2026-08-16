using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.HttpSignatures;

public class AdditionalSignatureTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AdditionalSignatureTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Signature_Create_Activity_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/create-1"",
            ""type"": ""Create"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/1"",
                ""type"": ""Note"",
                ""content"": ""Hello from remote""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Update_Activity_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/update-1"",
            ""type"": ""Update"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/1"",
                ""type"": ""Note"",
                ""content"": ""Updated content""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Delete_Activity_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/delete-1"",
            ""type"": ""Delete"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/1"",
                ""type"": ""Tombstone""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Follow_Activity_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/follow-1"",
            ""type"": ""Follow"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": ""https://localhost/users/bob""
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/bob/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Accept_Activity_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/bob/activities/accept-1"",
            ""type"": ""Accept"",
            ""actor"": ""https://remote.example/users/bob"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/activities/follow-1"",
                ""type"": ""Follow"",
                ""actor"": ""https://remote.example/users/alice"",
                ""object"": ""https://localhost/users/bob""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Reject_Activity_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/bob/activities/reject-1"",
            ""type"": ""Reject"",
            ""actor"": ""https://remote.example/users/bob"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/activities/follow-2"",
                ""type"": ""Follow"",
                ""actor"": ""https://remote.example/users/alice"",
                ""object"": ""https://localhost/users/bob""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Like_Activity_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/like-1"",
            ""type"": ""Like"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": ""https://localhost/users/bob/notes/5""
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/bob/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Announce_Activity_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/announce-1"",
            ""type"": ""Announce"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": ""https://localhost/users/bob/notes/5""
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/bob/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Flag_Activity_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/flag-1"",
            ""type"": ""Create"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/flag-1"",
                ""type"": ""Note"",
                ""content"": ""Flag test""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Activity_With_Language_Property_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/lang-1"",
            ""type"": ""Create"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/lang-1"",
                ""type"": ""Note"",
                ""content"": ""Hello in English"",
                ""language"": ""en""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Activity_With_Sensitive_Flag_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/sensitive-1"",
            ""type"": ""Create"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/sensitive-1"",
                ""type"": ""Note"",
                ""content"": ""Sensitive content"",
                ""sensitive"": true
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_undone_Like_Activity_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/undo-like-1"",
            ""type"": ""Undo"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/activities/like-1"",
                ""type"": ""Like"",
                ""actor"": ""https://remote.example/users/alice"",
                ""object"": ""https://localhost/users/bob/notes/5""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/bob/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Activity_With_Published_Date_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/published-1"",
            ""type"": ""Create"",
            ""actor"": ""https://remote.example/users/alice"",
            ""published"": ""2024-06-15T10:30:00Z"",
            ""to"": [""https://www.w3.org/ns/activitystreams#Public""],
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/2"",
                ""type"": ""Note"",
                ""content"": ""Dated post""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Activity_With_To_Array_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/toarray-1"",
            ""type"": ""Create"",
            ""actor"": ""https://remote.example/users/alice"",
            ""to"": [
                ""https://www.w3.org/ns/activitystreams#Public"",
                ""https://localhost/users/bob/followers""
            ],
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/3"",
                ""type"": ""Note"",
                ""content"": ""Multiple recipients""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Activity_With_Cc_Array_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/ccarray-1"",
            ""type"": ""Create"",
            ""actor"": ""https://remote.example/users/alice"",
            ""to"": [""https://www.w3.org/ns/activitystreams#Public""],
            ""cc"": [""https://localhost/users/bob"", ""https://localhost/users/charlie""],
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/4"",
                ""type"": ""Note"",
                ""content"": ""With CC""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Activity_With_Attributes_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/attrs-1"",
            ""type"": ""Create"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/6"",
                ""type"": ""Note"",
                ""content"": ""Rich post"",
                ""attributedTo"": ""https://remote.example/users/alice"",
                ""sensitive"": false,
                ""summary"": ""A summary""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Activity_With_InReplyTo_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/inreplyto-1"",
            ""type"": ""Create"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/7"",
                ""type"": ""Note"",
                ""content"": ""Reply content"",
                ""inReplyTo"": ""https://localhost/users/bob/notes/1""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Activity_With_Tag_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/tag-1"",
            ""type"": ""Create"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/8"",
                ""type"": ""Note"",
                ""content"": ""Tagged post"",
                ""tag"": [
                    {""type"": ""Hashtag"", ""href"": ""https://localhost/tags/test"", ""name"": ""#test""}
                ]
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Activity_With_Image_Attachment_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/image-1"",
            ""type"": ""Create"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/9"",
                ""type"": ""Note"",
                ""content"": ""Post with image"",
                ""attachment"": {
                    ""type"": ""Image"",
                    ""url"": ""https://example.com/image.jpg"",
                    ""mediaType"": ""image/jpeg""
                }
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Activity_With_Embed_Attachment_Succeeds()
    {
        var activity = @"{
            ""id"": ""https://remote.example/users/alice/activities/embed-1"",
            ""type"": ""Create"",
            ""actor"": ""https://remote.example/users/alice"",
            ""object"": {
                ""id"": ""https://remote.example/users/alice/notes/10"",
                ""type"": ""Note"",
                ""content"": ""Post with embed"",
                ""attachment"": {
                    ""type"": ""Document"",
                    ""url"": ""https://example.com/video.mp4"",
                    ""mediaType"": ""video/mp4"",
                    ""name"": ""My Video""
                }
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Sequential_Activities_All_Succeed()
    {
        for (int i = 0; i < 5; i++)
        {
            var activity = $@"{{
                ""id"": ""https://remote.example/users/alice/activities/seq-{i}"",
                ""type"": ""Create"",
                ""actor"": ""https://remote.example/users/alice"",
                ""object"": {{
                    ""id"": ""https://remote.example/users/alice/notes/seq-{i}"",
                    ""type"": ""Note"",
                    ""content"": ""Sequential {i}""
                }}
            }}";
            var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
            var response = await _client.PostAsync("/users/alice/inbox", content);
            Assert.True(response.IsSuccessStatusCode, $"Sequential activity {i} failed");
        }
    }

    [Fact]
    public async Task Signature_Concurrent_Activities_All_Succeed()
    {
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 5; i++)
        {
            var activity = $@"{{
                ""id"": ""https://remote.example/users/alice/activities/par-{i}"",
                ""type"": ""Create"",
                ""actor"": ""https://remote.example/users/alice"",
                ""object"": {{
                    ""id"": ""https://remote.example/users/alice/notes/par-{i}"",
                    ""type"": ""Note"",
                    ""content"": ""Parallel {i}""
                }}
            }}";
            var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
            tasks.Add(_client.PostAsync("/users/alice/inbox", content));
        }

        var results = await Task.WhenAll(tasks);
        foreach (var r in results)
            Assert.True(r.IsSuccessStatusCode);
    }
}
