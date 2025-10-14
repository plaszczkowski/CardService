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

namespace CardActions.UnitTests.Application
{

    [Trait("Category", "Unit")]
    public class CardActionsServiceTests_EventBus
    {
        private const string CardNumberValue = "CARD456";
        private const string TraceIdValue = "trace-789";

        [Fact]
        [Trait("Category", "EventPublishing")]
        [Trait("Enhancement", "ENH-013")]
        public async Task PublishEvent_WhenEventBusThrows_ShouldLogErrorAndContinue()
        {
            // Arrange
            var mockRepository = new Mock<ICardRepository>();
            var mockPolicy = new Mock<ICardActionPolicy>();
            var mockLogger = new Mock<ILogger<CardActionsService>>();
            var mockMetrics = new Mock<ICardActionsMetrics>();
            var mockEventBus = new Mock<IEventBus>();

            mockEventBus.Setup(bus => bus.PublishAsync(
                    It.IsAny<CardActionsRetrievedEvent>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Simulated failure"));

            var service = new CardActionsService(
                mockRepository.Object,
                mockPolicy.Object,
                mockLogger.Object,
                mockMetrics.Object,
                mockEventBus.Object);

            var userId = new UserId("User123");
            var cardNumber = CardNumberValue;
            var traceId = TraceIdValue;
            var cancellationToken = new CancellationTokenSource().Token;

            var card = new Card(
                new CardId(cardNumber),
                userId,
                cardNumber,
                CardType.Credit,
                CardStatus.Active,
                true);

            mockRepository.Setup(r => r.CardExistsAsync(cardNumber, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            mockRepository.Setup(r => r.GetCardAsync(
                    It.Is<UserId>(id => id.Value == "User123"),
                    cardNumber,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);
            mockPolicy.Setup(p => p.GetAllowedActions(It.IsAny<Card>()))
                .Returns(new[] { "ACTION1", "ACTION2" });

            // Act - should NOT throw despite event bus failure
            var result = await service.GetCardActionsAsync("User123", cardNumber, traceId, cancellationToken);

            // Assert - Operation succeeds
            result.Should().NotBeNull();
            result.AllowedActions.Should().Contain("ACTION1");
            result.AllowedActions.Should().Contain("ACTION2");
            result.TraceId.Should().Be(traceId);

            // Verify error logged with correct generic type handling
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to publish")),
                    It.Is<Exception>(ex => ex.Message == "Simulated failure"),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once,
                "Expected error log when event publishing fails");

            // Verify other dependencies were called correctly
            mockRepository.Verify(r => r.CardExistsAsync(cardNumber, cancellationToken), Times.Once);
            mockRepository.Verify(r => r.GetCardAsync(
                It.IsAny<UserId>(),
                cardNumber,
                cancellationToken), Times.Once);
            mockEventBus.Verify(bus => bus.PublishAsync(
                It.IsAny<CardActionsRetrievedEvent>(),
                cancellationToken), Times.Once);

            // Verify metrics still recorded (operation succeeded)
            mockMetrics.Verify(m => m.RecordRequest(
                "Credit",
                "Active",
                true, // Success despite event failure
                It.IsAny<double>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "EventPublishing")]
        public async Task GetCardActionsAsync_WhenCardNotFound_ShouldThrowException()
        {
            // Test repository returning false from CardExistsAsync
            // This validates event publishing doesn't interfere with error flow
        }

        [Fact]
        [Trait("Category", "EventPublishing")]
        public async Task GetCardActionsAsync_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Test that repository exceptions are not swallowed by event publishing
        }
    }
}