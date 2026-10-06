using System;
using System.Collections.Generic;
using System.Reflection;
using Orders.Application.Helpers.Mapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Orders.Test.Helper.Mapper
{
    public class ObjectMapperTests
    {
        public class SrcModel
        {
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
            public string Extra { get; set; } = string.Empty;
        }

        public class DestModel
        {
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
            public string Manual { get; set; } = string.Empty;
            public string Conditional { get; set; } = string.Empty;
        }

        public class DestModelNonWritable
        {
            public string Name { get; } = string.Empty;
        }

        private class TestMappingProfile : IMappingProfile
        {
            public void Register(MapperConfig cfg)
            {
                cfg.CreateMap<SrcModel, DestModel>()
                    .Auto()
                    .ForMember(d => d.Manual, s => s.Extra)
                    .ForMemberWithCondition(d => d.Conditional, s => "CondValue", s => s.Value > 10);
            }
        }

        [Fact]
        public void AddMyMapper_RegistersIMappingProfileAndIObjectMapper()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMyMapper(typeof(ObjectMapperTests).Assembly);
            var provider = services.BuildServiceProvider();

            // Assert
            var profiles = provider.GetServices<IMappingProfile>();
            Assert.Contains(profiles, p => p.GetType() == typeof(TestMappingProfile));

            var mapper = provider.GetService<IObjectMapper>();
            Assert.NotNull(mapper);
        }

        [Fact]
        public void ObjectMapper_ThrowsExceptionOnDuplicateMappings()
        {
            // Arrange
            var cfg = new MapperConfig();
            cfg.CreateMap<SrcModel, DestModel>();
            cfg.CreateMap<SrcModel, DestModel>(); // duplicate

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => new ObjectMapper(cfg.GetDefs(), cfg.GetPatchDefs()));
            Assert.Contains("Duplicate mappings found", exception.Message);
        }

        [Fact]
        public void Map_UnregisteredType_ThrowsInvalidOperationException()
        {
            // Arrange
            var cfg = new MapperConfig();
            var mapper = new ObjectMapper(cfg.GetDefs(), cfg.GetPatchDefs());

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => mapper.Map<DestModel>(new SrcModel()));
        }

        [Fact]
        public void Map_NullSource_ThrowsArgumentNullException()
        {
            // Arrange
            var cfg = new MapperConfig();
            var mapper = new ObjectMapper(cfg.GetDefs(), cfg.GetPatchDefs());

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => mapper.Map<DestModel>(null!));
        }

        [Fact]
        public void Map_ValidMapping_MapsAutoAndManualAndConditional()
        {
            // Arrange
            var cfg = new MapperConfig();
            cfg.CreateMap<SrcModel, DestModel>()
                .Auto()
                .ForMember(d => d.Manual, s => s.Extra)
                .ForMemberWithCondition(d => d.Conditional, s => "CondValue", s => s.Value > 10);

            var mapper = new ObjectMapper(cfg.GetDefs(), cfg.GetPatchDefs());

            var source1 = new SrcModel { Name = "Test1", Value = 5, Extra = "Extra1" };
            var source2 = new SrcModel { Name = "Test2", Value = 15, Extra = "Extra2" };

            // Act
            var dest1 = mapper.Map<DestModel>(source1);
            var dest2 = mapper.Map<DestModel>(source2);

            // Assert
            Assert.Equal("Test1", dest1.Name);
            Assert.Equal(5, dest1.Value);
            Assert.Equal("Extra1", dest1.Manual);
            Assert.Equal(string.Empty, dest1.Conditional); // precondition Value > 10 is false

            Assert.Equal("Test2", dest2.Name);
            Assert.Equal(15, dest2.Value);
            Assert.Equal("Extra2", dest2.Manual);
            Assert.Equal("CondValue", dest2.Conditional); // precondition Value > 10 is true
        }

        [Fact]
        public void MapList_NullSource_ThrowsArgumentNullException()
        {
            // Arrange
            var cfg = new MapperConfig();
            var mapper = new ObjectMapper(cfg.GetDefs(), cfg.GetPatchDefs());

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => mapper.MapList<SrcModel, DestModel>(null!));
        }

        [Fact]
        public void MapList_UnregisteredType_ThrowsInvalidOperationException()
        {
            // Arrange
            var cfg = new MapperConfig();
            var mapper = new ObjectMapper(cfg.GetDefs(), cfg.GetPatchDefs());

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => mapper.MapList<SrcModel, DestModel>(new List<SrcModel>()));
        }

        [Fact]
        public void MapList_ValidSourceList_MapsListCorrectly()
        {
            // Arrange
            var cfg = new MapperConfig();
            cfg.CreateMap<SrcModel, DestModel>().Auto();
            var mapper = new ObjectMapper(cfg.GetDefs(), cfg.GetPatchDefs());

            var list = new List<SrcModel>
            {
                new SrcModel { Name = "Name1", Value = 1 },
                null!, // test null item skip
                new SrcModel { Name = "Name2", Value = 2 }
            };

            // Act
            var destList = mapper.MapList<SrcModel, DestModel>(list);

            // Assert
            Assert.Equal(2, destList.Count);
            Assert.Equal("Name1", destList[0].Name);
            Assert.Equal("Name2", destList[1].Name);
        }

        [Fact]
        public void Patch_NullArguments_ThrowsArgumentNullException()
        {
            // Arrange
            var cfg = new MapperConfig();
            var mapper = new ObjectMapper(cfg.GetDefs(), cfg.GetPatchDefs());

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => mapper.Patch<SrcModel, DestModel>(null!, new DestModel()));
            Assert.Throws<ArgumentNullException>(() => mapper.Patch<SrcModel, DestModel>(new SrcModel(), null!));
        }

        [Fact]
        public void Patch_UnregisteredType_ThrowsInvalidOperationException()
        {
            // Arrange
            var cfg = new MapperConfig();
            var mapper = new ObjectMapper(cfg.GetDefs(), cfg.GetPatchDefs());

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => mapper.Patch(new SrcModel(), new DestModel()));
        }

        [Fact]
        public void Patch_ValidPatchMapping_PatchesCorrectly()
        {
            // Arrange
            var cfg = new MapperConfig();
            cfg.CreatePatchMap<SrcModel, DestModel>()
                .ForMemberIf(d => d.Manual, s => s.Extra, s => !string.IsNullOrEmpty(s.Extra));

            var mapper = new ObjectMapper(cfg.GetDefs(), cfg.GetPatchDefs());

            var source1 = new SrcModel { Extra = "PatchedExtra" };
            var source2 = new SrcModel { Extra = "" };

            var dest1 = new DestModel { Manual = "OldManual" };
            var dest2 = new DestModel { Manual = "OldManual" };

            // Act
            mapper.Patch(source1, dest1);
            mapper.Patch(source2, dest2);

            // Assert
            Assert.Equal("PatchedExtra", dest1.Manual);
            Assert.Equal("OldManual", dest2.Manual);
        }

        [Fact]
        public void Patch_NonWritableProperty_ThrowsArgumentException()
        {
            // Arrange
            var cfg = new MapperConfig();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                cfg.CreatePatchMap<SrcModel, DestModelNonWritable>()
                    .ForMemberIf(d => d.Name, s => s.Name, s => true)
            );
        }
    }
}
