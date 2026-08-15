using Xunit;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests;

[CollectionDefinition("FederationTests")]
public class FederationTestsCollection : ICollectionFixture<TestWebApplicationFactory>
{
}
