using System.Text.Json;
using System.Text.Json.Serialization;
using ActivityPub.Core.Models;
using Xunit;

namespace ActivityPub.Tests.Models;

public class ModelSerializationTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #region Video Tests

    [Fact]
    public void Video_Serialization_IncludesAllProperties()
    {
        var video = new Video
        {
            Id = "https://example.com/video/1",
            Type = "Video",
            Duration = "PT1H30M",
            Width = 1920,
            Height = 1080,
            Name = "Test Video",
            Content = "Test content"
        };

        var json = JsonSerializer.Serialize(video, _jsonOptions);
        
        Assert.Contains("\"duration\":\"PT1H30M\"", json);
        Assert.Contains("\"width\":1920", json);
        Assert.Contains("\"height\":1080", json);
        Assert.Contains("\"id\":\"https://example.com/video/1\"", json);
        Assert.Contains("\"type\":\"Video\"", json);
    }

    [Fact]
    public void Video_Deserialization_PropertiesArePopulated()
    {
        var json = """
        {
            "@context": "https://www.w3.org/ns/activitystreams",
            "id": "https://example.com/video/1",
            "type": "Video",
            "duration": "PT1H30M",
            "width": 1920,
            "height": 1080,
            "name": "Test Video"
        }
        """;

        var video = JsonSerializer.Deserialize<Video>(json, _jsonOptions);

        Assert.NotNull(video);
        Assert.Equal("https://example.com/video/1", video!.Id);
        Assert.Equal("Video", video.Type);
        Assert.Equal("PT1H30M", video.Duration);
        Assert.Equal(1920, video.Width);
        Assert.Equal(1080, video.Height);
        Assert.Equal("Test Video", video.Name);
    }

    [Fact]
    public void Video_NullableProperties_HandleNullValues()
    {
        var video = new Video
        {
            Id = "https://example.com/video/1",
            Type = "Video",
            Duration = null,
            Width = null,
            Height = null
        };

        var json = JsonSerializer.Serialize(video, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Video>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized.Duration);
        Assert.Null(deserialized.Width);
        Assert.Null(deserialized.Height);
    }

    [Fact]
    public void Video_PublicProperties_AreAccessible()
    {
        var video = new Video { Id = "test", Type = "Video" };
        
        video.Duration = "PT1H";
        video.Width = 100;
        video.Height = 200;
        video.Name = "Test";
        video.Content = "Content";
        video.Context = "context";

        Assert.Equal("test", video.Id);
        Assert.Equal("Video", video.Type);
        Assert.Equal("PT1H", video.Duration);
        Assert.Equal(100, video.Width);
        Assert.Equal(200, video.Height);
        Assert.Equal("Test", video.Name);
        Assert.Equal("Content", video.Content);
        Assert.Equal("context", video.Context);
    }

    #endregion

    #region Page Tests

    [Fact]
    public void Page_Serialization_IncludesBaseProperties()
    {
        var page = new Page
        {
            Id = "https://example.com/page/1",
            Type = "Page",
            Name = "Test Page",
            Content = "Page content"
        };

        var json = JsonSerializer.Serialize(page, _jsonOptions);
        
        Assert.Contains("\"id\":\"https://example.com/page/1\"", json);
        Assert.Contains("\"type\":\"Page\"", json);
        Assert.Contains("\"name\":\"Test Page\"", json);
    }

    [Fact]
    public void Page_Deserialization_PropertiesArePopulated()
    {
        var json = """
        {
            "id": "https://example.com/page/1",
            "type": "Page",
            "name": "Test Page",
            "content": "Page content"
        }
        """;

        var page = JsonSerializer.Deserialize<Page>(json, _jsonOptions);

        Assert.NotNull(page);
        Assert.Equal("https://example.com/page/1", page!.Id);
        Assert.Equal("Page", page.Type);
        Assert.Equal("Test Page", page.Name);
        Assert.Equal("Page content", page.Content);
    }

    [Fact]
    public void Page_PublicProperties_AreAccessible()
    {
        var page = new Page { Id = "test", Type = "Page" };
        
        page.Name = "Test";
        page.Content = "Content";
        page.Context = "context";

        Assert.Equal("test", page.Id);
        Assert.Equal("Page", page.Type);
        Assert.Equal("Test", page.Name);
        Assert.Equal("Content", page.Content);
        Assert.Equal("context", page.Context);
    }

    #endregion

    #region Article Tests

    [Fact]
    public void Article_Serialization_IncludesAllProperties()
    {
        var article = new Article
        {
            Id = "https://example.com/article/1",
            Type = "Article",
            Content = "Article content",
            MediaType = "text/markdown",
            Name = "Test Article"
        };

        var json = JsonSerializer.Serialize(article, _jsonOptions);
        
        Assert.Contains("\"content\":\"Article content\"", json);
        Assert.Contains("\"mediaType\":\"text/markdown\"", json);
        Assert.Contains("\"id\":\"https://example.com/article/1\"", json);
    }

    [Fact]
    public void Article_Deserialization_PropertiesArePopulated()
    {
        var json = """
        {
            "id": "https://example.com/article/1",
            "type": "Article",
            "content": "Article content",
            "mediaType": "text/markdown",
            "name": "Test Article"
        }
        """;

        var article = JsonSerializer.Deserialize<Article>(json, _jsonOptions);

        Assert.NotNull(article);
        Assert.Equal("https://example.com/article/1", article!.Id);
        Assert.Equal("Article", article.Type);
        Assert.Equal("Article content", article.Content);
        Assert.Equal("text/markdown", article.MediaType);
        Assert.Equal("Test Article", article.Name);
    }

    [Fact]
    public void Article_PublicProperties_AreAccessible()
    {
        var article = new Article { Id = "test", Type = "Article", Content = "Content", MediaType = "text/html" };
        
        article.Name = "Test";

        Assert.Equal("test", article.Id);
        Assert.Equal("Article", article.Type);
        Assert.Equal("Content", article.Content);
        Assert.Equal("text/html", article.MediaType);
        Assert.Equal("Test", article.Name);
    }

    #endregion

    #region Image Tests

    [Fact]
    public void Image_Serialization_IncludesAllProperties()
    {
        var image = new Image
        {
            Url = "https://example.com/image.png",
            MediaType = "image/png",
            Width = 800,
            Height = 600
        };

        var json = JsonSerializer.Serialize(image, _jsonOptions);
        
        Assert.Contains("\"url\":\"https://example.com/image.png\"", json);
        Assert.Contains("\"mediaType\":\"image/png\"", json);
        Assert.Contains("\"width\":800", json);
    }

    [Fact]
    public void Image_Deserialization_PropertiesArePopulated()
    {
        var json = """
        {
            "url": "https://example.com/image.png",
            "mediaType": "image/png",
            "width": 800,
            "height": 600
        }
        """;

        var image = JsonSerializer.Deserialize<Image>(json, _jsonOptions);

        Assert.NotNull(image);
        Assert.Equal("https://example.com/image.png", image!.Url);
        Assert.Equal("image/png", image.MediaType);
        Assert.Equal(800, image.Width);
        Assert.Equal(600, image.Height);
    }

    [Fact]
    public void Image_NullableProperties_HandleNullValues()
    {
        var image = new Image
        {
            Url = "https://example.com/image.png",
            MediaType = null,
            Width = null,
            Height = null
        };

        var json = JsonSerializer.Serialize(image, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Image>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("https://example.com/image.png", deserialized.Url);
        Assert.Null(deserialized.MediaType);
        Assert.Null(deserialized.Width);
        Assert.Null(deserialized.Height);
    }

    [Fact]
    public void Image_PublicProperties_AreAccessible()
    {
        var image = new Image { Url = "https://example.com/image.png" };
        
        image.MediaType = "image/png";
        image.Width = 800;
        image.Height = 600;

        Assert.Equal("https://example.com/image.png", image.Url);
        Assert.Equal("image/png", image.MediaType);
        Assert.Equal(800, image.Width);
        Assert.Equal(600, image.Height);
    }

    #endregion

    #region Tombstone Tests

    [Fact]
    public void Tombstone_Serialization_IncludesAllProperties()
    {
        var tombstone = new Tombstone
        {
            Id = "https://example.com/tombstone/1",
            Type = "Tombstone",
            Context = "https://www.w3.org/ns/activitystreams",
            Deleted = DateTime.UtcNow,
            FormerType = "Note"
        };

        var json = JsonSerializer.Serialize(tombstone, _jsonOptions);
        
        Assert.Contains("\"id\":\"https://example.com/tombstone/1\"", json);
        Assert.Contains("\"type\":\"Tombstone\"", json);
        Assert.Contains("\"formerType\":\"Note\"", json);
    }

    [Fact]
    public void Tombstone_Deserialization_PropertiesArePopulated()
    {
        var json = """
        {
            "@context": "https://www.w3.org/ns/activitystreams",
            "id": "https://example.com/tombstone/1",
            "type": "Tombstone",
            "deleted": "2024-01-01T00:00:00Z",
            "formerType": "Note"
        }
        """;

        var tombstone = JsonSerializer.Deserialize<Tombstone>(json, _jsonOptions);

        Assert.NotNull(tombstone);
        Assert.Equal("https://example.com/tombstone/1", tombstone!.Id);
        Assert.Equal("Tombstone", tombstone.Type);
        Assert.Equal("Note", tombstone.FormerType);
        Assert.NotNull(tombstone.Deleted);
    }

    [Fact]
    public void Tombstone_NullableProperties_HandleNullValues()
    {
        var tombstone = new Tombstone
        {
            Id = "https://example.com/tombstone/1",
            Type = "Tombstone",
            Context = null,
            Deleted = null,
            FormerType = null
        };

        var json = JsonSerializer.Serialize(tombstone, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Tombstone>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized.Context);
        Assert.Null(deserialized.Deleted);
        Assert.Null(deserialized.FormerType);
    }

    [Fact]
    public void Tombstone_PublicProperties_AreAccessible()
    {
        var tombstone = new Tombstone { Id = "test", Type = "Tombstone" };
        
        tombstone.Context = "context";
        tombstone.Deleted = DateTime.UtcNow;
        tombstone.FormerType = "Note";

        Assert.Equal("test", tombstone.Id);
        Assert.Equal("Tombstone", tombstone.Type);
        Assert.Equal("context", tombstone.Context);
        Assert.NotNull(tombstone.Deleted);
        Assert.Equal("Note", tombstone.FormerType);
    }

    #endregion

    #region Endpoints Tests

    [Fact]
    public void Endpoints_Serialization_IncludesAllProperties()
    {
        var endpoints = new Endpoints
        {
            ProxyUrl = "https://example.com/proxy",
            OAuthAuthorizationEndpoint = "https://example.com/oauth/authorize",
            OAuthTokenEndpoint = "https://example.com/oauth/token",
            SharedInbox = "https://example.com/inbox"
        };

        var json = JsonSerializer.Serialize(endpoints, _jsonOptions);
        
        Assert.Contains("\"proxyUrl\":\"https://example.com/proxy\"", json);
        Assert.Contains("\"oauthAuthorizationEndpoint\":\"https://example.com/oauth/authorize\"", json);
        Assert.Contains("\"oauthTokenEndpoint\":\"https://example.com/oauth/token\"", json);
        Assert.Contains("\"sharedInbox\":\"https://example.com/inbox\"", json);
    }

    [Fact]
    public void Endpoints_Deserialization_PropertiesArePopulated()
    {
        var json = """
        {
            "proxyUrl": "https://example.com/proxy",
            "oauthAuthorizationEndpoint": "https://example.com/oauth/authorize",
            "oauthTokenEndpoint": "https://example.com/oauth/token",
            "sharedInbox": "https://example.com/inbox"
        }
        """;

        var endpoints = JsonSerializer.Deserialize<Endpoints>(json, _jsonOptions);

        Assert.NotNull(endpoints);
        Assert.Equal("https://example.com/proxy", endpoints!.ProxyUrl);
        Assert.Equal("https://example.com/oauth/authorize", endpoints.OAuthAuthorizationEndpoint);
        Assert.Equal("https://example.com/oauth/token", endpoints.OAuthTokenEndpoint);
        Assert.Equal("https://example.com/inbox", endpoints.SharedInbox);
    }

    [Fact]
    public void Endpoints_NullableProperties_HandleNullValues()
    {
        var endpoints = new Endpoints
        {
            ProxyUrl = null,
            OAuthAuthorizationEndpoint = null,
            OAuthTokenEndpoint = null,
            SharedInbox = null
        };

        var json = JsonSerializer.Serialize(endpoints, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Endpoints>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized.ProxyUrl);
        Assert.Null(deserialized.OAuthAuthorizationEndpoint);
        Assert.Null(deserialized.OAuthTokenEndpoint);
        Assert.Null(deserialized.SharedInbox);
    }

    [Fact]
    public void Endpoints_PublicProperties_AreAccessible()
    {
        var endpoints = new Endpoints();
        
        endpoints.ProxyUrl = "https://example.com/proxy";
        endpoints.OAuthAuthorizationEndpoint = "https://example.com/oauth/authorize";
        endpoints.OAuthTokenEndpoint = "https://example.com/oauth/token";
        endpoints.SharedInbox = "https://example.com/inbox";

        Assert.Equal("https://example.com/proxy", endpoints.ProxyUrl);
        Assert.Equal("https://example.com/oauth/authorize", endpoints.OAuthAuthorizationEndpoint);
        Assert.Equal("https://example.com/oauth/token", endpoints.OAuthTokenEndpoint);
        Assert.Equal("https://example.com/inbox", endpoints.SharedInbox);
    }

    #endregion

    #region WebFingerCacheStats Tests

    [Fact]
    public void WebFingerCacheStats_Serialization_IncludesAllProperties()
    {
        var stats = new WebFingerCacheStats
        {
            Timestamp = DateTime.UtcNow,
            CacheSize = 100,
            CacheHits = 500,
            CacheMisses = 50,
            HitRatio = 0.9,
            MissRatio = 0.1,
            TotalRequests = 550,
            CacheLifetime = "10 minutes",
            CacheType = "MemoryCache",
            CacheImplementationDetails = "Test details"
        };

        var json = JsonSerializer.Serialize(stats, _jsonOptions);
        
        Assert.Contains("\"timestamp\"", json);
        Assert.Contains("\"cacheSize\":100", json);
        Assert.Contains("\"cacheHits\":500", json);
        Assert.Contains("\"cacheMisses\":50", json);
        Assert.Contains("\"hitRatio\":0.9", json);
        Assert.Contains("\"missRatio\":0.1", json);
        Assert.Contains("\"totalRequests\":550", json);
    }

    [Fact]
    public void WebFingerCacheStats_Deserialization_PropertiesArePopulated()
    {
        var json = """
        {
            "timestamp": "2024-01-01T00:00:00Z",
            "cacheSize": 100,
            "cacheHits": 500,
            "cacheMisses": 50,
            "hitRatio": 0.9,
            "missRatio": 0.1,
            "totalRequests": 550,
            "cacheLifetime": "10 minutes",
            "cacheType": "MemoryCache",
            "cacheImplementationDetails": "Test details"
        }
        """;

        var stats = JsonSerializer.Deserialize<WebFingerCacheStats>(json, _jsonOptions);

        Assert.NotNull(stats);
        Assert.Equal(100, stats!.CacheSize);
        Assert.Equal(500, stats.CacheHits);
        Assert.Equal(50, stats.CacheMisses);
        Assert.Equal(0.9, stats.HitRatio);
        Assert.Equal(0.1, stats.MissRatio);
        Assert.Equal(550, stats.TotalRequests);
        Assert.Equal("10 minutes", stats.CacheLifetime);
        Assert.Equal("MemoryCache", stats.CacheType);
        Assert.Equal("Test details", stats.CacheImplementationDetails);
    }

    [Fact]
    public void WebFingerCacheStats_DefaultValues_AreCorrect()
    {
        var stats = new WebFingerCacheStats();
        
        Assert.Equal("10 minutes", stats.CacheLifetime);
        Assert.Equal("MemoryCache", stats.CacheType);
        Assert.Equal("Cache statistics exposed via ActivityPub telemetry", stats.CacheImplementationDetails);
    }

    [Fact]
    public void WebFingerCacheStats_PublicProperties_AreAccessible()
    {
        var stats = new WebFingerCacheStats();
        
        stats.Timestamp = DateTime.UtcNow;
        stats.CacheSize = 200;
        stats.CacheHits = 1000;
        stats.CacheMisses = 100;
        stats.HitRatio = 0.91;
        stats.MissRatio = 0.09;
        stats.TotalRequests = 1100;
        stats.CacheLifetime = "20 minutes";
        stats.CacheType = "DistributedCache";
        stats.CacheImplementationDetails = "Custom details";

        Assert.Equal(200, stats.CacheSize);
        Assert.Equal(1000, stats.CacheHits);
        Assert.Equal(100, stats.CacheMisses);
        Assert.Equal(0.91, stats.HitRatio);
        Assert.Equal(0.09, stats.MissRatio);
        Assert.Equal(1100, stats.TotalRequests);
        Assert.Equal("20 minutes", stats.CacheLifetime);
        Assert.Equal("DistributedCache", stats.CacheType);
        Assert.Equal("Custom details", stats.CacheImplementationDetails);
    }

    #endregion

    #region Collection Tests

    [Fact]
    public void Collection_Serialization_IncludesAllProperties()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Context = "https://www.w3.org/ns/activitystreams",
            Name = "Test Collection",
            Items = new List<string> { "item1", "item2" },
            TotalItems = 2,
            Summary = "A test collection"
        };

        var json = JsonSerializer.Serialize(collection, _jsonOptions);
        
        Assert.Contains("\"id\":\"https://example.com/collection/1\"", json);
        Assert.Contains("\"type\":\"Collection\"", json);
        Assert.Contains("\"name\":\"Test Collection\"", json);
        Assert.Contains("\"totalItems\":2", json);
        Assert.Contains("\"summary\":\"A test collection\"", json);
    }

    [Fact]
    public void Collection_Deserialization_PropertiesArePopulated()
    {
        var json = """
        {
            "@context": "https://www.w3.org/ns/activitystreams",
            "id": "https://example.com/collection/1",
            "type": "Collection",
            "name": "Test Collection",
            "items": ["item1", "item2"],
            "totalItems": 2,
            "summary": "A test collection"
        }
        """;

        var collection = JsonSerializer.Deserialize<Collection>(json, _jsonOptions);

        Assert.NotNull(collection);
        Assert.Equal("https://example.com/collection/1", collection!.Id);
        Assert.Equal("Collection", collection.Type);
        Assert.Equal("Test Collection", collection.Name);
        Assert.Equal(2, collection.Items.Count);
        Assert.Contains("item1", collection.Items);
        Assert.Contains("item2", collection.Items);
        Assert.Equal(2, collection.TotalItems);
        Assert.Equal("A test collection", collection.Summary);
    }

    [Fact]
    public void Collection_EmptyItemsCollection_HandledCorrectly()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Items = new List<string>()
        };

        var json = JsonSerializer.Serialize(collection, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Collection>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Items);
        Assert.Empty(deserialized.Items);
    }

    [Fact]
    public void Collection_NullableProperties_HandleNullValues()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Context = null,
            Name = null,
            Summary = null
        };

        var json = JsonSerializer.Serialize(collection, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Collection>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized.Context);
        Assert.Null(deserialized.Name);
        Assert.Null(deserialized.Summary);
    }

    [Fact]
    public void Collection_PublicProperties_AreAccessible()
    {
        var collection = new Collection { Id = "test", Type = "Collection" };
        
        collection.Context = "context";
        collection.Name = "Test";
        collection.Items = new List<string> { "item1", "item2" };
        collection.TotalItems = 2;
        collection.Summary = "Summary";

        Assert.Equal("test", collection.Id);
        Assert.Equal("Collection", collection.Type);
        Assert.Equal("context", collection.Context);
        Assert.Equal("Test", collection.Name);
        Assert.Equal(2, collection.Items.Count);
        Assert.Equal(2, collection.TotalItems);
        Assert.Equal("Summary", collection.Summary);
    }

    #endregion

    #region OrderedCollection Tests

    [Fact]
    public void OrderedCollection_Serialization_IncludesAllProperties()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            Name = "Test Collection",
            OrderedItems = new List<string> { "item1", "item2" },
            TotalItems = 2,
            First = "item1",
            Last = "item2",
            Summary = "A test collection"
        };

        var json = JsonSerializer.Serialize(collection, _jsonOptions);
        
        Assert.Contains("\"id\":\"https://example.com/collection/1\"", json);
        Assert.Contains("\"type\":\"OrderedCollection\"", json);
        Assert.Contains("\"orderedItems\"", json);
        Assert.Contains("\"first\":\"item1\"", json);
        Assert.Contains("\"last\":\"item2\"", json);
    }

    [Fact]
    public void OrderedCollection_Deserialization_PropertiesArePopulated()
    {
        var json = """
        {
            "id": "https://example.com/collection/1",
            "type": "OrderedCollection",
            "name": "Test Collection",
            "orderedItems": ["item1", "item2"],
            "totalItems": 2,
            "first": "item1",
            "last": "item2",
            "summary": "A test collection"
        }
        """;

        var collection = JsonSerializer.Deserialize<OrderedCollection>(json, _jsonOptions);

        Assert.NotNull(collection);
        Assert.Equal("https://example.com/collection/1", collection!.Id);
        Assert.Equal("OrderedCollection", collection.Type);
        Assert.Equal("Test Collection", collection.Name);
        Assert.Equal(2, collection.OrderedItems.Count);
        Assert.Equal(2, collection.TotalItems);
        Assert.Equal("item1", collection.First);
        Assert.Equal("item2", collection.Last);
        Assert.Equal("A test collection", collection.Summary);
    }

    [Fact]
    public void OrderedCollection_PublicProperties_AreAccessible()
    {
        var collection = new OrderedCollection { Id = "test", Type = "OrderedCollection" };
        
        collection.Name = "Test";
        collection.OrderedItems = new List<string> { "item1", "item2" };
        collection.TotalItems = 2;
        collection.First = "item1";
        collection.Last = "item2";
        collection.Summary = "Summary";

        Assert.Equal("test", collection.Id);
        Assert.Equal("OrderedCollection", collection.Type);
        Assert.Equal("Test", collection.Name);
        Assert.Equal(2, collection.OrderedItems.Count);
        Assert.Equal(2, collection.TotalItems);
        Assert.Equal("item1", collection.First);
        Assert.Equal("item2", collection.Last);
        Assert.Equal("Summary", collection.Summary);
    }

    #endregion

    #region Round-trip Tests

    [Theory]
    [InlineData("Video")]
    [InlineData("Page")]
    [InlineData("Article")]
    [InlineData("Image")]
    [InlineData("Tombstone")]
    [InlineData("Endpoints")]
    [InlineData("WebFingerCacheStats")]
    [InlineData("Collection")]
    [InlineData("OrderedCollection")]
    public void RoundTrip_Serialization_Deserialization_PreservesData(string typeName)
    {
        object model;
        
        switch (typeName)
        {
            case "Video":
                model = new Video
                {
                    Id = "https://example.com/video/1",
                    Type = "Video",
                    Duration = "PT1H",
                    Width = 1920,
                    Height = 1080
                };
                break;
            case "Page":
                model = new Page
                {
                    Id = "https://example.com/page/1",
                    Type = "Page",
                    Name = "Test Page"
                };
                break;
            case "Article":
                model = new Article
                {
                    Id = "https://example.com/article/1",
                    Type = "Article",
                    Content = "Content",
                    MediaType = "text/html"
                };
                break;
            case "Image":
                model = new Image
                {
                    Url = "https://example.com/image.png",
                    MediaType = "image/png",
                    Width = 800
                };
                break;
            case "Tombstone":
                model = new Tombstone
                {
                    Id = "https://example.com/tombstone/1",
                    Type = "Tombstone",
                    FormerType = "Note"
                };
                break;
            case "Endpoints":
                model = new Endpoints
                {
                    ProxyUrl = "https://example.com/proxy",
                    SharedInbox = "https://example.com/inbox"
                };
                break;
            case "WebFingerCacheStats":
                model = new WebFingerCacheStats
                {
                    CacheSize = 100,
                    CacheHits = 500
                };
                break;
            case "Collection":
                model = new Collection
                {
                    Id = "https://example.com/collection/1",
                    Type = "Collection",
                    Items = new List<string> { "item1" }
                };
                break;
            case "OrderedCollection":
                model = new OrderedCollection
                {
                    Id = "https://example.com/collection/1",
                    Type = "OrderedCollection",
                    OrderedItems = new List<string> { "item1" }
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(typeName), typeName, null);
        }

        var json = JsonSerializer.Serialize(model, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize(json, model.GetType(), _jsonOptions);
        
        Assert.NotNull(deserialized);
    }

    #endregion
}
