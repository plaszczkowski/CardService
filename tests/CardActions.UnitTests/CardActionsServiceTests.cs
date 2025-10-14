using CardActions.Application.Interfaces;
using CardActions.Application.Services;
using CardActions.Domain.Abstractions;
using CardActions.Domain.Enums;
using CardActions.Domain.Events;
using CardActions.Domain.Models;
using CardActions.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CardActions.UnitTests;

[Trait("Category", "Unit")]
[Trait("Feature", "ACTION10_12_13")]
public class CardActionsServiceTests
{
    private readonly Mock<ICardRepository> _cardRepositoryMock;
    private readonly Mock<ICardActionPolicy> _actionPolicyMock;
    private readonly Mock<ILogger<CardActionsService>> _loggerMock;
    private readonly Mock<ICardActionsMetrics> _metricsMock;
    private readonly CardActionsService _service;
    private readonly Mock<IEventBus> _eventBus;

    public CardActionsServiceTests()
    {
        _cardRepositoryMock = new Mock<ICardRepository>();
        _actionPolicyMock = new Mock<ICardActionPolicy>();
        _loggerMock = new Mock<ILogger<CardActionsService>>();
        _metricsMock = new Mock<ICardActionsMetrics>();
        _eventBus = new Mock<IEventBus>();

        _service = new CardActionsService(
            _cardRepositoryMock.Object,
            _actionPolicyMock.Object,
            _loggerMock.Object,
            _metricsMock.Object,
            _eventBus.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCardActionsAsync_WithValidCard_ReturnsResponse()
    {
        // Arrange
        var userId = "User1";
        var cardNumber = "PREPAID_CLOSED";
        var traceId = "0HNG957N290SB";

        var card = new Card(
            new CardId(Guid.NewGuid().ToString()),
            new UserId(userId),
            cardNumber,
            CardType.Prepaid,
            CardStatus.Closed,
            false);

        var expectedActions = new List<string> { "ACTION3", "ACTION4", "ACTION9" };

        _cardRepositoryMock
            .Setup(x => x.CardExistsAsync(cardNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _cardRepositoryMock
            .Setup(x => x.GetCardAsync(It.Is<UserId>(u => u.Value == userId), cardNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);

        _actionPolicyMock
            .Setup(x => x.GetAllowedActions(card))
            .Returns(expectedActions);

        // Act
        var response = await _service.GetCardActionsAsync(userId, cardNumber, traceId);

        // Assert
        response.Should().NotBeNull();
        response.AllowedActions.Should().BeEquivalentTo(expectedActions);
        response.CardNumber.Should().Be(cardNumber);
        response.TraceId.Should().Be(traceId);

        // Verify metrics were called
        _metricsMock.Verify(x => x.RecordRequest("Prepaid", "Closed", true, It.IsAny<double>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCardActionsAsync_WithNonExistentCard_ThrowsKeyNotFoundException()
    {
        // Arrange
        var userId = "NonExistentUser";
        var cardNumber = "NonExistentCard";
        var traceId = "test-trace-id";

        _cardRepositoryMock
            .Setup(x => x.CardExistsAsync(cardNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetCardActionsAsync(userId, cardNumber, traceId));

        // Verify metrics were called with success = false
        _metricsMock.Verify(x => x.RecordRequest("unknown", "unknown", false, It.IsAny<double>()), Times.Once);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("", "CARD123")]
    [InlineData("User1", "")]
    public async Task GetCardActionsAsync_WithInvalidInput_ThrowsArgumentException(string userId, string cardNumber)
    {
        // Arrange
        var traceId = "test-trace-id";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetCardActionsAsync(userId, cardNumber, traceId));

        // Verify metrics were called with success = false
        _metricsMock.Verify(x => x.RecordRequest("unknown", "unknown", false, It.IsAny<double>()), Times.Once);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(null, "CARD123")]
    [InlineData("User1", null)]
    public async Task GetCardActionsAsync_WithNullInput_ThrowsArgumentException(string? userId, string? cardNumber)
    {
        // Arrange
        var traceId = "test-trace-id";

        // Act & Assert - non-null assertion operator (!) ponieważ wiemy że test przekazuje null
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetCardActionsAsync(userId!, cardNumber!, traceId));

        // Verify metrics were called with success = false
        _metricsMock.Verify(x => x.RecordRequest("unknown", "unknown", false, It.IsAny<double>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCardActionsAsync_WithUnauthorizedCard_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var userId = "User1";
        var cardNumber = "UNAUTHORIZED_CARD";
        var traceId = "test-trace-id";

        // Karta istnieje, ale nie należy do tego użytkownika
        _cardRepositoryMock
            .Setup(x => x.CardExistsAsync(cardNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // GetCardAsync zwraca null - użytkownik nie ma dostępu do karty
        _cardRepositoryMock
            .Setup(x => x.GetCardAsync(It.Is<UserId>(u => u.Value == userId), cardNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Card?)null);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetCardActionsAsync(userId, cardNumber, traceId));

        // Verify metrics were called with success = false
        _metricsMock.Verify(x => x.RecordRequest("unknown", "unknown", false, It.IsAny<double>()), Times.Once);
    }

    #region Event Publishing Tests

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "RabbitMQ")]
    public async Task GetCardActionsAsync_PublishesCardActionsRetrievedEvent_OnSuccess()
    {
        // Arrange
        var userId = "User1";
        var cardNumber = "TEST_CARD";
        var traceId = "test-trace-id";
        var eventBusMock = new Mock<IEventBus>();

        var service = new CardActionsService(
            _cardRepositoryMock.Object,
            _actionPolicyMock.Object,
            _loggerMock.Object,
            _metricsMock.Object,
            eventBusMock.Object);

        var card = new Card(
            new CardId(Guid.NewGuid().ToString()),
            new UserId(userId),
            cardNumber,
            CardType.Debit,
            CardStatus.Active,
            false);

        _cardRepositoryMock.Setup(x => x.CardExistsAsync(cardNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _cardRepositoryMock.Setup(x => x.GetCardAsync(It.IsAny<UserId>(), cardNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);
        _actionPolicyMock.Setup(x => x.GetAllowedActions(card))
            .Returns(new List<string> { "ACTION1" });

        // Act
        await service.GetCardActionsAsync(userId, cardNumber, traceId);

        // Assert
        eventBusMock.Verify(x => x.PublishAsync(
            It.Is<CardActionsRetrievedEvent>(e =>
                e.UserId == userId &&
                e.CardNumber == cardNumber &&
                e.TraceId == traceId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetCardActionsAsync_PublishesCardAccessDeniedEvent_OnAuthorizationFailure()
    {
        // Arrange
        var userId = "User1";
        var cardNumber = "UNAUTHORIZED_CARD";
        var traceId = "test-trace-id";
        var eventBusMock = new Mock<IEventBus>();

        var service = new CardActionsService(
            _cardRepositoryMock.Object,
            _actionPolicyMock.Object,
            _loggerMock.Object,
            _metricsMock.Object,
            eventBusMock.Object);

        _cardRepositoryMock.Setup(x => x.CardExistsAsync(cardNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _cardRepositoryMock.Setup(x => x.GetCardAsync(It.IsAny<UserId>(), cardNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Card?)null);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetCardActionsAsync(userId, cardNumber, traceId));

        eventBusMock.Verify(x => x.PublishAsync(
            It.Is<CardAccessDeniedEvent>(e =>
                e.UserId == userId &&
                e.CardNumber == cardNumber &&
                e.Reason.Contains("does not belong")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "EventBus")]
    public async Task GetCardActionsAsync_PublishesCardNotFoundEvent_WhenCardDoesNotExist()
    {
        // Arrange
        var userId = "User1";
        var cardNumber = "NON_EXISTENT";
        var traceId = "test-trace-id";
        var eventBusMock = new Mock<IEventBus>();

        var service = new CardActionsService(
            _cardRepositoryMock.Object,
            _actionPolicyMock.Object,
            _loggerMock.Object,
            _metricsMock.Object,
            eventBusMock.Object);

        _cardRepositoryMock.Setup(x => x.CardExistsAsync(cardNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetCardActionsAsync(userId, cardNumber, traceId));

        eventBusMock.Verify(x => x.PublishAsync(
            It.Is<CardNotFoundEvent>(e =>
                e.UserId == userId &&
                e.CardNumber == cardNumber),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}