using FluentAssertions;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using Xunit;

namespace ISP.Tests.Unit.Entities
{
    public class SubscriptionEntityTest
    {
        // CalculateEndDate Tests

        [Fact]
        [Trait("Category", "Entity")]
        public void CalculateEndDate_WithStander30DayPlan_ShouldSetCorrectEndDate()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 1);
            var plan = new Plan { DurationDays = 30 };
            var subscription = new Subscription
            {
                StartDate = startDate,
                Plan = plan
            };

            // Act 
            subscription.CalculateEndDate();

            // Assert
            subscription.EndDate.Should().Be(new DateTime(2025, 1, 31));
        }

        [Fact]
        [Trait("Category", "Entity")]
        public void CalculateEndDate_WithYearlyPlan_ShouldSetCorrectEndDate()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 1);
            var plan = new Plan { DurationDays = 365 };
            var subscription = new Subscription
            {
                StartDate = startDate,
                Plan = plan
            };

            // Act 
            subscription.CalculateEndDate();

            // Assert
            subscription.EndDate.Should().Be(new DateTime(2026, 1, 1));
        }

        [Fact]
        [Trait("Category", "Entity")]
        public void CalculateEndDate_WithZeroDayPlan_ShouldSetEndDateEqualToStartDate()
        {
            // Arrange
            var startDate = new DateTime(2025, 6, 15);
            var plan = new Plan { DurationDays = 0 };
            var subscription = new Subscription
            {
                StartDate = startDate,
                Plan = plan
            };

            // Act
            subscription.CalculateEndDate();

            // Assert
            subscription.EndDate.Should().Be(startDate);
        }

        [Fact]
        [Trait("Category", "Entity")]
        public void CalculateEndDate_WithWeeklyPlan_ShouldSetCorrectEndDate()
        {
            // Arrange
            var startDate = new DateTime(2025, 3, 1);
            var plan = new Plan { DurationDays = 7 };
            var subscription = new Subscription
            {
                StartDate = startDate,
                Plan = plan
            };

            // Act
            subscription.CalculateEndDate();

            // Assert
            subscription.EndDate.Should().Be(new DateTime(2025, 3, 8));
        }

        // UpdateStatus Tests

        [Fact]
        [Trait("Category", "Entity")]
        public void UpdateStatus_WhenEndDateIsMoreThan7DaysAway_ShouldBeActive()
        {
            // Arrange
            // نحدد تاريخ مرجعي ثابت
            var referenceDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            // EndDate = referenceDate + 8 أيام بالضبط
            var subscription = new Subscription
            {
                EndDate = referenceDate.AddDays(30)
            };

            // Act
            subscription.UpdateStatus(referenceDate);

            // Assert
            subscription.Status.Should().Be(SubscriptionStatus.Active);
        }

        [Fact]
        [Trait("Category", "Entity")]
        public void UpdateStatus_WhenEndDateIsExactly8DaysAway_ShouldBeActive()
        {
            // Arrange
            // نحدد تاريخ مرجعي ثابت
            var referenceDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            // EndDate = referenceDate + 8 أيام بالضبط
            var subscription = new Subscription
            {
                EndDate = referenceDate.AddDays(8)
            };

            // Act
            subscription.UpdateStatus(referenceDate);

            // Assert
            subscription.Status.Should().Be(SubscriptionStatus.Active);
        }

        [Fact]
        [Trait("Category", "Entity")]
        public void UpdateStatus_WhenEndDateIs7DaysAway_ShouldBeExpiring()
        {
            // Arrange
            var referenceDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var subscription = new Subscription
            {
                EndDate = referenceDate.AddDays(7)
            };

            // Act
            subscription.UpdateStatus(referenceDate);

            // Assert
            subscription.Status.Should().Be(SubscriptionStatus.Expiring);
        }

        [Fact]
        [Trait("Category", "Entity")]
        public void UpdateStatus_WhenEndDateIs3DaysAway_ShouldBeExpiring()
        {
            // Arrange
            var referenceDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var subscription = new Subscription
            {
                EndDate = referenceDate.AddDays(3)
            };

            // Act
            subscription.UpdateStatus(referenceDate);

            // Assert
            subscription.Status.Should().Be(SubscriptionStatus.Expiring);
        }

        [Fact]
        [Trait("Category", "Entity")]
        public void UpdateStatus_WhenEndDateIsToday_ShouldBeExpired()
        {
            // Arrange
            var referenceDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var subscription = new Subscription
            {
                // EndDate = نفس اليوم = 0 أيام متبقية
                EndDate = referenceDate
            };

            // Act
            subscription.UpdateStatus(referenceDate);

            // Assert
            subscription.Status.Should().Be(SubscriptionStatus.Expired);
        }

        [Fact]
        [Trait("Category", "Entity")]
        public void UpdateStatus_WhenEndDateWasYesterday_ShouldBeExpired()
        {
            // Arrange
            var referenceDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var subscription = new Subscription
            {
                // EndDate = أمس = -1 يوم
                EndDate = referenceDate.AddDays(-1)
            };

            // Act
            subscription.UpdateStatus(referenceDate);

            // Assert
            subscription.Status.Should().Be(SubscriptionStatus.Expired);
        }

        [Fact]
        [Trait("Category", "Entity")]
        public void UpdateStatus_WhenEndDateWasMonthAgo_ShouldBeExpired()
        {
            // Arrange
            var referenceDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var subscription = new Subscription
            {
                // EndDate = قبل شهر كامل
                EndDate = referenceDate.AddDays(-30)
            };

            // Act
            subscription.UpdateStatus(referenceDate);

            // Assert
            subscription.Status.Should().Be(SubscriptionStatus.Expired);
        }

        [Fact]
        [Trait("Category", "Entity")]
        public void UpdateStatus_CalledMultipleTimes_ShouldAlwaysReturnSameResult()
        {
            // Arrange
            var referenceDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var subscription = new Subscription
            {
                EndDate = referenceDate.AddDays(30)
            };

            // Act — نستدعيها 3 مرات متتالية
            subscription.UpdateStatus(referenceDate);
            subscription.UpdateStatus(referenceDate);
            subscription.UpdateStatus(referenceDate);

            // Assert — النتيجة نفسها دائماً
            subscription.Status.Should().Be(SubscriptionStatus.Active);
        }
    }
}