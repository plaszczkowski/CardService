using CardActions.Application.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace CardActions.IntegrationTests;

public class CardsControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CardsControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Theory]
    [Trait("Category", "Integration")]
    [Trait("Feature", "CardActions")]
    [InlineData("User1", "PREPAID_CLOSED", new[] { "ACTION3", "ACTION4", "ACTION9" })]
    [InlineData("User1", "CREDIT_BLOCKED_PIN", new[] { "ACTION3", "ACTION4", "ACTION5", "ACTION6", "ACTION7", "ACTION8", "ACTION9" })]
    public async Task GetCardActions_WithValidCard_ReturnsExpectedActions(string userId, string cardNumber, string[] expectedActions)
    {
        // Arrange
        var traceId = Guid.NewGuid().ToString();
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", traceId);

        // Act
        var response = await _client.GetAsync($"/api/v1/cards/actions?userId={userId}&cardNumber={cardNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<CardActionsResponse>();
        content.Should().NotBeNull();
        content!.AllowedActions.Should().BeEquivalentTo(expectedActions);
        content.CardNumber.Should().Be(cardNumber);
        content.TraceId.Should().NotBeNullOrEmpty();
        content.TraceId.Should().MatchRegex("^[a-zA-Z0-9]+$");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetCardActions_WithNonExistentCard_ReturnsNotFound()
    {
        // Arrange
        var userId = "NonExistentUser";
        var cardNumber = "NonExistentCard";

        // Act - Use query parameters
        var response = await _client.GetAsync($"/api/v1/cards/actions?userId={userId}&cardNumber={cardNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("", "CARD123")]           // Empty userId
    [InlineData("User1", "")]             // Empty cardNumber  
    [InlineData(null, "CARD123")]         // Null userId
    [InlineData("User1", null)]           // Null cardNumber
    [InlineData("Us", "CARD123")]         // UserId too short (min 3 chars)
    [InlineData("User1", "12")]           // CardNumber too short (min 3 chars)
    [InlineData("User1", "INVALID!@#")]   // CardNumber with invalid characters
    public async Task GetCardActions_WithInvalidInput_ReturnsBadRequest(string? userId, string? cardNumber)
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/cards/actions?userId={userId}&cardNumber={cardNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Verify it's a validation error with proper error format
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().NotBeNullOrEmpty();
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Errors.Should().NotBeEmpty();
        problemDetails.Extensions.Should().ContainKey("traceId");
        problemDetails.Extensions["traceId"].Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetCardActions_WithUnauthorizedCard_ReturnsForbidden()
    {
        // Arrange - Use User1 trying to access a card that belongs to User2
        var userId = "User1";
        var cardNumber = "Card21"; // This card belongs to User2 in sample data

        // Act - Use query parameters
        var response = await _client.GetAsync($"/api/v1/cards/actions?userId={userId}&cardNumber={cardNumber}");

        // Assert - Should return 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}