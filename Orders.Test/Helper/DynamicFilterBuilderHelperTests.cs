using System;
using System.Collections.Generic;
using System.Linq;
using Orders.Application.Helper;
using Xunit;

namespace Orders.Test.Helper
{
    public class DynamicFilterBuilderHelperTests
    {
        public class TestItem
        {
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
            public int? NullableValue { get; set; }
        }

        [Fact]
        public void ApplyFilter_NullOrEmptyColumnOrValue_ReturnsOriginalQuery()
        {
            // Arrange
            var list = new List<TestItem>
            {
                new TestItem { Name = "A", Value = 1 }
            }.AsQueryable();

            // Act
            var result1 = list.ApplyFilter(null, "value");
            var result2 = list.ApplyFilter("Name", "");

            // Assert
            Assert.Equal(list, result1);
            Assert.Equal(list, result2);
        }

        [Fact]
        public void ApplyFilter_StringProperty_AppliesContainsFilter()
        {
            // Arrange
            var list = new List<TestItem>
            {
                new TestItem { Name = "Apple", Value = 1 },
                new TestItem { Name = "Banana", Value = 2 }
            }.AsQueryable();

            // Act
            var result = list.ApplyFilter("Name", "an").ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("Banana", result[0].Name);
        }

        [Fact]
        public void ApplyFilter_NonStringProperty_AppliesEqualFilter()
        {
            // Arrange
            var list = new List<TestItem>
            {
                new TestItem { Name = "Apple", Value = 1 },
                new TestItem { Name = "Banana", Value = 2 }
            }.AsQueryable();

            // Act
            var result = list.ApplyFilter("Value", "2").ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("Banana", result[0].Name);
        }

        [Fact]
        public void ApplyFilter_NullableProperty_AppliesEqualFilter()
        {
            // Arrange
            var list = new List<TestItem>
            {
                new TestItem { Name = "Apple", NullableValue = 1 },
                new TestItem { Name = "Banana", NullableValue = 2 },
                new TestItem { Name = "Cherry", NullableValue = null }
            }.AsQueryable();

            // Act
            var result = list.ApplyFilter("NullableValue", "2").ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("Banana", result[0].Name);
        }

        [Fact]
        public void ApplySorting_NullOrEmptyColumn_ReturnsOriginalQuery()
        {
            // Arrange
            var list = new List<TestItem>
            {
                new TestItem { Name = "A", Value = 1 }
            }.AsQueryable();

            // Act
            var result = list.ApplySorting(null, "desc");

            // Assert
            Assert.Equal(list, result);
        }

        [Fact]
        public void ApplySorting_DescendingOrderBy_AppliesOrderByDescending()
        {
            // Arrange
            var list = new List<TestItem>
            {
                new TestItem { Name = "Banana", Value = 2 },
                new TestItem { Name = "Apple", Value = 1 }
            }.AsQueryable();

            // Act
            var result = list.ApplySorting("Name", "desc").ToList();

            // Assert
            Assert.Equal("Banana", result[0].Name);
            Assert.Equal("Apple", result[1].Name);
        }

        [Fact]
        public void ApplySorting_AscendingOrderBy_AppliesOrderBy()
        {
            // Arrange
            var list = new List<TestItem>
            {
                new TestItem { Name = "Banana", Value = 2 },
                new TestItem { Name = "Apple", Value = 1 }
            }.AsQueryable();

            // Act
            var result = list.ApplySorting("Name", "asc").ToList();

            // Assert
            Assert.Equal("Apple", result[0].Name);
            Assert.Equal("Banana", result[1].Name);
        }
    }
}
