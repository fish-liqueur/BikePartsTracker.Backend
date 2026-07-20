namespace BikePartsTracker.Backend.Tests.Infrastructure;

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "PostgresIntegration";
}
