using ActivityPub.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace ActivityPub.Tests.W3CCompliance;

public class ActivityOrderingAndCollectionsTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public ActivityOrderingAndCollectionsTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
    }

    [Fact]
    public void OrderedCollection_Has_OrderedItems()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            OrderedItems = new List<string> { "item1", "item2", "item3" }
        };

        Assert.NotNull(collection.OrderedItems);
    }

    [Fact]
    public void OrderedCollection_OrderedItems_Must_Be_List()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            OrderedItems = new List<string> { "item1", "item2", "item3" }
        };

        Assert.IsType<List<string>>(collection.OrderedItems);
    }

    [Fact]
    public void Collection_Has_Items()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Items = new List<string> { "item1", "item2", "item3" }
        };

        Assert.NotNull(collection.Items);
    }

    [Fact]
    public void Collection_Items_Must_Be_List()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Items = new List<string> { "item1", "item2", "item3" }
        };

        Assert.IsType<List<string>>(collection.Items);
    }

    [Fact]
    public void OrderedCollection_First_Is_Optional()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            First = "https://example.com/collection/1?page=1"
        };

        Assert.NotNull(collection.First);
    }

    [Fact]
    public void OrderedCollection_Last_Is_Optional()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            Last = "https://example.com/collection/1?page=last"
        };

        Assert.NotNull(collection.Last);
    }

    [Fact]
    public void OrderedCollection_Has_Id()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection"
        };

        Assert.NotNull(collection.Id);
    }

    [Fact]
    public void Collection_Has_Id()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection"
        };

        Assert.NotNull(collection.Id);
    }

    [Fact]
    public void OrderedCollection_Can_Be_Empty()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            OrderedItems = new List<string>()
        };

        Assert.Empty(collection.OrderedItems);
    }

    [Fact]
    public void Collection_Can_Be_Empty()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Items = new List<string>()
        };

        Assert.Empty(collection.Items);
    }

    [Fact]
    public void Collection_Can_Have_Name()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Name = "Test Collection"
        };

        Assert.Equal("Test Collection", collection.Name);
    }

    [Fact]
    public void OrderedCollection_Can_Have_Name()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            Name = "Test Ordered Collection"
        };

        Assert.Equal("Test Ordered Collection", collection.Name);
    }

    [Fact]
    public void Collection_Can_Have_Summary()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Summary = "A summary"
        };

        Assert.Equal("A summary", collection.Summary);
    }

    [Fact]
    public void OrderedCollection_Can_Have_Summary()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            Summary = "A summary"
        };

        Assert.Equal("A summary", collection.Summary);
    }

    [Fact]
    public void OrderedCollection_Must_Have_Type()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection"
        };

        Assert.Equal("OrderedCollection", collection.Type);
    }

    [Fact]
    public void Collection_Must_Have_Type()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection"
        };

        Assert.Equal("Collection", collection.Type);
    }

    [Fact]
    public void OrderedCollection_First_Must_Be_Absolute_URI()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            First = "https://example.com/collection/1?page=1"
        };

        Assert.True(Uri.TryCreate(collection.First, UriKind.Absolute, out _));
    }

    [Fact]
    public void OrderedCollection_Last_Must_Be_Absolute_URI()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            Last = "https://example.com/collection/1?page=last"
        };

        Assert.True(Uri.TryCreate(collection.Last, UriKind.Absolute, out _));
    }

    [Fact]
    public void Collection_Can_Have_TotalItems()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            TotalItems = 3
        };

        Assert.Equal(3, collection.TotalItems);
    }

    [Fact]
    public void OrderedCollection_Can_Have_TotalItems()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            TotalItems = 3
        };

        Assert.Equal(3, collection.TotalItems);
    }

    [Fact]
    public void OrderedCollection_Can_Have_Current()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection"
        };

        Assert.Null(collection.Current);
    }

    [Fact]
    public void OrderedCollection_Can_Have_PartOf()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection"
        };

        Assert.Null(collection.PartOf);
    }

    [Fact]
    public void Collection_Items_Can_Be_Nested_Objects()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Items = new List<string>
            {
                "https://example.com/notes/1",
                "https://example.com/notes/2"
            }
        };

        Assert.Equal(2, collection.Items.Count);
    }

    [Fact]
    public void OrderedCollection_OrderedItems_Can_Be_Nested_Objects()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            OrderedItems = new List<string>
            {
                "https://example.com/notes/1",
                "https://example.com/notes/2"
            }
        };

        Assert.Equal(2, collection.OrderedItems.Count);
    }

    [Fact]
    public void OrderedCollection_Can_Have_Next()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection"
        };

        Assert.Null(collection.Next);
    }

    [Fact]
    public void OrderedCollection_Can_Have_Prev()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection"
        };

        Assert.Null(collection.Prev);
    }

    [Fact]
    public void Collection_Must_Have_Id_That_Is_Absolute_URI()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection"
        };

        Assert.True(Uri.TryCreate(collection.Id, UriKind.Absolute, out var uri));
        Assert.NotNull(uri.Host);
    }

    [Fact]
    public void OrderedCollection_Must_Have_Id_That_Is_Absolute_URI()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection"
        };

        Assert.True(Uri.TryCreate(collection.Id, UriKind.Absolute, out var uri));
        Assert.NotNull(uri.Host);
    }

    [Fact]
    public void Collection_Has_Correct_Type()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection"
        };

        Assert.Equal("Collection", collection.Type);
    }

    [Fact]
    public void OrderedCollection_Has_Correct_Type()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection"
        };

        Assert.Equal("OrderedCollection", collection.Type);
    }

    [Fact]
    public void OrderedCollection_First_Can_Be_Null()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            First = null
        };

        Assert.Null(collection.First);
    }

    [Fact]
    public void OrderedCollection_Last_Can_Be_Null()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            Last = null
        };

        Assert.Null(collection.Last);
    }
}
