using System;
using Orders.Application.Helper;
using Xunit;

namespace Orders.Test.Helper
{
    public class GeneralHelperTests
    {
        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("   ", true)]
        [InlineData("3e31ffeb-d278-43d9-a292-2df0033c41ef", false)]
        public void ToGuidOrEmpty_ReturnsExpectedGuid(string? value, bool shouldBeEmpty)
        {
            // Act
            var result = GeneralHelper.ToGuidOrEmpty(value);

            // Assert
            if (shouldBeEmpty)
            {
                Assert.Equal(Guid.Empty, result);
            }
            else
            {
                Assert.Equal(Guid.Parse(value!), result);
            }
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("   ", 0)]
        [InlineData("+07:00", 7)]
        [InlineData("-05:00", -5)]
        [InlineData("02:30", 2.5)]
        [InlineData("invalid-offset", 0)]
        public void GetTimezoneOffsetInHours_ReturnsExpectedTimeSpan(string? offsetHeader, double expectedHours)
        {
            // Act
            var result = GeneralHelper.GetTimezoneOffsetInHours(offsetHeader);

            // Assert
            Assert.Equal(TimeSpan.FromHours(expectedHours), result);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        [InlineData("COMP_ALIAS_123", "COMP")]
        [InlineData("COMP", "COMP")]
        [InlineData("___", "___")]
        public void ExtractCompanyCode_ReturnsExpectedPrefix(string? alias, string expected)
        {
            // Act
            var result = GeneralHelper.ExtractCompanyCode(alias!);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData("CamelCase", "camelCase")]
        [InlineData("camelCase", "camelCase")]
        [InlineData("C", "c")]
        [InlineData("c", "c")]
        public void ToCamelCase_ReturnsExpectedString(string? input, string? expected)
        {
            // Act
            var result = input!.ToCamelCase();

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
