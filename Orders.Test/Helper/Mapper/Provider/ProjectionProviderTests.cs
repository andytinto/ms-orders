using System;
using System.Linq.Expressions;
using Orders.Application.Helpers.Mapper.Provider;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Orders.Test.Helper.Mapper.Provider
{
    public class ProjectionProviderTests
    {
        public class SrcModel
        {
            public string Name { get; set; } = string.Empty;
        }

        public class DestModel
        {
            public string Name { get; set; } = string.Empty;
        }

        private class TestProjectionProfile : IProjectionProfile
        {
            public void Register(ProjectionRegistry registry)
            {
                registry.Register<SrcModel, DestModel>(s => new DestModel { Name = s.Name });
            }
        }

        [Fact]
        public void AddMyProjections_RegistersIProjectionProfileAndIProjectionProvider()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMyProjections(typeof(ProjectionProviderTests).Assembly);
            var provider = services.BuildServiceProvider();

            // Assert
            var profiles = provider.GetServices<IProjectionProfile>();
            Assert.Contains(profiles, p => p.GetType() == typeof(TestProjectionProfile));

            var projProvider = provider.GetService<IProjectionProvider>();
            Assert.NotNull(projProvider);
        }

        [Fact]
        public void Get_RegisteredProjection_ReturnsExpression()
        {
            // Arrange
            var registry = new ProjectionRegistry();
            Expression<Func<SrcModel, DestModel>> expr = s => new DestModel { Name = s.Name };
            registry.Register(expr);

            var provider = new ProjectionProvider(registry.GetAll());

            // Act
            var retrieved = provider.Get<SrcModel, DestModel>();

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(expr, retrieved);
        }

        [Fact]
        public void Get_UnregisteredProjection_ThrowsInvalidOperationException()
        {
            // Arrange
            var registry = new ProjectionRegistry();
            var provider = new ProjectionProvider(registry.GetAll());

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => provider.Get<SrcModel, DestModel>());
        }
    }
}
